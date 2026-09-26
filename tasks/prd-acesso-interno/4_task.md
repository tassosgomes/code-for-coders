---
status: in_progress
task_kind: vertical
blocked_by: ["3.0"]
gate: 'dotnet test --project src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj -- --filter-class CodeForCoders.Identity.IntegrationTests.StaffInvitationIssuingTests --minimum-expected-tests 7 && dotnet test --project src/notification/tests/CodeForCoders.Notification.IntegrationTests/CodeForCoders.Notification.IntegrationTests.csproj -- --filter-class CodeForCoders.Notification.IntegrationTests.StaffInvitationEmailTests --minimum-expected-tests 3 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/CodeForCoders.BffAdmin.EndToEndTests.csproj -- --filter-class CodeForCoders.BffAdmin.EndToEndTests.StaffInvitationIssuingTests --minimum-expected-tests 3 && npm --prefix src/admin-spa run test -- -t StaffInvitationIssuing'
gate_expect: 'Pelo menos 14 testes passam: 7 Identity, 3 Notificação, 3 BFF, 1 SPA'
---

# 4.0 Administrador convida e o convidado recebe o e-mail

**Fatia:** V-03 · **Cobre:** RF-03, RF-04, RF-12 (convites pendentes), RF-14, RN-01, RN-13a, RN-14, RN-15, RN-19, RN-21, RN-26, RN-27, RN-28, RN-A05, RN-A08, RN-A14, US-01, US-03 · **Spec:** `techspec.md#v-03-administrador-convida-e-o-convidado-recebe-o-e-mail` · **ADR:** [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md)

## Comportamento

**Identity** (`createStaffInvitationInternal`, `listPendingStaffInvitationsInternal`), ator resolvido por
`X-Staff-Session` e exigindo `acesso.gerir`:

- Normaliza o e-mail (RN-01). Conta interna com o e-mail → 422 `EMAIL_BELONGS_TO_STAFF`; conta de aluno
  → 422 `EMAIL_BELONGS_TO_STUDENT`; motivo vazio ou só espaços → 422 `REASON_REQUIRED`; sem
  `acesso.gerir` → 403 `PERMISSION_DENIED` e nada gravado.
- Convite pendente para o mesmo e-mail passa a **substituído** e o novo devolve
  `supersededInvitationId` (DP-04). Índice único parcial garante um pendente por (tenant, e-mail).
- Num commit: convite (hash do segredo, validade `StaffInvitation:LifetimeHours`, padrão 168), pedido
  `convite-interno` com `dados.papel` e `dados.link` (`…/admin/convite?token=…`) e ato
  `convite-interno-emitido` (autor = conta do administrador, alvo = convite, `complemento.papel`,
  motivo) — ambos protegidos no outbox. O ato sai pelo exchange da Auditoria (`RabbitMq:AuditExchange`),
  o pedido pelo de Notificação. Reentrega repete `fatoId`/`pedidoId`.
- Lista de pendentes: só do tenant, mais recentes primeiro, paginada.

**Notificação 1.1.0:** aceita finalidade/modelo `convite-interno` com `papel` e `link` (sem `nome`),
renderiza o modelo de convite com papel, link e validade (`ValidityHoursByPurpose["convite-interno"]`
= 168, obrigatória na partida), trata como transacional. `convite-interno` sem `papel` → recusado como
dado faltante. `confirmacao-de-conta` e `recuperacao-de-senha` sem `nome` continuam recusados; os
exemplos da 1.0.0 continuam aceitos.

**BFF/SPA:** área "Acessos" (só `acesso.gerir`) com convites pendentes e ação *Convidar* (e-mail, um
papel, motivo); mensagens distintas para os três 422; professor que abre a rota recebe "sem
permissão" (403 na borda, antes de chamar Identity).

## Fora do escopo desta task

Aceite (5.0); lista de atores internos e ações de papel (6.0). Consulta da trilha pelo backoffice é do
segundo PRD de CAP-030.

## Decisões fechadas

- Contrato Notificação 1.1.0 e ordem de implantação (Notificação antes do primeiro convite): C-01 e "Evolução e compatibilidade" em `contracts.md`.
- Payload do ato protegido no outbox: techspec.md, Decisões Técnicas 4.
- Ator por `X-Staff-Session`: C-08.

## Modificar / Referenciar

