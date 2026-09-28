---
status: done
task_kind: vertical
blocked_by: ["5.0"]
gate: 'dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj -- --filter-class CodeForCoders.BffAdmin.IntegrationTests.AuditComplementPublicationTests --minimum-expected-tests 5 && dotnet test --project src/audit/tests/CodeForCoders.Audit.IntegrationTests/CodeForCoders.Audit.IntegrationTests.csproj -- --filter-class CodeForCoders.Audit.IntegrationTests.AuditComplementRecordingTests --minimum-expected-tests 9 && npm --prefix src/admin-spa run test -- audit-complement-recorded'
gate_expect: "Pelo menos 18 testes passam: 5 BFF, 9 Audit, 4 SPA"
---

# 6.0 A confirmação aceita vira complemento imutável e aparece ao lado do original

**Fatia:** V-03 (registro) · **Cobre:** RF-04 (registro e unicidade), RF-05, US-03, RN-A01, RN-A02, RN-A03, RN-A07, RN-A13, DP-03, DP-04, DP-05 · **Spec:** `techspec.md#v-03-administrador-confirma-uma-explicação-e-a-vê-como-complemento-imutável` · **ADR:** —

## Comportamento

- **BFF — publicação:** o worker reserva a linha do outbox numa transação curta com lease, decifra o
  payload em memória, publica **fora da transação** no exchange `audit.events` com a routing key de
  `asyncapi-contract.yaml` e confirmação do broker, e marca o resultado noutra transação. Com o
  broker fora do ar após o commit, a mensagem fica pendente e é publicada ao voltar; lease expirado
  após queda pode republicar (o consumidor deduplica). As mensagens já existentes do BFF continuam
  indo ao seu exchange próprio. O BFF só tem permissão de publicar em `audit.events`; a fila é de
  `audit`.
- **Audit — consumo:** fila própria com binding da nova routing key, sem alterar
  `auditoria.ato-praticado.v1`, que continua sendo consumido. Mensagem válida referindo original
  existente **do mesmo tenant** grava um novo Registro de Auditoria complementar com referência ao
  original, autor, `confirmedAt` e explicação, no mesmo commit que a unicidade
  (`tenantId`, `confirmationId`); ack só depois do commit. Redelivery idêntico faz ack sem nova
  linha. Mesma chave com conteúdo divergente, original inexistente, de outro tenant ou que é outro
  complemento → DLQ com alerta, sem mutação. Falha transitória não faz ack e respeita
  `x-delivery-limit`; a escolha entre `basic.reject` e `basic.nack` é comprovada com RabbitMQ 4.3
  real e o comentário incorreto do worker é corrigido. O original e sua conformidade ficam byte a
  byte iguais; complementos fora de ordem aparecem ordenados na leitura. A credencial de runtime de
  `audit` segue só com `SELECT, INSERT`.
- **Lista e detalhe:** o original complementado passa a ter `hasComplements` e "Complementado" na
  lista (sem virar linha); o detalhe mostra cada complemento na linha do tempo "ACRESCENTADO
  DEPOIS", com autor (rótulo resolvido como em 4.0) e momento, nunca dentro do Card original.
- **SPA (A4.b):** durante a espera de 5.0, encontrado o `confirmationId`, o item "Aguardando
  registro…" vira o complemento e aparece o toast "Complemento registrado"; um segundo complemento
  entra depois do primeiro sem alterar o anterior.

## Fora do escopo desta task

Retenção do outbox processado e da idempotência além do descarte operacional do BFF; métricas e
alertas de plataforma além dos citados em `techspec.md#verificação` ficam para a validação full.

## Decisões fechadas

- Consumidor antes do produtor: a fila de `audit` existe antes de o BFF publicar (`techspec.md#mensagem-e-dados`).
- Complemento não reescreve autor, alvo, tipo, motivo nem conformidade do original: DP-03, DP-04.
- O consumidor não reconsulta papel; processa um fato já confirmado na borda: ADR-0005.
- Migrations de `audit` pelo tooling do EF, nunca à mão.

## Modificar / Referenciar

