---
id: TASK-32.2
title: 'Compra: página do Pedido (O) conforme Figma'
status: To Do
assignee:
  - '@claude'
created_date: '2026-10-13 20:41'
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
