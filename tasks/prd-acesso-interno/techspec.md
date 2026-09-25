---
tsg_artifact: techspec
product: code-4-coders
capability: CAP-002
version: 1.0
status: approved
updated: 2026-09-25
sources: tasks/prd-acesso-interno/prd.md@1.0, tasks/prd-acesso-interno/contracts.md@1.1
---

# TechSpec — acesso interno por papel e permissão

> - **Escopo:** Full-stack (`identity`, `bff-admin`, `admin-spa`, `notification`, `commerce`)
> - **Modo:** Pipeline, API-First
> - **PRD de origem:** [prd.md](prd.md), v1.0, aprovado em 2026-09-25
> - **Contratos:** [contracts.md](contracts.md) — [OpenAPI SPA → BFF](api-contract.yaml), [AsyncAPI Identity 1.1.0](asyncapi-contract.yaml), [AsyncAPI Notificação 1.1.0](asyncapi-contract-notification.yaml), [AsyncAPI Auditoria 1.0.1](../prd-trilha-auditoria/asyncapi-contract.yaml). Contrato interno `bff-admin` → Identity/Commerce: **a produzir após a aprovação desta spec** (§ Interfaces entre fatias ou times)
> - **Data:** 2026-09-25
> - **Status:** Aprovado em 2026-09-25
> - **Handoff:** approved — contrato interno (EN-01) antes do Task Creator

## Resumo Executivo

Entregar o acesso interno ao backoffice nas quatro pontas que já existem como esqueleto ou serviço
entregue. **Identity** passa a ter conta interna, atribuição de papel, catálogo fixo de permissões,
Convite de Acesso Interno e sessão do ator interno; publica os quatro atos no envelope da Auditoria e
os pedidos de e-mail no canal de Notificação, sempre pelo outbox. **`bff-admin`** deixa de ser esqueleto:
expõe as 14 operações do contrato, guarda o identificador opaco em Valkey e valida a sessão em Identity
a cada ação protegida. **`admin-spa`** ganha entrada, recuperação de senha, aceite de convite, gestão de
acesso e o início por permissão. **Notificação** aceita o modelo `convite-interno` (contrato 1.1.0).
**`commerce`** expõe a área financeira reservada e a protege validando o JWT localmente.

**Decisões principais:**

- Vigência da sessão interna e permissão vigente decididas em Identity a cada ação protegida, com
  emissores de asserção separados por borda e JWT com claims de papel/permissão validado via JWKS no
  serviço dono ([ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md), nova).
- O primeiro administrador nasce por um comando de provisionamento do próprio binário de Identity e
  define a senha pelo mesmo fluxo de redefinição do RF-11 — a senha nunca passa pelo time.
- A troca de papel é uma transação só em Identity que grava estado, encerramento de sessões e os dois
  atos no outbox.

**Trade-off primário:** revogação imediata e autorização sem confiar no BFF custam uma chamada
`bff-admin` → Identity por ação protegida e a introdução de JWKS e de validação de JWT no primeiro
serviço de domínio. Em troca, nenhuma cópia de permissão sobrevive à revogação e o padrão de
autorização fica pronto para `CAP-005` e `CAP-011`.

---

## Arquitetura da Solução

```text
admin-spa ──/api/v1──▶ bff-admin ──asserção bff-admin──▶ identity ──outbox──▶ audit.events  (auditoria.ato-praticado.v1)
 (/admin/)              │  Valkey: sessão opaca          │                └─▶ notification.events.{ns} (notificacao.envio-solicitado.v1)
                        │                                └─ JWKS (rede interna)
                        └──JWT aud=commerce──▶ commerce (/internal/v1/finance-area, valida via JWKS)
```

**URLs públicas (local / Compose):** SPA do backoffice em `http://localhost:8081/admin/`; o nginx do SPA
encaminha `/api/v1/` ao `bff-admin`.

| Link | URL pública completa (local) | Configuração |
|---|---|---|
| Aceite de convite | `http://localhost:8081/admin/convite?token=<segredo>` | `StaffInvitation:AcceptanceBaseUrl` em Identity |
| Redefinição de senha interna | `http://localhost:8081/admin/redefinir-senha?token=<segredo>` | `StaffAccount:PasswordResetBaseUrl` em Identity |
| Entrada | `http://localhost:8081/admin/entrar` | rota do SPA |

Em outros ambientes a origem vem da configuração; o caminho `/admin/` é o `BASE_PATH` do SPA.

### Bloco Backend

**Identity — modelo**

- **Conta interna:** a mesma entidade `Account`, com `AccountType.InternalActor`, criada já confirmada
  (abrir o link do convite ou do seed comprova o e-mail). A unicidade de e-mail por tenant entre todos
  os tipos já existe no banco (`accounts`, índice único `tenant_id, normalized_email` filtrado por não
  desativada) e é o que impede e-mail de aluno em conta interna (RN-13a).
- **Atribuição de papel:** entidade própria por conta e papel, única por (`tenant_id`, conta, papel).
  Papéis acumulam (RN-13, OD26). Nenhuma permissão é atribuída à conta (RN-12).
- **Catálogo de permissões:** fixo no domínio de Identity, não em tabela: administrador →
  `acesso.gerir`; financeiro → `financeiro.ler`; professor → `autoria.ler`; suporte →
  `suporte.atender` (DP-03). Permissão efetiva = união das permissões dos papéis vigentes. Capacidade
  futura acrescenta permissão ao catálogo, não papel.
- **Convite de Acesso Interno:** entidade própria com e-mail e e-mail normalizado, papel ofertado,
  autor, motivo, hash do segredo, validade, situação (pendente / aceito / substituído) e conta criada no
  aceite. No máximo **um convite pendente** por (`tenant_id`, e-mail normalizado) — garantido por índice
  único parcial. Expirado é derivado do relógio, não gravado. O segredo não usa `VerificationToken`,
  que exige conta existente.
- **Sessão do ator interno:** entidade própria, separada de `StudentSession` (ADR-0005), com a mesma
  semântica de inatividade deslizante, revogação e expiração. Configuração própria
  (`StaffSession:InactivityTimeoutMinutes`, padrão 60 até QT-01 do PRD).
- **Motivo:** guardado no convite e nos atos; nunca em log, span, métrica ou erro.

**Identity — regras e transações** (cada item é um commit único que inclui o outbox)

