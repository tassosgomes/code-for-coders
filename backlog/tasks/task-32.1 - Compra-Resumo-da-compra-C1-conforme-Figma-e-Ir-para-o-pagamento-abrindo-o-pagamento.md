---
id: TASK-32.1
title: >-
  Compra: Resumo da compra (C1) conforme Figma e Ir para o pagamento abrindo o
  pagamento
status: In Progress
assignee:
  - '@claude'
created_date: '2026-10-13 20:41'
updated_date: '2026-10-13 20:51'
labels:
  - frontend
dependencies: []
references:
  - >-
    https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=288-84
  - student-spa
  - docs/design/wireframes-compra.md
modified_files:
  - >-
    src/student-spa/src/features/student-purchase/components/purchase-summary-screen.tsx
  - src/student-spa/src/features/student-purchase/utils/purchase-labels.ts
  - src/student-spa/src/app/student-purchase-summary.test.tsx
parent_task_id: TASK-32
type: bug
ordinal: 42000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
O resumo mostra 'Confirmar compra', cria o pedido e para na página do pedido, onde o aluno clica de novo em 'Ir para o pagamento'. O wireframe aprovado (G3, G6, fluxo 2.1) diz que o botão do resumo é 'Ir para o pagamento' e que o clique cria o pedido e abre o pagamento do gateway. O layout do cartão e os estados C1.b a C1.h também divergem do Figma (seção C1, node 288:84).
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Cartão de 640 com sobretítulo CURSO, título H3, linhas com rótulo de 140 entre divisórias, preço em H5 e ações '← Voltar ao curso' (ghost) e 'Ir para o pagamento' (primário) lado a lado (C1.a)
- [ ] #2 'Ir para o pagamento' cria o pedido e abre o pagamento do gateway; durante a espera fica desabilitado com 'Abrindo o pagamento…'; clique repetido não cria outro pedido
- [ ] #3 Pagamento indisponível depois de criar o pedido mostra o alerta do C1.e com Tentar de novo (mesmo pedido) e Ver meu pedido
- [ ] #4 Aviso de acesso existente (C1.b), pedido pendente (C1.c), opção indisponível (C1.d), carregando (C1.f) e erro (C1.g) seguem o Figma
- [ ] #5 Mobile 390 (C1.h) sem rolagem horizontal
- [ ] #6 Lint, build e testes do student-spa passam e a tela foi conferida em dev
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. purchase-summary-screen: cabeçalho COMPRA/H2; cartão de 640 (p-6, mobile p-5) com overline Curso, título H3 (H4 no mobile), dl entre divisórias com rótulos de 140 (84 no mobile), preço H5; alertas info com ícone azul, título semibold e texto muted; ações '← Voltar ao curso' ghost e 'Ir para o pagamento' (mobile: primário em largura cheia em cima).
2. Fluxo G6: o clique cria o pedido (mesma Idempotency-Key) e chama startOrderPayment; botão desabilitado com spinner 'Abrindo o pagamento…'; falha do pagamento mostra o alerta C1.e com Tentar de novo (mesmo pedido) e Ver meu pedido; falha da criação mantém o alerta atual.
3. Estados: C1.c (título, alerta, Voltar e Ver pedido pendente), C1.d (EmptyState com tile, Ver todos os cursos e Voltar), C1.f (skeleton do Figma), C1.g (alerta destrutivo com Tentar de novo outline sm).
4. Ajustar student-purchase-summary.test; lint, tsc, build, vitest; prévia local desktop e 390; conferência em dev com pedido de teste no sandbox.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Implementado: cabeçalho COMPRA/H2; cartão de 640 (p-6; p-5 no mobile) com overline Curso, título H3 (H4 no mobile), dl entre divisórias com rótulos de 140 (84 no mobile) e preço H5; alertas info com ícone info, título semibold e texto muted; ações '← Voltar ao curso' (ghost) e 'Ir para o pagamento' lado a lado (no mobile o primário fica em cima, em largura cheia). Fluxo G6: o clique cria o pedido (mesma Idempotency-Key em nova tentativa) e chama a sessão de pagamento; botão desabilitado com spinner 'Abrindo o pagamento…'; se o pagamento falha, alerta C1.e com Tentar de novo (reabre o pagamento do mesmo pedido, sem criar outro) e Ver meu pedido. Estados C1.c, C1.d (EmptyState com tile, Ver todos os cursos e Voltar pelo histórico), C1.f (skeleton) e C1.g (alerta destrutivo com Tentar de novo outline sm). Vigência do aluno passa a 'contados a partir da liberação do acesso' (G4), o que vale também no Pedido e em Meus pedidos; a página do curso (P2) mantém o texto dela. Decisão: o cartão do C1.d fica alinhado ao cabeçalho, sem o recuo de 24 do frame do Figma. Lint, tsc, build e vitest (33 arquivos, 224 testes) passam. Prévia local (DOM renderizado + CSS do build): cartão 640x518, padding 24, raio 16, rótulos 140, título 24/30, preço 16/24 semibold, alertas padding 16 raio 12, botões 40; mobile 390 com cartão 358, padding 20, rótulos 84, título 20, botões de 316 com o primário em cima e sem rolagem horizontal.

PR #232 aberta (branch fix/compra-resumo-figma). Conferência em dev com pedido de teste no sandbox do Stripe pendente após o deploy.

PR #232 mergeada em 2026-10-13.
<!-- SECTION:NOTES:END -->
