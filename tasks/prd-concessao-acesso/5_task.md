---
status: pending
task_kind: vertical
blocked_by: ["4.0"]
gate: 'dotnet test --project src/commerce/tests/CodeForCoders.Commerce.UnitTests/CodeForCoders.Commerce.UnitTests.csproj -- --filter-class CodeForCoders.Commerce.UnitTests.AccessTermTests --minimum-expected-tests 9 && dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.CourtesyTermPreviewTests --minimum-expected-tests 4 && dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.CourtesyGrantTests --minimum-expected-tests 14 && dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.EntitlementOutboxPublishingTests --minimum-expected-tests 3 && dotnet test --project src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj -- --filter-class CodeForCoders.Identity.IntegrationTests.StudentAccountConfirmationTests --minimum-expected-tests 6 && dotnet test --project src/audit/tests/CodeForCoders.Audit.UnitTests/CodeForCoders.Audit.UnitTests.csproj -- --filter-class CodeForCoders.Audit.UnitTests.CourtesyActPolicyTests --minimum-expected-tests 6 && dotnet test --project src/audit/tests/CodeForCoders.Audit.IntegrationTests/CodeForCoders.Audit.IntegrationTests.csproj -- --filter-class CodeForCoders.Audit.IntegrationTests.CourtesyActRecordingTests --minimum-expected-tests 2 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj -- --filter-class CodeForCoders.BffAdmin.IntegrationTests.CourtesyGrantProxyTests --minimum-expected-tests 6 && npm --prefix src/admin-spa run test -- courtesy-confirm'
gate_expect: "Pelo menos 54 testes passam: 9 unitários e 21 de integração de commerce, 6 de identity, 8 de audit, 6 do BFF, 4 do SPA"
---

# 5.0 O financeiro concede a cortesia e o acesso nasce com término correto, fato e ato

**Fatia:** V-03 · **Cobre:** RF-03, RF-04 (modelo e origem), RF-05, RF-07 (concedido), RF-08 (ato conforme), US-01, US-03, RN-D02, RN-D04, RN-D07, RN-D08, RN-D09, RN-D10, RN-D11, RN-D17, RN-O08, RN-O09, RN-A02, RN-A03, RN-A05, RN-A06, RN-A08, RN-A14, RN-21, DP-03, DP-04, DP-05, DP-08, C-03, C-05, C-06, C-09, C-10, C-11 · **Spec:** `techspec.md#v-03-o-financeiro-concede-a-cortesia--o-acesso-nasce-com-término-correto-fato-e-ato` · **ADR:** [ADR-0010](../../docs/adr/0010-autenticacao-de-servico-commerce-em-identity.md)

## Comportamento

- **`commerce` — modelo e término:** as migrations (EF) criam, no schema `entitlement`, `enrollments` (único por
  escola, aluno e curso), `access_grants` (origem `courtesy`, vigência, `status` `active`, `ends_on`, `expires_at`,
  motivo, autor, marcador de expiração), `grant_receipts` (recibo de 24 h por escola, ator e hash da chave) e o
  `outbox_messages` do módulo. O domínio calcula o término **uma vez, na concessão**: converte o momento da
  concessão para a data local no fuso da escola (configuração, `America/Sao_Paulo` na Fase 1, **validada na
  partida**), soma N meses à data (31/01 + 1 mês → 28/02; 29/02 em ano bissexto), e grava `ends_on` (último dia) e
  `expires_at` (início do dia seguinte no fuso, em UTC, exclusivo). Vitalícia grava ambos nulos. `expired` nunca é
  gravado. O fuso com horário de verão trata hora inexistente e ambígua.