- *Emitir convite* (RF-03): exige autor com `acesso.gerir` (verificado em Identity pelo ator da sessão,
  não pelo BFF); normaliza e-mail (RN-01); recusa e-mail com conta interna (`EMAIL_BELONGS_TO_STAFF`) ou
  de aluno (`EMAIL_BELONGS_TO_STUDENT`); marca o pendente anterior como substituído (DP-04); grava o
  convite, o pedido `convite-interno` (payload protegido) e o ato `convite-interno-emitido`.
- *Aceitar convite* (RF-05): valida segredo, validade e situação; recusa se o e-mail passou a ter conta
  (`INVITATION_EMAIL_UNAVAILABLE` — a unicidade do banco é a última barreira contra corrida); aplica a
  política de senha de CAP-001; cria conta interna, credencial, atribuição do papel ofertado, sessão do
  ator interno e o ato `convite-interno-aceito`. **Não** grava `papel-concedido`.
- *Conceder / revogar / trocar* (RF-06, RF-07, RF-09): exigem autor com `acesso.gerir` e alvo diferente
  do autor (RN-20); alvo inexistente ou de aluno → `STAFF_MEMBER_NOT_FOUND`. Sem efeito → nada gravado
  além do registro de idempotência (`changed: false`). Revogação e troca **revogam todas as sessões do
  ator interno alvo no mesmo commit** (RN-17). Troca = remoção de `fromRole` + inclusão de `toRole` +
  dois atos com o mesmo `praticadoEm` e `correlationId`, um commit; `fromRole` ausente →
  `ROLE_NOT_HELD`; `toRole` já presente → só a revogação e um ato.
- *Concorrência:* duas concessões simultâneas do mesmo papel colidem no índice único — a perdedora
  resolve como `changed: false`, sem segundo ato. Revogação concorrente do mesmo papel: a primeira
  remove, a segunda não encontra e resolve como `changed: false`.
- *Último administrador:* não há regra extra — RN-20 impede o autor de agir sobre si e só administradores
  agem, então sempre resta ao menos um (PRD, Riscos).
- *Entrada do ator interno* (RF-10): credencial errada, e-mail inexistente e conta de aluno → a mesma
  falha `INVALID_CREDENTIALS`, com verificação de senha contra hash fictício quando não há credencial,
  como em CAP-001. Conta interna sem papel entra (C-05). A entrada do aluno já recusa conta interna
  (`AuthenticateStudentSession` filtra `AccountType.Student`) — só precisa de teste.
- *Recuperação interna* (RF-11): mesmo comportamento de CAP-001 restrito a `InternalActor`; o link
  aponta para o backoffice; redefinir revoga as sessões internas da conta. A recuperação do aluno
  continua sem atender conta interna.
- *Validação da sessão interna* (ADR-0005): valida e renova atomicamente; devolve conta, nome, papéis,
  permissões e, se pedido, JWT com audiência permitida e claims `roles`/`permissions`. Sessão revogada
  ou expirada → `SESSION_REQUIRED`.
- *Idempotência:* reusa `IdempotencyRecord` de Identity, janela de 24 h, escopo por tenant, operação e
  conta autora; fingerprint inclui corpo e alvo.

**Identity — provisionamento do primeiro administrador** (RF-02)

Comando de linha do mesmo binário de Identity (`provision-first-admin --tenant <id> --email <e-mail>
--name <nome>`), executado pelo time uma vez por tenant, fora da API. Se o tenant já tem conta interna
com o papel administrador, termina sem mudar nada e informa. Senão, num commit: cria a conta interna sem
credencial, atribui administrador, cria o segredo de redefinição e grava o pedido
`recuperacao-de-senha` com link do backoffice. **Não** grava ato de Auditoria (RN-25, RN-A12). Recusa
e-mail de conta de aluno. O worker de outbox do Identity em execução publica o pedido. O comando não
imprime link, segredo nem e-mail; registra a execução (data, tenant) no log operacional.

**Identity — publicação**

- Ato: tipo de outbox próprio, routing key `auditoria.ato-praticado.v1`, exchange da Auditoria
  (`RabbitMq:AuditExchange`, `audit.events` local — o mesmo que `audit` declara em
  `src/audit/src/CodeForCoders.Audit.Api/appsettings.json:15`). Payload conforme
  `AtoPraticadoIdentidade` de [asyncapi-contract.yaml](asyncapi-contract.yaml); **protegido** no outbox
  porque carrega o motivo, texto livre.
- Pedido de envio: exchange de Notificação já configurado (`RabbitMq:NotificationExchange`), payload
  protegido (e-mail e segredo no link), `pedidoId` estável por intenção.
- `fatoId`/`pedidoId` gerados na transação; reentrega do outbox repete-os.

**Identity — JWKS e emissores**

- Endpoint JWKS interno com as chaves públicas de assinatura do JWT de usuário (atual e anterior, por
  `kid`), não exposto pelo BFF.
- Verificação de asserção com **vários emissores**, cada um com chaves públicas e escopos permitidos
  (`bff-student`: escopos de aluno de CAP-001; `bff-admin`: escopos de backoffice). Configuração
  validada na partida.

**`bff-admin`**

- Implementa as 14 operações do [api-contract.yaml](api-contract.yaml) chamando Identity (ou `commerce`,
  na área financeira) pelo contrato interno. Não guarda papel nem permissão; lê da validação a cada ação.
- Cookie opaco `staff_session` (nome do contrato), Valkey com o identificador da sessão interna, prova
  CSRF vinculada à sessão e verificação de origem — o mesmo modelo do `bff-student`, substituindo o
  CSRF por cookie do esqueleto atual.
- Autorização em duas camadas: o BFF recusa com 403 `PERMISSION_DENIED` quando a permissão vigente não
  cobre a operação (gestão: `acesso.gerir`; área financeira: `financeiro.ler`) e Identity/`commerce`
  recusam de novo.
- Falha de Identity → 502/504 `IDENTITY_UNAVAILABLE`, sem renovar TTL local (falha fechada).
- Asserção de serviço com chave exclusiva do `bff-admin` (ADR-0004/0005).

**`commerce`**

- Operação interna da área financeira reservada: valida JWT localmente via JWKS de Identity
  (emissor, audiência `commerce`, validade, assinatura) e exige `financeiro.ler` no claim; responde
  `{ "status": "reserved" }`. Sem banco, sem dado. É o primeiro uso de validação de JWT no serviço.

