---
tsg_artifact: techspec
product: code-4-coders
capability: CAP-001
status: Aprovado
updated: 2026-09-22
---

# TechSpec — conta e autenticação do aluno

> - **Escopo:** Full-stack
> - **Modo:** Pipeline, com contratos de integração aprovados
> - **PRD de origem:** [prd.md](prd.md), v1.1, aprovado
> - **Contratos:** [contracts.md](contracts.md), [OpenAPI SPA → BFF](api-contract.yaml), [OpenAPI BFF → Identity](internal-api-contract.yaml), [AsyncAPI Identity](asyncapi-contract.yaml) e [AsyncAPI Notificação](../prd-notificacao-transacional/asyncapi-contract.yaml)
> - **Status:** Aprovado
> **Handoff:** approved — pode alimentar o Task Creator

## Resumo executivo

Entregar o ciclo completo de conta do aluno em `student-spa`, `bff-student` e `identity`. Identity é dono de conta, credencial, token de verificação e estado de revogação; o BFF expõe as nove operações públicas aprovadas, guarda o identificador opaco da sessão em Valkey e impede que JWT interno chegue ao navegador. Os pedidos de e-mail e três fatos são gravados no outbox de Identity com as mudanças de negócio.

**Escolhas aprovadas:** para garantir que uma senha trocada ou redefinida impeça a *próxima* ação protegida mesmo após falha/reinício do BFF, ele consulta a vigência da sessão em Identity em cada ação protegida. Isso mantém uma fonte de verdade e revogação imediata, mas acrescenta uma chamada e cria a exceção explícita à regra geral do baseline ([ADR-0003](../../docs/adr/0003-verificacao-de-sessao-do-aluno.md)). A chamada pré-login usa autenticação própria do BFF, separada da identidade do aluno ([ADR-0004](../../docs/adr/0004-autenticacao-de-servico-bff-identity.md)). Todas as sessões simultâneas continuam permitidas.

## Arquitetura da solução

### Backend

