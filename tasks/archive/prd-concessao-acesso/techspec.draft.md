---
tsg_artifact: techspec
product: code-4-coders
capability: CAP-008
version: 1.0
status: approved
updated: 2026-10-01
sources: tasks/prd-concessao-acesso/prd.md@1.1, tasks/prd-concessao-acesso/contracts.md@1.1, context/architecture-baseline.md@1.2, domains/matricula-e-direito-de-acesso/domain.md@1.0
---

# TechSpec — concessão de acesso

> **Escopo:** Full-stack (`commerce` módulo Matrícula/`Entitlement`, `identity`, `audit`, `bff-admin`, `admin-spa`; `media` e `learning` **não mudam**)
> **Modo:** Pipeline, API-First
> **PRD de origem:** [prd.md](prd.md), v1.1 (errata de nome da área), aprovado em 2026-10-01
> **Contratos de integração:** [contracts.md](contracts.md) 1.1; OpenAPI [do backoffice](api-contract.yaml) 1.0.0, [interno de `commerce`](internal-api-contract-commerce.yaml) 1.2.0 e [interno de Identity](internal-api-contract-identity.yaml) 1.4.0; AsyncAPI [de `commerce`](asyncapi-contract.yaml) 1.1.0 e [da Auditoria](asyncapi-contract-audit.yaml) 1.4.0 — todos aprovados em 2026-10-01
> **Data:** 2026-10-01
> **Status:** Aprovada em 2026-10-01
> **Handoff:** liberado para geração de Tasks

## Resumo Executivo

O módulo `Entitlement` de `commerce` — reservado desde a fundação (`CommerceModules.cs:7`, schema `entitlement`, candidato a extração pela BA04) e ainda sem conteúdo — passa a ser o dono do direito de acesso:

- **Concessão com origem e vigência.** Uma matrícula por (escola, aluno, curso) agrupa as concessões; só a origem `courtesy` é concedível. O término é calculado **uma vez, na concessão**, no fuso da escola, e gravado como dois valores: `ends_on` (o último dia, para exibir) e `expires_at` (o instante UTC em que o acesso deixa de valer, exclusivo). Não existe coluna "expirada": a situação é derivada na leitura (RN-D06).
- **Decisão "pode acessar agora?" como uma única consulta indexada**, sem chamar Identity nem nenhum outro serviço, com `Cache-Control: private, max-age=30` e autenticada por asserção de serviço (ADR-0011). Nenhum consumidor existe nesta entrega.
- **Cortesia atômica.** Concessão, recibo de idempotência, fato `matricula.acesso-concedido` e ato `cortesia-concedida` nascem na mesma transação, no outbox do módulo (G06), como o Catálogo já faz para as ofertas. Antes de gravar, `commerce` reconfirma a conta em Identity (ADR-0010), com falha fechada.
- **Visão mínima do curso**, mantida pelo consumo de `conteudo.versao-publicada.v1` em fila própria, para o financeiro escolher o curso sem chamada a `learning`.
- **Fato de expiração por rotina idempotente**, informativo: a decisão nunca depende dele.

`identity` ganha a permissão `cortesia.conceder` no papel financeiro e duas operações internas sobre a conta de aluno; `audit` aceita o tipo `cortesia-concedida` (com motivo obrigatório); `bff-admin` e `admin-spa` ganham a área de cortesias e os rótulos da trilha.

**Trade-off primário:** guardar o término calculado na concessão faz a decisão ser uma comparação trivial e torna o direito imune a mudança de regra de calendário depois; em troca, a regra de término (DP-03) fica **congelada em cada concessão** — corrigi-la não corrige concessões já gravadas — e a cortesia passa a depender, em um salto síncrono, da disponibilidade de Identity.

## Arquitetura da Solução

```text
financeiro → admin-spa (/admin/cortesias) → bff-admin ─(asserção bff-admin, X-Staff-Session)──► identity   POST /student-account-lookups   (escopo student-account:lookup)
                                                      └─(JWT de ator, audiência commerce, cortesia.conceder)──► commerce (Entitlement)
                                                                                                                  │ commerce ─(asserção commerce, escopo student-account:confirm)──► identity (reconfirma a conta, ADR-0010)
                                                                                                                  │ entitlement.* (visão do curso, matrículas, concessões, recibos, outbox)
                                                                                                                  │ outbox do módulo ──► commerce.events: matricula.acesso-{concedido,expirado}.v1
                                                                                                                  │                  └─► audit.events:    auditoria.ato-praticado.v1 (origem matricula)
learning.events: conteudo.versao-publicada.v1 ──► fila de Entitlement ──► visão mínima do curso (id, título, versão)
[futuro] media / learning ─(asserção própria, escopo access-decision:read, ADR-0011)──► commerce  GET /access-decision
administrador → admin-spa (/admin/auditoria) → bff-admin ──► audit; rótulos: identity (conta-aluno, nome), learning (curso)
```

**URL pública.** A feature não emite links por e-mail ou mensagem; cria um item de menu e uma rota do backoffice: `<origem do backoffice>/admin/cortesias` (o `admin-spa` é servido com `base: '/admin/'`, `vite.config.ts:10`; localmente, `http://localhost:8081/admin/cortesias`). A evidência de que o item abre o destino esperado está em V-01.

### Bloco Backend

**Módulo `Entitlement`.** Schema `entitlement`, tabelas criadas por migration do EF (nunca à mão). Modelo:

| Tabela | Chave e campos essenciais | Observação |
|---|---|---|
| `course_views` | `(tenant_id, course_id)`; `title`, `title_search`, `version_number`, `applied_at` | Visão mínima do curso. Só o necessário a Matrícula (C-01). |
| `enrollments` | `id`; único `(tenant_id, student_id, course_id)`; `first_granted_at` | Nasce na primeira concessão. |
| `access_grants` | `id` (= `grantId`, UUID v7); `tenant_id`, `enrollment_id`, `student_id`, `course_id`, `origin` (`courtesy`), `origin_ref` (nulo na cortesia), `period_type` + `period_months`, `status` (`active`), `granted_at`, `ends_on`, `expires_at`, `reason`, `granted_by`, `expiry_event_id`, `expiry_published_at` | `status` existe como coluna para `CAP-009` acrescentar `suspended` e `revoked` sem migração de dados; nesta entrega só `active` é gravado. `expired` **não** é gravado. |
| `grant_receipts` | `(tenant_id, actor_id, key_hash)`; `request_hash`, `response_json`, `expires_at` | Recibo de idempotência de 24 h, como o do Catálogo. A chave nunca é guardada, só o hash. |
| `outbox_messages` | igual ao do Catálogo | Outbox próprio do módulo (G06), no schema `entitlement`. |

Índices: decisão por `(tenant_id, student_id, course_id)`; rotina de expiração parcial por `expires_at` onde `expiry_published_at IS NULL AND expires_at IS NOT NULL`; lista do aluno por `(tenant_id, student_id, granted_at DESC)`. Toda entidade traz o filtro global de tenant (G07).

**Término (RF-05, DP-03).** O domínio calcula na concessão: converte `granted_at` (UTC) para a data local no fuso da escola, soma N meses à **data** (aritmética de calendário que ancora no último dia do mês quando o dia não existe: 31/01 + 1 mês → 28/02, e 29/02 em ano bissexto) e define `expires_at` como o início do dia seguinte a `ends_on` no fuso da escola, convertido a UTC. Vitalícia grava ambos nulos. O fuso é configuração da escola (`America/Sao_Paulo` na Fase 1). A tabela de seis casos do RF-05 é o teste de unidade do cálculo.

**Concessão da cortesia (`GrantCourtesy`).** Ordem, sem segurar transação durante E/S de rede:

1. valida o corpo (motivo de 1 a 500 caracteres com ao menos um não-espaço; meses de 1 a 60; `lifetime` sem `months`) → `FIELD_INVALID`;
2. se o recibo da chave existe e não expirou: mesmo `request_hash` → devolve a resposta guardada (200); diferente → `IDEMPOTENCY_KEY_REUSED`;
3. exige o curso na `course_views` do tenant → `COURSE_NOT_ELIGIBLE` (curso nunca publicado, inexistente e de outra escola são indistintos);
4. **reconfirma a conta em Identity** (ADR-0010): `eligible: false` → `STUDENT_ACCOUNT_NOT_ELIGIBLE`; sem resposta, resposta inválida ou tempo esgotado → 503 `STUDENT_ACCOUNT_CHECK_UNAVAILABLE`, **nada gravado**;
5. abre a transação sob trava por `(tenant, ator, hash da chave)`, relê o recibo (a segunda requisição concorrente com a mesma chave vê o recibo da primeira), grava matrícula (insert condicional pela chave única), concessão, recibo e **duas** linhas no outbox do módulo — o fato `matricula.acesso-concedido.v1` e o ato `auditoria.ato-praticado.v1` —, e confirma. `fatoId` do ato = `eventId` do fato; `praticadoEm` = `occurredAt` = `granted_at`.

Já haver concessão ativa ao curso **não** impede (RN-D07): matrícula e concessões convivem. O padrão de referência é `PublishOffer` (recibo, trava, fato e ato na mesma transação, `PublishOffer.cs:15-56`).

**Decisão de acesso (`DecideAccess`).** Uma consulta por `(tenant_id, student_id, course_id)` sobre `access_grants` com `status = 'active'`, comparando `expires_at` com `TimeProvider.GetUtcNow()` (nunca `DateTime.Now`, `BannedSymbols.txt`):

- alguma concessão com `expires_at` nulo → `allowed`, `validity: lifetime`;
- senão alguma com `expires_at > agora` → `allowed`, `validity: until` com o **maior** `expires_at`;
- senão, se há concessões (todas vencidas) → `denied`, `grant-ended`, `lastExpiredAt` = o maior `expires_at`;
- senão → `denied`, `no-grant`.

Aluno, conta ou curso desconhecidos, conta que não é de aluno e outra escola caem em `no-grant`: não há consulta a Identity nem a `course_views` no caminho. Só `Entitlement` expõe o caso de uso de decisão (G11), verificado por teste de arquitetura. A resposta leva `Cache-Control: private, max-age=30`.

**Rotina de expiração.** Serviço de fundo de `commerce`, no padrão dos demais (`PurchaseIntentReceiptCleanupWorker`, registrado em `Infra.Data/DependencyInjection.cs:36`), em ciclos configuráveis (padrão de poucos minutos, bem abaixo da uma hora de DP-10). Em cada ciclo, seleciona em lote as concessões com `expires_at <= agora` e `expiry_published_at IS NULL`, com `FOR UPDATE SKIP LOCKED`, e **na mesma transação** grava `matricula.acesso-expirado.v1` no outbox e preenche `expiry_event_id` e `expiry_published_at`. Instâncias concorrentes não duplicam; uma rotina executada duas vezes não republica. Concessão vitalícia nunca entra. A decisão **não lê** esses campos. A defasagem (agora − `expires_at` do mais antigo pendente) é métrica, com alerta acima de 30 minutos.

**Visão do curso.** Fila nova de `commerce` (`commerce.entitlement-course`, quorum, DLX/DLQ e `x-delivery-limit` do padrão do serviço) ligada à chave `conteudo.versao-publicada.v1` do exchange de `learning`; consumidor no formato de `CatalogCourseConsumerWorker`, reaproveitando `PublishedCourseFact.Parse` (já valida o formato 1.0.0 e 1.1.0). Aplica só se `versionNumber` > aplicado (igual ou menor é confirmado sem efeito), serializando por `(tenant, curso)` com trava consultiva, como `CatalogCourseProjectionStore`. Guarda título, `title_search` e versão. Erro permanente (JSON inválido, `tenantId` ausente) → DLQ sem retry; transitório → repete.

**Busca por título sem acento.** `title_search` é o título normalizado (minúsculas, sem diacríticos) e o filtro compara trecho normalizado, sem extensão `unaccent`. É a mesma regra de `CourseTitleSearch.Normalize` em `learning` (`Course.cs:43`); como os serviços têm deploy independente (ADR-0001), `commerce` leva a **própria cópia** da normalização, com teste de conformidade sobre os mesmos casos.

**Outbox e publicação.** O publicador de `commerce` drena hoje só `catalog` e `sales` (`OutboxPublisherWorker.cs:31-32`) e escolhe a tabela por comparação de schema (`OutboxPublisherWorker.cs:78`); passa a drenar também `entitlement`. O exchange do ato já é escolhido pela chave de roteamento (`RabbitMqPublisher.cs:34`) e o cabeçalho `correlationId` já é enviado. **Fatos sem consumidor:** com `mandatory: true` e nenhuma fila ligada, o broker devolve a mensagem e o publicador esgota as tentativas; a topologia declara uma fila de retenção limitada (`commerce.entitlement-fact-retention`, quorum, `x-max-length`, `x-message-ttl`, `x-overflow: drop-head`, com as três opções em `RabbitMqOptions`) ligada às duas chaves `matricula.acesso-*.v1`, no padrão de `OfferRetentionQueue` (`RabbitMqOptions.cs`).

**Autenticação e tenant em `commerce`.**
- *Rotas de ator (cortesia):* JWT de Identity com audiência `commerce`, validado por JWKS (já existente); política nova exige `cortesia.conceder` no claim `permissions`. O tenant vem do claim `tenantId`; o autor, de `sub`.
- *Rota da decisão:* asserção de serviço com escopo `access-decision:read` (ADR-0011), `jti` consumido em Valkey. É **um único esquema por rota**: a asserção do `bff-student` (que não recebe o escopo) e o JWT de ator são recusados nela.
- *Chamada a Identity:* assinador de asserção em `commerce` (cópia do padrão de `ServiceAssertionTokenFactory.cs` do `bff-admin`), `iss` = `commerce`, escopo `student-account:confirm`.

**Identity.** Papel financeiro ganha `cortesia.conceder` em `StaffRoleCatalog` (`StaffRoleCatalog.cs:9`): a permissão efetiva da sessão e o JWT dela decorrem do catálogo, sem outro ponto de alteração (`AuthenticateStaffSession.cs:217`). Duas operações novas, no padrão de `AuditIdentityReferenceEndpoints.cs`:
- `lookupStudentAccountInternal`: valida a asserção do `bff-admin` (escopo `student-account:lookup`), a sessão interna vigente (`X-Staff-Session`) e a permissão `cortesia.conceder` na sessão; busca por `NormalizedEmail` no tenant, só `AccountType.Student`; devolve e-mail, nome, `emailConfirmed` e `status` (`active`/`disabled`, a partir de `DeactivatedOn`); 404 indistinto para inexistente, ator interno e outro tenant;
- `confirmStudentAccountInternal`: valida a asserção de `commerce` (escopo `student-account:confirm`); devolve `eligible` — verdadeiro só para conta de aluno, ativa, do tenant da asserção.
- `resolveAuditIdentityReferencesInternal` passa a aceitar a referência `conta-aluno` e devolve o **nome** como rótulo (C-07).

**Audit.** `AdministrativeActPolicy` passa a aceitar `cortesia-concedida` **com motivo obrigatório** (`AdministrativeActPolicy.cs:17-27`), exige origem `matricula` e alvo `conta-aluno` (como já exige origem `catalogo` e alvo `oferta` nas linhas 39-52) e valida as chaves de complemento `curso` (UUID), `concessao` (UUID) e `vigencia` (`vitalicia` ou `1m` a `60m`, regra de `IsValidPeriod`, linhas 118-121). Um ato de origem `matricula` com tipo, alvo ou complemento fora disso é gravado como **não conforme**, não descartado (RN-A06).

**Consulta da trilha no `bff-admin`.** `IdentityReferenceTypes` ganha `conta-aluno` (`AuditRecordEndpoints.cs:14`, usado nas linhas 168 e 360). O BFF também acrescenta ao detalhe o atributo **derivado** `cursoTitulo` quando o ato traz `attributes.curso`, resolvido por `learning` pela operação que já existe (`CourseAuditReferenceEnricher`, papel administrador); o schema não muda, porque `attributes` é mapa de texto (`additionalProperties: {type: string}`, CAP-030).