**Notificação** (contrato 1.1.0)

- Aceita finalidade/modelo `convite-interno` e `dados.papel`; `nome` passa a ser exigido só em
  `confirmacao-de-conta` e `recuperacao-de-senha`. Novo modelo de mensagem com papel ofertado, link e
  validade (texto de Notificação). Consentimento transacional aceita `convite-interno`. Validade
  `ValidityHoursByPurpose["convite-interno"]` validada na partida, **igual** a
  `StaffInvitation:LifetimeHours` de Identity (168 h, QT-02 do PRD).

### Bloco Frontend

- **Rotas novas no SPA** (base `/admin/`): `entrar`, `recuperar-senha`, `redefinir-senha`, `convite`,
  início, `acessos` (gestão) e `financeiro`. As três primeiras e `convite` ficam fora do layout
  autenticado.
- **Estado:** identidade, papéis e permissões vêm sempre de `getCurrentStaffSession` (estado de
  servidor); o menu e as rotas protegidas usam `permissions`. Nenhuma cópia em store global. Formulários
  e o token do link vivem só durante a operação.
- **Token do link:** lido da URL, removido da barra/histórico logo após a captura e enviado no corpo
  (`lookupStaffInvitation`, `acceptStaffInvitation`, `resetStaffPassword`).
- **Erros:** `INVALID_CREDENTIALS` e recuperação com mensagem genérica; `INVITATION_INVALID` com
  orientação de pedir novo convite; 401 leva à entrada; 403 `CSRF_INVALID` reobtém a sessão sem repetir
  a operação; 403 `PERMISSION_DENIED` mostra "sem permissão"; conta com `permissions: []` vê a
  orientação de conta sem acesso (RF-08).
