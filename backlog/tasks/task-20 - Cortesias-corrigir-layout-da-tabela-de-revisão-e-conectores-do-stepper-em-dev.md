---
id: TASK-20
title: 'Cortesias: corrigir layout da tabela de revisão e conectores do stepper em dev'
status: In Progress
assignee:
  - '@claude'
created_date: '2026-10-10 23:59'
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
- [ ] #1 A tabela de revisão mostra cada linha com rótulo, valor e Editar lado a lado, sem a grade da regra global, e sem borda na última linha
- [ ] #2 Os conectores entre passos futuros do stepper ficam visíveis nos temas claro e escuro sem alterar tokens de tema
- [ ] #3 A linha do curso escolhido mantém a altura das demais
- [ ] #4 Lint, build e testes do admin-spa passam
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Escopar display:block na .courtesy-review-table dentro de .courtesy-page. 2. Conectores futuros com --border-strong (token existente). 3. min-height nas linhas da tabela de cursos. 4. Verificar com CSS injetado em dev e depois do deploy.
<!-- SECTION:PLAN:END -->