### Bloco Frontend

**Área de cortesias do `admin-spa`.** Feature nova, no padrão de `features/catalog-courses` (`api/`, `components/`, `hooks/`, `types/`, `utils/`), rota `/cortesias` (`config/paths.ts`), item de menu em `get-staff-areas.ts` com `permission: 'cortesia.conceder'`.

**Jornada (PRD, Experiência do Usuário).** Formulário sequencial de seis passos — aluno, curso, vigência, motivo, revisão, confirmação —, com o estado de cada passo no cliente até confirmar e **nenhum dado de cortesia em storage do navegador**. Onde mora o estado:
- *servidor:* cursos concedíveis (consulta paginada com busca por título) e concessões do aluno — React Query, como o resto do backoffice;
- *cliente:* o aluno localizado, o curso e a vigência escolhidos, o motivo digitado, a `Idempotency-Key` da confirmação (gerada **uma vez por tentativa de revisão** e reaproveitada no reenvio) e a confirmação reforçada da vitalícia.

**O e-mail digitado não vira chave de consulta.** A localização é uma **mutação** (POST), nunca uma consulta com o e-mail na chave do cache do React Query; a resposta vive no estado do formulário e é descartada ao sair da área.

**Cálculo de datas na tela.** A tela **não recalcula** o término: mostra o `endsOn` devolvido pelo servidor. Antes da confirmação (RF-03, passo 3), pede a prévia a `previewCourtesyTerm` (C-11) ao escolher os meses e de novo ao abrir a revisão, e mostra "até DD/MM/AAAA"; depois de conceder, mostra o `endsOn` da concessão. A prévia não é guardada.

> Biblioteca de fetching, formulário, validação e estrutura de pastas são decisões do projeto, na skill `react`; nada aqui as altera.

---

## Mapa de Fatias Verticais

A ordem de construção está em `Bloqueado por`. Toda fatia cruza as pontas que o comportamento exige. **Telas só começam depois do EN-01** (wireframe ASCII → Figma → aprovação).

### V-01: o financeiro abre a área de cortesias e localiza o aluno pelo e-mail

- **Cobre:** RF-01, RF-02 (conta); RN-D09 (localização), RN-D17; Identidade RN-01, RN-12, RN-16, RN-17, RN-18, RN-21; DP-01, DP-02.
- **Entrada / gatilho:** menu **Cortesias**; `lookupStudentAccount` com o e-mail.
- **Processamento:** `identity` inclui `cortesia.conceder` no papel financeiro e registra o escopo `student-account:lookup` no emissor `bff-admin`; `bff-admin` valida a sessão, exige a permissão na sessão, e chama Identity com a asserção e o `X-Staff-Session`; Identity valida, normaliza o e-mail e busca; `admin-spa` mostra o item e o passo "Aluno" (e-mail, nome, aviso de e-mail ainda não confirmado, bloqueio de conta desativada).
- **Saída observável:** o financeiro vê a conta de aluno; o aviso "e-mail ainda não confirmado" aparece e não impede; conta desativada aparece como tal e o fluxo não segue; inexistente e conta de ator interno mostram o **mesmo** "Não há conta de aluno com este e-mail"; professor, suporte, administrador e aluno não veem o item e recebem 403 na rota direta.
- **Evidência / checkpoint:** Compose com Identity, `bff-admin` e SPA: abrir `http://localhost:8081/admin/cortesias` como financeiro → passo "Aluno"; `Joana.Ribeiro@example.com ` (maiúsculas e espaços) encontra a conta; inexistente e ator interno → o mesmo 404; desativada → `status: disabled`; as chamadas como professor, suporte e administrador → 403 no BFF **e** em Identity chamado direto com a asserção e a sessão; revogar o papel financeiro recusa a próxima ação na sessão aberta (RN-17); o e-mail não aparece em nenhum log, span nem URL (teste que inspeciona a saída de log da chamada).
- **Bloqueado por:** EN-01 (só a parte de tela).

### V-02: o financeiro escolhe o curso entre os publicados da escola

- **Cobre:** RF-03 (curso), RF-04 (escola); RN-D17; Conteúdo RN-C17; DP-05.
- **Entrada / gatilho:** fato `conteudo.versao-publicada.v1` na fila de Entitlement; `listCourtesyCourses`; passo "Curso" da tela.
- **Processamento:** migration cria o schema `entitlement` e `course_views`; topologia declara fila, DLQ e ligação; o consumidor aplica o fato pela regra de versão; `commerce` lista a visão da escola do `tenantId`, ordenada por título, com o filtro sem acento e a política `cortesia.conceder`; `bff-admin` repassa com o JWT; `admin-spa` mostra o passo com busca. Cursos **sem oferta** aparecem.
- **Saída observável:** o financeiro vê os cursos com versão vigente da escola, com ou sem oferta; curso nunca publicado não aparece; a busca por "fundamentos" acha "Fundamentos de C#" e a busca sem acento acha título acentuado.
- **Evidência / checkpoint:** Compose com `learning`, RabbitMQ, PostgreSQL: publicar um curso em `learning` → ele aparece na lista; reentrega do mesmo fato e fato de versão menor não alteram; fato com `tenantId` ausente vai à DLQ sem retry; JWT de outra escola → lista só daquela escola; sem a permissão → 403; **o reenvio inicial** de `learning` (`CatalogInitialLoad:Enabled`) com o consumidor de Entitlement já declarado popula os cursos existentes (ordem de implantação).
- **Bloqueado por:** V-01.

### V-03: o financeiro concede a cortesia — o acesso nasce com término correto, fato e ato

- **Cobre:** RF-03, RF-04 (modelo e origem), RF-05, RF-07 (`acesso-concedido`), RF-08 (ato gravado **conforme**); RN-D02, RN-D04, RN-D08, RN-D09, RN-D10, RN-D11, RN-D17; Auditoria RN-A05, RN-A06, RN-A08, RN-A14; Identidade RN-21; DP-03, DP-04, DP-05, DP-08; ADR-0010.
- **Entrada / gatilho:** `grantCourtesy` após a confirmação da revisão.
- **Processamento:** o caso de uso da seção "Concessão da cortesia" (validação, recibo, curso, **reconfirmação em Identity**, transação única de matrícula + concessão + recibo + fato + ato); `identity` registra o emissor `commerce` com o escopo `student-account:confirm` e expõe a operação; o publicador drena o outbox de Entitlement, envia o fato ao exchange de `commerce` (a fila de retenção o absorve) e o ato ao exchange da Auditoria com `correlationId`; `audit` aceita `cortesia-concedida` (origem `matricula`, alvo `conta-aluno`, motivo obrigatório) e grava o ato como conforme; `previewCourtesyTerm` calcula o término pela **mesma função de domínio** da concessão, sem gravar; `admin-spa` mostra a prévia no passo "Vigência", a revisão (frase única e término em data) e o resultado.
- **Saída observável:** concessão `courtesy`, `status: active`, com `endsOn` e `expiresAt` conforme a tabela de RF-05; o fato conforme `AcessoConcedidoPayload`; o ato conforme na trilha; motivo vazio, só com espaços, com 501 caracteres, 0 ou 61 meses → 422 `FIELD_INVALID` e **nada** concedido; curso nunca publicado ou de outra escola → 422 `COURSE_NOT_ELIGIBLE`; conta desativada, de ator interno, de outra escola ou desativada entre a localização e a confirmação → 422 `STUDENT_ACCOUNT_NOT_ELIGIBLE`; Identity sem resposta → `STUDENT_ACCOUNT_CHECK_UNAVAILABLE` e nada gravado; duplo clique com a mesma `Idempotency-Key` → uma concessão, um fato, um ato, e 200 com a original; corpo diferente com a mesma chave → `IDEMPOTENCY_KEY_REUSED`; vitalícia grava `endsOn` e `expiresAt` nulos.
- **Evidência / checkpoint:** Compose com Identity, `commerce`, `audit`, `bff-admin`, RabbitMQ, PostgreSQL e Valkey, **com `audit` 1.4.0 antes de `commerce` publicar** (ordem de implantação): conceder 6 meses → uma concessão, um fato e um ato conformes; a prévia de 6 meses devolve o mesmo `endsOn` e `expiresAt` da concessão feita no mesmo instante, 0 e 61 meses → 400, e **nada** é gravado; teste de unidade com os seis casos de RF-05, mais fuso (concessão às 23:59 do dia 15 conta do dia 15 no fuso da escola); falha forçada entre a gravação e o commit → nada nasce (atomicidade); o publicador completa o ciclo com a fila de retenção ligada e **esgota** as tentativas se ela não existir (teste que documenta a razão da fila); `audit` marca `cortesia-concedida` sem motivo, com origem diferente de `matricula` ou alvo diferente de `conta-aluno` como **não conforme**; nenhum e-mail, nome ou texto de motivo em fato, log ou span; o motivo só na concessão e no ato; conceder sem `cortesia.conceder` → 403 no BFF e em `commerce` direto.
- **Bloqueado por:** V-02.