- **Identity:** Conta, Credencial e Token de Verificação vivem no seu banco, com `tenant_id` e filtro de tenant. Normalização e unicidade de e-mail são impostas no domínio e no banco. Guardar somente derivação lenta da senha e hash não reversível do token; nunca guardar token em claro. Validação e consumo de token, troca de senha, invalidação de tokens de recuperação e revogação das demais sessões ocorrem numa transação de Identity. A gravação do outbox integra o mesmo commit. Conta não confirmada permanece não confirmada após recuperação.
- **Política de senha:** Identity rejeita toda senha nova de cadastro, redefinição ou troca que tenha menos de oito caracteres ou não contenha pelo menos uma letra maiúscula, uma minúscula, um dígito e um símbolo. Espaços não contam como símbolo. A SPA mostra a mesma orientação e valida para resposta rápida; a validação autoritativa permanece em Identity. `currentPassword` é conferida contra a Credencial existente sem reaplicar a política nova, permitindo a troca de credencial legada.
- **Sessão:** Identity mantém a situação autoritativa por `tenant_id`/conta/sessão e emite o JWT interno de vida curta. BFF mantém o cookie opaco e a sessão operacional em Valkey. Em toda ação protegida, o BFF verifica presença/TTL em Valkey e chama Identity para validar **e renovar atomicamente** a inatividade da sessão antes de renovar o TTL local, emitir/obter JWT com audiência específica e encaminhar. Falha de Identity ou Valkey fecha o acesso, sem prolongar o TTL no BFF. Logout revoga em Identity e remove em Valkey; a revogação em Identity prevalece se a remoção do cache falhar. Se o commit de login em Identity ocorrer e a gravação no Valkey falhar, nenhum cookie é emitido; a sessão órfã expira sem uso. Respostas de login, leitura de sessão e logout nunca expõem JWT.
- **BFF → Identity antes do login:** identidade de serviço por credencial assimétrica de curta duração, exclusiva do BFF do aluno, verificada por Identity em todos os endpoints internos conforme [OpenAPI interno](internal-api-contract.yaml). A credencial de serviço autentica o chamador, não o aluno. Login entrega credenciais somente pelo canal TLS; Identity cria a sessão após verificar senha e situação da conta, e só emite JWT de aluno quando o BFF pede audiência permitida na validação de sessão. O BFF não pode criar claims de aluno. Chave privada fica no secret manager, com rotação por `kid`; Identity limita emissor, audiência, escopo, tenant, validade e `jti`. Nenhum endpoint interno é exposto ao navegador.
- **Idempotência pública:** janela de **24 horas** por tenant, operationId e `Idempotency-Key`; operações autenticadas incluem a conta no isolamento. O BFF propaga a chave para Identity. Identity persiste fingerprint protegido e resultado lógico com a transação de negócio, sobrevivendo a reinício do BFF e evitando duplicação após falha entre commit e resposta. Mesma chave/corpo mantém o efeito e status/corpo lógico; corpo diferente gera 422 `IDEMPOTENCY_CONFLICT`. Replay de login devolve a mesma sessão lógica; o BFF pode reemitir cookie opaco para ela, mas não a reativa se já foi revogada. Senha, token de verificação e cookie não entram em claro no registro de idempotência. A validação de sessão é repetível e não cria outra sessão.
- **Links e e-mail:** URL pública configurada por ambiente aponta para a SPA, que lê o token da URL e o envia no corpo da operação pertinente. A SPA retira o token da barra/histórico assim que o captura. Identity forma o link do pedido endereçado à Notificação sem pôr segredo em fato difundido, log ou telemetria. Validade por finalidade é configuração validada no início; deve coincidir com o texto configurado em Notificação. Não há chamada de rede na transação de negócio.
- **Destino das mensagens:** os três fatos `identidade.*` vão ao exchange de Identity. O pedido `notificacao.envio-solicitado.v1` deve sair pelo exchange de Notificação configurado para o mesmo namespace operacional do consumidor, com destino registrado no outbox da intenção. A routing key sozinha não cruza exchanges. A publicação só é marcada concluída após confirmação do broker; falha/reentrega mantém o mesmo `pedidoId`. O produtor não lê banco de Notificação nem decide entrega.
- **E-mail local:** no Compose de desenvolvimento, smtp4dev captura as mensagens de confirmação e recuperação. O `notification` atual possui apenas `HttpTransactionalEmailSender`; é necessário um adaptador SMTP atrás da porta `ITransactionalEmailSender`, selecionado por configuração de ambiente, para enviar a `smtp4dev:25`. A UI do container (porta 80) fica acessível somente em `127.0.0.1:5000` no host; a porta SMTP não precisa ser publicada no host. Essa mudança cabe na V-01 porque entrega o primeiro e-mail observável e é reutilizada na V-02/V-04. O adaptador HTTP segue disponível para o provedor de produção. O teste local não depende de domínio remetente ou DNS públicos.

### Frontend

- Cadastro, confirmação/reenvio, login, recuperação/redefinição e troca de senha são jornadas da SPA. Estado de formulário e token de link fica no cliente somente durante a operação; identidade e vigência da sessão vêm de `getCurrentStudentSession`, sem cópia em store global.
- A SPA chama só o BFF. A resposta 401 de sessão expirada leva à entrada; `EMAIL_NOT_CONFIRMED` oferece reenvio; erros de credencial e pedido de recuperação preservam mensagens genéricas. `getCurrentStudentSession` fornece a prova CSRF vinculada à sessão para escrita autenticada; erro 403 de CSRF exige reobter a prova, sem repetição automática de operação sensível.
- Texto visível em português, foco/estado de erro acessíveis por teclado e leitor de tela. A decisão visual permanece com design. A identidade autenticada não implica autorização para curso.

## Mapa de fatias verticais

Cada fatia inclui UI, BFF e Identity, além da evidência indicada. Os contratos públicos e de mensagem são os identificadores estáveis; nomes de tipos internos e arquivos novos derivam das skills `react` e `dotnet`.

### V-01 — cadastro solicita confirmação

