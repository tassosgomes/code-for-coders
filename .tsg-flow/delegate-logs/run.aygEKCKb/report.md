# Implementer — task 4.0 (fix, tentativa 3/3)

Run: run.aygEKCKb

**Resultado: IMPLEMENTATION COMPLETE** — gate passed.

## Bloqueante corrigido
- **B5** (prd_review.md, run.QeuEMpDO): mutante que neutraliza a checagem de `StaffRoleCatalog.ManageAccess`
  em `StaffInvitationEndpoints.cs:136` sobrevivia à suíte do Identity.

## Arquivos alterados
- `src/identity/tests/CodeForCoders.Identity.EndToEndTests/StaffInvitationIssuingEndpointTests.cs` (novo)
  - `CreateStaffInvitationInternal_RejectsASessionWithoutManageAccessWithoutWrites`: API real (`IdentityApiFactory`,
    Postgres/Valkey via Testcontainers), asserção de serviço válida do `bff-admin` com `staff-invitations:write`,
    `X-Staff-Session` de conta interna com papel `professor` (sem `acesso.gerir`). Espera 403 problem+json
    `PERMISSION_DENIED`, nenhum convite para o e-mail, e contagens de outbox e idempotência inalteradas.
  - `CreateStaffInvitationInternal_CreatesTheInvitationForASessionWithManageAccess`: controle positivo
    (administrador → 201, 1 convite, +2 mensagens na outbox), provando que o setup do teste não recusa por outro motivo.
- Nenhum código de produção, contrato ou techspec alterado.

## Prova do mutante
- Mutação temporária na linha 136: `if (actor.Session!.Session.Permissions.Contains("__mutant__", ...))`
  (a forma `false && ...` não compila por warnings-as-errors CS8602).
- Com a mutação: teste de recusa **falhou** (`Expected: Forbidden / Actual: Created`), exit 2 do runner (1 failed, 1 succeeded).
- Mutação revertida (arquivo restaurado; `git diff` vazio). Sem ela: 2/2 passam.

## Gate (comando inalterado, via `rtk proxy`)
exit 0 — Identity 8 (StaffInvitationIssuingTests), Notificação 3, BFF 3, SPA 5 (≥14 esperado).

## Checks (cada um exit 0)
| Check | Resultado |
|---|---|
| identity format --verify-no-changes | 0 |
| identity build | 0, 0 warnings |
| identity ArchitectureTests | 0 (8) |
| identity UnitTests | 0 (15) |
| identity IntegrationTests (suíte completa) | 0 (80) |
| identity EndToEndTests (suíte completa) | 0 (8, inclui os 2 novos) |
| notification format / build / UnitTests | 0 / 0 (0 warnings) / 0 (20) |
| bff-admin format / build / Arch / Unit | 0 / 0 (0 warnings) / 0 (8) / 0 (2) |
| admin-spa lint / typecheck / build | 0 / 0 / 0 (aviso de chunk size do Vite, pré-existente) |

## Limitações
- Smoke local (Docker Compose) não executado, conforme decisão do orquestrador.
- Suítes completas de Notification IntegrationTests, BFF-admin EndToEndTests e `admin-spa test` não reexecutadas
  por inteiro: esses componentes não foram tocados nesta correção (seus filtros do gate passaram).
