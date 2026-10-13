---
id: TASK-32.3
title: 'Compra: Meus pedidos (M) conforme Figma'
status: To Do
assignee:
  - '@claude'
created_date: '2026-10-13 20:42'
labels:
  - frontend
dependencies: []
references:
  - >-
    https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=291-960
  - student-spa
  - docs/design/wireframes-compra.md
parent_task_id: TASK-32
type: bug
ordinal: 44000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Meus pedidos monta um card por linha, mostra a situação sem badge e usa paginação Anterior/Próxima; o Figma (seção M) tem o pendente em card de acento e o histórico em tabela.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Pendente em card com borda de acento, badge, linha de prazo e Retomar pagamento à direita
- [ ] #2 Histórico em tabela num card só, com cabeçalho em overline, OrderStatusBadge e ação Ir para o curso / Ver pedido
- [ ] #3 Paginação compacta conforme o Figma
- [ ] #4 Vazio, carregando, erro e mobile 390 (M2 a M5 e variantes .m) seguem o Figma sem rolagem horizontal
- [ ] #5 Lint, build e testes do student-spa passam e a tela foi conferida em dev
<!-- AC:END -->