- **`commerce` — `grantCourtesyInternal`:** valida o corpo (motivo de 1 a 500 caracteres com ao menos um
  não-espaço; meses de 1 a 60; `lifetime` sem `months`) → 422 `FIELD_INVALID`; recibo existente e não vencido:
  mesmo corpo devolve a resposta guardada (200), corpo diferente → 422 `IDEMPOTENCY_KEY_REUSED`; exige o curso na
  visão (4.0) → 422 `COURSE_NOT_ELIGIBLE`; **reconfirma a conta em Identity fora da transação** →
  `eligible: false` → 422 `STUDENT_ACCOUNT_NOT_ELIGIBLE`; Identity sem resposta, resposta inválida ou tempo
  esgotado → 503 `STUDENT_ACCOUNT_CHECK_UNAVAILABLE` e **nada gravado**. Em seguida, uma transação sob trava por
  (escola, ator, hash da chave) relê o recibo e grava matrícula, concessão, recibo e **duas** linhas no outbox: o
  fato `matricula.acesso-concedido.v1` e o ato `auditoria.ato-praticado.v1` (`fatoId` = `eventId`, `praticadoEm` =
  momento da concessão). Já haver concessão ativa ao curso **não** impede (RN-D07). Mesma chave concorrente → uma
  concessão. O autor vem do `sub` do JWT, nunca do corpo.
- **`commerce` — `previewCourtesyTermInternal`:** `GET` de leitura, sem efeito, com `months` de 1 a 60; devolve
  `endsOn` e `expiresAt` pela **mesma função de domínio** da concessão; 0 e 61 → 400; sem permissão → 403;
  nada é gravado (C-11).
- **`commerce` — publicação:** o publicador passa a drenar também o outbox de `entitlement`; a topologia declara
  a fila de retenção limitada dos fatos `matricula.acesso-*.v1` (sem ela o broker devolve a mensagem e o
  publicador esgota as tentativas); o ato vai ao exchange da Auditoria com `correlationId`. Uma rotina horária
  apaga os recibos vencidos de `grant_receipts` (D-10).
- **`commerce` — chamada a Identity:** um assinador de asserção com chave exclusiva de `commerce`
  (`iss` = `commerce`, escopo `student-account:confirm`, vida de até 60 s) e tempo-limite curto, **sem repetição
  automática**; a chave e o emissor vêm da configuração do ambiente e são validados na partida.
- **`identity` — `confirmStudentAccountInternal`:** `POST /internal/v1/student-account-confirmations` valida a
  asserção de `commerce` (emissor novo, escopo `student-account:confirm`, tenants permitidos); devolve
  `eligible: true` só para conta **de aluno**, **ativa**, do tenant da asserção; qualquer outro caso, `false`, sem
  dizer qual. Sem asserção válida → 401; sem escopo → 403.
- **`audit`:** `AdministrativeActPolicy` aceita `cortesia-concedida` **com motivo obrigatório**, origem
  `matricula`, alvo `conta-aluno` e complemento (`curso` e `concessao` UUID, `vigencia` `vitalicia` ou de `1m` a
  `60m`); tipo, origem, alvo ou complemento fora disso, ou motivo ausente, são gravados como **não conforme**, não
  descartados. O ato conforme aparece como registro único mesmo reentregue.
- **`bff-admin`:** `grantCourtesy` (com `Idempotency-Key` obrigatória, CSRF, sessão e permissão) e
  `previewCourtesyTerm`, repassando o JWT de ator; 201 com `Location`, 200 no reenvio, 422 com o `code`, 502
  `COMMERCE_UNAVAILABLE`/`STUDENT_ACCOUNT_CHECK_UNAVAILABLE`, 504 `UPSTREAM_TIMEOUT`; `getCourtesyGrant` devolve a
  cortesia da escola (outra origem, inexistente ou de outra escola → 404 `GRANT_NOT_FOUND`).
- **`admin-spa`:** os passos *Vigência* (com a prévia da data pedida ao servidor a cada mudança de meses e ao abrir
  a revisão), *Motivo*, *Revisão* (frase única e término em data), *Confirmar* e *Resultado*. A `Idempotency-Key`
  é gerada **uma vez por tentativa de revisão** e reaproveitada no reenvio; o erro de cada `code` aparece no campo
  ou na mensagem certa, preservando o texto digitado; confirmação reforçada da vitalícia fica em 6.0. Conforme o
  Figma aprovado em 2.0.