- **Ações de gestão:** conceder, revogar, trocar exigem motivo e confirmam o efeito ("a pessoa será
  desconectada agora" em revogação e troca); a linha `isSelf` não mostra ações.
- **nginx do SPA:** passa a encaminhar `/api/v1/` ao `bff-admin` e a registrar acesso sem query string,
  como o `student-spa` — os links carregam segredo na query.

---

## Mapa de Fatias Verticais

Cada fatia atravessa SPA, BFF e os serviços necessários, com teste e observabilidade no mesmo
incremento. Nomes internos derivam das skills `dotnet` e `react`.

### V-01: Primeiro administrador provisionado define a senha pelo link recebido

- **Cobre:** RF-02, RF-11 (redefinição), RF-01 (papel administrador); RN-06, RN-07, RN-13a, RN-25,
  RN-A12.
- **Entrada / gatilho:** `provision-first-admin` executado contra o Identity local; depois,
  `http://localhost:8081/admin/redefinir-senha?token=…` aberto do e-mail → `resetStaffPassword`.
- **Processamento:** Identity cria conta interna + papel administrador + segredo + pedido
  `recuperacao-de-senha` num commit, sem ato; Notificação 1.0.0 já entrega o e-mail. O SPA lê o token,
  limpa a URL e envia a nova senha; `bff-admin` → Identity consome o segredo e grava a credencial.
- **Saída observável:** e-mail no smtp4dev com link do backoffice; 204 na redefinição; repetir o comando
  informa "tenant já tem administrador" e não cria nada; nenhum registro na trilha.
- **Evidência / checkpoint:** teste de integração de Identity (Testcontainers) para o comando
  idempotente e para e-mail de aluno recusado; smoke local: comando → smtp4dev → página de redefinição
  → 204; consulta à tabela de registros da Auditoria vazia para o tenant.
- **Bloqueado por:** EN-01.

### V-02: Ator interno entra, vê as áreas das suas permissões e sai

- **Cobre:** RF-10, RF-01, RF-08 (parte da entrada); RN-05, RN-08, RN-10, RN-11, RN-13, RN-18.
- **Entrada / gatilho:** formulário `entrar` → `createStaffSession`; carregamento do layout →
  `getCurrentStaffSession`; botão sair → `endCurrentStaffSession`.
- **Processamento:** Identity autentica só `InternalActor`, cria sessão interna; `bff-admin` grava
  Valkey e emite `staff_session`; a cada ação protegida valida a sessão em Identity (ADR-0005) e recebe
  papéis/permissões vigentes.
- **Saída observável:** administrador vê o início com a área "Acessos"; credencial errada, e-mail
  inexistente e conta de aluno recebem o mesmo erro; conta interna não entra no `student-spa`; saída
  invalida a sessão.
- **Evidência / checkpoint:** testes de Identity para as três credenciais inválidas indistinguíveis e
  para conta sem papel entrando com `permissions: []`; teste do `bff-admin` para falha fechada com
  Identity indisponível e para CSRF; teste do `student-spa`/Identity para conta interna recusada na
  entrada do aluno; smoke local no navegador com o administrador da V-01.
- **Bloqueado por:** V-01.

### V-03: Administrador convida e o convidado recebe o e-mail

- **Cobre:** RF-03, RF-04, RF-12 (convites pendentes), RF-14; RN-01, RN-13a, RN-14, RN-15, RN-19, RN-21,
  RN-26 a RN-28, RN-A05, RN-A08, RN-A14; DP-04, DP-05.
- **Entrada / gatilho:** gestão de acesso → `createStaffInvitation`; lista → `listPendingStaffInvitations`.
- **Processamento:** regras de *emitir convite*; Notificação 1.1.0 valida e renderiza `convite-interno`;
  Auditoria registra `convite-interno-emitido`.
- **Saída observável:** e-mail de convite no smtp4dev com papel, validade e link
  `…/admin/convite?token=…`; convite na lista de pendentes; novo convite ao mesmo e-mail substitui o
  anterior (`supersededInvitationId`); e-mail de conta interna/aluno recusado com o motivo; professor
  recebe 403.
- **Evidência / checkpoint:** testes de Identity para os quatro casos de recusa e para a substituição;
  teste de Notificação para `convite-interno` aceito, `convite-interno` sem `papel` recusado e os dois
  modelos anteriores inalterados; teste de contrato do ato contra o schema da Auditoria 1.0.1; smoke:
  convite → smtp4dev → registro **conforme** na trilha.
- **Bloqueado por:** V-02.

### V-04: Convidado aceita o convite e entra no backoffice

- **Cobre:** RF-05, RF-14; RN-01, RN-06, RN-15, RN-19, RN-A05.
- **Entrada / gatilho:** link `…/admin/convite?token=…` → `lookupStaffInvitation` →
  formulário → `acceptStaffInvitation`.
- **Processamento:** regras de *aceitar convite*; sessão aberta na mesma resposta (C-04).
- **Saída observável:** página mostra o papel ofertado e a validade; após o aceite o convidado cai no
  início com as áreas do papel; link reusado, expirado ou substituído mostra "convite não vale mais";
  registro `convite-interno-aceito` conforme na trilha, sem `papel-concedido`.
- **Evidência / checkpoint:** testes de Identity para uso único, expirado (relógio controlado),
  substituído, e-mail que ganhou conta entre emissão e aceite, senha fora da política; smoke: V-03 →
  aceite → início.
- **Bloqueado por:** V-03.

### V-05: Administrador concede e revoga papel, e a revogação desconecta na hora

- **Cobre:** RF-06, RF-07, RF-08, RF-12 (atores internos), RF-14; RN-12, RN-16, RN-17, RN-19, RN-20, RN-24,
  RN-A05; C-07.
- **Entrada / gatilho:** gestão de acesso → `listStaffMembers`, `grantStaffRole`, `revokeStaffRole`.
- **Processamento:** regras de *conceder/revogar*; revogação encerra as sessões internas do alvo no mesmo
  commit.
- **Saída observável:** papéis atualizados na lista; ator com sessão aberta recebe 401 na próxima ação
  depois da revogação; revogar o último papel leva à orientação de conta sem acesso; ação sem efeito
  responde `changed: false` sem registro na trilha; ação sobre si recusada; `isSelf` sem ações.
- **Evidência / checkpoint:** teste de Identity para concessão concorrente do mesmo papel (um ato só);
  teste de integração `bff-admin` + Identity: sessão B aberta, A revoga, próxima chamada de B → 401;
  smoke com dois navegadores.
- **Bloqueado por:** V-04.

### V-06: Administrador troca o papel numa ação só

- **Cobre:** RF-09, RF-14; RN-17, RN-19 (OD22), RN-20; C-03.
- **Entrada / gatilho:** gestão de acesso → `changeStaffRole`.
- **Processamento:** regras de *trocar*, um commit com estado, encerramento de sessões e dois atos.
- **Saída observável:** conta com `toRole` e sem `fromRole`; ator desconectado; trilha com
  `papel-revogado` e `papel-concedido` de mesmo autor, motivo, `praticadoEm` e `correlationId`.
- **Evidência / checkpoint:** teste de Identity que força falha antes do commit e verifica estado
  inalterado e outbox vazio; teste para `toRole` já presente (um ato) e `fromRole` ausente
  (`ROLE_NOT_HELD`); smoke com consulta à trilha.
- **Bloqueado por:** V-05.

### V-07: Professor não abre a área financeira por nenhuma via

- **Cobre:** RF-13; RN-16, RN-17, RN-18.
- **Entrada / gatilho:** menu/rota `financeiro` → `getFinanceArea`; chamada direta ao `commerce` com
  JWT de professor.
- **Processamento:** `bff-admin` recusa sem `financeiro.ler`; com a permissão, pede JWT de audiência
  `commerce` na validação e chama o `commerce`, que valida o token via JWKS e exige o claim.
- **Saída observável:** financeiro abre a área (`reserved`); professor não vê o item de menu, recebe 403
  por link direto e o `commerce` recusa o token dele; revogado o papel financeiro, a próxima chamada é
  recusada na borda.
- **Evidência / checkpoint:** testes do `commerce` para token sem permissão, audiência errada, assinatura
  de outra chave e expirado; teste de Identity para o JWKS com `kid` atual e anterior; teste do
  `bff-admin` para 403 antes de chamar o `commerce`.
- **Bloqueado por:** V-02. Pode correr em paralelo a V-03..V-06.

### V-08: Ator interno recupera a senha sozinho

- **Cobre:** RF-11 (pedido); RN-03, RN-04, RN-07.
- **Entrada / gatilho:** `recuperar-senha` → `requestStaffPasswordReset`.
- **Processamento:** pedido neutro; só conta interna gera `recuperacao-de-senha` com link do backoffice;
  a redefinição (V-01) revoga as sessões internas da conta.
- **Saída observável:** mesma resposta para conta interna, conta de aluno e e-mail inexistente; e-mail
  só para a conta interna; sessões anteriores encerradas após redefinir.
- **Evidência / checkpoint:** testes de Identity para as três respostas idênticas e para e-mail de aluno
  sem envio; smoke com smtp4dev.
- **Bloqueado por:** V-02.

### Habilitadores inevitáveis

| Habilitador | Por que não cabe numa fatia | Menor escopo | Primeira fatia desbloqueada |
|---|---|---|---|
| EN-01 — contrato interno e credencial de serviço do `bff-admin` | O contrato interno aprovado é pré-requisito de ADR-0003/0004/0005 antes de implementar, e a chave do `bff-admin` precisa existir na configuração local antes de qualquer chamada; nenhum comportamento observável sozinho | `internal-api-contract.yaml` deste PRD (via `tsg-flow-contract-creator`); verificador de asserção com vários emissores e escopos; geração da chave do `bff-admin` em `scripts/generate-local-env.sh` e variáveis no Compose | V-01 |

---

## Contratos e Fronteiras

### Mapeamento do contrato de API

| operationId | Caminho de implementação |
|---|---|
| `createStaffSession` | `bff-admin` → Identity *criar sessão interna* → cookie `staff_session` + Valkey |
| `getCurrentStaffSession` | middleware de sessão do `bff-admin` (validação ADR-0005) → resposta com papéis/permissões/CSRF |
| `endCurrentStaffSession` | `bff-admin` → Identity *revogar sessão interna* → remove Valkey e cookie |
| `requestStaffPasswordReset` | `bff-admin` → Identity *pedir recuperação interna* |
| `resetStaffPassword` | `bff-admin` → Identity *redefinir senha interna* |
| `lookupStaffInvitation` | `bff-admin` → Identity *consultar convite* |
| `acceptStaffInvitation` | `bff-admin` → Identity *aceitar convite* → cookie + Valkey |
| `listPendingStaffInvitations`, `createStaffInvitation` | `bff-admin` (exige `acesso.gerir`) → Identity *listar/emitir convite* |
| `listStaffMembers`, `grantStaffRole`, `revokeStaffRole`, `changeStaffRole` | `bff-admin` (exige `acesso.gerir`) → Identity *listar/conceder/revogar/trocar* |
| `getFinanceArea` | `bff-admin` (exige `financeiro.ler`, pede JWT `aud=commerce`) → `commerce` *área financeira* |

**Validações além do contrato:**

| operationId | Regra | Camada |
|---|---|---|
| `createStaffInvitation` | Autor com `acesso.gerir` pela sessão verificada em Identity; um pendente por e-mail; e-mail livre de conta | Identity (domínio + banco) |
| `acceptStaffInvitation` | Segredo pendente e válido; e-mail ainda livre; política de senha de CAP-001 | Identity |
| `grantStaffRole`, `revokeStaffRole`, `changeStaffRole` | Autor ≠ alvo (RN-20); alvo `InternalActor` do tenant; motivo não vazio após aparar | Identity (domínio) |
| `createStaffSession` | Só `InternalActor` não desativada; hash fictício quando não há credencial | Identity |
| `getFinanceArea` | Claim `financeiro.ler` no JWT validado | `commerce` |

**Resposta de domínio → HTTP** (códigos do contrato):

| Situação | HTTP | `code` |
|---|---|---|
| Motivo vazio | 422 | `REASON_REQUIRED` |
| Ação sobre a própria conta | 422 | `SELF_ROLE_CHANGE_FORBIDDEN` |
| `fromRole` ausente na troca | 422 | `ROLE_NOT_HELD` |
| E-mail de conta interna / aluno no convite | 422 | `EMAIL_BELONGS_TO_STAFF` / `EMAIL_BELONGS_TO_STUDENT` |
| Convite sem validade | 422 | `INVITATION_INVALID` |
| E-mail ganhou conta antes do aceite | 422 | `INVITATION_EMAIL_UNAVAILABLE` |
| Senha fora da política | 422 | `PASSWORD_POLICY_VIOLATION` |
| Segredo de redefinição inválido | 422 | `RESET_TOKEN_INVALID` |
| Chave de idempotência com outro corpo | 422 | `IDEMPOTENCY_KEY_REUSED` |
| Alvo inexistente ou de aluno | 404 | `STAFF_MEMBER_NOT_FOUND` |
| Sem permissão | 403 | `PERMISSION_DENIED` |

> O contrato público usa `IDEMPOTENCY_KEY_REUSED`; o Identity de CAP-001 usa `IDEMPOTENCY_CONFLICT`
> internamente. O `bff-admin` traduz para o código do contrato público.

### Mapeamento de mensagens e dados

| Contrato e identificador | Produtor → consumidor | Comportamento a implementar | Evidência |
|---|---|---|---|
| AsyncAPI Identity 1.1.0 `publicarAtoPraticado` | Identity (send) → Auditoria | Quatro tipos; um ato por mudança efetiva; troca = dois atos no mesmo commit; `fatoId` estável na reentrega; exchange da Auditoria | Registro conforme por ato; nenhum para ação sem efeito, falha ou seed; reentrega sem duplicar |
| AsyncAPI Identity 1.1.0 `publicarPedidoDeEnvio` | Identity (send) → Notificação | `convite-interno` (papel, link) e `recuperacao-de-senha` (nome, link do backoffice); `pedidoId` estável | E-mail entregue no smtp4dev; pedido de recuperação para e-mail de aluno não publicado |
| AsyncAPI Notificação 1.1.0 `receberPedidoDeEnvio` | Notificação (receive) | Aceitar `convite-interno` com `papel`+`link`; manter os dois modelos anteriores | Aceito/recusado conforme o contrato; exemplos de 1.0.0 continuam aceitos |

**Percurso dos dados sensíveis:**

| Dado | Criado | Persistido | Viaja | Descartado |
|---|---|---|---|---|
| Segredo do convite | Identity, ao emitir | Só o hash, no convite; em claro apenas no payload **protegido** do outbox | Pedido a Notificação (link) → e-mail; URL do SPA (removida do histórico); corpo JSON ao BFF e a Identity | Hash invalidado no aceite/substituição; o payload do outbox segue a retenção do outbox de CAP-001 |
| Segredo de redefinição interna | Identity (pedido ou seed) | Hash em `verification_tokens`; payload protegido no outbox | Idem | Consumido na redefinição; invalidado por nova redefinição |
| E-mail do convidado/ator | SPA do administrador | Convite e conta; payload protegido no outbox | Pedido a Notificação (exceção RN-26) | Nunca em log, span, métrica, erro, ato de auditoria nem URL |
| Motivo | SPA do administrador | Convite; payload protegido do ato no outbox; registro da Auditoria | Ato à Auditoria | Nunca em log, span, métrica ou erro |
| Senha | SPA | Só derivação lenta na credencial | Corpo JSON sobre TLS até Identity | Nunca em idempotência em claro (fingerprint), log ou telemetria |
| JWT do ator interno | Identity, na validação | Não persistido; memória do BFF durante a requisição | `bff-admin` → `commerce` | Expira em minutos; nunca ao navegador |

### Mapeamento de jornada

| User Story | Tela | operationId | Evidência |
|---|---|---|---|
| US-01 | Acessos → Convidar | `createStaffInvitation` | V-03 |
| US-02 | Convite (link) | `lookupStaffInvitation`, `acceptStaffInvitation` | V-04 |
| US-03 | Acessos | `listStaffMembers`, `listPendingStaffInvitations` | V-03, V-05 |
| US-04 | Acessos → Conceder / Revogar | `grantStaffRole`, `revokeStaffRole` | V-05 |
| US-05 | Acessos → Trocar papel | `changeStaffRole` | V-06 |
| US-06 | Entrar, Início | `createStaffSession`, `getCurrentStaffSession` | V-02 |
| US-07 | Financeiro | `getFinanceArea` | V-07 |
| US-08 | (efeito no ator revogado) | `revokeStaffRole` + validação de sessão | V-05 |

### Entidades do domínio

| Entidade do Domain Doc | Representação técnica | Local |
|---|---|---|
| Conta (ator interno) | `Account` com `AccountType.InternalActor`, criada confirmada | Identity, tabela `accounts` existente |
| Credencial | `Credential` existente | Identity |
| Papel | Valor do catálogo de papéis no domínio; atribuição como entidade por conta e papel | Identity, tabela nova via EF |
| Permissão | Catálogo fixo no domínio (DP-03) | Identity, código |
| Convite de Acesso Interno | Entidade nova | Identity, tabela nova via EF |
| Sessão (ator interno) | Entidade nova, separada de `StudentSession` | Identity, tabela nova via EF |
| Token de Verificação | `VerificationToken` existente, finalidade de recuperação | Identity |

Migrations de Identity pela ferramenta do EF (instrução do projeto); nenhuma migration manual.

### Interfaces entre fatias ou times

**Contrato interno a produzir (EN-01)** — `tasks/prd-acesso-interno/internal-api-contract.yaml`, pelo
`tsg-flow-contract-creator`, antes das tasks. Conteúdo mínimo acordado aqui:

- `bff-admin` → Identity, base `/internal/v1`, asserção de serviço do emissor `bff-admin` com escopos
  por grupo: `staff-sessions:create|validate|revoke`, `staff-passwords:reset`,
  `staff-invitations:read|write`, `staff-members:read|write`. Operações espelham as 13 operações
  públicas que Identity decide e carregam, nas autenticadas, o identificador da sessão interna do autor
  (Identity resolve o ator pela sessão, nunca por header de identidade).
- Validação de sessão interna: entrada `sessionId` e audiência opcional; saída conta, nome, papéis,
  permissões, expiração e JWT opcional com claims `roles`/`permissions`.
- JWKS de Identity (rede interna).
- `bff-admin` → `commerce`: área financeira com `Authorization: Bearer <JWT aud=commerce>`.

---

## Arquivos a Modificar e a Referenciar

### A modificar

| Caminho | Fatia | Alteração |
|---|---|---|
| `src/identity/src/CodeForCoders.Identity.Domain/Entities/Account.cs` | V-01 | Fábrica de conta interna confirmada |
| `src/identity/src/CodeForCoders.Identity.Api/Security/ServiceAssertionVerifier.cs` | EN-01 | Vários emissores, escopos permitidos por emissor |
| `src/identity/src/CodeForCoders.Identity.Api/Security/ServiceAssertionOptions.cs` | EN-01 | Configuração por emissor |
| `src/identity/src/CodeForCoders.Identity.Api/Extensions/ServiceAssertionExtensions.cs` | EN-01 | Validação de partida por emissor |
| `src/identity/src/CodeForCoders.Identity.Api/Security/StudentSessionTokenIssuer.cs` | V-07 | Emissão com claims de papel/permissão para ator interno (ou emissor irmão), mesmo `kid`/chave |
| `src/identity/src/CodeForCoders.Identity.Api/Extensions/EndpointExtensions.cs` | V-01..V-08 | Mapear endpoints internos de backoffice e JWKS |
| `src/identity/src/CodeForCoders.Identity.Api/Program.cs` | V-01 | Modo de comando `provision-first-admin` antes de subir a API |
| `src/identity/src/CodeForCoders.Identity.Api/appsettings.json` | V-01, V-03 | `StaffAccount`, `StaffInvitation`, `StaffSession`, emissores de asserção, `RabbitMq:AuditExchange` |
| `src/identity/src/CodeForCoders.Identity.Application/Common/OutboxDestinationOptions.cs` | V-03 | Exchange da Auditoria |
| `src/identity/src/CodeForCoders.Identity.Infra.Data/IdentityDbContext.cs` | V-01 | Novas entidades |
| `src/identity/src/CodeForCoders.Identity.Infra.Data/DependencyInjection.cs` | V-01, V-03 | Registro de stores e opções novas |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Security/BffSecurityMiddleware.cs` | V-02 | Validação de sessão em Identity por ação, CSRF vinculado à sessão, origem, permissão |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Security/CsrfProtection.cs` | V-02 | Prova vinculada à sessão (como `bff-student`) |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/SessionEndpoints.cs` | V-02 | Substituir `/bff/session` pelas operações de sessão do contrato |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/EndpointExtensions.cs` | V-01..V-08 | Mapear as operações públicas |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Application/Common/OpaqueBffSession.cs` | V-02 | Sessão com identificador da sessão interna e CSRF, sem token upstream |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Application/Common/BffSecurityOptions.cs` | V-02 | Opções de cookie/CSRF/origem |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Api/appsettings.json` | V-02 | Cookie `staff_session`, cliente Identity/Commerce, asserção |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Security/BffSessionTransformProvider.cs` | V-02 | Deixar de usar `UpstreamAccessToken` da sessão; usar o JWT da validação corrente |
| `src/admin-spa/src/app/router.tsx` | V-01..V-08 | Rotas públicas e protegidas |
| `src/admin-spa/src/config/paths.ts` | V-01..V-08 | Caminhos novos |
| `src/admin-spa/src/components/app-shell.tsx` | V-02 | Menu por permissão, sair |
| `src/admin-spa/src/lib/api-client.ts` | V-02 | `withCredentials`, CSRF, `Idempotency-Key`, base `/api/v1` |
| `src/admin-spa/src/testing/handlers.ts` | V-02..V-08 | Handlers MSW das operações |
| `src/admin-spa/nginx.conf.template` | V-01 | `/api/v1/` → `bff-admin`; log sem query string |
| `src/notification/src/CodeForCoders.Notification.Application/Common/NotificationPurposes.cs` | V-03 | `convite-interno` |
| `src/notification/src/CodeForCoders.Notification.Application/Common/NotificationSendRequestRules.cs` | V-03 | Regras condicionais por modelo |
| `src/notification/src/CodeForCoders.Notification.Application/Services/MessageTemplateRenderer.cs` | V-03 | Modelo de convite |
| `src/notification/src/CodeForCoders.Notification.Application/Services/TransactionalConsentService.cs` | V-03 | `convite-interno` transacional |
| `src/notification/src/CodeForCoders.Notification.Contracts/TransactionalNotificationV1.cs` | V-03 | `Papel` em `NotificationTemplateDataV1` |
| `src/notification/src/CodeForCoders.Notification.Infra.Data/DependencyInjection.cs` | V-03 | Validade de `convite-interno` obrigatória na partida |
| `src/notification/src/CodeForCoders.Notification.Infra.Data/Configuration/EmailOptions.cs` | V-03 | Padrão de validade do convite |
| `src/notification/src/CodeForCoders.Notification.Api/appsettings.json` | V-03 | `ValidityHoursByPurpose["convite-interno"]: 168` |
| `src/commerce/src/CodeForCoders.Commerce.Api/Program.cs` e extensões de configuração | V-07 | Validação de JWT via JWKS e operação interna |
| `src/commerce/src/CodeForCoders.Commerce.Api/appsettings.json` | V-07 | Emissor, audiência, endereço JWKS |
| `docker-compose.yml` | EN-01, V-01, V-07 | Chaves e emissor do `bff-admin` em Identity; URLs de link do backoffice; `bff-admin` → Identity/Commerce; `commerce` → JWKS |
| `scripts/generate-local-env.sh` | EN-01 | Par de chaves do `bff-admin` |
| `docs/student-registration-local-development.md` | V-01 | Passo local do `provision-first-admin` |

### A referenciar (não alterar)

| Caminho | Por que consultar |
|---|---|
| `src/bff-student/src/CodeForCoders.BffStudent.Api/Security/BffSecurityMiddleware.cs` | Padrão de validação por ação, falha fechada, CSRF e cookie |
| `src/bff-student/src/CodeForCoders.BffStudent.Api/Security/ServiceAssertionTokenFactory.cs` | Emissão da asserção de serviço |
| `src/identity/src/CodeForCoders.Identity.Application/UseCases/Accounts/AuthenticateStudentSession/AuthenticateStudentSession.cs` | Idempotência, hash fictício, respostas indistinguíveis |
| `src/identity/src/CodeForCoders.Identity.Application/Services/StudentPasswordRecoveryMessageWriter.cs` | Pedido de envio protegido no outbox, link com segredo |
| `src/identity/src/CodeForCoders.Identity.Application/Common/StudentPasswordPolicy.cs` | Política de senha reaproveitada |
| `src/identity/src/CodeForCoders.Identity.Infra.Data/Outbox/OutboxMessageWriter.cs` | Exchange por mensagem e proteção de payload |
| `src/audit/src/CodeForCoders.Audit.Api/appsettings.json` | Exchange e routing key que a Auditoria consome |
| `src/student-spa/nginx.conf.template` | Log sem query string |
| `src/student-spa/src/app/router.tsx` | Padrão de rotas públicas vs. protegidas por loader |
| `tasks/prd-conta-aluno/internal-api-contract.yaml` | Forma do contrato interno a seguir no EN-01 |

---

## Análise de Impacto

| Componente | Tipo | Impacto e risco | Ação requerida |
|---|---|---|---|
| Identity — verificação de asserção | modificado | Mudar para vários emissores pode quebrar o `bff-student` se a configuração antiga não for migrada | Manter compatibilidade da configuração do `bff-student`; teste de regressão dos endpoints de CAP-001 |
| Identity — banco | modificado | Três tabelas novas; `accounts` passa a ter contas internas | Migrations EF; índices únicos parciais |
| Notificação | modificado (contrato 1.1.0) | Convite recusado se Identity publicar antes da 1.1.0 | **Implantar Notificação antes** do primeiro convite; V-03 depende da mudança no mesmo incremento |
| Auditoria | sem mudança | Passa a receber tráfego real | Nenhuma; conferir exchange |
| `bff-admin` | modificado | De esqueleto a borda completa; proxy `/proxy` → `learning` existente | Manter o proxy, agora com JWT da validação corrente |
| `commerce` | modificado | Primeira validação de JWT; depende do JWKS de Identity na partida | Falha de JWKS fecha o acesso sem derrubar o health do serviço |
| `student-spa` / `bff-student` | sem mudança funcional | Conta interna já é recusada | Só teste de regressão |
| Compose / env local | modificado | Nova chave e novas variáveis obrigatórias | `generate-local-env.sh` não sobrescreve `.env` existente — documentar a regeneração |

---

## Riscos e Preocupações

| Preocupação | Local (`arquivo:linha`) | Impacto | Mitigação |
|---|---|---|---|
| Verificador de asserção aceita um único emissor | `src/identity/src/CodeForCoders.Identity.Api/Security/ServiceAssertionVerifier.cs:63` | `bff-admin` não autentica, ou compartilharia a identidade do `bff-student` | EN-01: emissores com chaves e escopos próprios (ADR-0005) |
| Compose configura só o emissor `bff-student` | `docker-compose.yml:116` | Ambiente local sem backoffice funcional | EN-01 acrescenta o emissor `bff-admin` |
| Cookie do esqueleto difere do contrato | `src/bff-admin/src/CodeForCoders.BffAdmin.Api/appsettings.json:27` | SPA e BFF desalinhados | V-02 usa `staff_session` |
| CSRF do esqueleto por cookie duplo, não vinculado à sessão; só `/proxy` exige sessão | `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Security/BffSecurityMiddleware.cs:26` | Proteção abaixo do padrão de CAP-001 | V-02 adota o modelo do `bff-student` |
| Proxy injeta token guardado na sessão Valkey | `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Security/BffSessionTransformProvider.cs:21` | Token longo em cache sobrevive à revogação | Usar só o JWT emitido na validação da requisição corrente |
| nginx do SPA encaminha `/v1/admin/` e loga query string | `src/admin-spa/nginx.conf.template:5` | Contrato em `/api/v1`; segredo de convite/redefinição em log | V-01 ajusta rota e formato de log |
| Notificação exige `nome` e fecha o enum em dois modelos | `src/notification/src/CodeForCoders.Notification.Application/Common/NotificationSendRequestRules.cs:14` | Convite recusado como modelo desconhecido/dado faltante | V-03 com contrato 1.1.0 |
| DTO do pedido sem campo de papel | `src/notification/src/CodeForCoders.Notification.Contracts/TransactionalNotificationV1.cs:12` | `papel` descartado na desserialização | V-03 acrescenta o campo |
| Consentimento transacional lista só dois propósitos | `src/notification/src/CodeForCoders.Notification.Application/Services/TransactionalConsentService.cs:13` | Convite bloqueado por consentimento | V-03 |
| Validade por propósito validada só para dois propósitos | `src/notification/src/CodeForCoders.Notification.Infra.Data/DependencyInjection.cs:52` | Convite sem validade no texto | V-03 exige `convite-interno` na partida |
| Destinos de outbox sem exchange da Auditoria | `src/identity/src/CodeForCoders.Identity.Application/Common/OutboxDestinationOptions.cs:7` | Ato publicado no exchange de Identity não chega à Auditoria | V-03 acrescenta `AuditExchange` e teste de consumo real |
| `Account` só cria aluno e `Confirm` só aceita aluno | `src/identity/src/CodeForCoders.Identity.Domain/Entities/Account.cs:24` | Não há como criar conta interna | V-01 acrescenta fábrica de conta interna confirmada |
| Emissor de JWT com claims fixos de aluno | `src/identity/src/CodeForCoders.Identity.Api/Security/StudentSessionTokenIssuer.cs:32` | Serviço não recebe papel/permissão | V-07 emite claims `roles`/`permissions` para sessão interna |
| Identity sem modo de comando | `src/identity/src/CodeForCoders.Identity.Api/Program.cs:3` | Sem caminho para o seed de RN-25 | V-01 acrescenta o modo `provision-first-admin` |
| `commerce` sem autenticação | `src/commerce/src/CodeForCoders.Commerce.Api/Program.cs` (nenhuma configuração de autenticação encontrada) | Recusa no serviço dono inexistente | V-07 |

---

## Decisões Técnicas

- **Decisão:** sessão do ator interno como entidade própria em Identity, não reuso de `StudentSession`.
  - **Racional:** as consultas de sessão de CAP-001 filtram `AccountType.Student`
    (`IdentitySessionStore.cs:58`, `:77`); separar evita mexer no caminho do aluno e deixa a revogação
    por conta interna explícita.
  - **Trade-offs:** duas entidades com semântica parecida.
  - **Alternativas rejeitadas:** generalizar `StudentSession` — exigiria migration de renomeação e
    regressão no fluxo entregue.
- **Decisão:** catálogo de permissões em código, não em tabela.
  - **Racional:** DP-03 fixa o catálogo e o PRD exclui papel/permissão configurável; tabela criaria
    uma superfície de escrita sem caso de uso.
  - **Trade-offs:** permissão nova exige deploy — que já é o caso, porque vem com a tela que a usa.
  - **Alternativas rejeitadas:** tabela de permissões com seed.
- **Decisão:** primeiro administrador por comando do binário de Identity, definindo a senha pelo fluxo
  de redefinição.
  - **Racional:** RN-25 exige seed fora do convite, datado e sem a senha passar pelo time; reusar a
    redefinição (RF-11) evita um segundo mecanismo de primeiro acesso.
  - **Trade-offs:** depende de Notificação e do smtp local/provedor para o primeiro acesso.
  - **Alternativas rejeitadas:** SQL de seed (senha ou hash manipulados pelo time; sem idempotência
    de domínio); endpoint HTTP de seed (superfície permanente para um ato único).
- **Decisão:** payload do ato de auditoria protegido no outbox.
  - **Racional:** o motivo é texto livre do administrador; a regra de dado sensível vale também na
    tabela de outbox.
  - **Trade-offs:** custo de proteção por mensagem, já existente no outbox.
  - **Alternativas rejeitadas:** payload em claro por "não ter dado pessoal" — o motivo pode ter.
- **Decisão:** área financeira reservada servida pelo `commerce`.
  - **Racional:** vendas e dinheiro vivem em `commerce` (baseline, topologia); `CAP-011` preenche a
    mesma área no mesmo serviço.
  - **Trade-offs:** introduz JWT/JWKS no `commerce` antes de ele ter dado.
  - **Alternativas rejeitadas:** resposta só no BFF — não prova a recusa no serviço dono (RF-13).

---

## Verificação

- **Cenários críticos não óbvios:** troca de papel com falha antes do commit (estado e outbox intactos);
  concessão concorrente do mesmo papel (um ato); revogação com requisição do alvo em andamento (a
  garantia vale para a próxima ação admitida após o commit, ADR-0003/0005); aceite de convite com e-mail
  que ganhou conta no meio; asserção do `bff-student` recusada em operação de backoffice e vice-versa;
  JWT de professor e JWT com audiência `identity` recusados pelo `commerce`.
- **Dados ou ambiente especiais:** Testcontainers (PostgreSQL, RabbitMQ, Valkey) como em CAP-001;
  relógio controlado para validade de convite e sessão; smoke no Compose local com
  `scripts/generate-local-env.sh` (regerar `.env` para incluir a chave do `bff-admin`), `apps.sh`,
  `provision-first-admin` e smtp4dev em `127.0.0.1:5000`.
- **Observabilidade além do padrão:** métrica e span da validação de sessão interna separados dos do
  aluno (latência e falhas, ADR-0005); contagem de atos publicados por tipo para comparar com os
  registros da Auditoria (métrica "Ato sem registro" do PRD). Nenhum atributo com e-mail, nome, motivo ou
  segredo.
- **Verificação dos contratos:** tipos/mocks do SPA a partir do `api-contract.yaml`; testes do produtor
  validando os payloads contra `AtoPraticadoIdentidade` e contra o schema do receptor Auditoria 1.0.1;
  teste de Notificação com os exemplos de 1.1.0 e de 1.0.0; consumo real no smoke (convite e ato
  chegam aos consumidores pelos exchanges configurados).

---

## Questões em Aberto

- [ ] **QT-01 do PRD — política de sessão do ator interno.** Revisão de segurança do `admin-spa`.
  Até lá, 60 minutos de inatividade configuráveis. Não bloqueia.
- [ ] **Chaves em produção.** Emissão e rotação da chave do `bff-admin` e das chaves JWKS no secret
  manager do Coolify (ADR-0002) — plataforma. Não bloqueia o desenvolvimento; bloqueia o deploy.

---

## Architecture Decision Records

- [ADR-0005: Sessão do ator interno e autenticação de serviço do BFF do backoffice](../../docs/adr/0005-sessao-e-servico-do-backoffice.md) — **nova, Accepted em 2026-09-25**. Estende ADR-0003/0004 ao backoffice: validação por ação, emissores separados, JWT com papéis/permissões, JWKS.
- [ADR-0003](../../docs/adr/0003-verificacao-de-sessao-do-aluno.md) — conformada: o mesmo mecanismo, agora para sessão interna.
- [ADR-0004](../../docs/adr/0004-autenticacao-de-servico-bff-identity.md) — conformada: asserção assimétrica, com emissor próprio para o `bff-admin`.
- [ADR-0001](../../docs/adr/0001-monorepo-de-codigo.md), [ADR-0002](../../docs/adr/0002-plataforma-de-runtime-coolify.md) — herdadas sem mudança.