### V-04: o financeiro vê as concessões do aluno e é avisado antes de duplicar; a vitalícia exige confirmação reforçada

- **Cobre:** RF-02 (concessões), RF-03 (aviso, reforço), RF-04 (convivência); RN-D07; DP-06, DP-07.
- **Entrada / gatilho:** `listStudentAccessGrants` ao localizar o aluno e na revisão; `getCourtesyGrant` após conceder.
- **Processamento:** `commerce` lista as concessões do aluno na escola, da mais recente à mais antiga, com `status` **derivado na leitura** (`expired` quando `agora >= expires_at`) e o título atual do curso da visão; aluno sem concessão, inexistente ou de outra escola → página vazia; `getCourtesyGrant` só devolve origem cortesia da escola; `admin-spa` mostra a lista no passo "Aluno", o aviso "este aluno já tem acesso até DD/MM/AAAA" na revisão (sem impedir) e a confirmação reforçada da vitalícia, com o texto de que **não há como desfazer pela tela**.
- **Saída observável:** a lista traz as concessões com situação correta, **sem rotina rodada**; duas cortesias ao mesmo curso convivem e a segunda não estende nem encerra a primeira; a vitalícia só é confirmada depois do passo reforçado.
- **Evidência / checkpoint:** concessão com término no passado aparece `expired` sem a rotina de V-06; duas concessões na mesma matrícula; aviso aparece com o término da ativa e não bloqueia; `getCourtesyGrant` de outra origem, inexistente ou de outra escola → 404 `GRANT_NOT_FOUND`; teste do SPA para o aviso e a confirmação reforçada.
- **Bloqueado por:** V-03.

### V-05: qualquer serviço pergunta "este aluno pode acessar este curso agora?" e recebe a única resposta

- **Cobre:** RF-06; RN-D01, RN-D03, RN-D06, RN-D07, RN-D08, RN-D15, RN-D17; BA07, G11; ADR-0011.
- **Entrada / gatilho:** `decideAccessInternal` com a asserção de serviço de um emissor de teste.
- **Processamento:** o caso de uso `DecideAccess` da seção "Decisão de acesso"; `commerce` verifica a asserção (assinatura, `kid`, emissor, audiência, escopo `access-decision:read`, tenant, validade, `jti` único); a resposta sai com `Cache-Control: private, max-age=30`; **nenhuma chave de `media` nem de `learning` é provisionada**: a verificação é exercida com um emissor de teste, e a política de rota recusa os demais esquemas.
- **Saída observável:** `allowed` com término (ou `lifetime`); `denied` com `no-grant` ou `grant-ended` + `lastExpiredAt`; aluno desconhecido, conta que não é de aluno, curso desconhecido e outra escola → `denied`/`no-grant`, **sem 404**.
- **Evidência / checkpoint:** os casos de RF-06: cortesia de 6 meses → `allowed`; sem concessão → `no-grant`; concessão vencida ontem → `grant-ended` **sem rotina rodada**; uma vencida e uma ativa → `allowed`; 3 meses e vitalícia → `lifetime`; última noite do último dia (23:59:59) → `allowed` e 00:00:00 do dia seguinte → `denied` (com relógio fake); publicar nova versão do curso e alterar ou despublicar oferta → decisão idêntica; asserção sem o escopo, com `jti` repetido, de `bff-student` ou com JWT de ator na mesma rota → recusada (403 `SCOPE_DENIED` ou 401 `SERVICE_UNAUTHORIZED`); arquitetura: só `Entitlement` expõe o caso de uso de decisão; nenhum identificador de pessoa em log, span ou métrica.
- **Bloqueado por:** V-03.

### V-06: a concessão que vence gera o fato informativo, uma vez, sem que a decisão dependa dele

- **Cobre:** RF-07 (`acesso-expirado`); RN-D06; DP-10.
- **Entrada / gatilho:** ciclo da rotina de expiração.
- **Processamento:** a rotina da seção "Rotina de expiração": seleciona as vencidas sem fato, com `SKIP LOCKED`, grava o fato e o marcador na mesma transação; o publicador o entrega (fila de retenção); métrica de defasagem.
- **Saída observável:** um único `acesso-expirado` por concessão por período, em até uma hora do término; vitalícia nunca; uma vencida e outra ativa → fato da primeira e decisão continua `allowed`.
- **Evidência / checkpoint:** com relógio fake: cruzar o término → um fato no ciclo seguinte; duas instâncias concorrentes e a rotina executada duas vezes → um fato; fato atrasado ou perdido (rotina desligada) → a decisão de V-05 é a mesma; vitalícia → nenhum; alerta de defasagem acima de 30 min.
- **Bloqueado por:** V-03.

### V-07: o administrador vê a cortesia na trilha, com o aluno, o curso, a vigência e o motivo legíveis

- **Cobre:** RF-08 (rótulos e filtro); Auditoria RN-A08; C-07; Identidade RN-21.
- **Entrada / gatilho:** consulta da trilha e detalhe do registro `cortesia-concedida`.
- **Processamento:** `identity` resolve `conta-aluno` (nome); o `bff-admin` aceita o tipo de referência, resolve o **nome** com Identity, acrescenta `attributes.cursoTitulo` pela resolução existente de `learning` e devolve; `admin-spa` ganha o rótulo *Cortesia concedida* e a opção no filtro por tipo (`audit-trail-screen.tsx`, lista de tipos), mostra aluno (nome), curso (título), vigência formatada ("6 meses" ou "Vitalícia") e motivo no detalhe.
- **Saída observável:** o detalhe mostra "Cortesia concedida", o autor, o aluno **pelo nome**, o curso **pelo título**, a vigência e o motivo; o e-mail do aluno **não** aparece; o filtro por tipo oferece *Cortesia concedida*; referência não resolvida mostra só a identificação, sem nome inventado.
- **Evidência / checkpoint:** Compose: conceder (V-03) e abrir `http://localhost:8081/admin/auditoria` como administrador → registro conforme com os rótulos; resolução de conta de outro tenant ou inexistente → sem rótulo, sem dizer qual; sem sessão de administrador → 403; teste do SPA para o rótulo, o filtro e a vigência.
- **Bloqueado por:** V-03; EN-01 (parte de tela).

### Habilitadores inevitáveis

| Habilitador | Por que não cabe numa fatia | Menor escopo | Primeira fatia desbloqueada |
|---|---|---|---|
| EN-01 | O PRD exige wireframe ASCII → Figma → aprovação antes de código de tela; a aprovação é do responsável pelo produto e não é produzida por nenhuma fatia | Novo `docs/design/wireframes-cortesias.md`: área, os seis passos, aviso de concessão existente, confirmação reforçada da vitalícia, estados de erro e os rótulos da trilha, com registro de aprovação no cabeçalho | V-01 (tela), V-07 (tela) |

---

## Contratos e Fronteiras

Schemas, parâmetros, exemplos e erros vivem nos documentos aprovados de [contracts.md](contracts.md); esta spec não os repete. Os contratos **registram o acordo desta implementação**; compatibilidade com produção não foi verificada (nenhuma das operações novas tem versão anterior).

### Mapeamento de mensagens e dados

