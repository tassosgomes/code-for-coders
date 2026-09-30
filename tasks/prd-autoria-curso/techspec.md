---
tsg_artifact: techspec
product: code-4-coders
capability: CAP-005
version: 1.0
status: approved
updated: 2026-09-29
sources: tasks/prd-autoria-curso/prd.md@1.0, tasks/prd-autoria-curso/contracts.md@1.0, context/architecture-baseline.md@1.2
---

# TechSpec — autoria e publicação de curso

> **Escopo:** Full-stack (`identity`, `learning`, `media`, `audit`, `bff-admin` e `admin-spa`)
> **Modo:** Pipeline, API-First
> **PRD de origem:** [prd.md](prd.md), v1.0, aprovado em 2026-09-28
> **Contratos:** [contracts.md](contracts.md) 1.0; [OpenAPI público](api-contract.yaml), [OpenAPI interno de learning](internal-api-contract-learning.yaml), [AsyncAPI de learning](asyncapi-contract.yaml), [Media](asyncapi-contract-media.yaml) e [Audit](asyncapi-contract-audit.yaml), aprovados em 2026-09-28
> **Data:** 2026-09-29
> **Status:** Aprovado pelo responsável em 2026-09-29
> **Handoff:** approved — pode alimentar o Task Creator

## Resumo Executivo

O professor edita um currículo da escola em `learning`, por item, através do backoffice. Cada publicação congela um retrato imutável e move o ponteiro de versão vigente. O mesmo commit grava duas mensagens no outbox: a versão completa para Mídia e consumidores futuros, e o ato para Auditoria. `media` substitui as Referências de Uso do curso pela maior versão recebida. `audit` passa a reconhecer o novo tipo como conforme. `identity` concede `autoria.editar` ao professor e emite JWT para `learning`; o BFF valida a sessão e o serviço decide a autorização final.

O desenho herda o [baseline](../../context/architecture-baseline.md), a [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md) e C-01 a C-11 dos contratos. A errata do PRD sobre "Auditoria não muda" é resolvida pelo contrato aprovado C-01: a política receptora e a apresentação da trilha mudam. Não há nova decisão arquitetural permanente nem ADR proposta.

**Trade-off primário:** a visão local de vídeos e as Referências de Uso retiram Mídia do commit de publicação e preservam um salto síncrono no editor. A consequência é atraso de propagação e a necessidade de reconstruir a visão local de vídeos prontos que já existiam antes da nova fila.

## Arquitetura da Solução

```text
professor → admin-spa (/admin/autoria) → bff-admin → identity (sessão, JWT)
                                      │             └→ learning (rascunho, versões, outbox)
                                      └→ media (seletor e enriquecimento da leitura)
learning ← media.events: midia.ativo-pronto / midia.preparacao-falhou
learning outbox → learning.events: conteudo.versao-publicada → media (Referências de Uso)
                → audit.events: auditoria.ato-praticado → audit → bff-admin → trilha do administrador
```

### Bloco Backend

**Identidade e borda.** `identity` acrescenta `autoria.editar` ao professor e a audiência `learning`, mantendo `autoria.ler` e `midia.enviar`. O middleware existente do BFF revalida a sessão em cada ação; os endpoints de autoria pedem o JWT `audience: learning`, checam a permissão da operação e encaminham a chave de idempotência. `learning` valida assinatura, emissor, audiência, validade, `tenantId`, `sub` e permissão localmente. A leitura especial `resolveCourseReferencesInternal` exige papel `administrador` e só devolve título do curso do mesmo tenant, sem exigir `autoria.ler`. O BFF remove qualquer `Authorization` e `X-Actor-Name` vindo do navegador e cria os headers internos a partir da sessão validada; o nome é somente retrato de exibição, nunca identidade ou decisão de acesso. CSRF continua obrigatório no BFF para toda escrita.

**Conteúdo e Currículo.** `Curso` é o limite transacional da edição e publicação. O rascunho guarda curso, módulos e aulas com `tenantId`, IDs estáveis, posições contíguas, `draftRevision`, criador e último editor. Uma escrita por item bloqueia o curso, aplica só o item pedido, renumera irmãos afetados e incrementa a revisão; edições simultâneas do mesmo campo seguem OD44 (última gravação confirmada vence). O curso publicado conserva rascunho independente e um ponteiro para a versão vigente. `hasUnpublishedChanges` compara a estrutura e os campos editáveis do rascunho com o retrato vigente por uma impressão determinística persistida, excluindo metadados de edição e revisão; editar e voltar ao valor original ou descartar deixa o indicador falso mesmo com `draftRevision` maior. A versão guarda metadados e retrato imutável do currículo com títulos, descrições, ordem e `videoId`; sua leitura histórica nunca recompõe dados a partir do rascunho. O retrato pode ser persistido como JSONB versionado no schema `content` de `learning`, pois nenhuma consulta desta entrega filtra o conteúdo interno de versões anteriores; índices e consultas são por tenant, curso e número de versão. O outbox desta publicação também pertence a `content`; o outbox já existente em `progress` permanece com o seu dono. `Curso` nunca recebe preço, nível ou oferta.

