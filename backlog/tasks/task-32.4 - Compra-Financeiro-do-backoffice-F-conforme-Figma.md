---
id: TASK-32.4
title: 'Compra: Financeiro do backoffice (F) conforme Figma'
status: In Progress
assignee:
  - '@claude'
created_date: '2026-10-13 20:42'
updated_date: '2026-10-13 21:59'
labels:
  - frontend
dependencies: []
references:
  - >-
    https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=292-1223
  - >-
    https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=292-2048
  - >-
    https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=292-2049
  - admin-spa
  - docs/design/wireframes-compra.md
modified_files:
  - src/admin-spa/src/assets/finance.css
  - src/admin-spa/src/assets/app.css
  - src/admin-spa/src/app/routes/finance-area-route.tsx
  - src/admin-spa/src/app/routes/finance-orders.test.tsx
  - >-
    src/admin-spa/src/features/finance-area/components/finance-order-detail-screen.tsx
  - src/admin-spa/src/features/finance-area/components/finance-order-filters.tsx
  - src/admin-spa/src/features/finance-area/components/finance-order-status.tsx
  - src/admin-spa/src/features/finance-area/components/finance-orders-screen.tsx
  - src/admin-spa/src/features/finance-area/types/finance-filter-support.ts
  - src/admin-spa/src/features/finance-area/utils/finance-order-format.ts
parent_task_id: TASK-32
type: bug
ordinal: 45000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
A lista e o detalhe de pedidos do backoffice seguem a estrutura do Figma, mas o caminho do detalhe é um link único, a concessão aparece em texto e a lista, filtros e estados precisam de conferência visual (seções F).
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Lista, filtros e estados F1.a a F1.f e F2 conferidos e alinhados ao Figma
- [ ] #2 Detalhe com trilha Financeiro / Pedidos / #NNNNNN, badges de situação e concessão e referência do pagamento em mono com Copiar (F3)
- [ ] #3 Estados F3.b1 a F3.e e mobile 390 seguem o Figma sem rolagem horizontal
- [ ] #4 Lint, build e testes do admin-spa passam e as telas foram conferidas em dev
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Mover as regras do Financeiro de app.css para assets/finance.css e reescrever: cabeçalho, FinanceFilterBar (card 16/20, rótulos 14 medium, controles de 40, raio 10), tabela num card com cabeçalho em overline sobre surface-muted, linhas 14/20 e colunas 84/96/flex/flex/96/120/176, OrderStatusBadge com ícones lucide, paginação '20 por página ‹ 1 2 ›', mobile em cards (F1.i) e filtros em sheet inferior (F2.a).
2. Filtros: tirar 'Buscar curso' e a paginação de cursos (o Figma usa um select de Curso); e-mail com Localizar ao lado; chip quando localizado, erro sob o campo quando não achado, alerta destrutivo com Tentar de novo quando indisponível (F2.b); Limpar filtros como link e Filtrar primário; período inválido (F2.c).
3. Estados: F1.c skeleton, F1.d e F1.e EmptyState com tile, F1.f alerta com Tentar de novo.
4. Detalhe: trilha Financeiro / Pedidos / #N, H2 com badge, cards com overline, linhas de 180 (rótulo em cima no mobile), referência em mono com Copiar outline sm, momentos 'DD/MM/AAAA às HH:MM' e 'válida até …', concessão em badge (Existe, Ainda não existe, Sem concessão), F3.c EmptyState, F3.d skeleton, F3.e alerta.
5. Testes; lint, tsc, build, vitest; prévia local desktop e 390.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Implementado: regras do Financeiro saem de app.css para assets/finance.css. Lista: cabeçalho FINANCEIRO/Pedidos/subtítulo; FinanceFilterBar em card (padding 20, raio 16, rótulos 14 medium, controles de 40 com raio 10); campo Curso volta a ser um select só (sai 'Buscar curso' e a paginação de cursos, que não existem no Figma); e-mail com Localizar ao lado, chip 'Nome · e-mail' com ✕ quando localizado, erro sob o campo quando não achado e alerta destrutivo com Tentar de novo quando a busca está indisponível (F2.b); Limpar filtros como link e Filtrar primário; tabela num card com cabeçalho em overline sobre surface-muted, número em primary, aluno e curso com segunda linha em 12 muted, OrderStatusBadge com ícones lucide (relógio, check, relógio riscado em vermelho sólido, X em accent); paginação '20 por página ‹ 1 2 ›' com a atual em outline; carregando em card com 5 barras; vazio com filtros (F1.d: lupa, 'Ajuste a situação…', Limpar filtros primário) e sem pedidos (F1.e: receipt); erro em alerta destrutivo com Tentar de novo. Mobile: botão Filtros (N) em largura cheia, Sheet com alça, título Filtros e ✕ 'Fechar filtros', pedidos em cards ('#N · data', aluno, e-mail, curso, opção, 'valor · meio', badge). Detalhe: trilha com links Financeiro / Pedidos / #N, H2 com badge, cards com overline e linhas de 180 (rótulo em cima no mobile), Aluno 'Nome · e-mail', referência em JetBrains Mono com Copiar outline sm, momentos 'DD/MM/AAAA às HH:MM' no fuso da escola (America/Sao_Paulo, como no SPA do aluno) e 'válida até …', concessão em badge (✓ Existe, Ainda não existe, — Sem concessão[: pedido não pago]), o id da concessão deixa de aparecer e o botão Atualizar sai (o Figma F3.b2 só tem o texto); não encontrado em EmptyState com Voltar para Pedidos primário; carregando em skeleton de título e quatro cards; erro em alerta com Tentar de novo. Fora de escopo: 'Pague até' do F3.b1 (o contrato do detalhe no backoffice não traz o prazo do PIX/boleto) e o ícone do tema sobreposto ao selo Backoffice no topo do mobile (shell). Testes de finance-orders ajustados e dois novos (busca indisponível com Tentar de novo; lista vazia e pedido aguardando sem concessão). Lint, tsc, build e vitest do admin-spa (60 arquivos, 396 testes) passam. Prévia local: card de filtros com 328 de altura como no Figma, select 40/raio 10, cabeçalho da tabela 12 semibold uppercase, badge 12 medium com padding 2/12; detalhe como F3.a e F3.b1; mobile 390 como F1.i e F3.g, sem rolagem horizontal.

PR #235 aberta (branch fix/compra-financeiro-figma). Conferência em dev pendente após o deploy, com conta de financeiro.

PR #235 mergeada em 2026-10-13.
<!-- SECTION:NOTES:END -->