| Contrato e identificador | Aplicação/produtor e consumidores | Comportamento a implementar | Evidência |
|---|---|---|---|
| AsyncAPI `commerce`: `receberVersaoPublicadaEmMatricula` · `conteudo.versao-publicada.v1` 1.1.0 | `learning` (send) → Entitlement (receive) | visão mínima por versão monotônica; fila própria com DLQ; erro permanente → DLQ; nenhuma chamada a `learning` | V-02 |
| `publicarAcessoConcedido` · `matricula.acesso-concedido.v1` | Entitlement (send); nenhum consumidor nesta entrega | outbox do módulo, mesma transação da concessão e do ato; sem dado pessoal; fila de retenção | V-03 |
| `publicarAcessoExpirado` · `matricula.acesso-expirado.v1` | Entitlement (send); nenhum consumidor | rotina idempotente com marcador por concessão, em até 1 h; nunca vitalícia | V-06 |
| `publicarAtoDeCortesia` · `auditoria.ato-praticado.v1` 1.4.0 | Entitlement (send) → `audit` 1.4.0 (receive) | exchange da Auditoria, `fatoId` = `eventId` do fato, motivo obrigatório, alvo `conta-aluno`, complemento só com identificadores e `vigencia` | V-03, V-07 |

**Diferenças para o acordo anterior (Auditoria).** 1.3.0 → 1.4.0 é aditiva, de **receptor**; produtores existentes seguem válidos. **Ordem de implantação obrigatória:** Identity 1.4.0 → `audit` 1.4.0 → `commerce` com o consumidor de versão de Entitlement → **reenvio** de `learning` (`CatalogInitialLoad:Enabled`) → `commerce` expondo cortesia e decisão → `bff-admin` → `admin-spa`. O reenvio é de execução única, com marcador (`CatalogInitialLoad.cs:45`); ver Riscos.

**Dados sensíveis e credenciais transitórias.**
- *E-mail do aluno:* **criado** no campo do passo "Aluno" (memória do SPA); **trafega** no corpo do POST ao `bff-admin` e no corpo do POST do BFF a Identity, sempre por TLS; **lido** por Identity para a busca por `NormalizedEmail`; **devolvido** (e-mail e nome) ao BFF e ao SPA; **descartado** ao sair da área. Nunca em URL, chave de cache, log, span, métrica, outbox, mensagem de broker nem em tabela de `commerce`. As respostas de localização saem com `Cache-Control: no-store`.
- *Motivo da cortesia:* digitado no SPA; trafega até `commerce`; **persistido** em `access_grants.reason` e no payload do ato no outbox (necessário: a Auditoria o guarda como parte do registro, RN-A05); **copiado** ao broker e à Auditoria **somente** no ato — o fato `acesso-concedido` não o leva; nunca em log, span ou métrica. O recibo de idempotência guarda a resposta (que contém o motivo) por 24 horas e é descartado pela limpeza dos recibos; o backup mantém o dado pelo prazo da retenção do banco.
- *`Idempotency-Key`:* criada no navegador por tentativa; trafega até `commerce`; persistida **só como hash SHA-256** no recibo de 24 horas; nunca em log, span ou métrica.
- *Asserções de serviço (ADR-0010 e ADR-0011):* assinadas por chamada, vida de até 60 s, `jti` consumido em Valkey até `exp`; não persistidas em banco, fora de log e span; chaves privadas vêm do secret manager do ambiente.
- *Referências de pessoa* (`studentId`, `sub` do financeiro): identificadores, não dado pessoal; vão a fatos e ao ato por referência (RN-A08).

### Mapeamento do contrato de API

Duas camadas por operação: BFF público ↔ interno de `commerce`, mesmos schemas, com a regra no serviço dono.

| operationId (BFF · interno) | Caminho de implementação |
|---|---|
| `lookupStudentAccount` · `lookupStudentAccountInternal` (Identity) | `StudentAccountEndpoints` (`bff-admin`) → cliente de Identity com asserção e `X-Staff-Session` → `LookupStudentAccount` → consulta por `NormalizedEmail` |
| `previewCourtesyTerm` · `previewCourtesyTermInternal` | endpoint → `PreviewCourtesyTerm` → **a mesma função de domínio** do término usada por `GrantCourtesy`; nenhuma gravação |
| `listCourtesyCourses` · `listCourtesyCoursesInternal` | endpoint → `ListCourtesyCourses` → consulta sobre `course_views` com `title_search` |
| `listStudentAccessGrants` · `listStudentAccessGrantsInternal` | endpoint → `ListStudentAccessGrants` → consulta sobre `access_grants` + `course_views`; `status` derivado de `expires_at` e `TimeProvider` |
| `grantCourtesy` · `grantCourtesyInternal` | endpoint → `GrantCourtesy` → matrícula/concessão/recibo/outbox em uma transação; cliente de Identity (`confirmStudentAccountInternal`) fora da transação |
| `getCourtesyGrant` · `getCourtesyGrantInternal` | endpoint → `GetCourtesyGrant` |
| `decideAccessInternal` | endpoint com política de asserção `access-decision:read` → `DecideAccess` |
| `confirmStudentAccountInternal` (Identity) | endpoint → `ConfirmStudentAccount` |

**Validações além do contrato:**

| operationId | Regra | Camada |
|---|---|---|
| `previewCourtesyTermInternal` | `months` inteiro de 1 a 60; relógio por `TimeProvider`; fuso da escola da configuração; permissão `cortesia.conceder` | application |
| `grantCourtesyInternal` | motivo com ao menos um caractere que não seja espaço; `lifetime` sem `months`; término calculado no servidor, **nunca aceito do cliente** | domain/application |
| `grantCourtesyInternal` | curso na `course_views` da escola; conta reconfirmada; o ator vem do `sub` do JWT, nunca do corpo | application |
| `listStudentAccessGrantsInternal` | filtra por `tenant_id` do token; aluno de outra escola é página vazia | application/infra |
| `decideAccessInternal` | `tenantId` da asserção; nunca consulta Identity nem `course_views`; relógio por `TimeProvider` | application |
| `lookupStudentAccountInternal` | permissão `cortesia.conceder` na **sessão** validada por Identity, além do escopo da asserção | application |

**Exceção → resposta HTTP:**

| Exceção | HTTP | code do contrato |
|---|---|---|
| `EntitlementRuleException` (campo) | 422 | `FIELD_INVALID` |
| `EntitlementRuleException` (curso) | 422 | `COURSE_NOT_ELIGIBLE` |
| `EntitlementRuleException` (conta) | 422 | `STUDENT_ACCOUNT_NOT_ELIGIBLE` |
| `EntitlementRuleException` (chave) | 422 | `IDEMPOTENCY_KEY_REUSED` |
| `NotFoundException` (concessão) | 404 | `GRANT_NOT_FOUND` |
| Identity sem resposta, inválida ou com tempo esgotado | 503 (502 no BFF) | `STUDENT_ACCOUNT_CHECK_UNAVAILABLE` |
| `commerce` ou Identity indisponível, no BFF | 502 | `COMMERCE_UNAVAILABLE` · `IDENTITY_UNAVAILABLE` |
| tempo esgotado, no BFF | 504 | `UPSTREAM_TIMEOUT` |

### Mapeamento de jornada

| História do PRD | Tela / componente | operationId ou ação local | Evidência |
|---|---|---|---|
| conceder acesso por período ou vitalício, com motivo | área Cortesias, passos 1 a 6 | `lookupStudentAccount`, `listCourtesyCourses`, `grantCourtesy` | V-01, V-02, V-03 |
| ver as concessões do aluno antes de conceder | passo "Aluno" e aviso da revisão | `listStudentAccessGrants` | V-04 |
| ver até quando o acesso vai valer | passo "Vigência" e revisão | `previewCourtesyTerm` (C-11); `endsOn` no resultado | V-03 |
| ver na trilha quem concedeu, a quem, qual curso, quanto tempo e por quê | trilha: lista, filtro e detalhe | consulta da trilha (CAP-030) com rótulos novos | V-07 |
| o aluno perde o acesso quando prometido; um lugar único responde | — (consulta entre serviços) | `decideAccessInternal` | V-05, V-06 |