- **Cobre:** RF-01; RN-01, RN-02, RN-06, RN-13/13a/13b, RN-24, RN-26–28.
- **Entrada:** formulário de cadastro → `registerStudent`, com chave de idempotência.
- **Processamento:** Identity normaliza e-mail, impede duplicidade por tenant e entre tipos de conta, grava Conta aluno não confirmada e Credencial derivada; token de confirmação é de uso único. Conta, token, `publicarContaCriada` e `publicarPedidoDeEnvioSolicitado` entram no mesmo commit. O BFF traduz duplicidade para `ACCOUNT_ALREADY_EXISTS`, sem criar sessão.
- **Saída:** orientação de verificar e-mail; um pedido para `confirmacao-de-conta` entregue à Notificação, nenhum acesso a curso.
- **Evidência/checkpoint:** cadastro normalizado cria uma conta e um pedido; repetição de chave não duplica; e-mail já usado não gera credencial; pedido é consumido pelo fluxo de CAP-026.
- **Senha no checkpoint:** sete caracteres ou ausência de qualquer classe exigida impedem a Conta; uma senha que atende a todas as classes permite prosseguir. O link aparece no smtp4dev local, após o consumidor de Notificação aceitar o pedido.
- **Bloqueado por:** contrato BFF → Identity e decisão de autenticação de serviço.

### V-02 — confirmação e reenvio habilitam login

- **Cobre:** RF-02; RN-02, RN-03, RN-26–28.
- **Entrada:** link da SPA → `confirmStudentAccount`; formulário de reenvio → `requestAccountConfirmation`.
- **Processamento:** Identity consome atomicamente token válido da finalidade correta, confirma a Conta e registra `publicarContaConfirmada`. Token alterado/usado/expirado não muda estado. Reenvio só para conta não confirmada elegível emite novo token e novo `pedidoId`, sem criar conta.
- **Saída:** confirmação ou orientação de reenvio; pedido de e-mail novo quando elegível.
- **Evidência/checkpoint:** token usado duas vezes só confirma uma; finalidade trocada falha; link expirado oferece reenvio e o segundo pedido é aceito por Notificação.
- **Bloqueado por:** V-01.

### V-03 — login mantém sessão e logout a encerra

- **Cobre:** RF-03, RF-04; RN-02, RN-05, RN-08–10, RN-13/13a/13b, RN-22, RN-24.
- **Entrada:** login → `createStudentSession`; navegação → `getCurrentStudentSession`; saída → `endCurrentStudentSession`.
- **Processamento:** Identity valida credencial e elegibilidade e cria sessão autoritativa; o BFF grava identificador opaco em Valkey e cookie seguro. Identity emite JWT interno apenas na validação posterior para audiência autorizada. Toda ação protegida consulta situação da sessão e renova TTL por atividade válida. Logout revoga apenas a sessão corrente; outra sessão permanece ativa. Se o cookie vier de sessão já encerrada ou expirada, logout remove o cookie e responde sem reativá-la, conforme o OpenAPI. Conta não confirmada só é distinguida após senha correta; conta interna ou desativada não autentica como aluno.
- **Saída:** sessão com identidade/CSRF sem JWT público; 401 indistinguível para e-mail inexistente e senha errada; próxima ação após logout ou inatividade exige login.
- **Evidência/checkpoint:** duas sessões paralelas; logout de uma preserva a outra; TTL deslizante; 401 após expiração; CSRF bloqueia escrita autenticada sem prova; injeção de falha em Identity/Valkey fecha acesso.
- **Bloqueado por:** V-02; aprovação da exceção de verificação por request e contrato interno.

### V-04 — recuperação redefine senha sem revelar conta

- **Cobre:** RF-05; RN-03–07, RN-08, RN-21, RN-24, RN-26–28.
- **Entrada:** pedido → `requestPasswordReset`; link → `resetStudentPassword`.
- **Processamento:** BFF retorna a mesma resposta pública para endereço elegível e inelegível. Apenas o elegível gera token e `publicarPedidoDeEnvioSolicitado`. Redefinição consome token correto, altera Credencial, invalida os demais tokens de recuperação, revoga demais sessões em Identity e grava `publicarSenhaRedefinida` no commit. A situação de confirmação da conta não muda.
- **Saída:** mensagem neutra no pedido; nova senha aceita no login somente se conta confirmada; sessões antigas e links anteriores recusados.
- **Evidência/checkpoint:** payload/status públicos indistinguíveis; token usado/expirado/outra finalidade não muda senha; senha nova fora da política preserva token e Credencial; após commit nenhuma sessão antiga passa na próxima ação protegida; o link de recuperação aparece no smtp4dev local.
- **Bloqueado por:** V-03.

### V-05 — aluno autenticado troca senha