Para publicar, uma única transação de `learning` bloqueia a linha do curso, consulta o registro de idempotência, relê e confere `draftRevision`, valida a estrutura e os vídeos da visão local, aloca `currentVersion + 1`, grava versão e ponteiro vigente, grava o resultado da chave e insere **duas linhas distintas** no outbox de `content`. O fato de versão tem `eventId` estável; o ato usa esse mesmo valor como `fatoId`, mas cada linha do outbox tem ID próprio porque `Id` é chave primária. Publicar não incrementa `draftRevision`: dois pedidos concorrentes com chaves diferentes e a mesma revisão geram versões sequenciais; repetição da mesma intenção usa a mesma chave e devolve o resultado gravado, sem nova versão. Uma edição concorrente incrementa a revisão sob o mesmo bloqueio e força `409 DRAFT_CHANGED` no pedido que viu a revisão antiga. A chave é isolada por tenant, ator, operação e recurso, como no precedente de Mídia; a janela é 24 h. Chave igual com corpo diferente retorna `IDEMPOTENCY_KEY_REUSED`. Nenhuma chamada de rede ocorre dentro da transação. Descartar alterações restaura o retrato vigente no rascunho, preservando IDs, e incrementa a revisão; curso nunca publicado responde `COURSE_NEVER_PUBLISHED`.

**Visão de vídeo pronto.** `learning` consome `receberAtivoPronto` e `receberPreparacaoFalhou` de `media.events` em uma fila durável própria, com deduplicação por `eventId` e tenant. A projeção guarda apenas ID do vídeo, tenant, estado final e, se útil, duração recebida; título e autor continuam em `media`. Vínculo de vídeo consulta esta visão em `learning`, dentro do tenant, e responde `VIDEO_NOT_AVAILABLE` sem alterar o rascunho quando o fato ainda não chegou, o vídeo falhou ou pertence a outra escola. O seletor usa `listVideos` existente com `status=ready`, paginado; `getCourse` é enriquecido pelo BFF via `getVideo` com JWT de audiência `media`. O BFF deduplica os IDs de vídeo do curso, limita as consultas paralelas e aplica o prazo geral de 20 s/5 s por tentativa do baseline; cada vídeo que não responder no prazo conserva só `videoId`. A leitura do curso continua com enriquecimento parcial; o seletor mostra erro e permite tentar novamente. A consulta de enriquecimento não decide se o vínculo é válido.

**Reconstrução da visão.** Antes de liberar a autoria, declarar a fila de `learning` e sua ligação a `media.events`, depois reproduzir os fatos `midia.ativo-pronto.v1` e `midia.preparacao-falhou.v1` já confirmados no outbox do próprio `media`, mantendo `eventId` e payload originais. Só o serviço dono lê seu outbox e republica; `learning` não lê o banco de Mídia. A operação é idempotente e deve comparar, por tenant, os vídeos `ready` existentes em Mídia com os IDs prontos projetados em `learning`. O código atual mantém linhas processadas do outbox; se o ambiente tiver descartado parte desse histórico, a liberação fica bloqueada até um mecanismo de reconstrução compatível com o contrato ser acordado com Mídia. A fila de auditoria temporária de Mídia, com TTL e descarte por pressão, não é fonte de reconstrução.

**Publicação e consumidores.** O publicador do outbox de `content` seleciona o exchange pelo destino acordado: `learning.events` para `publicarVersaoPublicada` e `audit.events` para `publicarAtoVersaoPublicada`, seguindo o precedente de `identity`; o processamento do outbox de `progress` não muda. O papel worker de `media` recebe `receberVersaoPublicada` de `learning.events`, sem acrescentar trabalho à API durante uma requisição (ADR-0006), e, em uma transação por tenant/curso, só aplica `versionNumber` maior que a registrada: substitui todas as referências `(courseId, lessonId) → videoId` e grava a versão aplicada. O registro contém apenas IDs e tenant; não exige FK para um vídeo que Mídia ainda desconheça. Fato atrasado ou reentregue é confirmado sem efeito. `audit` amplia a política para `origem=conteudo`, `tipo=versao-publicada`, alvo `curso` e `complemento.versao`; o motivo segue opcional. Na consulta da trilha, o BFF resolve alvos `curso` em `learning` apenas depois da resposta de `audit`, tanto na lista quanto no detalhe; falha de resolução mantém a referência sem título. A UI apresenta o ato como "Versão publicada" e trata alvo `curso` como curso, inclusive no comando de filtrar atos por alvo.

