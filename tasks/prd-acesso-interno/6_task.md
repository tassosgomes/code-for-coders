---
status: done
task_kind: vertical
blocked_by: ["5.0"]
gate: 'dotnet test --project src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj -- --filter-class CodeForCoders.Identity.IntegrationTests.StaffRoleGrantRevokeTests --minimum-expected-tests 8 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/CodeForCoders.BffAdmin.EndToEndTests.csproj -- --filter-class CodeForCoders.BffAdmin.EndToEndTests.StaffRoleGrantRevokeTests --minimum-expected-tests 4 && npm --prefix src/admin-spa run test -- -t StaffMembers'
gate_expect: 'Pelo menos 13 testes passam: 8 Identity, 4 BFF, 1 SPA'
---

# 6.0 Administrador concede e revoga papel, e a revogação desconecta na hora

**Fatia:** V-05 · **Cobre:** RF-06, RF-07, RF-08, RF-12 (atores internos), RF-14, RN-12, RN-13, RN-16, RN-17, RN-19, RN-20, RN-24, RN-A05, US-03, US-04, US-08 · **Spec:** `techspec.md#v-05-administrador-concede-e-revoga-papel-e-a-revogação-desconecta-na-hora` · **ADR:** [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md)

## Comportamento

Ator por `X-Staff-Session`, exigindo `acesso.gerir`:

- `listStaffMembers`: atores internos do tenant, inclusive sem papel, por nome, `isSelf` na linha do
  ator. Nunca de outro tenant.
- `grantStaffRole`: acrescenta o papel e grava `papel-concedido` (alvo = conta, `complemento.papel`,
  motivo) num commit. Papel já presente → 200 `changed: false`, nada gravado. Concessões simultâneas do
  mesmo papel: uma grava, a outra resolve `changed: false` pela unicidade; **um** ato.
- `revokeStaffRole`: remove o papel, **revoga todas as sessões internas do alvo** e grava
  `papel-revogado`, num commit; `sessionsEnded: true`. Papel ausente → `changed: false`, nada gravado.
  Revogar o último papel deixa a conta sem papel: na próxima entrada ela vê a orientação de conta sem
  acesso (RF-08).
- Autor = alvo → 422 `SELF_ROLE_CHANGE_FORBIDDEN`; alvo inexistente, de outro tenant ou de aluno → 404
  `STAFF_MEMBER_NOT_FOUND`; motivo vazio → 422 `REASON_REQUIRED`; sem permissão → 403.
- SPA "Acessos": lista com papéis; *Conceder* e *Revogar* pedem motivo e avisam "a pessoa será
  desconectada agora" na revogação; a linha `isSelf` não tem ações.

## Fora do escopo desta task

Troca de papel numa ação (7.0).

## Decisões fechadas

- Ação sem efeito → 200 `changed: false`: C-07. Revogação encerra sessões no mesmo commit: ADR-0005 (2).
- Último administrador protegido por RN-20, sem regra extra: techspec.md, Bloco Backend.

## Modificar / Referenciar

- **modificar:** `src/identity/src/CodeForCoders.Identity.Api/Extensions/EndpointExtensions.cs`; `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/EndpointExtensions.cs`; `src/admin-spa/src/app/router.tsx`, `src/admin-spa/src/config/paths.ts`, `src/admin-spa/src/testing/handlers.ts`
- **ref:** `api-contract.yaml`, `internal-api-contract.yaml`, `asyncapi-contract.yaml`, skills `dotnet`/`react`

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

- [ ] Gate passa (exit 0) com pelo menos 13 testes.
- [ ] Integração BFF + Identity: sessão de B aberta, A revoga um papel de B, próxima chamada de B → 401 `SESSION_REQUIRED`.
- [ ] Concessão concorrente do mesmo papel → um ato só.
- [ ] Ação sem efeito e ação sobre si não gravam ato.
- [ ] Smoke com dois navegadores: revogar desconecta o outro na próxima ação.