- **Cobre:** RF-06; RN-06–08, RN-11.
- **Entrada:** formulário protegido → `changeStudentPassword`, senha atual, nova senha e CSRF.
- **Processamento:** Identity verifica senha atual, altera Credencial, invalida tokens de recuperação, revoga outras sessões e grava `publicarSenhaRedefinida`. A sessão corrente fica válida. Senha atual errada não altera credencial nem sessões.
- **Saída:** confirmação de troca; outra sessão recebe 401 na próxima ação; sessão corrente permanece ativa.
- **Evidência/checkpoint:** senha errada ou nova senha fora da política não revoga; nova senha válida entra, antiga falha; sessão paralela perde acesso imediatamente; CSRF inválido não chega a Identity.
- **Bloqueado por:** V-03.

## Contratos e fronteiras

### API pública e chamada interna

| operationId público | operationId interno ou decisão do BFF | Verificação específica |
|---|---|---|
| `registerStudent` | `createStudentAccountInternal` | unicidade, outbox e replay |
| `confirmStudentAccount` | `confirmStudentAccountInternal` | finalidade, expiração, uso único |
| `requestAccountConfirmation` | `requestAccountConfirmationInternal` | só conta elegível; novo `pedidoId` |
| `createStudentSession` | `createStudentSessionInternal`; BFF cria cookie opaco | respostas genéricas, cookie sem JWT |
| `getCurrentStudentSession` | Valkey + `validateStudentSessionInternal` sem audiência | revogação/expiração e CSRF |
| `endCurrentStudentSession` | `revokeStudentSessionInternal`; BFF remove sessão/cookie | reiteração não reativa; outra sessão preservada |
| `requestPasswordReset` | `requestPasswordResetInternal` | ausência de enumeração |
| `resetStudentPassword` | `resetStudentPasswordInternal` | atomicidade, links/sessões antigos |
| `changeStudentPassword` | `changeStudentPasswordInternal` | sessão corrente preservada, CSRF |

Os dois OpenAPI definem rotas, corpos e `code`s; esta tabela os conecta sem duplicar schemas. O BFF converte falha interna em ProblemDetails público com `traceId`, sem detalhe de infraestrutura. Para chamada protegida a outro serviço, `validateStudentSessionInternal` recebe a audiência autorizada e retorna JWT interno curto. Não foi encontrado contrato HTTP anterior; compatibilidade em produção não foi verificada.

### Mensagens e dados

| Operação AsyncAPI | Produtor → consumidor | Comportamento e evidência |
|---|---|---|
| `publicarContaCriada` | Identity → consumidores futuros não definidos | fato sem e-mail/token; mesmo `eventId` na reentrega; V-01 |
| `publicarContaConfirmada` | Identity → consumidores futuros não definidos | após commit de confirmação; V-02 |
| `publicarSenhaRedefinida` | Identity → consumidores futuros não definidos | após reset e troca; V-04/V-05 |
| `publicarPedidoDeEnvioSolicitado` | Identity → Notificação CAP-026 | `pedidoId` estável na reentrega e novo no reenvio deliberado; testes de serialização e aceitação pelo consumidor; V-01/V-02/V-04 |

O pedido endereçado segue CAP-026 v1.0.0; os três fatos são novos e não têm assinantes declarados nesta entrega. A implantação deve configurar o exchange de pedidos conforme o namespace efetivo de Notificação e testar que o pedido alcança o consumidor, não apenas que o broker aceitou publicação. Validade exibida no e-mail deve ser igual à validade aplicada em Identity; configurar e verificar por finalidade antes de tráfego real. Não há contrato ODCS porque não se fornece produto de dados.

### Jornada

| História | Operações | Evidência |
|---|---|---|
| Criar conta e confirmar | `registerStudent`, `confirmStudentAccount`, `requestAccountConfirmation` | cadastro → e-mail → confirmação → entrada; erro oferece reenvio |
| Entrar e sair | `createStudentSession`, `getCurrentStudentSession`, `endCurrentStudentSession` | identidade estável, expiração e logout visíveis |
| Recuperar acesso | `requestPasswordReset`, `resetStudentPassword` | resposta neutra e link de uso único |
| Trocar senha | `changeStudentPassword`, `getCurrentStudentSession` | sessão atual persiste; demais exigem login |

### Entidades do domínio

| Conceito em [Identidade e Acesso](../../domains/identidade-e-acesso/domain.md) | Representação e fronteira |
|---|---|
| Conta | agregado persistido por Identity, único por tenant/e-mail não desativado |
| Credencial | segredo derivado pertencente à Conta, nunca retornado |
| Token de Verificação | hash, finalidade, validade e consumo atômico no banco de Identity |
| Sessão | vigência/revogação em Identity; estado operacional opaco no Valkey do BFF |