**Dados pessoais e credenciais.** Identity cria o JWT curto; o BFF o mantém em memória durante a requisição para `learning` ou `media` e o descarta depois. Cookies opacos e prova CSRF permanecem no fluxo de sessão existente. O nome do ator é copiado da sessão validada para `X-Actor-Name`, persistido como retrato em curso/versão no PostgreSQL de `learning` e alcançado pelos backups desse banco; somente leituras de autoria autorizadas o exibem. O ato e o fato usam o ID do autor, sem nome ou e-mail. Títulos, descrições e nota são criados pelo professor e persistidos em `learning`/backup; apenas os títulos do currículo publicado são copiados para o outbox, broker e possíveis filas de erro do fato `conteudo.versao-publicada`, conforme C-09. Media descarta esses títulos ao registrar referências; descrição e nota não saem de `learning`. O acesso ao outbox, ao broker, às filas de erro e aos backups deve ficar restrito às credenciais de serviço/operação, e reprocessamento deve preservar tenant e `eventId`; a política de retenção ainda não foi definida. Não colocar nome, token, título/descrição livres ou payload integral em log, span, métrica, routing key ou erro. O inventário e atendimento de solicitação do titular para esses dados devem constar na evolução de `CAP-031`/AB03.

### Bloco Frontend

A área Autoria troca o item reservado do menu por navegação real. URLs públicas no ambiente Compose: lista `http://localhost:8081/admin/autoria`, editor `http://localhost:8081/admin/autoria/{courseId}` e versão histórica `http://localhost:8081/admin/autoria/{courseId}/versoes/{versionNumber}`. Uma pendência de aula leva a `http://localhost:8081/admin/autoria/{courseId}#aula-{lessonId}`; uma de módulo, a `#modulo-{moduleId}` do mesmo editor. No Coolify de desenvolvimento, as URLs são `https://c4c-admin.lab.tasso.dev.br/admin/autoria`, `https://c4c-admin.lab.tasso.dev.br/admin/autoria/{courseId}` e `https://c4c-admin.lab.tasso.dev.br/admin/autoria/{courseId}/versoes/{versionNumber}`. `paths.ts` guarda as rotas relativas; o router aplica `BASE_URL=/admin/`. O editor abre pelo `courseId` retornado na criação, sem construir URL a partir de `Location` da API. A evidência de navegação inclui clicar no menu, abrir editor e versão, seguir a pendência, recarregar cada URL diretamente e verificar o destino e foco com sessão válida.

Lista, editor, histórico, seletor e erros de mutação refletem respostas do servidor; `draftRevision` da última resposta renderizada acompanha publicar/descartar. Cada mutação reutiliza a mesma chave de idempotência durante retry da mesma intenção, atualiza o curso exibido com a resposta integral e invalida a lista e o histórico afetados. `409 DRAFT_CHANGED` recarrega o curso e exige nova confirmação; não repete a publicação automaticamente. Estados rascunho/publicado/alterações não publicadas aparecem em texto. Arrastar módulo ou aula tem comandos equivalentes por teclado; remover pede confirmação e informa a quantidade de aulas do módulo. Pendências de `COURSE_INCOMPLETE` aparecem na ordem devolvida, anunciam o erro e levam foco ao módulo/aula correspondente. `autoria.ler` sem `autoria.editar` oferece leitura sem controles de escrita; nenhuma tela presume que ocultar o botão substitui a decisão do serviço. Os visuais do editor precisam seguir o ciclo de wireframe, Figma e aprovação previsto no PRD antes do código de tela.

## Mapa de Fatias Verticais

Cada fatia inclui as mudanças de UI, BFF, serviço, persistência e observabilidade que seu comportamento requer. As regras RF/RN aparecem abaixo sem separar uma fatia por camada.

### V-01: professor cria e encontra cursos da escola

- **Cobre:** RF-01, RF-02, RF-05; US-01, US-08; RN-C02, RN-C03, RN-C10, RN-C14, RN-17/18 de Identidade.
- **Entrada / gatilho:** sessão interna válida; abrir `Autoria`, criar curso com título e descrição, abrir a lista ou um ID direto.
- **Processamento:** Identity concede `autoria.editar` e audiência `learning`; BFF valida sessão/permissões; `learning` valida JWT, tenant e permissão, cria o rascunho idempotentemente e lista todos os cursos da escola com estado e último editor. Outro tenant recebe 404 indistinto.
- **Saída observável:** menu só para `autoria.ler`; curso novo aparece como rascunho e abre no editor; ator apenas leitor vê dados sem ações de escrita; revogação barra a próxima ação.
- **Evidência / checkpoint:** Compose com migrations aplicadas, professor A e B do mesmo tenant, leitor, papel revogado e curso de outro tenant: `createCourse`, `listCourses`, `getCourse` e chamadas diretas `*Internal` dão 201/200/401/403/404 acordados; abrir e recarregar `http://localhost:8081/admin/autoria/{courseId}` mostra o curso certo.
- **Bloqueado por:** nenhum.

### V-02: professor organiza o rascunho sem mudar identidades

