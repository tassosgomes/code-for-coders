---
status: done
task_kind: vertical
blocked_by: ["4.0"]
gate: 'dotnet test --project src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj -- --filter-class CodeForCoders.Identity.IntegrationTests.StaffInvitationAcceptanceTests --minimum-expected-tests 7 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/CodeForCoders.BffAdmin.EndToEndTests.csproj -- --filter-class CodeForCoders.BffAdmin.EndToEndTests.StaffInvitationAcceptanceTests --minimum-expected-tests 3 && npm --prefix src/admin-spa run test -- -t StaffInvitationAcceptance'
gate_expect: 'Pelo menos 11 testes passam: 7 Identity, 3 BFF, 1 SPA'
---

# 5.0 Convidado aceita o convite e entra no backoffice

**Fatia:** V-04 · **Cobre:** RF-05, RF-14, RN-01, RN-06, RN-15, RN-19, RN-A05, US-02 · **Spec:** `techspec.md#v-04-convidado-aceita-o-convite-e-entra-no-backoffice` · **ADR:** —

## Comportamento

- `/admin/convite?token=…`: o SPA lê o token, remove-o do histórico e chama `lookupStaffInvitation`,
  que mostra o papel ofertado e a validade. Convite expirado, aceito, substituído ou inexistente → a
  mesma 422 `INVITATION_INVALID` e a página orienta pedir novo convite.
- Formulário nome + senha → `acceptStaffInvitation` → `acceptStaffInvitationInternal`. Num commit:
  conta `InternalActor` confirmada, credencial (política de CAP-001), atribuição do papel ofertado,
  convite aceito, sessão interna e ato `convite-interno-aceito` (autor = conta nova, alvo = convite,
  **sem motivo nem complemento**). **Nenhum** `papel-concedido`. O BFF grava Valkey, emite
  `staff_session` e o SPA leva ao início com as áreas do papel.
- Segundo uso do mesmo link → `INVITATION_INVALID`. Senha fraca → 422 `PASSWORD_POLICY_VIOLATION`,
  nada gravado. E-mail que ganhou conta entre emissão e aceite → 422 `INVITATION_EMAIL_UNAVAILABLE`;
  corrida resolvida pela unicidade do banco.

## Fora do escopo desta task

Concessões posteriores ao aceite (6.0).

## Decisões fechadas

- Aceite abre sessão: C-04. Aceite não gera `papel-concedido`: RF-05 do PRD.

## Modificar / Referenciar

- **modificar:** `src/identity/src/CodeForCoders.Identity.Api/Extensions/EndpointExtensions.cs`; `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/EndpointExtensions.cs`; `src/admin-spa/src/app/router.tsx`, `src/admin-spa/src/config/paths.ts`, `src/admin-spa/src/testing/handlers.ts`
- **ref:** `api-contract.yaml` (`lookupStaffInvitation`, `acceptStaffInvitation`), `internal-api-contract.yaml`, `asyncapi-contract.yaml` (exemplo `conviteInternoAceito`), skills `dotnet`/`react`

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

- [ ] Gate passa (exit 0) com pelo menos 11 testes.
- [ ] Expirado (relógio controlado), aceito e substituído respondem igual.
- [ ] Aceite grava exatamente um ato, do tipo `convite-interno-aceito`, sem `motivo`.
- [ ] Smoke: convite de 4.0 → aceite → início com as áreas do papel → registro conforme na trilha.
