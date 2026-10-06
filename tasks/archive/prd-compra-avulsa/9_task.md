---
status: done
task_kind: vertical
blocked_by: ["8.0"]
gate: 'dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.StudentOrderListTests --minimum-expected-tests 4 && dotnet test --project src/bff-student/tests/CodeForCoders.BffStudent.IntegrationTests/CodeForCoders.BffStudent.IntegrationTests.csproj -- --filter-class CodeForCoders.BffStudent.IntegrationTests.MyOrdersProxyTests --minimum-expected-tests 3 && npm --prefix src/student-spa run test -- my-orders'
gate_expect: "Pelo menos 7 testes .NET passam (4 de commerce, 3 do BFF) e os testes do student-spa com o fragmento my-orders passam"
---

# 9.0 O aluno vê os próprios pedidos

**Fatia:** V-06 · **Cobre:** RF-10; RN-V03, RN-V12; US-05 · **Spec:** `techspec.md` § V-06 · **ADR:** [ADR-0016](../../docs/adr/0016-jwt-de-aluno-em-commerce.md)

## Comportamento

`listMyOrders` (BFF) → `listStudentOrdersInternal`: pedidos do aluno do token na escola do token, do mais recente para o mais antigo, paginados (`_page`, `_size` até 50), com o que cada um congelou — inclusive de oferta alterada ou despublicada depois. Sem pedidos → `data: []`.

`student-spa` (`/pedidos`, telas de 2.0): lista com curso, opção, valor, data, meio e situação; pendente em destaque com o prazo e o caminho para a retomada (página do pedido); pago leva ao pedido e de lá ao curso; vazio com caminho para a vitrine (`/cursos`); link para *Meus pedidos* na navegação da conta.

Casos que os testes provam: três pedidos (pago, expirado, aguardando boleto) na ordem certa; paginação; pedido de outro aluno nunca aparece; outra escola nunca aparece; oferta despublicada → valores congelados; vazio; token de ator → 403. No SPA: lista com os três estados, pendente em destaque, vazio, erro com *Tentar de novo*.

## Fora do escopo desta task

- Detalhe e ações do pedido → já entregues em 4.0 a 7.0.

## Decisões fechadas

- Só leitura; nenhuma ação na lista além de navegar.

## Modificar / Referenciar

- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Api/Extensions/EndpointExtensions.cs`; `src/bff-student/src/CodeForCoders.BffStudent.Api/Extensions/EndpointExtensions.cs`; `src/student-spa/src/config/paths.ts`, `src/student-spa/src/app/router.tsx`
- **ref:** `internal-api-contract-commerce.yaml` (`listStudentOrdersInternal`), `api-contract.yaml` (`listMyOrders`); `docs/design/wireframes-compra.md`; `src/bff-student/src/CodeForCoders.BffStudent.Api/Endpoints/MyCoursesEndpoints.cs`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/commerce` | `dotnet format src/commerce/CodeForCoders.Commerce.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-student` | `dotnet format src/bff-student/CodeForCoders.BffStudent.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/student-spa` | `npm --prefix src/student-spa run lint`; `npm --prefix src/student-spa run typecheck` | exit 0 cada | `ci-react-ts.yml` |

## Pronto quando

- [ ] Gate focalizado passa (exit 0).
- [ ] Em `http://localhost:8082/student/pedidos`, o aluno com um pedido pago, um expirado e um PIX pendente vê os três, o pendente em destaque, e cada um abre a página do pedido.