### Entidades do domínio

| Entidade do Domain Doc | Representação técnica |
|---|---|
| Matrícula | `entitlement.enrollments` |
| Concessão de Acesso | `entitlement.access_grants` (origem, referência da origem, situação, momento da concessão) |
| Vigência | `period_type`, `period_months`, `ends_on`, `expires_at` |
| Suspensão, Revogação, Turma | **fora desta entrega** (`CAP-009`, `CAP-010`); a coluna `status` e a matrícula os acomodam sem migração de dados |

### Interfaces entre fatias ou times

Apenas o que funciona como contrato entre fatias: a **política de autorização da rota de decisão** (escopo `access-decision:read`, emissor e tenant permitidos), a **normalização do título** compartilhada por convenção entre `learning` e `commerce` (cada serviço com a própria cópia e o mesmo teste de casos) e o **cálculo do término**, que só o domínio de Matrícula faz — o SPA não o reproduz.

---

## Arquivos a Modificar e a Referenciar

Arquivos **a criar** não são listados: a estrutura é determinística pelas skills `dotnet` e `react`.

### A modificar

| Caminho | Fatia | Alteração |
|---|---|---|
| `src/identity/src/CodeForCoders.Identity.Domain/Entities/StaffRoleCatalog.cs` (`:9`, `:20`) | V-01 | `cortesia.conceder` no papel financeiro |
| `src/identity/src/CodeForCoders.Identity.Api/Security/ServiceAssertionScopes.cs` | V-01, V-03 | escopos `student-account:lookup` (staff) e `student-account:confirm` |
| `src/identity/src/CodeForCoders.Identity.Api/Endpoints/` e registro de endpoints | V-01, V-03 | duas operações internas novas |
| `src/identity/src/CodeForCoders.Identity.Application/UseCases/Accounts/ResolveAuditIdentityReferences/` e a consulta de rótulos em `Infra.Data` | V-07 | aceitar a referência `conta-aluno`, devolvendo o nome |
| `src/identity/src/CodeForCoders.Identity.Api/Endpoints/AuditIdentityReferenceEndpoints.cs` | V-07 | validação do tipo de referência aceito |
| `src/commerce/src/CodeForCoders.Commerce.Infra.Data/CommerceDbContext.cs` (`:26-34`) | V-02, V-03 | `DbSet`s do módulo e filtros de tenant, um por entidade |
| `src/commerce/src/CodeForCoders.Commerce.Infra.Data/Migrations/` | V-02, V-03, V-06 | migrations geradas pelo EF (schema `entitlement`; nunca à mão) |
| `src/commerce/src/CodeForCoders.Commerce.Infra.Messaging/OutboxPublisherWorker.cs` (`:31-32`, `:78`) | V-03 | drenar também o outbox de `entitlement` |
| `src/commerce/src/CodeForCoders.Commerce.Infra.Messaging/RabbitMqTopologyInitializer.cs` | V-02, V-03 | fila do consumidor de versão e fila de retenção dos fatos `matricula.*` |
| `src/commerce/src/CodeForCoders.Commerce.Infra.Messaging/Configuration/RabbitMqOptions.cs` | V-02, V-03 | nomes e limites das duas filas novas |
| `src/commerce/src/CodeForCoders.Commerce.Infra.Messaging/DependencyInjection.cs` (`:25-28`) | V-02 | registrar o consumidor |
| `src/commerce/src/CodeForCoders.Commerce.Infra.Data/DependencyInjection.cs` (`:36`) | V-03, V-06 | registrar casos de uso, repositórios e a rotina de expiração |
| `src/commerce/src/CodeForCoders.Commerce.Api/Extensions/ServiceConfigurationExtensions.cs` (`:38-44`) | V-01 | política `cortesia.conceder` |
| `src/commerce/src/CodeForCoders.Commerce.Api/Extensions/ServiceAssertionExtensions.cs` (`:24-29`) e `Security/ServiceAssertionScopes.cs` (`:5-8`) | V-05 | política e escopo `access-decision:read` |
| `src/commerce/src/CodeForCoders.Commerce.Api/Extensions/EndpointExtensions.cs` | V-01..V-05 | mapear os endpoints do módulo |
| `src/commerce/tests/CodeForCoders.Commerce.ArchitectureTests/ModuleSchemaConventionTest.cs` | V-03 | asserção do outbox de `entitlement` e de que só `Entitlement` expõe a decisão |
| `src/audit/src/CodeForCoders.Audit.Domain/Policies/AdministrativeActPolicy.cs` (`:17-27`, `:39-52`, `:118-121`) | V-03 | aceitar `cortesia-concedida` com motivo obrigatório, origem `matricula`, alvo `conta-aluno`, complemento validado |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Security/ServiceAssertionTokenFactory.cs` (`:12-23`) | V-01 | escopo `student-account:lookup` na lista permitida |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/AuditRecordEndpoints.cs` (`:14`, `:168`, `:360`) | V-07 | tipo de referência `conta-aluno` e atributo derivado `cursoTitulo` |
| `src/admin-spa/src/features/staff-session/utils/get-staff-areas.ts` (`:6-13`) e `src/config/paths.ts` | V-01 | item e rota da área |
| `src/admin-spa/src/components/app-shell.tsx` (`:40`, `:46`) | V-01 | agrupamento e ícone do novo item |
| `src/admin-spa/src/features/audit-trail/components/audit-trail-screen.tsx` e `audit-record-detail-screen.tsx`, `utils/format-offer-audit-attribute.ts` | V-07 | rótulo, filtro, vigência e curso no detalhe |
| `docker-compose.yml`, `docker-compose.coolify.yml`, `docker-compose.remote.yml`, `scripts/generate-local-env.sh` | V-01, V-03 | chaves e emissores: `bff-admin` com o escopo novo; `commerce` como emissor em Identity |

### A referenciar (não alterar)

| Caminho | Por que consultar |
|---|---|
| `src/commerce/src/CodeForCoders.Commerce.Application/UseCases/CatalogOffers/PublishOffer/PublishOffer.cs` | padrão de recibo, trava, fato e ato na mesma transação |
| `src/commerce/src/CodeForCoders.Commerce.Infra.Data/Catalog/CatalogCourseProjectionStore.cs` e `Infra.Messaging/CatalogCourseConsumerWorker.cs`, `PublishedCourseFact.cs` | visão do curso monotônica, DLQ, retry |
| `src/learning/src/CodeForCoders.Learning.Domain/Entities/CourseTitleSearch.cs` | regra de normalização do título |
| `src/learning/src/CodeForCoders.Learning.Infra.Messaging/CatalogInitialLoad.cs` | reenvio de execução única, com marcador |
| `src/identity/src/CodeForCoders.Identity.Api/Endpoints/AuditIdentityReferenceEndpoints.cs` | padrão de operação interna com asserção, `X-Staff-Session` e tratamento de erro |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/CatalogOfferEndpoints.cs` | padrão de proxy com sessão, permissão, `Idempotency-Key` e JWT de audiência `commerce` |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Clients/CourseAuditReferenceEnricher.cs` | resolução do título do curso pela operação existente de `learning` |
| `docs/adr/0003`, `0004`, `0005`, `0009`, `0010`, `0011` | autenticação de aluno, de serviço e do ator |
| `domains/matricula-e-direito-de-acesso/domain.md` | RN-D01 a RN-D17 |

---

## Análise de Impacto

