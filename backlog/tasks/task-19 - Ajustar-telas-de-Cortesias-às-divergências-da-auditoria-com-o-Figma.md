---
id: TASK-19
title: Ajustar telas de Cortesias às divergências da auditoria com o Figma
status: Done
assignee:
  - '@claude'
created_date: '2026-10-10 22:29'
updated_date: '2026-10-11 14:07'
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
- [x] #1 As subtarefas de oferta no BFF, passo Aluno/stepper e passos Curso a Resultado estão concluídas
- [x] #2 Lint, build e testes do admin-spa e testes dos serviços alterados passam
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
Subtarefas 19.1 (hasOffer no Commerce/bff-admin), 19.2 (stepper e passo Aluno) e 19.3 (passos Curso a Resultado), seguidas de conferência em dev (TASK-20 e #212).
<!-- SECTION:PLAN:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Telas de Cortesias alinhadas ao Figma em #208, #209 e #212, com hasOffer exposto pelo Commerce. Verificado em dev ponta a ponta com uma cortesia real e por admin-spa em main (2026-10-11): lint ok, build ok, vitest 52 arquivos/313 testes. Testes de integração do Commerce e bff-admin passaram na 19.1.
<!-- SECTION:FINAL_SUMMARY:END -->