- **modificar:** `src/bff-admin/src/CodeForCoders.BffAdmin.Infra.Messaging/RabbitMqPublisher.cs`, `src/bff-admin/src/CodeForCoders.BffAdmin.Infra.Messaging/OutboxPublisherWorker.cs`, `src/bff-admin/src/CodeForCoders.BffAdmin.Infra.Messaging/Configuration/RabbitMqOptions.cs`, `src/bff-admin/src/CodeForCoders.BffAdmin.Api/appsettings.json`
- **modificar:** `src/audit/src/CodeForCoders.Audit.Infra.Messaging/AuditTopologyInitializer.cs`, `src/audit/src/CodeForCoders.Audit.Infra.Messaging/AuditEventConsumerWorker.cs`, `src/audit/src/CodeForCoders.Audit.Infra.Messaging/Configuration/RabbitMqOptions.cs`, `src/audit/src/CodeForCoders.Audit.Domain/Entities/AuditRecord.cs`, `src/audit/src/CodeForCoders.Audit.Infra.Data/Configuration/AuditRecordConfiguration.cs`, `src/audit/src/CodeForCoders.Audit.Infra.Data/AuditDbContext.cs` (unicidade por tenant e `confirmationId`; migration via EF)
- **modificar:** `docker-compose.yml`, `docker-compose.coolify.yml` (permissão de publicação do BFF em `audit.events`, fila de complemento de `audit`)
- **modificar:** `src/admin-spa/src/testing/handlers.ts`
- **ref:** `asyncapi-contract.yaml` (`receberComplementoConfirmado`), `tasks/prd-trilha-auditoria/asyncapi-contract.yaml` (canal existente); `techspec.md#backend` (Publicação e consumo); `src/bff-admin/src/CodeForCoders.BffAdmin.Infra.Messaging/RabbitMqTopologyInitializer.cs`; `src/audit/src/CodeForCoders.Audit.Infra.Data/Configuration/AuditDatabasePermissions.sql`; documentação RabbitMQ 4.3 de `nack` via Context7; skills `dotnet` e `react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/bff-admin` | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-admin` | `dotnet build src/bff-admin/CodeForCoders.BffAdmin.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/bff-admin` | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.ArchitectureTests/CodeForCoders.BffAdmin.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/bff-admin` | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/CodeForCoders.BffAdmin.EndToEndTests.csproj` | exit 0 (aplicação parte com publicador e worker reais registrados) | `ci-dotnet.yml` Testes |
| `src/audit` | `dotnet format src/audit/CodeForCoders.Audit.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/audit` | `dotnet build src/audit/CodeForCoders.Audit.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/audit` | `dotnet test --project src/audit/tests/CodeForCoders.Audit.ArchitectureTests/CodeForCoders.Audit.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/audit` | `dotnet test --project src/audit/tests/CodeForCoders.Audit.IntegrationTests/CodeForCoders.Audit.IntegrationTests.csproj -- --filter-class CodeForCoders.Audit.IntegrationTests.AdministrativeActRecordingTests` | exit 0 (canal `auditoria.ato-praticado.v1` intacto) | Regressão do 1º PRD |
| Contratos | `npx --yes @asyncapi/cli@6.1.0 validate tasks/prd-consulta-trilha-auditoria/asyncapi-contract.yaml` | exit 0 | `contracts.md` § Validação |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run build` | exit 0 | `ci-react-ts.yml` |

Cobertura agregada ≥ 70%, `dotnet publish` e imagens ficam para a validação full.

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 18 testes, com PostgreSQL e RabbitMQ reais (Testcontainers).
- [ ] Broker suspenso após o `202`: outbox pendente; broker restaurado: um complemento em `audit`. Redelivery da mesma mensagem e republicação por lease expirado não criam segunda linha.
- [ ] Conteúdo divergente e original inválido vão à DLQ sem mutação; o original é byte a byte igual antes e depois; dois complementos fora de ordem aparecem ordenados.
- [ ] Smoke no Compose: em `http://localhost:8081/admin/auditoria/{recordId}`, o administrador confirma uma explicação e, sem recarregar, vê "Aguardando registro…" virar o complemento com seu nome e o momento; a lista mostra "Complementado".