- **Cobre:** RF-03; US-01; RN-C01, RN-C05, RN-C07.
- **Entrada / gatilho:** criar/renomear/mover/reordenar/remover módulo ou aula no editor, inclusive por teclado.
- **Processamento:** `learning` aplica operações por item dentro do curso/tenant, valida limites e posição, renumera irmãos de forma contígua, preserva ID em edição/movimento e gera ID novo após remoção/criação; incrementa `draftRevision` e devolve o curso inteiro. A UI confirma remoção de módulo com contagem de aulas.
- **Saída observável:** estrutura salva na ordem escolhida, sem posições repetidas; professor B vê a última gravação; versão publicada existente continua inalterada.
- **Evidência / checkpoint:** `createModule`, `updateModule`, `deleteModule`, `createLesson`, `updateLesson`, `deleteLesson` pelo SPA/BFF e direto em `learning`; mover aula entre módulos conserva ID, remover/criar muda ID, teclado executa a mesma ordem; 422 para limite/posição inválidos e 404 para item de outro curso/tenant.
- **Bloqueado por:** V-01.

### V-03: professor escolhe vídeo pronto da escola

- **Cobre:** RF-04; US-02; RN-C04, RN-M02, RN-M04, RN-M05; OD35.
- **Entrada / gatilho:** abrir seletor, escolher/trocar/desvincular vídeo; chegada de `midia.ativo-pronto.v1` ou `midia.preparacao-falhou.v1`.
- **Processamento:** consumidor de `learning` mantém projeção idempotente; seletor usa `listVideos` pronto do tenant; `createLesson`/`updateLesson` validam localmente ID, tenant e estado; BFF enriquece a consulta de aula com `getVideo` e tolera falha de Mídia.
- **Saída observável:** apenas vídeos prontos aparecem com título, duração e autor; fato novo torna o vídeo vinculável; ID inválido ou de outra escola não altera a aula; falha do enriquecimento mostra ID sem bloquear o editor.
- **Evidência / checkpoint:** RabbitMQ com fila ligada antes do replay, outbox histórico disponível, migrations e vídeos ready/preparing/failed de dois tenants; conferir projeção contra `media`, reentrega por `eventId`, `VIDEO_NOT_AVAILABLE` e leitura degradada com Mídia indisponível.
- **Bloqueado por:** V-02.

### V-04: professor publica um curso completo com rastro e referências

- **Cobre:** RF-06, RF-10, RF-11, RF-12; US-03, US-07; RN-C06, RN-C08, RN-C11, RN-C12, RN-C13, RN-M09/10, RN-A02/03/05/06/08/14.
- **Entrada / gatilho:** confirmar publicação no SPA com `draftRevision`, nota opcional e chave da intenção.
- **Processamento:** `learning` valida completude e revisão, grava versão imutável, ponteiro e dois outbox no mesmo commit; outbox entrega ao exchange correto; `media` troca referências pela maior versão; `audit` registra ato conforme; BFF da trilha resolve título do curso.
- **Saída observável:** UI mostra versão 1 e autor/momento; currículo incompleto mostra pendências focáveis sem versão; Media registra `(courseId, lessonId, videoId)` e a trilha mostra "Versão publicada" com autor, curso e momento. Um retry da mesma chave devolve a mesma versão.
- **Evidência / checkpoint:** Compose com Identity, Learning, Media, Audit, RabbitMQ, PostgreSQL e migrations; provocar falha entre a montagem das duas mensagens e o commit, repetir pedido e entregar mensagens duplicadas; conferir atomicidade no banco/outbox, uma versão, um ato conforme e referências sem títulos. Abrir `http://localhost:8081/admin/auditoria` como administrador e conferir filtro, lista e detalhe.
- **Bloqueado por:** V-03.

### V-05: professor corrige, republica, descarta e consulta versões

- **Cobre:** RF-07, RF-08; US-04, US-06; RN-C05 a RN-C09, RN-C17; OD41/44.
- **Entrada / gatilho:** editar curso já publicado, consultar histórico/versão, republicar ou descartar alterações confirmadas.
- **Processamento:** rascunho muda sem tocar o retrato vigente; republicar cria próximo número e novos fatos; descartar reconstrói o rascunho da vigente se a revisão coincide; `media` substitui todas as referências pela maior `versionNumber` aplicada.
- **Saída observável:** lista/editor indicam alterações não publicadas; versão antiga continua consultável com IDs e títulos originais; versão nova é vigente; aula removida deixa de estar referenciada; histórico mostra autor, momento e nota.
- **Evidência / checkpoint:** `listCourseVersions`, `getCourseVersion`, `publishCourse`, `discardCourseDraft` e URLs diretas de versão; duas publicações com chaves diferentes sob mesma revisão produzem números sequenciais, edição concorrente causa `DRAFT_CHANGED`, versão antiga não muda, entrega da versão 1 após a 2 não reverte Mídia, repetição não duplica referências.
- **Bloqueado por:** V-04.

### V-06: professor exclui apenas curso nunca publicado

