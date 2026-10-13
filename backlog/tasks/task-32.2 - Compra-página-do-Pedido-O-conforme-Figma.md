---
id: TASK-32.2
title: 'Compra: página do Pedido (O) conforme Figma'
status: In Progress
assignee:
  - '@claude'
created_date: '2026-10-13 20:41'
updated_date: '2026-10-13 21:12'
labels:
  - frontend
dependencies: []
references:
  - >-
    https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=289-286
  - >-
    https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=290-796
  - student-spa
  - docs/design/wireframes-compra.md
modified_files:
  - >-
    src/student-spa/src/features/student-purchase/components/student-order-screen.tsx
  - >-
    src/student-spa/src/features/student-purchase/components/cancel-order-confirm.tsx
  - >-
    src/student-spa/src/features/student-purchase/components/order-status-badge.tsx
  - src/student-spa/src/features/student-purchase/components/purchase-alert.tsx
  - >-
    src/student-spa/src/features/student-purchase/components/purchase-summary-screen.tsx
  - src/student-spa/src/features/student-purchase/api/get-order.ts
  - src/student-spa/src/features/student-purchase/utils/purchase-labels.ts
  - src/student-spa/src/app/order-states.test.tsx
  - src/student-spa/src/app/order-cancel.test.tsx
  - src/student-spa/src/app/order-pending-payment.test.tsx
  - src/student-spa/src/app/order-return.test.tsx
  - src/student-spa/src/app/my-orders.test.tsx
  - src/student-spa/src/app/student-purchase-summary.test.tsx
parent_task_id: TASK-32
type: bug
ordinal: 43000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
A página /pedidos/:orderId repete o número do pedido como título, usa um Alert simples para a situação e parágrafos soltos no resumo, e o diálogo de desistência tem textos diferentes do Figma (seções O e O mobile).
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Cabeçalho com PEDIDO #NNNNNN em overline, curso em H2 e linha 'opção · valor · criado em DD/MM/AAAA'
- [ ] #2 OrderStatePanel com ícone em quadrado colorido, título H3, texto e ações para O1 a O10 e O12.c
- [ ] #3 Resumo do pedido com linhas rotuladas e a situação em OrderStatusBadge (ícone e texto, G10)
- [ ] #4 Desistência em AlertDialog no desktop e Sheet inferior no mobile com os textos do Figma (O8, O8.b, O8.m)
- [ ] #5 Não encontrado (O11), carregando (O12.a) e erro (O12.b) seguem o Figma; mobile 390 sem rolagem horizontal
- [ ] #6 Lint, build e testes do student-spa passam e a tela foi conferida em dev
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Componentes compartilhados: OrderStatusBadge (ícone e texto, G10) e alertas da compra (InfoAlert, ErrorAlert), reaproveitados no Resumo.
2. student-order-screen: cabeçalho PEDIDO #N / curso H2 / 'opção · valor · criado em'; OrderStatePanel (tile de 40 colorido, título H3 com aria-live) para O1 a O10 e O12.c; Resumo do pedido com linhas rotuladas e badge; '← Meus pedidos' ghost; carregando (O12.a), erro (O12.b) e não encontrado (O11).
3. Desistência: AlertDialog (desktop) e Sheet inferior (mobile) com os textos do Figma, 'Cancelando…', toast 'Pedido cancelado.', erro 'Nada mudou. Tente de novo.' e 422 fechando o diálogo.
4. Testes; lint, tsc, build, vitest; prévia local desktop e 390.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Implementado: OrderStatusBadge e alertas compartilhados (o Resumo passa a usá-los); página do Pedido com cabeçalho (overline PEDIDO #N, curso em H2/H3 no mobile, linha 'opção · valor · criado em'), OrderStatePanel com tile de 40 (progresso, aguardando, sucesso, expirado, cancelado) e título H3/H4 anunciado por aria-live, para O1 a O10 e O12.c; O2 troca o texto pelo aviso de demora (com link para Meus pedidos); O5.b e O7.b com alerta destrutivo e Tentar de novo; O9 e O10 com data e o aviso 'Se você pagou…'; Resumo do pedido com linhas de 120 (96 no mobile) e badge; O11 em EmptyState com Ver meus pedidos; O12.a em skeleton; O12.b em alerta com Tentar de novo. Desistência: AlertDialog de 440 com ✕, Manter pedido (foco inicial) e Desistir do pedido (destrutivo, 'Cancelando…'), toast 'Pedido cancelado.', erro 'Nada mudou. Tente de novo.' dentro do diálogo e 422 fechando e recarregando o pedido; no mobile (<768) Sheet inferior com alça e ações em largura cheia. Mudança de comportamento: pedido pago sem concessão (O4) passa a ser acompanhado mesmo sem ?resultado=concluido (antes ficava parado em Liberando seu acesso). Decisões: O7 sem página de pagamento aberta (pedido criado mas pagamento não aberto) usa 'Seu pedido continua aguardando pagamento. Cartão, PIX ou boleto são escolhidos no ambiente seguro de pagamento.' e o botão 'Ir para o pagamento'; botões PIX/boleto mostram spinner com o mesmo rótulo. Testes: order-cancel, order-pending-payment, order-return, my-orders e student-purchase-summary ajustados; novo order-states (cabeçalho e resumo, O7 com prazo, O11, O12.b, O12.c, O4 sem resultado). Lint, tsc, build e vitest (34 arquivos, 231 testes) passam. Prévia local: diálogo 440x300, padding 24, raio 16, título 20/28, botões 40; mobile 390 com painel padding 20, título 24, rótulos 96, ações de 316 empilhadas, Sheet de 390 com padding 20 e botões de 350, sem rolagem horizontal.

PR #233 aberta (branch fix/compra-pedido-figma). Conferência em dev com pedido de teste no sandbox do Stripe pendente após o deploy.

PR #233 mergeada em 2026-10-13.
<!-- SECTION:NOTES:END -->
