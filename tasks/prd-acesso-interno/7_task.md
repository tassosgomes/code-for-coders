---
status: pending
task_kind: vertical
blocked_by: ["6.0"]
gate: 'dotnet test --project src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj -- --filter-class CodeForCoders.Identity.IntegrationTests.StaffRoleChangeTests --minimum-expected-tests 5 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/CodeForCoders.BffAdmin.EndToEndTests.csproj -- --filter-class CodeForCoders.BffAdmin.EndToEndTests.StaffRoleChangeTests --minimum-expected-tests 2 && npm --prefix src/admin-spa run test -- -t StaffRoleChange'
gate_expect: 'Pelo menos 8 testes passam: 5 Identity, 2 BFF, 1 SPA'
---

# 7.0 Administrador troca o papel numa ação só

**Fatia:** V-06 · **Cobre:** RF-09, RF-14, RN-17, RN-19, RN-20, US-05 · **Spec:** `techspec.md#v-06-administrador-troca-o-papel-numa-ação-só` · **ADR:** [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md)

## Comportamento

`changeStaffRole` → `changeStaffRoleInternal` (ator com `acesso.gerir`, diferente do alvo). Num commit:
remove `fromRole`, inclui `toRole`, revoga as sessões internas do alvo e grava `papel-revogado`
(`fromRole`) e `papel-concedido` (`toRole`) com o **mesmo** autor, motivo, `praticadoEm` e
`correlationId`, `fatoId` distintos, sem campo de correlação no payload.

- Falha em qualquer ponto antes do commit → conta com o papel anterior, nenhum ato no outbox.
- `toRole` já presente → equivale a revogar `fromRole`: um ato só.
- `fromRole` ausente → 422 `ROLE_NOT_HELD`; `fromRole` = `toRole` → 422; ação sobre si → 422
  `SELF_ROLE_CHANGE_FORBIDDEN`.
- SPA: ação *Trocar papel* na linha do ator, com motivo e aviso de desconexão.

## Fora do escopo desta task

Consulta da trilha (segundo PRD de CAP-030).

## Decisões fechadas

- Troca = revogar + conceder, dois atos: OD22, DP-01, C-03.

## Modificar / Referenciar

- **modificar:** `src/identity/src/CodeForCoders.Identity.Api/Extensions/EndpointExtensions.cs`; `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/EndpointExtensions.cs`; `src/admin-spa/src/testing/handlers.ts`
- **ref:** `asyncapi-contract.yaml` (exemplos `trocaDePapel*`), `api-contract.yaml`, skills `dotnet`/`react`

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

- [ ] Gate passa (exit 0) com pelo menos 8 testes.
- [ ] Falha forçada antes do commit → estado e outbox intactos.
- [ ] Troca bem-sucedida → dois atos com mesmo autor, motivo, `praticadoEm` e `correlationId`; ator desconectado.
- [ ] Smoke: troca de professor para financeiro → trilha com os dois registros conformes.