- **Cobre:** RF-09; US-05; RN-C02, RN-C06; OD45.
- **Entrada / gatilho:** confirmar exclusão do rascunho na lista/editor.
- **Processamento:** `learning` confere tenant, permissão e ausência de qualquer versão, remove o curso e seus itens de rascunho idempotentemente; não gera ato de auditoria.
- **Saída observável:** curso some da lista; curso já publicado não oferece a ação e `deleteCourse` direto responde `COURSE_ALREADY_PUBLISHED`.
- **Evidência / checkpoint:** exclusão, replay da mesma chave, tentativa em curso publicado e de outro tenant via UI/BFF/`learning`; nenhum evento de auditoria ou publicação no outbox.
- **Bloqueado por:** V-01.

## Contratos e Fronteiras

Os schemas, parâmetros e respostas vivem nos cinco documentos aprovados de [contracts.md](contracts.md); este mapa associa os identificadores ao comportamento e à evidência das fatias.

### Mapeamento de HTTP

| operationId público → interno | Responsabilidade | Fatia |
|---|---|---|
| `listCourses` → `listCoursesInternal`; `getCourse` → `getCourseInternal`; `createCourse` → `createCourseInternal`; `updateCourse` → `updateCourseInternal` | BFF valida sessão e pede token `learning`; `learning` aplica tenant e permissão; BFF enriquece vídeo apenas em `getCourse` | V-01, V-02, V-03 |
| `createModule` → `createModuleInternal`; `updateModule` → `updateModuleInternal`; `deleteModule` → `deleteModuleInternal` | edição de módulo e posições no rascunho | V-02 |
| `createLesson` → `createLessonInternal`; `updateLesson` → `updateLessonInternal`; `deleteLesson` → `deleteLessonInternal` | edição de aula; vídeo conferido pela projeção local | V-02, V-03 |
| `publishCourse` → `publishCourseInternal` | revisão, completude, versão, idempotência e dois outbox | V-04, V-05 |
| `discardCourseDraft` → `discardCourseDraftInternal` | restaura rascunho se revisão coincide | V-05 |
| `listCourseVersions` → `listCourseVersionsInternal`; `getCourseVersion` → `getCourseVersionInternal` | histórico e retrato imutável | V-05 |
| `deleteCourse` → `deleteCourseInternal` | exclusão só antes da primeira publicação | V-06 |
| `resolveCourseReferencesInternal` | consulta administrativa de títulos atuais, restrita ao papel administrador | V-04 |
| `listVideos`, `getVideo` de CAP-006 | seletor e enriquecimento; não autorizam o vínculo em `learning` | V-03 |

**Regras além da forma OpenAPI:** toda consulta por curso, módulo, aula ou versão filtra `tenantId` antes de avaliar existência; item pertencente a outro curso também resulta em 404. `draftRevision` é verificada apenas em publicar/descartar, não nas edições por item. Mesmo `Idempotency-Key` e corpo na janela de 24 h repetem status, corpo e `Location`; corpo diferente responde 422. `COURSE_INCOMPLETE` contém uma entrada por falta na ordem pedagógica. O BFF preserva os erros de negócio acordados de `learning`, mas substitui falha/timeout de upstream por `LEARNING_UNAVAILABLE` 502/504 sem detalhe interno. Consulta de curso continua 200 quando falha apenas o enriquecimento em Mídia.

### Mapeamento de mensagens e dados

| Contrato e identificador | Produtor → consumidor | Aplicação e evidência |
|---|---|---|
| [AsyncAPI learning](asyncapi-contract.yaml) `receberAtivoPronto`, `receberPreparacaoFalhou` | Media → `learning` | visão por tenant/ID, deduplicação e `VIDEO_NOT_AVAILABLE` até pronto; V-03 |
| [AsyncAPI learning](asyncapi-contract.yaml) `publicarVersaoPublicada` · `conteudo.versao-publicada.v1` | `learning` → Media; futuros Catálogo e Aprendizagem | estado completo da vigente, `eventId` fixo por versão, IDs e títulos conforme C-09; V-04/V-05 |
| [AsyncAPI audit](asyncapi-contract-audit.yaml) `publicarAtoVersaoPublicada`/`receberAtoPraticado` · `auditoria.ato-praticado.v1` | `learning` → `audit` | mesmo `fatoId` do fato; conforme, único por (`origem`,`fatoId`), sem motivo; V-04 |
| [AsyncAPI media](asyncapi-contract-media.yaml) `receberVersaoPublicada` | `learning` → Media | substituição atômica por maior número, só IDs, reentrega/atraso sem efeito; V-04/V-05 |

Não há ODCS nesta entrega: a estrutura publicada é comunicada pelo fato, e o banco de `learning` não é produto de dados compartilhado. Versões de entrada são os contratos de CAP-006 (Media 1.0.0), CAP-030 (Audit 1.1.0) e CAP-002 (Identity 1.1.0); o acordo deste PRD acrescenta receptor Media 1.1.0, Audit 1.2.0, permissão/audiência Identity 1.2.0 e as interfaces novas de `learning`/BFF 1.0.0. Isso é compatibilidade de contrato aprovado, não evidência de implantação.

