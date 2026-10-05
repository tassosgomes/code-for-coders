---
status: pending
task_kind: vertical
blocked_by: ["2.0"]
gate: 'dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.OrderCreationTests --minimum-expected-tests 12 && dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.PurchaseSummaryTests --minimum-expected-tests 5 && dotnet test --project src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj -- --filter-class CodeForCoders.Identity.IntegrationTests.CommerceAudienceTests --minimum-expected-tests 2 && dotnet test --project src/bff-student/tests/CodeForCoders.BffStudent.IntegrationTests/CodeForCoders.BffStudent.IntegrationTests.csproj -- --filter-class CodeForCoders.BffStudent.IntegrationTests.OrderProxyTests --minimum-expected-tests 5 && npm --prefix src/student-spa run test -- student-purchase-summary'
gate_expect: "Pelo menos 24 testes .NET passam (12 e 5 de integração de commerce, 2 de identity, 5 do BFF) e os testes do student-spa com o fragmento student-purchase-summary passam"
---

# 4.0 O aluno escolhe uma opção e ganha um pedido com preço e vigência congelados

**Fatia:** V-01 · **Cobre:** RF-01, RF-02, RF-03; RN-V01, RN-V02, RN-V03, RN-V04, RN-V06, RN-V11; RN-O05, RN-O09, RN-O10, RN-O12; US-01, US-06, US-07; DP-06, DP-08; C-03, C-08, C-11, C-12 · **Spec:** `techspec.md` § V-01, § Bloco Backend (Vendas), § Bloco Frontend · **ADR:** [ADR-0016](../../docs/adr/0016-jwt-de-aluno-em-commerce.md)

## Comportamento

**Identity e BFF.** O JWT de aluno passa a ter a audiência `commerce` com `scope: orders:use` e sem `email` (configuração nos três composes; teste em Identity). O `bff-student` mapeia `/api/v1/offers` e `/api/v1/orders` para a audiência `commerce` e expõe `getPurchaseSummary` e `createOrder` (`api-contract.yaml`), repassando o JWT; os erros seguem o contrato do BFF (401 `SESSION_REQUIRED`, 403 `CSRF_INVALID`, 502/504).

**`commerce` — política de aluno.** Rotas de aluno exigem `scope orders:use` **e ausência de `permissions`** (ADR-0016); token de ator → 403 `SCOPE_DENIED`, inclusive por chamada direta. O comprador é sempre o `sub`.

**`getPurchaseSummaryInternal`.** Lê a oferta publicada pela porta de leitura do Catálogo e o acesso existente pela porta de leitura de Matrícula (nunca tabelas de outro módulo, D-02); devolve curso, opção, preço, moeda, vigência, `pendingOrderId` (pedido do aluno aguardando pagamento **nesta** oferta) e `existingAccess` (concessão ativa e dentro da vigência que termina por último; vitalícia prevalece). Matrícula falhando → `existingAccessChecked: false`, `existingAccess: null`, 200. Oferta não publicada, inexistente ou de outra escola → 404 `OFFER_NOT_AVAILABLE`.

**`createOrderInternal`.** Recibo de idempotência por (escola, aluno, hash da chave) com janela de 24 h; lê a oferta vigente e **congela** curso e título, oferta e nome, preço, moeda e vigência; número sequencial por escola com 6 dígitos; situação `awaiting-payment`; grava `vendas.pedido-criado.v1` no outbox de `sales` na mesma transação. Pendente do mesmo aluno na mesma oferta → 200 com ele, sem fato (índice único parcial; criação concorrente perde no índice e devolve o existente). Pedido pendente em outra oferta do mesmo curso não impede. Mesma chave, corpo diferente → 422 `IDEMPOTENCY_KEY_REUSED`. `getStudentOrderInternal` devolve o pedido ao dono; de outro aluno → 404 `ORDER_NOT_FOUND`.

**Clique anônimo depreciado** (C-08): `registerPurchaseIntentInternal` responde 202 `available` e não grava contagem.

**`student-spa`** (telas de 2.0): *Comprar* leva a `/comprar/:offerId`; resumo com aviso de acesso existente e de ida ao pagamento; confirmar → `createOrder` com `Idempotency-Key` por confirmação → `/pedidos/:orderId` mostrando o pedido aguardando pagamento (sem botão de pagar ainda); `pendingOrderId` leva direto ao pedido. Visitante: 401 → guarda a compra pendente no `localStorage` (24 h, só `offerId` e `courseId`) e vai ao login com `returnTo`; o login sem `returnTo` consulta a compra pendente e leva ao resumo; a compra pendente é apagada ao abrir o resumo ou ao vencer (D-10).

Casos que os testes provam: oferta R$ 497/12 meses → pedido com R$ 497/12 meses; oferta alterada para R$ 597/6 meses depois → pedido inalterado; despublicada depois → pedido inalterado; despublicada antes → 404 e nenhum pedido; duas criações simultâneas na mesma oferta → um pedido; outra oferta do mesmo curso → segundo pedido; replay da mesma chave → mesmo pedido e mesmo status; chave reusada com outro corpo → 422; token de ator → 403; pedido de outro aluno → 404; outra escola → 404; número sequencial; fato `pedido-criado` conforme o AsyncAPI (`AssertSends` com o recorte do PRD); resumo com cortesia até 30/11 → aviso; concessão vencida → sem aviso; Matrícula fora → resumo sem aviso; Identity emite token `commerce` com `orders:use` e sem `email`. No SPA: visitante → login → resumo; cadastro + confirmação em outra aba → resumo pela compra pendente; resumo com aviso; confirmar → página do pedido; pendente existente → pedido.