| Componente | Tipo | Impacto e risco | Ação requerida |
|---|---|---|---|
| `commerce` módulo `Entitlement` | novo | primeiro conteúdo de negócio do módulo; primeira migration do schema `entitlement`; nó mais consultado do sistema no futuro | schema, contratos e casos de uso próprios desde o primeiro commit (baseline); testes de arquitetura de fronteira |
| `commerce` publicador de outbox | modificado | passa a drenar três schemas; falha de um não pode travar os outros | ciclo por schema, com o tratamento de erro atual |
| `commerce` topologia de mensageria | modificado | duas filas novas e uma ligação nova ao exchange de `learning` | declaração idempotente na partida, como a do Catálogo |
| `identity` | modificado | permissão nova no papel financeiro; duas operações internas; `commerce` vira emissor | ADR-0010; teste de rotação de chave; emissor só em ambiente com o par de chaves |
| `audit` | modificado | tipo novo, aditivo, com motivo obrigatório | `audit` 1.4.0 antes do primeiro ato de `commerce` |
| `bff-admin` / `admin-spa` | modificado | área nova; rótulos da trilha; item de menu novo | EN-01 antes de qualquer tela |
| Consulta da trilha (CAP-030) | modificado | novo tipo de referência e atributo derivado; schema inalterado | nenhum contrato de CAP-030 muda além da aditividade já registrada (C-07) |
| `learning` | **não muda** | o reenvio já existente alimenta a visão de Entitlement | respeitar a ordem de implantação; ver Riscos |
| `media`, `learning` como consumidores da decisão | nenhum nesta entrega | credenciais **não** provisionadas (ADR-0011) | `CAP-007` e `CAP-017` provisionam |
| Segredos (Compose, Coolify, secret manager) | modificado | um par de chaves novo (`commerce` → Identity) e um escopo novo no `bff-admin` | `scripts/generate-local-env.sh`, ambientes remotos |

---

## Riscos e Preocupações

| Preocupação | Local (`arquivo:linha`) | Impacto | Mitigação |
|---|---|---|---|
| **O nome "Acessos" do PRD já é do administrador** (gestão de papéis e convites, rota `/acessos`, permissão `acesso.gerir`). Um ator com os dois papéis veria dois itens com o mesmo nome | `src/admin-spa/src/features/staff-session/utils/get-staff-areas.ts:6`, `src/admin-spa/src/config/paths.ts:10` | confusão de menu e de rota; colisão do caminho | **D-01:** a área nova se chama **Cortesias**, rota `/cortesias`. É uma errata de nome ao PRD (RF-01 e Experiência do Usuário), sem mudar comportamento nem contrato |
| O publicador só drena `catalog` e `sales`; o outbox de `entitlement` nunca seria publicado | `OutboxPublisherWorker.cs:31-32`, `:78` | fato e ato ficariam parados e a cortesia não chegaria à trilha | modificar o publicador em V-03; a evidência de V-03 exige o ato gravado no `audit` |
| Fato sem consumidor devolvido por `mandatory: true` esgota as tentativas do outbox | `RabbitMqPublisher.cs:34` (publicação com `mandatory`), `RabbitMqTopologyInitializer.cs` (retenção do Catálogo) | publicador falha e retenta para sempre | fila de retenção limitada ligada às duas chaves; teste que documenta a razão |
| **Reenvio de execução única**: com a flag ligada **antes** do consumidor de Entitlement, a visão nunca recebe os cursos existentes, e repetir exige remover o marcador | `CatalogInitialLoad.cs:45` (`AnyAsync` do marcador), `CatalogInitialLoadOptions.cs` (`Enabled`) | lista de cursos concedíveis vazia ou incompleta | **a flag não está ligada em nenhum Compose versionado** (verificado: nenhuma ocorrência de `CatalogInitialLoad` nos três `docker-compose*.yml` nem no `.env`); a ordem de implantação põe os **dois** consumidores (Catálogo e Entitlement) antes de ligá-la; V-02 verifica com o consumidor declarado antes do reenvio; se em algum ambiente o reenvio já rodou, o procedimento é operacional e fora do produto (ver Questões em Aberto) |
| `audit` desatualizado grava o ato como não conforme | `AdministrativeActPolicy.cs:17-27`, `:39-52` | trilha poluída, alerta de operação | ordem de implantação; V-03 verifica o ato **conforme** |
| Normalização do e-mail repetida em linha em vários casos de uso: a localização pode divergir do cadastro | `RegisterStudentAccount.cs:39`, `AuthenticateStaffSession.cs:33`, `CreateStaffInvitation.cs:37` | conta não encontrada por diferença de normalização | a localização usa **a mesma** expressão (`Trim` e minúsculas, RN-01) e um teste prova que o e-mail que o cadastro grava é encontrado pela localização |
| Cada entidade de `commerce` declara o próprio filtro de tenant à mão | `CommerceDbContext.cs:26-34` | uma entidade nova sem filtro vaza dado entre escolas | teste de arquitetura que falha para entidade de `entitlement` sem filtro (G07) |
| Lista de escopos permitidos do assinador do `bff-admin` é fixa no código | `ServiceAssertionTokenFactory.cs:12-23` | escopo novo recusado em tempo de execução | incluir `student-account:lookup` na lista, com teste |
| Escopos conhecidos de Identity são constantes por superfície | `ServiceAssertionScopes.cs:5-30` (Identity) | emissor `commerce` ou escopo novo rejeitado na partida | acrescentar as duas constantes e o teste de configuração do emissor |
| Primeiro uso de fuso do projeto; a imagem de runtime é Debian (`mcr.microsoft.com/dotnet/aspnet:10.0`), e **não verifiquei** se traz `tzdata` | `src/commerce/Dockerfile:30`; nenhuma ocorrência de `TimeZoneInfo` em `src/` | `FindSystemTimeZoneById("America/Sao_Paulo")` falharia no contêiner e a cortesia não concederia | teste de fumaça **dentro da imagem** que resolve o fuso; falha na partida (validação de opções) em vez de na primeira concessão |
| Sem corte de feriado de fuso: um fuso com horário de verão pode ter meia-noite inexistente ou ambígua | cálculo de `expires_at` | término errado por uma hora em fuso futuro | o domínio trata `IsInvalidTime` e `IsAmbiguousTime`; o fuso de hoje (Brasília) não tem horário de verão |
| **Os recibos de edição do Catálogo (`catalog.edit_receipts`) nunca são apagados**: guardam `ExpiresAt` de 24 h, mas nenhuma rotina os remove — a limpeza existente só trata as intenções de compra | `CatalogEditReceipt.cs` (`Store` define `ExpiresAt`), `PurchaseIntentReceiptCleanupWorker.cs:8-30`; nenhuma outra remoção encontrada em `src/commerce/src` | crescimento ilimitado da tabela a cada escrita do backoffice (dívida **já existente**, fora desta entrega) | Entitlement tem a própria limpeza (D-10); a dívida do Catálogo é registrada como pendência separada |
| Cortesia errada **não pode ser desfeita** até `CAP-009` | PRD DP-07, QA-01 (aceito) | acesso vitalício indevido | confirmação reforçada; ato com autor e motivo; correção operacional pelo time, fora do produto |

---

## Decisões Técnicas

- **D-01. A área do backoffice se chama "Cortesias", não "Acessos" (aprovada em 2026-10-01).**
  - **Racional:** "Acessos" e a rota `/acessos` já pertencem à gestão de acesso interno do administrador (`get-staff-areas.ts:6`); o PRD não conhecia a colisão. Errata de nome aplicada: PRD 1.1.
  - **Trade-offs:** nada muda em contrato (`Cortesias` é só a `tag` do OpenAPI); o wireframe nasce com o nome certo.
  - **Alternativas rejeitadas:** manter "Acessos" (dois itens iguais para quem acumula papéis); "Direitos de acesso" (compete com o item do administrador no mesmo vocabulário).
- **D-02. Visão mínima do curso própria de Entitlement, sem ler a do Catálogo.**
  - **Racional:** Matrícula e Catálogo são módulos distintos; o baseline manda `Sales` falar com `Entitlement` por evento, "como se já fosse outro serviço", e `Entitlement` é o primeiro candidato a extração. Ler a visão do Catálogo acoplaria os dois. Só título e versão precisam existir.
  - **Trade-offs:** mais uma fila e uma tabela; o mesmo fato é aplicado duas vezes em `commerce`.
  - **Alternativas rejeitadas:** consulta em processo ao Catálogo; leitura síncrona a `learning` (OD56).