### Mapeamento de jornada

| Jornada | Operações e resultado verificável |
|---|---|
| US-01/08 | `createCourse`, `listCourses`, `getCourse`: rota pública, escola e permissão corretas |
| US-02 | `listVideos`, `getVideo`, `updateLesson`: somente pronto e mesma escola |
| US-03/07 | `publishCourse`, `receberAtoPraticado`, consulta existente da trilha: pendências ou publicação conforme |
| US-04/06 | `updateCourse`/`updateLesson`, `discardCourseDraft`, `listCourseVersions`, `getCourseVersion`: vigente intacta até republicar |
| US-05 | `deleteCourse`: apenas nunca publicado |

### Entidades do domínio

| Entidade do Domain Doc | Representação e limite |
|---|---|
| Curso, Rascunho, Módulo, Aula | agregado de Conteúdo em `learning`, schema `content`, com tenant e IDs estáveis; rascunho é o único lado editável |
| Versão de Publicação | retrato imutável numerado por curso em `learning`; ponteiro único para vigente |
| Mídia, Referência de Uso | vídeo existente em Media; projeção de prontidão em `learning` e referências opacas versionadas em Media |
| Ato Administrativo, Registro de Auditoria | envelope produzido por `learning`; registro append-only em `audit`, sem cópia do nome do autor |

## Arquivos a Modificar e a Referenciar

### A modificar

