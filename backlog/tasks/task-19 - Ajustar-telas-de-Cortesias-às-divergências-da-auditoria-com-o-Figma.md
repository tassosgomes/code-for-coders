---
id: TASK-19
title: Ajustar telas de Cortesias às divergências da auditoria com o Figma
status: In Progress
assignee:
  - '@claude'
created_date: '2026-10-10 22:29'
updated_date: '2026-10-10 22:30'
labels:
  - frontend
  - backend
  - bug
dependencies: []
type: bug
ordinal: 19000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Auditoria de 2026-10-10 comparando o SPA admin em dev (/admin/cortesias) com o Figma (arquivo kKNfxTqSFT5IfQHcoTh5gn, página Screens — Cortesias, node 227:3, frames T0 a T6 desktop) encontrou bugs de estado, partes faltando, desalinhamentos e textos divergentes. Fora de escopo: troca de tema claro/escuro (centralizada na TASK-15), mobile 390 e telas de trilha de auditoria (T7). Decisões do usuário: o passo Curso mostra a coluna Oferta, exigindo o campo no BFF/contrato; as concessões do aluno usam paginação de 10 por página; o link 'Ver concessões do aluno' volta ao passo Aluno com o aluno carregado; o stepper some no Resultado e os erros de confirmação aparecem como tela de Resultado (T6.b1–b3).
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 As subtarefas de oferta no BFF, passo Aluno/stepper e passos Curso a Resultado estão concluídas
- [ ] #2 Lint, build e testes do admin-spa e testes dos serviços alterados passam
<!-- AC:END -->
