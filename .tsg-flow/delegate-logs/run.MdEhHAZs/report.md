# Implementer — task 4.0 (fix, tentativa 2/3)

Run: run.MdEhHAZs
Resultado: IMPLEMENTATION COMPLETE

## Correção do bloqueante B-01 (motivo com maxLength 1000)

- `src/identity/src/CodeForCoders.Identity.Application/UseCases/Accounts/CreateStaffInvitation/CreateStaffInvitationInputValidator.cs`:
  `Reason` `MaximumLength(2000)` → `MaximumLength(1000)`. A `ValidationException` resultante é mapeada pelo
  `GlobalExceptionHandler` para 400 `INVALID_REQUEST`, conforme contrato; o validator roda antes de qualquer gravação.
- `src/admin-spa/src/features/staff-access/api/staff-invitations.ts`: schema zod `reason` `.max(1000, 'O motivo deve ter no máximo 1000 caracteres.')`.
- BFF: `CreateStaffInvitationRequestV1` não valida o motivo; só repassa para Identity. Nada a alterar.

## Testes adicionados

- `src/identity/tests/CodeForCoders.Identity.IntegrationTests/StaffInvitationIssuingTests.cs`:
  `StaffInvitation_RejectsAReasonLongerThanTheContractLimitWithoutWrites` — motivo com 1001 caracteres → `ValidationException`
  (400) em `Reason`, sem convite, outbox (pedido/ato) nem registro de idempotência.
- `src/admin-spa/src/app/routes/staff-access-route.test.tsx`: `refuses a reason longer than 1000 characters without sending the invitation`
  — mostra a mensagem de limite e não envia nenhum POST `/api/v1/staff-invitations`.

Recomendações opcionais 1–4 da revisão: não tratadas (fora do bloqueante; escopo não ampliado).

## Gate (comando do frontmatter, sem alteração, via `rtk proxy`)

Exit 0. Identity 8/8 (mín. 7) · Notificação 3/3 · BFF 3/3 · SPA 5 passaram (7 ignorados pelo filtro). Total 19 ≥ 14 (`gate_expect` atendido).

## Verificações do projeto e suítes completas (cada uma em comando separado)

| Check | Exit | Resultado |
|---|---|---|
| identity format --verify-no-changes | 0 | |
| identity build | 0 | 0 warnings |
| identity ArchitectureTests | 0 | 8 |
| identity UnitTests | 0 | 15 |
| identity IntegrationTests (completa) | 0 | 46 |
| notification format | 0 | |
| notification build | 0 | 0 warnings |
| notification UnitTests | 0 | 20 |
| notification IntegrationTests (completa) | 0 | 22 |
| bff-admin format | 0 | |
| bff-admin build | 0 | 0 warnings |
| bff-admin ArchitectureTests | 0 | 8 |
| bff-admin UnitTests | 0 | 2 |
| bff-admin EndToEndTests (completa) | 0 | 14 |
| admin-spa lint / typecheck / build | 0 / 0 / 0 | |
| admin-spa `npm run test` (completa) | 0 | 12 |

## Limitações

- Smoke via Docker Compose não executado (proibido pelo context.txt); fica para a validação full.
- Sem mudança de modelo EF nesta correção (nenhuma migration).