| Caminho existente | Fatia | Alteração |
|---|---|---|
| `src/identity/src/CodeForCoders.Identity.Domain/Entities/StaffRoleCatalog.cs`; configuração de audiência de Identity em `docker-compose.yml` e deploy | V-01 | permissão do professor e token para `learning` |
| `src/learning/src/CodeForCoders.Learning.Infra.Data/LearningDbContext.cs`; `src/learning/src/CodeForCoders.Learning.Api/Extensions/EndpointExtensions.cs`; `src/learning/src/CodeForCoders.Learning.Api/Extensions/ServiceConfigurationExtensions.cs` | V-01 a V-05 | persistência, endpoints internos e validação do JWT |
| `src/learning/src/CodeForCoders.Learning.Infra.Messaging/RabbitMqPublisher.cs`; `src/learning/src/CodeForCoders.Learning.Infra.Messaging/RabbitMqTopologyInitializer.cs`; `src/learning/src/CodeForCoders.Learning.Infra.Messaging/Configuration/RabbitMqOptions.cs` | V-03/V-04 | consumir fatos de Mídia e entregar mensagens de `content` aos exchanges acordados |
| `src/media/src/CodeForCoders.Media.Infra.Data/MediaDbContext.cs`; `src/media/src/CodeForCoders.Media.Infra.Messaging/RabbitMqTopologyInitializer.cs`; `src/media/src/CodeForCoders.Media.Infra.Messaging/DependencyInjection.cs` | V-04/V-05 | referências opacas, versão aplicada e consumo de `learning.events` no papel worker |
| `src/audit/src/CodeForCoders.Audit.Domain/Policies/AdministrativeActPolicy.cs` | V-04 | novo tipo conforme, sem motivo obrigatório |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/EndpointExtensions.cs`; `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/ServiceConfigurationExtensions.cs`; `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/AuditRecordEndpoints.cs` | V-01 a V-06 | autoria, clientes `learning`/Media e resolução de curso na trilha |
| `src/admin-spa/src/config/paths.ts`; `src/admin-spa/src/app/router.tsx`; `src/admin-spa/src/features/staff-session/utils/get-staff-areas.ts` | V-01/V-05 | rotas e navegação da área Autoria |
| `src/admin-spa/src/features/audit-trail/components/audit-trail-screen.tsx`; `src/admin-spa/src/features/audit-trail/components/audit-record-detail-screen.tsx` | V-04 | rótulo e tratamento de alvo curso |

### A referenciar (não alterar)

| Caminho | Por que consultar |
|---|---|
| [PRD](prd.md), [índice de contratos](contracts.md) e os cinco contratos ligados no cabeçalho | comportamento, schemas e operação aprovados; nenhuma definição duplicada aqui |
| `src/media/src/CodeForCoders.Media.Application/UseCases/Videos/PrepareVideo/PrepareVideo.cs`; `src/media/src/CodeForCoders.Media.Api/Endpoints/VideoEndpoints.cs` | IDs dos fatos existentes e consulta pronta para seletor |
| `src/learning/src/CodeForCoders.Learning.Infra.Data/Outbox/OutboxMessageConfiguration.cs`; `src/learning/src/CodeForCoders.Learning.Infra.Messaging/OutboxPublisherWorker.cs` | outbox atual em `progress`, a preservar ao acrescentar o outbox de `content` |
| `src/identity/src/CodeForCoders.Identity.Api/Security/StaffSessionTokenIssuer.cs`; `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Security/BffSecurityMiddleware.cs` | token, sessão e revogação da ADR-0005 |
| `src/media/src/CodeForCoders.Media.Infra.Data/Videos/VideoUploadRepository.cs` | precedente de bloqueio por linha e transação no PostgreSQL |
| `.agents/skills/dotnet/SKILL.md`; `.agents/skills/react/SKILL.md` | convenções de implementação, teste e observabilidade da stack |

## Análise de Impacto

| Componente | Tipo | Impacto e ação requerida |
|---|---|---|
| Identity | modificado | enum de permissão e audiência `learning`; emitir em sessão validada sem estender vida do token |
| Learning | novo domínio sobre serviço existente | schema `content`, filas de consumo, duas rotas de outbox, migrations geradas por EF; mantém banco e deploy próprios |
| Media | modificado | registrar só IDs por curso/versão; receber novo fato; prover replay do outbox histórico sem novo HTTP |
| Audit | modificado | política aditiva; instalar versão 1.2.0 antes da primeira publicação para evitar `tipo-desconhecido` |
| BFF e SPA | modificados | nova área, clientes internos e resolução de curso na trilha; nenhuma URL de serviço no SPA |
| Catálogo/Aprendizagem | consumidores futuros | podem consumir o estado completo publicado; nenhuma implementação nem leitura síncrona nesta entrega |
| Plataforma local/deploy | configuração | exchanges, bindings e permissões RabbitMQ, migrations EF, ordem de rollout e reconstrução da projeção |

## Riscos e Preocupações

| Preocupação | Local (`arquivo:linha`) | Impacto | Mitigação |
|---|---|---|---|
| Outbox de `learning` publica sempre no mesmo exchange | `src/learning/src/CodeForCoders.Learning.Infra.Messaging/RabbitMqPublisher.cs:28` | ato não chega a Audit | roteamento por destino persistido na linha, com `audit.events` declarado e confirmado antes da resposta ao cliente |
| Outbox existente de `learning` pertence a `progress` | `src/learning/src/CodeForCoders.Learning.Infra.Data/Outbox/OutboxMessageConfiguration.cs:11` | publicar conteúdo ali cruzaria a propriedade de schema do baseline | outbox próprio em `content`, no mesmo commit do Curso; preservar o de `progress` |
| ID da linha do outbox é chave primária | `src/learning/src/CodeForCoders.Learning.Infra.Data/Outbox/OutboxMessage.cs:9` | usar o mesmo `eventId` nas duas linhas falha o commit | duas IDs de mensagem; `fatoId` do ato igual ao `eventId` do fato no payload |
| `learning` só liga fila de heartbeat | `src/learning/src/CodeForCoders.Learning.Infra.Messaging/RabbitMqTopologyInitializer.cs:38` | fatos de vídeo não alimentam a visão local | fila de domínio ligada a `media.events`, com DLQ, replay e evidência de consumo |
| Mídia emite `ativo-pronto` só na transição; fila temporária tem TTL e descarte | `src/media/src/CodeForCoders.Media.Application/UseCases/Videos/PrepareVideo/PrepareVideo.cs:205`; `src/media/src/CodeForCoders.Media.Infra.Messaging/RabbitMqTopologyInitializer.cs:82` | vídeos antigos nunca são vinculáveis | replay pelo outbox durável de Media e reconciliação com vídeos ready antes do rollout |
| Media só oferece leitura individual de vídeo para IDs conhecidos | `src/media/src/CodeForCoders.Media.Api/Endpoints/VideoEndpoints.cs:24` | curso grande pode gerar muitas chamadas de enriquecimento | deduplicar IDs, limitar concorrência/prazo no BFF e manter `videoId` quando a consulta não responder; medir latência e fração enriquecida |
| `audit` reconhece apenas tipos de Identidade | `src/audit/src/CodeForCoders.Audit.Domain/Policies/AdministrativeActPolicy.cs:15` | publicação registrada como não conforme | ampliar política e implantar Audit antes de Learning publicar |
| Consulta da trilha resolve só tipos de Identidade | `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/AuditRecordEndpoints.cs:14` | administrador vê ID do curso sem título | resolver `curso` em Learning na lista e no detalhe, sem expor estrutura a administrador |
| Detalhe da trilha trata todo alvo como pessoa | `src/admin-spa/src/features/audit-trail/components/audit-record-detail-screen.tsx:377` | ação e rótulo incorretos para curso | apresentar alvo como curso e filtrar por `targetId` com texto próprio |
| Autoria é item de menu sem endereço | `src/admin-spa/src/features/staff-session/utils/get-staff-areas.ts:10` | professor não alcança editor e link direto não tem rota | ligar `paths.ts`, router e menu; smoke com base `/admin/` e refresh direto |
| Nome do ator será copiado para `learning` | `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Security/BffSecurityMiddleware.cs:128` | novo dado pessoal persiste em banco e backups | restringir a retratos, não enviar em fatos/logs, catalogar para CAP-031/AB03 |

## Decisões Técnicas

- **Bloqueio do Curso e revisão:** serializar edição/publicação por curso no PostgreSQL e conferir `draftRevision` somente nas duas ações contratadas. Ganha numeração contínua e impede publicação de edição invisível; custa contenção por curso, limitada ao tempo da transação sem rede. É decisão local da feature; há precedente de bloqueio por linha no repositório.
- **Retrato imutável de versão:** guardar versão completa e metadados num registro independente do rascunho; leitura histórica usa o retrato, nunca joins com estrutura editável. Ganha isolamento de republicação; custa armazenamento proporcional às versões. O contrato já exige fato completo e histórico.
- **Replay de fatos antigos de vídeo:** usar o outbox retido de Media, com IDs originais, para construir a projeção após ligar a fila. Ganha reconstrução sem acesso cruzado a bancos; custa uma operação controlada e verificação por tenant antes do rollout. Se o histórico não estiver completo em algum ambiente, a equipe de Mídia deve resolver a lacuna antes de publicar o primeiro curso ali.

## Verificação

**Cenários críticos:** duplicação e inversão de fatos; commit interrompido antes/depois de cada linha do outbox; dois professores publicando o mesmo `draftRevision`; edição concorrente com publicação; reuso da chave com corpo diferente; módulo movido/removido; vídeo pronto de outro tenant; título do vídeo alterado em Media; Mídia indisponível no enriquecimento; papel revogado na sessão aberta; alvo curso na lista e no detalhe da Auditoria. Validar o tamanho do fato de uma estrutura no limite C-07 contra a configuração real do broker antes de liberar cursos desse porte.

**Ambiente reproduzível:** `./scripts/generate-local-env.sh`, `./scripts/apps.sh start`, migrations EF geradas e aplicadas como etapa de deploy (o `start` não as aplica), professor com dois vídeos prontos e administrador, bancos por serviço, RabbitMQ e Valkey saudáveis. No Compose local, aplicar `dotnet ef database update` com a connection string de cada serviço e seus projetos `Infra.Data`/`Api`, no padrão reproduzível de [desenvolvimento local](../../docs/student-registration-local-development.md); no ambiente remoto, `./scripts/remote-infra.sh migrate`. Para integração e smoke em infraestrutura compartilhada, usar tenant/IDs, bases e filas com namespace de teste ou rodar apenas uma stack por vez, conforme [README](../../README.md); não deixar consumers de dois ambientes competindo pela mesma fila. Antes do smoke de autoria, ligar as filas, aplicar migrations de Identity/Learning/Media/Audit e reconciliar a projeção de vídeos.

**Ordem de implantação:** Identity 1.2.0 (`autoria.editar`, audiência `learning`) → Audit 1.2.0 → Media com receptor de versão → Learning com consumidor de Mídia e fila ligada aos fatos existentes, ainda sem rota pública no BFF → replay/reconciliação da visão de vídeos → Learning publicando e BFF/SPA expondo Autoria. A ordem evita ato não conforme e versão publicada sem consumidor de referências.

**Contratos:** confrontar respostas e erros das 15 operações públicas e 16 internas com os OpenAPI aprovados; validar consumo/publicação das seis operações AsyncAPI nas duas direções, incluindo `eventId`/`fatoId`, maior versão e DLQ/reprocessamento. A validação sintática dos YAMLs já registrada em `contracts.md` não comprova esses cenários. Verificar ainda as URLs públicas de lista, editor e versão após refresh e a consulta administrativa em `http://localhost:8081/admin/auditoria`.