## Arquivos a modificar e a referenciar

### A modificar

| Caminho existente | Fatia | Alteração |
|---|---|---|
| `src/identity/src/CodeForCoders.Identity.Infra.Data/IdentityDbContext.cs` | V-01–V-05 | persistência dos agregados e filtros de tenant |
| `src/identity/src/CodeForCoders.Identity.Api/Extensions/EndpointExtensions.cs` | V-01–V-05 | mapear interface interna contratada |
| `src/identity/src/CodeForCoders.Identity.Api/Extensions/ServiceConfigurationExtensions.cs` | V-01–V-05 | autenticação de serviço/JWT e configuração validada |
| `src/identity/src/CodeForCoders.Identity.Infra.Messaging/RabbitMqTopologyInitializer.cs` | V-01/V-02/V-04/V-05 | exchange de fatos e declaração necessária à publicação dos pedidos |
| `src/identity/src/CodeForCoders.Identity.Infra.Messaging/RabbitMqPublisher.cs` | V-01/V-02/V-04/V-05 | publicar cada entrada do outbox no exchange de destino, com confirmação |
| `src/identity/src/CodeForCoders.Identity.Infra.Messaging/Configuration/RabbitMqOptions.cs` | V-01/V-02/V-04/V-05 | validar exchange endereçado a Notificação por ambiente |
| `src/identity/src/CodeForCoders.Identity.Infra.Data/Outbox/OutboxMessage.cs` | V-01/V-02/V-04/V-05 | registrar destino do fato ou pedido na intenção persistida |
| `src/identity/src/CodeForCoders.Identity.Infra.Data/Outbox/OutboxMessageConfiguration.cs` | V-01/V-02/V-04/V-05 | mapear o destino do outbox na migration evolutiva |
| `src/identity/src/CodeForCoders.Identity.Application/Interfaces/IOutboxMessageWriter.cs` | V-01/V-02/V-04/V-05 | propagar destino do fato/pedido até o outbox |
| `src/notification/src/CodeForCoders.Notification.Api/Extensions/ServiceConfigurationExtensions.cs` | V-01/V-02/V-04 | selecionar adaptador SMTP em desenvolvimento e HTTP para o provedor externo |
| `src/notification/src/CodeForCoders.Notification.Infra.Data/Configuration/EmailOptions.cs` | V-01/V-02/V-04 | configurar transporte SMTP local sem reinterpretar o endpoint HTTP existente |
| `docker-compose.yml` | V-01/V-02/V-04 | incluir smtp4dev, restringir UI ao host local e apontar Notificação ao SMTP interno |
| `src/bff-student/src/CodeForCoders.BffStudent.Application/Common/OpaqueBffSession.cs` | V-03–V-05 | modelo de sessão compatível com vigência e JWT curto |
| `src/bff-student/src/CodeForCoders.BffStudent.Infra.Data/ValkeyBffSessionStore.cs` | V-03–V-05 | renovação deslizante e remoção efetiva |
| `src/bff-student/src/CodeForCoders.BffStudent.Api/Security/BffSecurityMiddleware.cs` | V-03–V-05 | verificação de vigência e CSRF ligado à sessão |
| `src/bff-student/src/CodeForCoders.BffStudent.Api/Extensions/EndpointExtensions.cs` | V-01–V-05 | rotas públicas do OpenAPI |
| `src/bff-student/src/CodeForCoders.BffStudent.Api/Extensions/ServiceConfigurationExtensions.cs` | V-01–V-05 | cliente Identity e configuração |
| `src/bff-student/src/CodeForCoders.BffStudent.Api/Endpoints/SessionEndpoints.cs` | V-03 | resolver sobreposição do endpoint de status atual com o contrato aprovado |
| `src/student-spa/src/app/router.tsx` | V-01–V-05 | jornadas e proteção de rota |
| `src/student-spa/src/config/paths.ts` | V-01–V-05 | rotas de jornada e links de e-mail |
| `src/student-spa/src/lib/api-client.ts` | V-01–V-05 | cookies, idempotência, CSRF e 401 sem exposição de segredo |

### A referenciar

