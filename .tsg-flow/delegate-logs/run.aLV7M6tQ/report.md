# Implementer — task 2.0 (fix, tentativa 2/3)

Run: run.aLV7M6tQ

**Resultado: IMPLEMENTATION COMPLETE** — gate `passed`.

## Bloqueio tratado
B4 (prd_review.md, run.QeuEMpDO): mutante que remove `|| token.ConsumedOn is not null` em
`ResetStaffPassword.cs:48-51` sobrevivia à suíte do Identity.

## Arquivos alterados
- `src/identity/tests/CodeForCoders.Identity.IntegrationTests/StaffPasswordResetTests.cs` — novo teste
  `ResetStaffPassword_RejectsAnAlreadyUsedLinkWithAnotherIdempotencyKey`: provisiona, redefine com a
  chave `staff-reset-reuse-1` (204), reusa o mesmo link com **outra** Idempotency-Key e outra senha →
  `StaffPasswordRecoveryException` `RESET_TOKEN_INVALID` (422); a credencial continua verificando a
  primeira senha e não a segunda.
- Nenhum código de produção alterado (`git status` limpo em `src/identity/src`).

## Prova de discriminação do mutante
- Mutação aplicada temporariamente (remoção de `|| token.ConsumedOn is not null`), classe
  `StaffPasswordResetTests`: exit 2, total 4, failed 1, succeeded 3 — falha exatamente o novo teste com
  `Assert.Throws() Failure: No exception was thrown`.
- Mutação revertida (arquivo restaurado da cópia original; diff vazio): classe passa 4/4, exit 0.
- Observação: a primeira execução mutada falhou os 4 testes por instabilidade de inicialização do
  Testcontainers (~1 min até o container subir); foi descartada e repetida, com o resultado acima.

## Gate (comando declarado, sem alteração, via `rtk proxy bash -c`)
Exit 0. Identity FirstAdministratorProvisioningTests 4/4; Identity StaffPasswordResetTests 4/4
(mínimo 3); BFF-admin StaffPasswordResetTests 2/2; admin-spa `-t StaffPasswordReset` 1 passed.
Total 11 ≥ 10 exigidos (`gate_expect` atendido).

## Verificações do projeto (componente tocado: `src/identity`)
| Comando | Exit | Evidência |
|---|---|---|
| `dotnet format src/identity/CodeForCoders.Identity.slnx --verify-no-changes` | 0 | |
| `dotnet build src/identity/CodeForCoders.Identity.slnx` | 0 | 0 Warning(s) |
| ArchitectureTests do Identity | 0 | 8/8 |
| UnitTests do Identity | 0 | 15/15 |
| IntegrationTests do Identity (suíte completa) | 0 | 80/80 |

`src/bff-admin` e `src/admin-spa` não foram tocados nesta correção; seus checks não foram reexecutados
(o gate exercitou os testes deles com sucesso).

## Limitações
- Smoke local (Docker Compose) não executado, por decisão do orquestrador.
