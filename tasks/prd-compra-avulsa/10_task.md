---
status: pending
task_kind: vertical
blocked_by: ["9.0"]
gate: 'dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.FinanceOrdersTests --minimum-expected-tests 7 && dotnet test --project src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj -- --filter-class CodeForCoders.Identity.IntegrationTests.StudentAccountResolutionTests --minimum-expected-tests 4 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj -- --filter-class CodeForCoders.BffAdmin.IntegrationTests.FinanceOrdersProxyTests --minimum-expected-tests 4 && npm --prefix src/admin-spa run test -- finance-orders'
gate_expect: "Pelo menos 15 testes .NET passam (7 de commerce, 4 de identity, 4 do bff-admin) e os testes do admin-spa com o fragmento finance-orders passam"
---

# 10.0 O financeiro consulta os pedidos da escola no backoffice

**Fatia:** V-07 · **Cobre:** RF-11; RN-16, RN-V12; US-08; DP-09; C-09 · **Spec:** `techspec.md` § V-07 · **ADR:** [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md)

## Comportamento

**`commerce`.** `listFinanceOrdersInternal` e `getFinanceOrderInternal` com o JWT do ator (`aud commerce`) e `financeiro.ler`: pedidos da escola, mais recentes primeiro, com filtros combináveis por `status`, `courseId`, `studentId` e período de criação (`createdFrom`/`createdTo`, datas inclusivas no fuso da escola; invertido → 400); detalhe com vigência, momentos, referência do gateway, valor confirmado e concessão. Sem `financeiro.ler` (professor, suporte, administrador sem o papel) → 403 `PERMISSION_DENIED`, inclusive por chamada direta; token de aluno → 403; outra escola → nunca aparece / 404.

**`identity`.** `resolveStudentAccountsInternal` (`POST /internal/v1/student-account-resolutions`) para o `bff-admin`, escopo novo `student-account:resolve`, `X-Staff-Session` com `financeiro.ler` vigente: até 50 ids, únicos; devolve nome, e-mail e situação das contas de aluno do tenant; desconhecido, interno ou de outro tenant → omitido; desativada → `disabled`; `no-store`.

**`bff-admin`.** `listFinanceOrders` e `getFinanceOrder` (`api-contract-admin.yaml`): verifica `financeiro.ler` na sessão, obtém o JWT `aud commerce`, busca a página em `commerce` e resolve os alunos da página **numa** chamada a Identity; Identity fora → 502 `IDENTITY_UNAVAILABLE` (nunca lista sem aluno); nome e e-mail só na resposta, nunca em URL, log ou cache. O filtro por e-mail é o `lookupStudentAccount` existente (C-09) seguido de `studentId`.

**`admin-spa`** (telas de 2.0): `/financeiro` deixa de mostrar "Área reservada" e passa a ter a lista com filtros (situação, curso, período, e-mail do aluno) e paginação; `/financeiro/pedidos/:orderId` com o detalhe; estados vazio, sem permissão e indisponível.

Casos que os testes provam: filtros combinados; período invertido → 400; detalhe com referência e concessão; professor, suporte e administrador → 403 em `commerce` e no BFF; papel revogado → recusado na sessão aberta; outra escola nunca aparece; Identity: lote com desconhecido e interno omitidos, desativada com `disabled`, 51 ids → 400, sessão sem `financeiro.ler` → 403; BFF: página com nome e e-mail, Identity fora → 502. No SPA: lista, filtro por e-mail (conta encontrada e "nenhuma conta de aluno com este e-mail"), detalhe, sem permissão.

## Fora do escopo desta task

- Ações sobre pedido (cancelar, estornar, reenviar comprovante, conceder manualmente) — não-objetivos do PRD.

## Decisões fechadas

- Permissão existente `financeiro.ler`, só leitura (DP-09).
- Aluno resolvido por página em Identity, sem réplica em `commerce`; filtro por e-mail pela busca existente, que exige `cortesia.conceder` (C-09, aceito).

## Modificar / Referenciar

- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Api/Extensions/EndpointExtensions.cs`; `src/identity/src/CodeForCoders.Identity.Api/Endpoints/StudentAccountLookupEndpoints.cs` (ou endpoint irmão no mesmo padrão); `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/FinanceAreaEndpoints.cs`; `src/admin-spa/src/features/finance-area/components/finance-area-screen.tsx`, `src/admin-spa/src/config/paths.ts`, `src/admin-spa/src/app/app-routes.tsx`, `src/admin-spa/src/app/routes/finance-area-route.test.tsx`; `docker-compose.yml`, `docker-compose.remote.yml`, `docker-compose.coolify.yml` (escopo `student-account:resolve` no emissor `bff-admin`)
- **ref:** `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Clients/StudentAccountIdentityClient.cs`, `Clients/CommerceFinanceAreaClient.cs`; `src/identity/tests/CodeForCoders.Identity.IntegrationTests/StudentAccountLookupFixture.cs`; `internal-api-contract-commerce.yaml`, `internal-api-contract-identity.yaml`, `api-contract-admin.yaml`; `docs/design/wireframes-compra.md`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/commerce` | `dotnet format src/commerce/CodeForCoders.Commerce.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/identity` | `dotnet format src/identity/CodeForCoders.Identity.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/identity` | `dotnet test --project src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj -- --filter-class CodeForCoders.Identity.IntegrationTests.StudentAccountLookupTests` | exit 0 | `ci-dotnet.yml` Testes |
| `src/bff-admin` | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint`; `npm --prefix src/admin-spa run typecheck` | exit 0 cada | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run test -- finance-area-route` | exit 0 (abertura da área com e sem permissão) | `ci-react-ts.yml` |
| Compose | `docker compose ... config -q` nos três arranjos | exit 0 cada | AGENTS.md |

## Pronto quando

- [ ] Gate focalizado passa (exit 0).
- [ ] Em `http://localhost:8081/admin/financeiro`, o financeiro vê os pedidos com nome e e-mail, filtra por "Aguardando pagamento" e por um curso, encontra os pedidos de um aluno pelo e-mail e abre o detalhe com a referência do gateway e a concessão.
- [ ] Um professor que abre `/admin/financeiro` vê "Esta área não é do seu papel", e `listFinanceOrdersInternal` chamado com o token dele responde 403.