| Caminho | Por quê |
|---|---|
| `context/architecture-baseline.md` | BA05/BA06/BA10, G06/G09/G18/G23 e conflito de consulta por request |
| `domains/identidade-e-acesso/domain.md` | RN-01–11, RN-13/13a/13b, RN-21/22/24/26–28 |
| `docs/adr/0001-monorepo-de-codigo.md` e `docs/adr/0002-plataforma-de-runtime-coolify.md` | fronteiras de deploy, dados e segredos |
| `tasks/prd-notificacao-transacional/asyncapi-contract.yaml` | payload e semântica do consumidor |
| `src/notification/src/CodeForCoders.Notification.Api/appsettings.json` | exchange e namespace atuais da fila receptora; configuração efetiva varia por ambiente |
| `src/notification/src/CodeForCoders.Notification.Infra.Data/Adapters/HttpTransactionalEmailSender.cs` | semântica de envio e classificação de falhas a preservar no adaptador local |
| `tasks/prd-conta-aluno/internal-api-contract.yaml` | operações, autenticação e falhas da chamada BFF → Identity |
| `.agents/skills/dotnet/SKILL.md` e `.agents/skills/react/SKILL.md` | convenções de stack, testes e estrutura sem duplicação aqui |

## Análise de impacto

| Componente | Impacto | Ação |
|---|---|---|
| Identity/PostgreSQL | novos dados de conta, credencial, token, sessão e idempotência | migration evolutiva sob dono Identity; índices de unicidade por tenant |
| BFF/Valkey | sessão operacional, cookie, TTL e checagem de vigência | fail closed e instrumentar latência da verificação |
| Notificação/RabbitMQ | novos pedidos no contrato CAP-026 existente | provar aceitação e deduplicação; não alterar o consumidor sem necessidade |
| Notificação/smtp4dev local | adaptador SMTP de desenvolvimento e caixa de captura no Compose | provar e-mail recebido com link; manter transporte HTTP do provedor externo |
| Outros serviços | passarão a validar JWT de Identity via JWKS quando consumirem identidade | publicar chave pública e manter rotação com sobreposição; não há consumidor novo nesta entrega |
| SPA | novas rotas e formulários | testar navegação, erros, acessibilidade e remoção do segredo da URL |

## Riscos e preocupações

| Preocupação | Local | Impacto | Mitigação |
|---|---|---|---|
| CSRF atual compara cookie e header, mas não os vincula à sessão; o cookie pode ser enviado fora do fluxo de `getCurrentStudentSession` | `src/bff-student/src/CodeForCoders.BffStudent.Api/Security/BffSecurityMiddleware.cs:30` | escrita autenticada pode aceitar prova de outra sessão | gerar/verificar prova vinculada ao identificador opaco; E2E de prova cruzada |
| Store atual tem `Get` sem renovação do TTL | `src/bff-student/src/CodeForCoders.BffStudent.Infra.Data/ValkeyBffSessionStore.cs:33` | sessão expira durante atividade, contrariando RN-08 | renovar somente após atividade válida e verificar expiração |
| Sessão atual persiste `UpstreamAccessToken` no Valkey | `src/bff-student/src/CodeForCoders.BffStudent.Application/Common/OpaqueBffSession.cs:6` | JWT pode ultrapassar vida curta ou sobreviver à revogação | trocar/renovar com emissão curta; nunca aceitar token como prova de sessão |
| Endpoint atual responde status de sessão fora da forma do OpenAPI aprovado | `src/bff-student/src/CodeForCoders.BffStudent.Api/Endpoints/SessionEndpoints.cs:11` | SPA recebe shape ou rota incompatível | alinhar a `getCurrentStudentSession`; testar contrato de borda |
| Configuração da sessão traz TTL de 60 minutos, mas store ainda não desliza | `src/bff-student/src/CodeForCoders.BffStudent.Api/appsettings.json:31` | prazo real difere do comunicado | validar configuração e testar com relógio controlado |
| Cliente HTTP existente permite três retries sem desabilitar método inseguro explicitamente | `src/bff-student/src/CodeForCoders.BffStudent.Api/Extensions/ServiceConfigurationExtensions.cs:17` | escrita pode duplicar efeito | garantir idempotência propagada ou desabilitar retry de escrita; testar falha após commit |
| Notificação persiste link recebido | `src/notification/src/CodeForCoders.Notification.Domain/DeliveryRecords/DeliveryRecord.cs:92` | segredo recuperável em dado operacional além do período de uso | limitar acesso e retenção, aplicar purga de CAP-026 e verificar que token expirado nunca é aceito |
| Publicador de Identity usa apenas `identity.events`, mas Notificação liga sua fila ao próprio exchange | `src/identity/src/CodeForCoders.Identity.Infra.Messaging/RabbitMqPublisher.cs:32` | pedido confirmado pelo broker pode não chegar a Notificação | persistir exchange de destino no outbox, configurar namespace compatível e provar consumo real |
| Envio atual de Notificação usa POST HTTP, incompatível com servidor SMTP | `src/notification/src/CodeForCoders.Notification.Infra.Data/Adapters/HttpTransactionalEmailSender.cs:32` | apontar `Email:Endpoint` para smtp4dev não entrega e-mail | selecionar adaptador SMTP em desenvolvimento e verificar mensagem na UI local |