- **Dados sensíveis:** o motivo vai à concessão e ao ato (necessário à Auditoria) e **não** ao fato nem a log,
  span ou métrica; a `Idempotency-Key` só existe como hash; nenhum e-mail ou nome em fato, log ou span.
- **Configuração:** par de chaves de `commerce`, emissor `commerce` em Identity (só com o par presente), fuso da
  escola e as filas novas nos três Compose e em `scripts/generate-local-env.sh`.

## Fora do escopo desta task

Aviso de concessão existente, confirmação reforçada da vitalícia e lista de concessões do aluno (6.0). Decisão de acesso (7.0). Rotina e fato de expiração (8.0). Rótulos da trilha (9.0). Revogação ou desfazer (CAP-009).

## Decisões fechadas

- Término gravado, situação derivada; a regra de DP-03 fica congelada por concessão (`techspec.md#decisões-técnicas`, D-04).
- Reconfirmação em Identity **antes** da transação de banco, com falha fechada (D-05, [ADR-0010](../../docs/adr/0010-autenticacao-de-servico-commerce-em-identity.md)).
- Idempotência por recibo de (escola, ator, chave), não por (aluno, curso, vigência): duas cortesias deliberadas são duas concessões (D-06, C-09).
- Outbox próprio do módulo, no schema `entitlement`; concessão, fato e ato nascem juntos ou não nascem (G06).
- O término é sempre calculado no servidor; o cliente nunca o envia nem o recalcula (D-03).
- O aluno não é avisado por e-mail nesta entrega (DP-08).

## Modificar / Referenciar

- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Infra.Data/CommerceDbContext.cs`, `DependencyInjection.cs` (entidades, repositórios, rotina de limpeza); migrations via `dotnet ef migrations add`
- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Infra.Messaging/OutboxPublisherWorker.cs` (drenar `entitlement`), `RabbitMqTopologyInitializer.cs`, `Configuration/RabbitMqOptions.cs` (fila de retenção)
- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Api/Extensions/EndpointExtensions.cs`, `ServiceConfigurationExtensions.cs` (endpoints e cliente de Identity); `src/commerce/tests/CodeForCoders.Commerce.ArchitectureTests/ModuleSchemaConventionTest.cs` (outbox de `entitlement`, filtro de tenant)
- **modificar:** `src/identity/src/CodeForCoders.Identity.Api/Security/ServiceAssertionScopes.cs` e o registro de emissores (escopo `student-account:confirm`); endpoints de Identity (operação nova)
- **modificar:** `src/audit/src/CodeForCoders.Audit.Domain/Policies/AdministrativeActPolicy.cs`
- **modificar:** `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/EndpointExtensions.cs` (grupo de cortesias: concessão, prévia e leitura)
- **modificar:** `src/admin-spa/src/features` (feature de cortesias: passos *Vigência* a *Resultado*), `src/admin-spa/src/testing/handlers.ts`
- **modificar:** `docker-compose.yml`, `docker-compose.coolify.yml`, `docker-compose.remote.yml`, `scripts/generate-local-env.sh` (chave de `commerce`, emissor em Identity, fuso, filas)
- **ref:** `src/commerce/src/CodeForCoders.Commerce.Application/UseCases/CatalogOffers/PublishOffer/PublishOffer.cs` (recibo, trava, fato e ato na mesma transação); `Infra.Data/Catalog/PurchaseIntentReceiptCleanupWorker.cs` (limpeza); `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Security/ServiceAssertionTokenFactory.cs` (assinador de asserção); `src/identity/src/CodeForCoders.Identity.Api/Security/ServiceAssertionVerifier.cs` (verificação); `src/audit/src/CodeForCoders.Audit.Domain/Policies/AdministrativeActPolicy.cs` (precedente das ofertas); `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/CatalogOfferEndpoints.cs` (proxy com `Idempotency-Key`); `internal-api-contract-commerce.yaml`, `internal-api-contract-identity.yaml`, `api-contract.yaml`, `asyncapi-contract.yaml`, `asyncapi-contract-audit.yaml`; `docs/design/wireframes-cortesias.md` e Figma aprovado; skills `dotnet` e `react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/commerce` | `dotnet format src/commerce/CodeForCoders.Commerce.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/commerce` | `dotnet build src/commerce/CodeForCoders.Commerce.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj` | exit 0 (sem regressão) | `ci-dotnet.yml` Testes |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.ArchitectureTests/CodeForCoders.Commerce.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/identity` | `dotnet format src/identity/CodeForCoders.Identity.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/identity` | `dotnet build src/identity/CodeForCoders.Identity.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/identity` | `dotnet test --project src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj` | exit 0 (sem regressão) | `ci-dotnet.yml` Testes |
| `src/audit` | `dotnet format src/audit/CodeForCoders.Audit.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/audit` | `dotnet build src/audit/CodeForCoders.Audit.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/audit` | `dotnet test --project src/audit/tests/CodeForCoders.Audit.IntegrationTests/CodeForCoders.Audit.IntegrationTests.csproj` | exit 0 (sem regressão) | `ci-dotnet.yml` Testes |
| `src/bff-admin` | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-admin` | `dotnet build src/bff-admin/CodeForCoders.BffAdmin.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/bff-admin` | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj` | exit 0 (sem regressão) | `ci-dotnet.yml` Testes |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run test -- courtesy-course-step` | exit 0 (sem regressão) | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run build -- --base=/admin/` | exit 0 | `ci-react-ts.yml` |