- **modificar:** `src/identity/src/CodeForCoders.Identity.Application/Common/OutboxDestinationOptions.cs` (exchange da Auditoria), `src/identity/src/CodeForCoders.Identity.Infra.Data/IdentityDbContext.cs` (convite; migration pelo EF), `src/identity/src/CodeForCoders.Identity.Infra.Data/DependencyInjection.cs`, `src/identity/src/CodeForCoders.Identity.Api/Extensions/EndpointExtensions.cs`, `src/identity/src/CodeForCoders.Identity.Api/appsettings.json` (`StaffInvitation`, `RabbitMq:AuditExchange`)
- **modificar:** `src/notification/src/CodeForCoders.Notification.Application/Common/NotificationPurposes.cs`, `src/notification/src/CodeForCoders.Notification.Application/Common/NotificationSendRequestRules.cs`, `src/notification/src/CodeForCoders.Notification.Application/Services/MessageTemplateRenderer.cs`, `src/notification/src/CodeForCoders.Notification.Application/Services/TransactionalConsentService.cs`, `src/notification/src/CodeForCoders.Notification.Contracts/TransactionalNotificationV1.cs`, `src/notification/src/CodeForCoders.Notification.Infra.Data/DependencyInjection.cs`, `src/notification/src/CodeForCoders.Notification.Infra.Data/Configuration/EmailOptions.cs`, `src/notification/src/CodeForCoders.Notification.Api/appsettings.json`
- **modificar:** `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/EndpointExtensions.cs`; `src/admin-spa/src/app/router.tsx`, `src/admin-spa/src/config/paths.ts`, `src/admin-spa/src/testing/handlers.ts`; `docker-compose.yml` (URL de aceite, exchange da Auditoria)
- **ref:** `asyncapi-contract.yaml`, `asyncapi-contract-notification.yaml`, `../prd-trilha-auditoria/asyncapi-contract.yaml` (schema do receptor), `src/audit/src/CodeForCoders.Audit.Api/appsettings.json`, `src/identity/src/CodeForCoders.Identity.Infra.Data/Outbox/OutboxMessageWriter.cs`, skills `dotnet`/`react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/identity` | `dotnet format src/identity/CodeForCoders.Identity.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` passo Lint |
| `src/identity` | `dotnet build src/identity/CodeForCoders.Identity.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` (build no test) |
| `src/identity` | `dotnet test --project src/identity/tests/CodeForCoders.Identity.ArchitectureTests/CodeForCoders.Identity.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` passo Testes |
| `src/identity` | `dotnet test --project src/identity/tests/CodeForCoders.Identity.UnitTests/CodeForCoders.Identity.UnitTests.csproj` | exit 0 | `ci-dotnet.yml` passo Testes |
| `src/notification` | `dotnet format src/notification/CodeForCoders.Notification.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` passo Lint |
| `src/notification` | `dotnet build src/notification/CodeForCoders.Notification.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/notification` | `dotnet test --project src/notification/tests/CodeForCoders.Notification.UnitTests/CodeForCoders.Notification.UnitTests.csproj` | exit 0 | `ci-dotnet.yml` passo Testes |
| `src/bff-admin` | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` passo Lint |
| `src/bff-admin` | `dotnet build src/bff-admin/CodeForCoders.BffAdmin.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/bff-admin` | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.ArchitectureTests/CodeForCoders.BffAdmin.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` passo Testes |
| `src/bff-admin` | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.UnitTests/CodeForCoders.BffAdmin.UnitTests.csproj` | exit 0 | `ci-dotnet.yml` passo Testes |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run build` | exit 0 | `ci-react-ts.yml` |

Cobertura agregada ≥ 70% por componente, `dotnet publish` e build de imagem ficam para a validação full.

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 14 testes.
- [ ] Payload do ato gravado valida contra `AtoPraticadoPayload` da Auditoria 1.0.1 e contra `AtoPraticadoIdentidade`.
- [ ] Novo convite ao mesmo e-mail → anterior substituído, dois atos `convite-interno-emitido`, dois pedidos.
- [ ] Os três 422 e o 403 não gravam convite, pedido nem ato.
- [ ] Smoke: convite → e-mail no smtp4dev com papel, validade e link `http://localhost:8081/admin/convite?token=…` → registro **conforme** na trilha.
