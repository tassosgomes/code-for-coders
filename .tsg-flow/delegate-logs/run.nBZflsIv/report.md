# Implementer — task 7.0 (fix, tentativa 2/3)

Run: run.nBZflsIv
Resultado: **IMPLEMENTATION COMPLETE**

## Bloqueio B1 (run.U8XX8u3R)
Resolvido pelo dono: `ROLE_CHANGE_INVALID` (422, `fromRole` = `toRole`) formalizado em
`api-contract.yaml`/`internal-api-contract.yaml` 1.0.1, `contracts.md` 1.2 e techspec. Nenhum contrato
ou techspec foi editado nesta chamada. Conferido que as camadas usam exatamente código e status:
- Identity `StaffRoleActionExecutor.cs:220` → 422 `ROLE_CHANGE_INVALID`; `:265` → 422 `ROLE_NOT_HELD` (testes de integração cobrem ambos).
- BFF `StaffMemberIdentityClient.cs:122` repassa ambos como 422; `StaffMemberEndpoints.cs:254-255` mensagens.
- SPA `staff-members.ts:166-169` trata ambos os códigos.
Sem divergência; nenhum ajuste de código foi necessário.

## Recomendação 3 (feita)
- `src/admin-spa/src/app/routes/staff-access-route.test.tsx`: novo teste
  `StaffRoleChange › shows the role not held rejection from the server` (422 `ROLE_NOT_HELD` → alerta
  "A pessoa não tem mais o papel de origem selecionado.", sem status de sucesso).
- A validação local de papéis iguais já estava coberta por
  `StaffRoleChange › rejects the same source and destination role before submitting` (nenhuma requisição).
Recomendações 1–2 (opcionais) não aplicadas.

## Arquivos alterados nesta chamada
- `src/admin-spa/src/app/routes/staff-access-route.test.tsx`

## Gate (sem alteração, via `rtk proxy`)
| Parte | Exit | Testes |
|---|---|---|
| Identity StaffRoleChangeTests | 0 | 7/7 |
| BFF StaffRoleChangeTests | 0 | 2/2 |
| SPA `-t StaffRoleChange` | 0 | 3 passaram, 19 ignorados pelo filtro |
`gate_expect` (≥ 8: 5 Identity, 2 BFF, 1 SPA): atendido (12).

## Verificações do projeto
| Comando | Exit | Obs |
|---|---|---|
| identity format --verify-no-changes | 0 | |
| identity build | 0 | 0 warnings |
| identity ArchitectureTests | 0 | 8/8 |
| identity UnitTests | 0 | 15/15 |
| identity IntegrationTests (completa) | 0 | 73/73 |
| bff-admin format --verify-no-changes | 0 | |
| bff-admin build | 0 | 0 warnings |
| bff-admin ArchitectureTests | 0 | 8/8 |
| bff-admin UnitTests | 0 | 2/2 |
| bff-admin EndToEndTests (completa) | 0 | 27/27 |
| admin-spa lint / typecheck / build | 0 / 0 / 0 | |
| admin-spa test (completa) | 0 | 22/22 |

## Limitações
- Smoke com Docker Compose não executado (orientação do orquestrador); fica para a validação full.
- Notification IntegrationTests não rodada: componente não tocado pela task.
