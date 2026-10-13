---
id: TASK-32
title: Alinhar as telas da compra avulsa às divergências da auditoria com o Figma
status: Done
assignee:
  - '@claude'
created_date: '2026-10-13 20:41'
updated_date: '2026-10-13 22:21'
labels:
  - frontend
dependencies: []
references:
  - >-
    https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=284-13063
  - student-spa
  - admin-spa
type: bug
ordinal: 41000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Auditoria de 2026-10-13 comparou o código com o Figma Screens — Compra (node 284:13063) e o wireframe aprovado docs/design/wireframes-compra.md. Resumo da compra, Pedido e Meus pedidos do student-spa não seguem o layout do Figma, e o resumo tem divergência de fluxo (G6). O Financeiro do admin-spa está próximo, com ajustes de detalhe. Menu lateral (N) e botão Comprar (P2) já batem. Não alterar a troca de tema claro/escuro. Dev tem o sandbox do Stripe configurado, então pedidos de teste em dev são permitidos para a conferência.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 As subtasks do Resumo da compra, Pedido, Meus pedidos e Financeiro estão concluídas
<!-- AC:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Subtasks 32.1 a 32.4 concluídas (PRs #232 a #235, mergeadas em 2026-10-13) e conferidas em dev em 2026-10-13 com o pedido de teste #000023 no sandbox do Stripe.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Telas da compra avulsa alinhadas ao Figma Screens — Compra: Resumo (com o fluxo G6 de abrir o pagamento direto), Pedido, Meus pedidos e Financeiro do backoffice. Verificado com lint, build e testes do student-spa e do admin-spa, prévias locais e conferência em dev com pedido de teste no sandbox do Stripe.
<!-- SECTION:FINAL_SUMMARY:END -->
