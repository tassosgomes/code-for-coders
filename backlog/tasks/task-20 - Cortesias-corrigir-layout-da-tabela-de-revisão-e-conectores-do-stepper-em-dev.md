---
id: TASK-20
title: 'Cortesias: corrigir layout da tabela de revisão e conectores do stepper em dev'
status: Done
assignee:
  - '@claude'
created_date: '2026-10-10 23:59'
updated_date: '2026-10-11 14:07'
labels:
  - frontend
  - bug
dependencies: []
modified_files:
  - src/admin-spa/src/assets/courtesies-flow.css
  - src/admin-spa/src/assets/courtesies-steps.css
type: bug
ordinal: 23000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Conferência em dev depois da PR #208 (TASK-19): na Revisão, a regra global .catalog-record-card dl (app.css) transforma a lista de definição em grade de 2 colunas e esmaga a tabela Aluno/Curso/Vigência/Motivo (nome do aluno quebrando letra a letra). Os conectores dos passos futuros do stepper usam --border, que no tema escuro é quase igual ao fundo e some. A linha do curso escolhido também encolhe ao trocar o botão por "✓ Escolhido".
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 A tabela de revisão mostra cada linha com rótulo, valor e Editar lado a lado, sem a grade da regra global, e sem borda na última linha
- [x] #2 Os conectores entre passos futuros do stepper ficam visíveis nos temas claro e escuro sem alterar tokens de tema
- [x] #3 A linha do curso escolhido mantém a altura das demais
- [x] #4 Lint, build e testes do admin-spa passam
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Escopar display:block na .courtesy-review-table dentro de .courtesy-page. 2. Conectores futuros com --border-strong (token existente). 3. min-height nas linhas da tabela de cursos. 4. Verificar com CSS injetado em dev e depois do deploy.
<!-- SECTION:PLAN:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Tabela da revisão isolada da grade de .catalog-record-card dl, conectores do stepper com --border-strong e altura fixa da linha escolhida. Verificado em dev após o deploy (revisão por período e vitalícia, conectores no tema escuro) e com admin-spa em main (2026-10-11): lint ok, build ok, vitest 52 arquivos/313 testes.
<!-- SECTION:FINAL_SUMMARY:END -->