Cobertura agregada ≥ 70%, `dotnet publish` e imagens ficam para a validação full.

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 54 testes.
- [ ] Composição: `commerce`, `identity`, `audit` e `bff-admin` iniciam no ambiente de teste com os registros reais; o cliente de Identity em `commerce` e o do BFF são exercitados contra a fronteira controlada do teste, sem dublê do adaptador; a aplicação **não** inicia com um fuso inválido.
- [ ] Os seis casos de término de RF-05 (15/03 + 12 m; 15/03 + 1 m; 31/01 + 1 m; 31/01/2028 + 1 m; 31/03 + 6 m; vitalícia) e o caso da concessão às 23:59 do dia 15 passam como teste de unidade; a prévia devolve o mesmo término da concessão no mesmo instante.
- [ ] Conceder 6 meses → uma concessão `courtesy`, um fato conforme ao `AcessoConcedidoPayload` e um ato conforme na trilha; falha forçada entre a gravação e o commit → nada nasce; duplo clique com a mesma chave → uma concessão, um fato, um ato, 200 com a original; corpo diferente com a mesma chave → `IDEMPOTENCY_KEY_REUSED`.
- [ ] Motivo vazio, só com espaços ou com 501 caracteres, e 0 ou 61 meses → 422 `FIELD_INVALID`, nada concedido; curso nunca publicado ou de outra escola → `COURSE_NOT_ELIGIBLE`; conta desativada, de ator interno, de outra escola ou desativada entre a localização e a confirmação → `STUDENT_ACCOUNT_NOT_ELIGIBLE`; Identity sem resposta → `STUDENT_ACCOUNT_CHECK_UNAVAILABLE` e nada gravado; curso sem oferta aceita a cortesia.
- [ ] O publicador completa o ciclo com a fila de retenção ligada e esgota as tentativas se ela não existir; `audit` marca `cortesia-concedida` sem motivo, com origem diferente de `matricula` ou alvo diferente de `conta-aluno` como não conforme.
- [ ] Nenhum e-mail, nome ou texto de motivo em fato, log ou span; o motivo só na concessão e no ato; conceder sem `cortesia.conceder` → 403 no BFF e em `commerce` direto.
- [ ] Smoke no Compose (Identity, `learning`, `commerce`, `audit`, `bff-admin`, `admin-spa`, RabbitMQ, PostgreSQL, Valkey; **`audit` e Identity antes de `commerce`**; chaves por `./scripts/generate-local-env.sh`): `http://localhost:8081/admin/cortesias` → conceder 6 meses a um aluno ativo e ver a concessão com a data do término; o ato aparece como registro conforme na trilha do administrador.