## Decisões técnicas aprovadas

1. **Verificação de vigência em Identity por ação protegida (ADR-0003).** Revogação imediata e fonte de verdade persistente; custo de latência/disponibilidade e exceção à regra do baseline. Alternativas e trade-offs estão na ADR.
2. **Identidade de serviço assimétrica para BFF → Identity antes do login (ADR-0004).** Autentica chamadas pré-login sem atribuir identidade falsa de aluno; requer ciclo de chaves. Alternativas e trade-offs estão na ADR.
3. **Janela de idempotência pública de 24 horas.** Cobre retry humano e de rede por um dia, com armazenamento limitado; depois da janela o mesmo pedido pode ser uma intenção nova, sujeito às regras do domínio. Replay seguro de login e operações com token é definido no contrato interno.

## Verificação

- Os checkpoints V-01–V-05 são o gate funcional. Executar testes de contrato de cada operationId público e da interface interna, além de serialização/consumo real do pedido por CAP-026 através do exchange de destino. Lint YAML sozinho não prova conformidade.
- Concorrência crítica: duas confirmações do mesmo token; duas redefinições; cadastro simultâneo do mesmo e-mail; retry depois de commit sem resposta; troca de senha concorrente com ação de outra sessão. Em todos, exigir um efeito lógico e nenhuma sessão revogada aceita na ação seguinte.
- Injetar indisponibilidade de Identity, Valkey, broker e provedor de e-mail em pontos distintos. Broker indisponível mantém outbox para retry; Identity/Valkey indisponível bloqueia ação protegida, sem renovar sessão. Medir latência da verificação de sessão e falhas por causa, sem e-mail/segredo como dimensão.
- Testar localmente V-01 → V-02 → V-03 e V-04 com `notification` enviando via SMTP ao smtp4dev, consultar a mensagem na UI e seguir o link; verificar que o endereço de captura não é configurado em produção. O teste comprova o fluxo funcional, não a entrega a destinatários externos.
- Validar a política aprovada de senha em cadastro, redefinição e troca, inclusive uma credencial antiga que não atende à nova política; validar validade por finalidade, inatividade e texto de validade de CAP-026 como configurações coerentes antes de tráfego real. Confirmar provedor, domínio e DNS de e-mail com plataforma.

## Questões em aberto

- [ ] Validade dos dois links e duração de inatividade para operação real — Segurança e Produto — sem valor aprovado, o aceite de produção fica bloqueado; implementação usa configuração validada.
- [ ] Provedor/domínio de envio e SPF/DKIM/DMARC de CAP-026 — Plataforma — sem isso o ciclo não entrega a destinatários externos; smtp4dev cobre o teste local.

## Architecture Decision Records

- [ADR-0001 — Monorepo de código](../../docs/adr/0001-monorepo-de-codigo.md) — deploy e banco independentes por serviço.
- [ADR-0002 — Plataforma de runtime Coolify](../../docs/adr/0002-plataforma-de-runtime-coolify.md) — Valkey, RabbitMQ, segredos e runtime.
- [ADR-0003 — Verificação de sessão do aluno](../../docs/adr/0003-verificacao-de-sessao-do-aluno.md) — **Accepted**, exceção explícita ao baseline.
- [ADR-0004 — Autenticação de serviço BFF → Identity](../../docs/adr/0004-autenticacao-de-servico-bff-identity.md) — **Accepted**, fecha a chamada pré-login.