## Fora do escopo desta task

- Ir ao pagamento, `billing` e concessão → 5.0.
- *Meus pedidos* → 9.0; desistência → 7.0.

## Decisões fechadas

- Vendas lê Catálogo e Matrícula pelas portas dos módulos (D-02); unicidade no banco (D-03).
- Preço e vigência nunca vêm do cliente (C-11).
- Compra pendente de login no `localStorage`, sem dado pessoal (D-10).

## Modificar / Referenciar

- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Infra.Data/CommerceDbContext.cs` (entidades de Vendas, filtro de tenant, migration pelo EF); `src/commerce/src/CodeForCoders.Commerce.Api/Security/FinanceAreaJwtBearerOptionsSetup.cs` (401/403 com o `code` do contrato nas rotas de aluno); `src/commerce/src/CodeForCoders.Commerce.Application/UseCases/Showcase/RegisterPurchaseIntent/RegisterPurchaseIntent.cs`; `src/commerce/src/CodeForCoders.Commerce.Api/Extensions/EndpointExtensions.cs`; `src/commerce/tests/CodeForCoders.Commerce.ArchitectureTests/ModuleSchemaConventionTest.cs` (Vendas não lê tabela de outro módulo)
- **modificar:** `src/bff-student/src/CodeForCoders.BffStudent.Application/Common/BffSecurityOptions.cs` (`RouteAudiences`); `src/bff-student/src/CodeForCoders.BffStudent.Api/Extensions/EndpointExtensions.cs`
- **modificar:** `src/student-spa/src/config/paths.ts`, `src/student-spa/src/app/router.tsx`, `src/student-spa/src/features/student-showcase/components/purchase-intent-button.tsx`, `src/student-spa/src/features/student-showcase/api/register-purchase-intent.ts` (fora de uso), `src/student-spa/src/features/student-session/components/student-login-screen.tsx`, `src/student-spa/src/app/student-purchase-intent.test.tsx`
- **modificar:** `docker-compose.yml`, `docker-compose.remote.yml`, `docker-compose.coolify.yml` (`StudentSessionTokens__AudienceScopes__commerce: orders:use`)
- **ref:** `src/commerce/src/CodeForCoders.Commerce.Application/UseCases/CourtesyGrants/GrantCourtesy/GrantCourtesy.cs` (recibo, bloqueio, fato na transação); `src/learning/src/CodeForCoders.Learning.Api/Extensions/AuthenticationConfigurationExtensions.cs` (política de aluno); `src/bff-student/src/CodeForCoders.BffStudent.Api/Endpoints/MyCoursesEndpoints.cs`; `src/student-spa/src/app/student-login-return.test.tsx`; `src/contract-testing/AsyncApiContract.cs`; `api-contract.yaml`, `api-contract-vitrine.yaml`, `internal-api-contract-commerce.yaml`, `internal-api-contract-identity-student.yaml`, `asyncapi-contract.yaml`; `docs/design/wireframes-compra.md`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/commerce` | `dotnet format src/commerce/CodeForCoders.Commerce.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.ArchitectureTests/CodeForCoders.Commerce.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.PurchaseIntentTests` | exit 0 (atualizado para `available` e nenhuma contagem) | `ci-dotnet.yml` Testes |
| `src/identity` | `dotnet format src/identity/CodeForCoders.Identity.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-student` | `dotnet format src/bff-student/CodeForCoders.BffStudent.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-student` | `dotnet test --project src/bff-student/tests/CodeForCoders.BffStudent.IntegrationTests/CodeForCoders.BffStudent.IntegrationTests.csproj -- --filter-class CodeForCoders.BffStudent.IntegrationTests.ShowcaseAnonymousRouteTests` | exit 0 (vitrine continua anônima) | `ci-dotnet.yml` Testes |
| `src/student-spa` | `npm --prefix src/student-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| Compose | `docker compose -f docker-compose.yml config -q`; idem com `-f docker-compose.remote.yml`; `docker compose -f docker-compose.coolify.yml config -q` | exit 0 cada | AGENTS.md |

Cobertura agregada ≥ 70%, `publish`, `vite build` e imagens ficam para a validação full.

## Pronto quando

- [ ] Gate focalizado passa (exit 0).
- [ ] `commerce` e o `bff-student` iniciam com os registros reais (política de aluno, rotas, portas de leitura) no ambiente de teste.
- [ ] Em `http://localhost:8082/student/cursos/{courseId}`, *Comprar* na opção de 12 meses → resumo com R$ 497 e "12 meses a partir da liberação" → confirmar → `/student/pedidos/{orderId}` com o número e "Aguardando pagamento".
- [ ] Visitante clica em *Comprar*, cria conta, confirma o e-mail e entra → chega ao resumo da mesma opção.
- [ ] Token de ator em `createOrderInternal` → 403, e nenhum pedido.
