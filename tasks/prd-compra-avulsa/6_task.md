---
status: pending
task_kind: vertical
blocked_by: ["5.0"]
gate: 'dotnet test --project src/billing/tests/CodeForCoders.Billing.IntegrationTests/CodeForCoders.Billing.IntegrationTests.csproj -- --filter-class CodeForCoders.Billing.IntegrationTests.PendingPaymentTests --minimum-expected-tests 5 && dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.PendingPaymentOrderTests --minimum-expected-tests 4 && npm --prefix src/student-spa run test -- order-pending-payment'
gate_expect: "Pelo menos 9 testes .NET passam (5 de billing, 4 de commerce) e os testes do student-spa com o fragmento order-pending-payment passam"
---

# 6.0 O aluno gera PIX ou boleto, volta depois e o acesso sai quando o pagamento compensa

**Fatia:** V-03 · **Cobre:** RF-04 (PIX e boleto), RF-05 (aguardando), RF-06 (retomar); RN-CB02, RN-CB03; RN-D04; US-01, US-03; C-02 · **Spec:** `techspec.md` § V-03 · **ADR:** —

## Comportamento

**`billing`.** `checkout.session.completed` com pagamento não confirmado (PIX ou boleto gerado) → Pagamento `awaiting` com meio e prazo → `cobranca.pagamento-aguardando.v1` (meio, `expiresAt`, referência) uma vez por pagamento. `ensurePaymentSessionInternal` de um pagamento `awaiting` devolve `kind` `pix-instructions` ou `boleto-instructions`, `method` e `expiresAt`, com o endereço das instruções obtido do PaymentIntent **na hora da chamada** e não persistido; nunca abre outra página. `checkout.session.async_payment_succeeded` → `cobranca.pagamento-confirmado.v1` com o meio `pix`/`boleto` e o `confirmedAt` da compensação — o resto é o caminho de 5.0. A página e os prazos seguem RN-CB03: PIX 24 h, boleto 3 dias corridos, configurados na criação da página.

**`commerce` — Vendas.** `pagamento-aguardando` em pedido `awaiting-payment` → `paymentMethod`, `pendingPayment` (meio e prazo) e referência; `paymentPageExpiresAt` deixa de valer. Em pedido `paid`, `expired` ou `cancelled` → sem efeito (ordem não garantida).

**`student-spa`.** Página do pedido: "Aguardando pagamento do boleto" / "do PIX" com o prazo, *Ver boleto* / *Ver código PIX* (→ `startOrderPayment` → instruções) e o aviso de que o acesso é liberado sozinho quando o pagamento compensar; a mesma página aparece ao voltar de *Comprar* na mesma opção (pendente existente, 4.0).

Casos que os testes provam: aguardando → pedido com meio e prazo; retomada devolve instruções do mesmo pagamento e nenhuma página nova no gateway (dublê conta as criações); aguardando depois de confirmado → sem efeito; boleto gerado 05/10 e confirmado 08/10 → concessão começa em 08/10 e termina 08/10 do ano seguinte; o adaptador real pede ao servidor HTTP local os prazos de 24 h para PIX e 3 dias para boleto; o endereço das instruções não aparece em tabela, outbox, log nem fato; `cobranca.pagamento-aguardando` conforme o AsyncAPI.

## Fora do escopo desta task

- Expiração do PIX/boleto e desistência → 7.0.
- *Meus pedidos* → 9.0 (aqui o pendente é visto pela página do pedido).

## Decisões fechadas

- O endereço das instruções nunca sai de `billing` a não ser na resposta à retomada (C-02, D-05).
- A vigência conta da concessão, não da compra (RN-D04).

## Modificar / Referenciar

- **modificar:** `src/billing/` (tradução de aguardando e confirmado assíncrono, instruções); `src/commerce/src/CodeForCoders.Commerce.Infra.Data/CommerceDbContext.cs` (campos do pendente, migration pelo EF); `src/student-spa/src/config/paths.ts` (estados do pedido)
- **ref:** `techspec.md` § Riscos (prazos do PIX e do boleto no Checkout: confirmar na documentação do Stripe pelo Context7; se o PIX não aceitar 24 h, parar e levar a DP-03 ao responsável); `asyncapi-contract-billing.yaml`; `internal-api-contract-billing.yaml`; `docs/design/wireframes-compra.md`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/billing` | `dotnet format src/billing/CodeForCoders.Billing.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/billing` | `dotnet test --project src/billing/tests/CodeForCoders.Billing.IntegrationTests/CodeForCoders.Billing.IntegrationTests.csproj -- --filter-class CodeForCoders.Billing.IntegrationTests.GatewayWebhookTests` | exit 0 (5.0 continua valendo) | `ci-dotnet.yml` Testes |
| `src/commerce` | `dotnet format src/commerce/CodeForCoders.Commerce.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.OrderPaymentTests` | exit 0 | `ci-dotnet.yml` Testes |
| `src/student-spa` | `npm --prefix src/student-spa run lint`; `npm --prefix src/student-spa run typecheck` | exit 0 cada | `ci-react-ts.yml` |

## Pronto quando

- [ ] Gate focalizado passa (exit 0).
- [ ] No sandbox do Stripe: pedido → PIX de teste gerado → volta → "Aguardando pagamento do PIX" com prazo → *Ver código PIX* reabre o mesmo código → pagamento de teste → "Compra confirmada" e aula reproduz.
- [ ] Boleto: o mesmo cenário quando OD80 confirmar o meio na conta; sem confirmação, registrar na task que o boleto ficou fora do E2E.
