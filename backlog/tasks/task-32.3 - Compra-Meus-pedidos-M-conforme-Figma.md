---
id: TASK-32.3
title: 'Compra: Meus pedidos (M) conforme Figma'
status: Done
assignee:
  - '@claude'
created_date: '2026-10-13 20:42'
updated_date: '2026-10-13 22:14'
labels:
  - frontend
dependencies: []
references:
  - >-
    https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=291-960
  - student-spa
  - docs/design/wireframes-compra.md
modified_files:
  - src/student-spa/src/features/student-purchase/components/my-order-row.tsx
  - >-
    src/student-spa/src/features/student-purchase/components/my-orders-screen.tsx
  - src/student-spa/src/app/my-orders.test.tsx
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
- [x] #1 Pendente em card com borda de acento, badge, linha de prazo e Retomar pagamento à direita
- [x] #2 Histórico em tabela num card só, com cabeçalho em overline, OrderStatusBadge e ação Ir para o curso / Ver pedido
- [x] #3 Paginação compacta conforme o Figma
- [x] #4 Vazio, carregando, erro e mobile 390 (M2 a M5 e variantes .m) seguem o Figma sem rolagem horizontal
- [x] #5 Lint, build e testes do student-spa passam e a tela foi conferida em dev
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. my-order-row: PendingOrderCard (borda de acento de 2, dados à esquerda, badge e Retomar pagamento à direita; no mobile empilhado com o botão em largura cheia) e MyOrderRow (card no mobile, linha da tabela no desktop com colunas de 96/flex/110/104/140/190, OrderStatusBadge e ação Ir para o curso outline sm ou Ver pedido link).
2. my-orders-screen: cabeçalho MINHA CONTA/H2; tabela num card com cabeçalho em overline sobre muted e divisórias; paginação ‹ 1 2 › com a atual em outline; vazio (M2) em EmptyState com receipt; carregando (M3) em dois cartões de skeleton; erro (M4) em alerta destrutivo com Tentar de novo.
3. Testes; lint, tsc, build, vitest; prévia local desktop e 390.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Implementado: PendingOrderCard (borda primary de 2, '#N · curso · opção', 'valor · data · meio', 'Pague até …', badge e Retomar pagamento à direita; mobile como no M5: '#N · data', curso, opção, 'valor · meio', badge, prazo e botão em largura cheia) e MyOrderRow (mesmo DOM reorganizado por CSS: card no mobile com '#N · data' e 'valor · meio'; linha da tabela no desktop com colunas 96/flex/110/104/140/190, número em primary, curso Inter 14 medium com a opção em 12 muted, OrderStatusBadge e ação Ir para o curso outline sm, Retomar pagamento outline sm ou Ver pedido link). Lista num card com cabeçalho em overline sobre muted e divisórias; a linha da opção passa a mostrar só o nome da opção (sem a vigência), como no Figma. Paginação compacta ‹ 1 2 › (atual em outline, aria-current) com anúncio 'Página X de Y' só para leitor de tela. Vazio (M2) em EmptyState com receipt, carregando (M3) em dois cartões de skeleton, erro (M4) em alerta destrutivo com Tentar de novo. Título da página passa a h1. Testes de my-orders ajustados (Próxima página, aria-current, Ver pedido). Lint, tsc, build e vitest (34 arquivos, 231 testes) passam. Prévia local 1180 e 390: tabela e cartão pendente como no M1; cartões do mobile como no M5, sem rolagem horizontal.

PR #234 aberta (branch fix/compra-meus-pedidos-figma). Conferência em dev pendente após o deploy.

PR #234 mergeada em 2026-10-13.

Conferência em dev em 2026-10-13: pendente #000023 em destaque (borda de acento, 'R$ 397,00 · 13/10/2026 · Ainda não escolhido', 'Pague até 14/10/2026 às 19:12', badge e Retomar pagamento) e histórico em tabela com #000022 Pago e Ir para o curso (M1); janela de 500 (mínimo do Chrome) em cards como no M5, sem rolagem horizontal. Vazio, carregando e erro conferidos pela prévia local e pelos testes.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Meus pedidos alinhado ao Figma: pendente em destaque, histórico em tabela (cards no mobile) com OrderStatusBadge, paginação compacta e estados vazio, carregando e erro. Verificado com lint, build e testes do student-spa, prévia local e conferência em dev (M1, M5) (PR #234).
<!-- SECTION:FINAL_SUMMARY:END -->