**Observabilidade específica:** contagem de publicação por resultado, atraso do outbox por destino, atraso/falha dos consumidores de vídeo e versão, divergência entre versão vigente e versão aplicada em Media, e ato de publicação não conforme; dimensões apenas técnicas e IDs opacos, sem nome ou título. Alertar quando DLQ ou lag impedirem a prova do rollout.

## Questões em Aberto

- [x] Aprovação dos wireframes/Figma da nova área Autoria antes do código visual — responsável pelo produto aprovou explicitamente em 2026-09-29; [registro e desenho](../../docs/design/wireframes-autoria-curso.md) — gate visual satisfeito, sem mudar os contratos ou o modelo técnico.
- [ ] Política de retenção e atendimento ao titular para o retrato do nome do professor — negócio e Auditoria, em AB03/CAP-031 — afeta operação futura; o retrato fica identificado no inventário de dados de `learning` até essa decisão.

## Architecture Decision Records

- [ADR-0001](../../docs/adr/0001-monorepo-de-codigo.md) — serviços e deploys continuam independentes apesar da mudança coordenada.
- [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md) — revalidação de sessão, JWT curto e autorização no serviço dono.
- [ADR-0006](../../docs/adr/0006-preparacao-de-video-e-custodia-de-chave.md) — consumo do fato de versão no papel worker de Media, preservando a API para HTTP.
- [ADR-0007](../../docs/adr/0007-snapshots-efemeros-da-consulta-de-auditoria.md) — consulta da trilha mantém sua paginação estável; o BFF só enriquece os alvos retornados.

Nenhuma ADR nova ou Accepted substituída.
