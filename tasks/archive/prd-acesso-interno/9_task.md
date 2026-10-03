---
status: done
task_kind: vertical
blocked_by: ["3.0"]
gate: 'dotnet test --project src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj -- --filter-class CodeForCoders.Identity.IntegrationTests.StaffPasswordRecoveryRequestTests --minimum-expected-tests 3 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/CodeForCoders.BffAdmin.EndToEndTests.csproj -- --filter-class CodeForCoders.BffAdmin.EndToEndTests.StaffPasswordRecoveryRequestTests --minimum-expected-tests 2 && npm --prefix src/admin-spa run test -- -t StaffPasswordRecovery'
gate_expect: 'Pelo menos 6 testes passam: 3 Identity, 2 BFF, 1 SPA'
---

# 9.0 Ator interno recupera a senha sozinho

**Fatia:** V-08 · **Cobre:** RF-11, RN-03, RN-04, RN-07 · **Spec:** `techspec.md#v-08-ator-interno-recupera-a-senha-sozinho` · **ADR:** —

## Comportamento

- `/admin/recuperar-senha` → `requestStaffPasswordReset` → `requestStaffPasswordResetInternal`:
  202 sem corpo **idêntico** para conta interna, conta de aluno e e-mail inexistente (RN-04). Só a
  conta interna grava segredo e pedido `recuperacao-de-senha` com link do backoffice; conta de aluno e
  e-mail inexistente não publicam nada.
- O link leva à redefinição já entregue em 2.0; redefinir encerra as sessões internas anteriores
  (RN-07). A recuperação do **aluno** continua sem atender conta interna.
- A página de entrada oferece o link "Esqueci a senha".

## Fora do escopo desta task

Política de sessão própria do backoffice (QT-01 do PRD).

## Decisões fechadas

- Reuso do modelo `recuperacao-de-senha` com link do backoffice: C-02.

## Modificar / Referenciar

- **modificar:** `src/identity/src/CodeForCoders.Identity.Api/Extensions/EndpointExtensions.cs`; `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/EndpointExtensions.cs`; `src/admin-spa/src/app/router.tsx`, `src/admin-spa/src/config/paths.ts`, `src/admin-spa/src/testing/handlers.ts`
- **ref:** `src/identity/src/CodeForCoders.Identity.Application/UseCases/Accounts/RequestStudentPasswordReset`, `api-contract.yaml`, skills `dotnet`/`react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/identity` | `dotnet format src/identity/CodeForCoders.Identity.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` passo Lint |
| `src/identity` | `dotnet build src/identity/CodeForCoders.Identity.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` (build no test) |
| `src/identity` | `dotnet test --project src/identity/tests/CodeForCoders.Identity.ArchitectureTests/CodeForCoders.Identity.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` passo Testes |
| `src/identity` | `dotnet test --project src/identity/tests/CodeForCoders.Identity.UnitTests/CodeForCoders.Identity.UnitTests.csproj` | exit 0 | `ci-dotnet.yml` passo Testes |
| `src/bff-admin` | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` passo Lint |
| `src/bff-admin` | `dotnet build src/bff-admin/CodeForCoders.BffAdmin.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/bff-admin` | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.ArchitectureTests/CodeForCoders.BffAdmin.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` passo Testes |
| `src/bff-admin` | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.UnitTests/CodeForCoders.BffAdmin.UnitTests.csproj` | exit 0 | `ci-dotnet.yml` passo Testes |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run build` | exit 0 | `ci-react-ts.yml` |

Cobertura agregada ≥ 70% por componente, `dotnet publish` e build de imagem ficam para a validação full.

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 6 testes.
- [ ] Três respostas idênticas; um único pedido publicado, para a conta interna.
- [ ] Após redefinir, sessão anterior do ator → 401 na próxima ação.
- [ ] Smoke: "Esqueci a senha" → smtp4dev → redefinir → entrar.