- **D-03. A prévia do término na tela vem do servidor, não de cálculo no cliente (aprovada em 2026-10-01).**
  - **Racional:** a regra (fim do dia no fuso da escola, mês sem o dia) não deve existir em dois lugares; DP-03 só vale com uma implementação. O contrato ganhou uma operação de leitura sem efeito, `previewCourtesyTerm` (C-11), derivada da mesma função de domínio da concessão.
  - **Trade-offs:** uma chamada a mais por escolha de meses; o resultado só vale se a confirmação ocorrer no mesmo dia no fuso da escola (a revisão pede a prévia de novo, e a data definitiva é a da concessão).
  - **Alternativas rejeitadas:** reimplementar a aritmética de calendário no SPA (diverge); mostrar a data só na revisão e no resultado (contraria RF-03, passo 3).
- **D-04. Término gravado, situação derivada.** `ends_on` e `expires_at` são gravados na concessão; `expired` nunca é gravado.
  - **Racional:** RN-D06 pede que o acesso vença sem rotina; uma comparação com `expires_at` é a decisão inteira; a rotina de V-06 só emite um fato informativo.
  - **Trade-offs:** a regra de DP-03 fica congelada por concessão; corrigi-la depois não retifica o que foi gravado.
  - **Alternativas rejeitadas:** gravar a situação `expired` e atualizá-la por rotina (a decisão passaria a depender da rotina); calcular o término na leitura (a decisão repetiria a aritmética de calendário em cada consulta).
- **D-05. Reconfirmação em Identity fora da transação de banco.** A chamada de rede acontece antes de abrir a transação; a transação só grava.
  - **Racional:** não segurar trava e conexão durante E/S de rede; a idempotência por recibo absorve o reenvio.
  - **Trade-offs:** a conta pode mudar entre a confirmação e o commit (janela de milissegundos), aceita: o PRD exige recusar a conta que mudou **entre a localização e a confirmação**, não entre a confirmação e a gravação.
  - **Alternativas rejeitadas:** confirmar dentro da transação (conexão presa à rede de outro serviço).
- **D-06. Idempotência pelo recibo de (escola, ator, chave), não pela tripla (aluno, curso, vigência).** Duas cortesias deliberadas ao mesmo curso são duas concessões (RN-D07); só o **reenvio da mesma intenção** é deduplicado (C-09).
- **D-07. Nenhuma credencial de `media` ou `learning` é provisionada.** O verificador e o escopo existem e são testados com um emissor de teste; as chaves nascem com o primeiro consumidor (ADR-0011).
- **D-08. Fuso da escola como configuração, com validação na partida.** `America/Sao_Paulo` na Fase 1; vira atributo da escola quando houver mais de uma. A partida falha se o identificador não resolve.
- **D-09. `cursoTitulo` como atributo derivado no detalhe da trilha.** O BFF o resolve na leitura, não o grava; `attributes` já é mapa de texto, então nenhum schema muda. Falha de resolução mostra só o identificador.
- **D-10. Limpeza própria dos recibos de `grant_receipts`.** Entra em V-03 uma rotina horária que apaga os recibos vencidos, no padrão de `PurchaseIntentReceiptCleanupWorker.cs` (`ExecuteDeleteAsync` com `ExpiresAt <= agora`). **Racional:** o recibo vence em 24 horas, mas nada o remove da tabela se não houver rotina. **Trade-offs:** mais uma rotina de fundo. **Alternativas rejeitadas:** copiar o comportamento do Catálogo, cujos recibos de edição nunca são apagados (ver Riscos).

> Decisões que valem além desta feature estão nas ADR-0010 e ADR-0011.

---

## Verificação

Só o que foge do padrão das skills `dotnet` e `react`:

- **Cenários críticos não óbvios:**
  - os **seis casos de término** de RF-05 como tabela de teste de unidade, mais o limite exato (23:59:59 → `allowed`; 00:00:00 do dia seguinte → `denied`) com relógio controlado (`TimeProvider` fake);
  - **atomicidade:** falha forçada entre a gravação da concessão e o commit → nada de concessão, fato, ato, recibo ou matrícula;
  - **concorrência de reenvio:** duas requisições simultâneas com a mesma `Idempotency-Key` → uma concessão, um fato, um ato; a segunda recebe a resposta guardada;
  - **falha fechada:** Identity sem resposta, resposta inválida, tempo esgotado → nada gravado e erro explícito;
  - **expiração sem rotina:** concessão vencida sem a rotina de V-06 rodar → `denied`/`grant-ended`; **duas instâncias** da rotina → um fato;
  - **publicador:** o ciclo completa com a fila de retenção e esgota as tentativas se ela não existir;
  - **esquemas de autenticação:** a rota da decisão recusa JWT de ator, asserção do `bff-student` e asserção sem escopo; as rotas de cortesia recusam asserção de serviço;
  - **e-mail fora dos lugares errados:** inspeção da saída de log, dos spans e das métricas de uma localização e de uma concessão.
- **Dados ou ambiente especiais:** Compose com Identity, `commerce`, `audit`, `learning`, `bff-admin`, `admin-spa`, RabbitMQ, PostgreSQL e Valkey; **dados semeados:** uma conta de aluno ativa, uma não confirmada, uma desativada e uma conta de ator interno; um curso publicado em `learning` (fato real) e um curso sem oferta. O smoke que depende de infraestrutura local registra, no plano de tasks, as variáveis (chaves de asserção, escopos, tenants) e a ordem de subida. Um teste roda **dentro da imagem de `commerce`** e resolve o fuso.
- **Observabilidade além do padrão:** métrica de defasagem da expiração (agora − `expires_at` do mais antigo pendente) com alerta acima de 30 minutos; contadores de concessões por origem e de decisões por resultado, **sem rótulo de aluno, e-mail ou curso por pessoa** (G10); span da reconfirmação em Identity sem o identificador da conta como atributo de texto livre.
- **Verificação dos contratos:** os cenários de [contracts.md](contracts.md), seção "Validação e verificação", são o roteiro: HTTP (os códigos de cada operação), mensagens (um fato por concessão, `acesso-expirado` uma vez, ato conforme) e a decisão. Validação do YAML (Spectral, AsyncAPI CLI, `jsonschema`) já foi feita e é distinta da conformidade da implementação.

---

## Questões em Aberto

- [ ] **Reenvio já executado em algum ambiente** — responsável pela operação — se algum ambiente já rodou o reenvio de `learning` antes do consumidor de Entitlement, repetir exige remover o marcador, o que é procedimento operacional. Impacto: só nesse ambiente; não bloqueia o handoff.
- [ ] **Limpeza dos recibos de edição do Catálogo (dívida existente)** — time de `commerce` — fora desta entrega; sem a rotina, a tabela cresce a cada escrita. Impacto: armazenamento; resolver em tarefa separada, antes de o Catálogo ter uso intenso.

---

## Architecture Decision Records

Herdadas: [ADR-0001](../../../docs/adr/0001-monorepo-de-codigo.md) (serviços com deploy independente; sem biblioteca compartilhada), [ADR-0003](../../../docs/adr/0003-verificacao-de-sessao-do-aluno.md), [ADR-0004](../../../docs/adr/0004-autenticacao-de-servico-bff-identity.md), [ADR-0005](../../../docs/adr/0005-sessao-e-servico-do-backoffice.md) (JWT de ator por audiência do serviço dono) e [ADR-0009](../../../docs/adr/0009-autenticacao-de-servico-bff-aluno-em-servicos-de-dominio.md). Nenhuma ADR `Accepted` conflita com o desenho.

Novas (status `Accepted`, aprovadas com esta TechSpec em 2026-10-01):

- [ADR-0010: Autenticação de serviço de `commerce` em Identity, para confirmar a conta de aluno ao conceder acesso](../../../docs/adr/0010-autenticacao-de-servico-commerce-em-identity.md) — `commerce` passa a ser emissor de asserção em Identity, com escopo único `student-account:confirm` e falha fechada (C-03).
- [ADR-0011: Autenticação de serviço de `media` e `learning` em `commerce`, para consultar a decisão de acesso](../../../docs/adr/0011-autenticacao-de-servico-media-learning-em-commerce.md) — emissores e escopo `access-decision:read` para a decisão; chaves só nascem com o primeiro consumidor (C-04).
