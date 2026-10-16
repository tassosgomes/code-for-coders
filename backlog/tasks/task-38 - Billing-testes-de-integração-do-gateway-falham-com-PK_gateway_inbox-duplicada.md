---
id: TASK-38
title: 'Billing: testes de integração do gateway falham com PK_gateway_inbox duplicada'
status: In Progress
assignee:
  - '@claude'
created_date: '2026-10-16 23:36'
updated_date: '2026-10-16 23:46'
labels:
  - billing
  - tests
dependencies: []
modified_files:
  - >-
    src/billing/tests/CodeForCoders.Billing.IntegrationTests/GatewayWebhookTests.cs
  - >-
    src/billing/tests/CodeForCoders.Billing.IntegrationTests/PendingPaymentTests.cs
priority: high
type: bug
ordinal: 53000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Desde o commit 19a2412 (2026-10-13) o gate de Build & Test do billing falha na main e em toda PR, nos mesmos 4 testes de integração: GatewayWebhookTests.RepeatedProcessingDoesNotProduceDuplicateFact, GatewayWebhookTests.CheckoutSessionCompletedPaidProducesPaymentConfirmedFactInOutbox, GatewayWebhookTests.PaymentConfirmedFactConformsToAsyncApiContract e PendingPaymentTests.CardPaidSessionWithAllMethodsAllowedStaysCardWithoutAskingTheGateway. O log mostra 23505 duplicate key em billing_access.gateway_inbox (PK_gateway_inbox). Com o gate vermelho, falhas reais do billing passam despercebidas. Runs de referência: main 38475382827 e PR #239 38980854940.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 A causa raiz está identificada e registrada nas notas, incluindo por que começou em 19a2412
- [ ] #2 Os 4 testes passam de forma determinística, localmente e no CI, inclusive rodando a suíte inteira mais de uma vez
- [x] #3 Se a causa estiver no código de produção e não no teste, o comportamento corrigido tem teste cobrindo
- [ ] #4 O gate Build & Test do billing fica verde na PR
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1) Reproduzir localmente; 2) confirmar o gatilho em 19a2412; 3) alinhar os testes ao comportamento deliberado do tradutor (metodo vem do PaymentIntent); 4) gate build + test x2.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Causa raiz: o PK_gateway_inbox 23505 e ruido (ReceiveAsync trata a duplicata como idempotencia e o EF so loga). A falha real: 19a2412 mudou StripeEventTranslator para nao assumir method=card em checkout.session.completed paid; ReceiveGatewayEvent passou a chamar gateway.GetPaymentMethodAsync, que no GatewayMock (Moq frouxo) devolve null, entao o fato PaymentConfirmed nunca e gerado (Method null). O mesmo commit ajustou so um teste (Theory card/pix) e esqueceu 3 testes + o de 'sem perguntar ao gateway'. Correcao: setup do mock com 'card' nos 3 testes de GatewayWebhookTests e CardPaid... renomeado para CardPaidSessionResolvesTheMethodFromThePaymentIntent (Verify Times.Once). Gate: build 0 warnings; dotnet test --solution 87/87 duas vezes.
<!-- SECTION:NOTES:END -->
