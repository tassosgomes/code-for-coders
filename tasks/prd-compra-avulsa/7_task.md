---
status: done
task_kind: vertical
blocked_by: ["6.0"]
gate: 'dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.OrderExpirationTests --minimum-expected-tests 6 && dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.OrderCancellationTests --minimum-expected-tests 6 && dotnet test --project src/billing/tests/CodeForCoders.Billing.IntegrationTests/CodeForCoders.Billing.IntegrationTests.csproj -- --filter-class CodeForCoders.Billing.IntegrationTests.PaymentCancellationTests --minimum-expected-tests 5 && npm --prefix src/student-spa run test -- order-cancel'
gate_expect: "Pelo menos 17 testes .NET passam (6 e 6 de commerce, 5 de billing) e os testes do student-spa com o fragmento order-cancel passam"
---

# 7.0 O pedido não pago expira, o aluno pode desistir, e o pagamento tardio ainda concede

**Fatia:** V-04 · **Cobre:** RF-06 (desistir), RF-08; RN-V05, RN-V07, RN-V08; RN-CB03; US-03; DP-03, DP-04, DP-05; C-10 · **Spec:** `techspec.md` § V-04 · **ADR:** —

## Comportamento

**`billing`.** `checkout.session.async_payment_failed` (PIX/boleto vencido) e `checkout.session.expired` sem meio escolhido → Pagamento `not-confirmed` `expired` → `cobranca.pagamento-nao-confirmado.v1` (`reason: expired`). Fila `billing.order-cancellations` ligada a `commerce.events` (`vendas.pedido-cancelado.v1`): encerra a página aberta ou cancela o pagamento pendente no gateway e grava `not-confirmed` `cancelled` com o fato (`reason: cancelled`); pedido sem pagamento → sem efeito; gateway fora → a mensagem volta e, esgotadas as entregas, vai à DLQ reprocessável. `ensurePaymentSessionInternal` de pagamento expirado → 422 `PAYMENT_EXPIRED`; cancelado → 422 `PAYMENT_CANCELLED`. Confirmação que chega depois de `not-confirmed` → `pagamento-confirmado` mesmo assim (RN-V08: `billing` não julga o pedido).

**`commerce` — Vendas.**
- `nao-confirmado` `expired` em pedido `awaiting-payment` → `expired` + `vendas.pedido-expirado.v1`; `cancelled` → sem efeito; pedido `paid` → sem efeito.
- Rotina de expiração (formato do `AccessExpirationWorker`, relógio injetado, idempotente): pedido `awaiting-payment` sem página aberta 24 h depois de criado; e pedido cujo prazo conhecido (página ou pendente) venceu há mais de 24 h sem fato de `billing` → `expired` + fato. Um pedido nunca recebe dois `pedido-expirado`, mesmo quando a rotina e o fato de `billing` coincidem.
- `cancelOrderInternal`: `awaiting-payment` → `cancelled` + `vendas.pedido-cancelado.v1`; `cancelled` → 200 sem novo fato; `paid`/`expired` → 422 `ORDER_NOT_CANCELLABLE`.
- `startOrderPaymentInternal` de pedido expirado ou cancelado → 422 `ORDER_NOT_PAYABLE`; `billing` responde expirado → 422 `ORDER_NOT_PAYABLE`.
- `pagamento-confirmado` em pedido `expired` ou `cancelled` → `paid` e concede com a vigência que o pedido congelou (RN-V08), pelo caminho de 5.0.
- Depois de expirado ou cancelado, `createOrderInternal` na mesma oferta cria pedido novo pelas condições vigentes (o índice de pendente único não o bloqueia).

**BFF e `student-spa`.** `cancelMyOrder` (CSRF). Na página do pedido: *Desistir* / *Desistir e pagar de outra forma* com confirmação; "Expirado" e "Cancelado" com *Comprar de novo* (leva ao resumo da mesma opção); "o prazo venceu" ao tentar continuar.

Casos que os testes provam: rotina com relógio — sem página há 24 h → expirado; página aberta há 23 h → não; pendente vencido + 24 h sem fato → expirado; rotina rodada duas vezes → um fato; `nao-confirmado` `expired` → expirado; rotina e fato no mesmo pedido → um fato; desistir → cancelado e fato; desistir de novo → 200 sem fato; desistir de pago/expirado → 422; pagar cancelado → 422; cancelado + confirmação → pago e concessão com a vigência congelada; nova compra depois de cancelar usa o preço atual; em `billing`: cancelamento encerra a sessão/cancela o pagamento no dublê; gateway fora → reentrega; expirado e cancelado recusam a retomada; confirmação tardia publica `pagamento-confirmado`; fatos `pedido-cancelado`, `pedido-expirado` e `pagamento-nao-confirmado` conformes aos AsyncAPI.

## Fora do escopo desta task

- Reembolso de pagamento em duplicidade: manual no painel do Stripe até `CAP-015` (PRD, Riscos).
- Aviso por e-mail de pedido expirado (não-objetivo do PRD).

## Decisões fechadas

- Pagamento confirmado sempre conclui o pedido (DP-05, RN-V08).
- Expiração por fato de `billing` e por rotina de Vendas (C-10).
- Desistir cria pedido novo pelas condições vigentes (DP-04, RN-V07).

## Modificar / Referenciar

- **modificar:** `src/billing/` (tradução de expiração, consumidor da desistência, recusas); `src/commerce/src/CodeForCoders.Commerce.Infra.Data/CommerceDbContext.cs`; `src/commerce/src/CodeForCoders.Commerce.Infra.Messaging/DependencyInjection.cs` (rotina); `src/commerce/src/CodeForCoders.Commerce.Api/Extensions/EndpointExtensions.cs`; `src/bff-student/src/CodeForCoders.BffStudent.Api/Extensions/EndpointExtensions.cs`; `docker-compose.yml`, `docker-compose.remote.yml`, `docker-compose.coolify.yml` (fila de `billing`, opções da rotina)
- **ref:** `src/commerce/src/CodeForCoders.Commerce.Infra.Data/Entitlement/AccessExpirationWorker.cs`, `AccessExpirationCycle.cs` (rotina); `src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/AccessExpirationFixture.cs` (relógio nos testes); `asyncapi-contract.yaml`, `asyncapi-contract-billing.yaml`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/commerce` | `dotnet format src/commerce/CodeForCoders.Commerce.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.OrderCreationTests` | exit 0 (pendente único continua valendo) | `ci-dotnet.yml` Testes |
| `src/billing` | `dotnet format src/billing/CodeForCoders.Billing.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-student` | `dotnet format src/bff-student/CodeForCoders.BffStudent.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/student-spa` | `npm --prefix src/student-spa run lint`; `npm --prefix src/student-spa run typecheck` | exit 0 cada | `ci-react-ts.yml` |
| Compose | `docker compose ... config -q` nos três arranjos | exit 0 cada | AGENTS.md |

## Pronto quando

- [ ] Gate focalizado passa (exit 0).
- [ ] No sandbox: PIX gerado e expiração simulada (`stripe trigger` ou sessão de teste vencida) → pedido "Expirado" e *Comprar de novo* cria pedido novo.
- [ ] No sandbox: boleto ou PIX pendente → *Desistir e pagar de outra forma* → "Cancelado" → nova compra paga com cartão → acesso.
