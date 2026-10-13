---
id: TASK-30
title: >-
  Corrigir o cartão de vídeo do seletor da aula (A6.f) que espreme a caixa
  interna
status: Done
assignee:
  - '@claude'
created_date: '2026-10-13 18:45'
updated_date: '2026-10-13 19:03'
labels:
  - frontend
dependencies: []
references:
  - >-
    https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=324-41056
  - admin-spa
modified_files:
  - src/admin-spa/src/assets/authoring.css
priority: medium
type: bug
ordinal: 37000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Na conferência da TASK-25 em dev, os cartões do seletor 'Escolher vídeo' da Autoria apareciam com a caixa interna (rádio, título, meta e etiquetas) colada às bordas do cartão externo. A lista é um fieldset em grid de altura fixa, e o Chrome espremia as linhas até o min-height de 94 px, quando o VideoOption do Figma (A6.f, 324:41056) tem 126 px. A caixa interna também divergia em padding, gap, peso do título e posição do rádio.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Cada cartão do seletor ocupa a altura do conteúdo (126 px no caso de uma linha de etiquetas), com a caixa interna a 16 px das bordas do cartão externo
- [x] #2 Caixa interna com padding 10/12, gap de 6 px entre título, meta e etiquetas, título em peso 500 e rádio alinhado ao título (2 px abaixo do topo do texto)
- [x] #3 Lint, build e testes do admin-spa passam; conferência em dev
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Medir em dev o cartão contra o VideoOption do Figma (324:41056).
2. grid-auto-rows: max-content na lista; padding, gap, peso e margem do rádio na caixa interna (authoring.css).
3. Lint, build e testes; prévia em dev com o CSS do build; conferência depois do deploy.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Causa: fieldset .single-choice-options em display:grid com altura fixa (flex 1 1 auto, overflow auto); as linhas auto eram espremidas até o min-height do label (94 px). Com grid-auto-rows: max-content o cartão fica com 126 px, como no Figma.
O peso 400 do título vinha de 65135c1 (auditoria da Autoria). O adendo A6.f, mais novo, usa Body/SM Medium (500).
Prévia em dev (CSS do build injetado só no navegador): cartão de 415×126, caixa interna em (17,17) com 313×92, rádio em (30,30), texto em (58,28), gap 6 e selo Pronto a 17 px da direita, como no Figma. Lint, build e 394 testes passam.

PR #229 aberta.

PR #229 mergeada (c38618b) e publicada em dev (index-DGyFS6Br.css). Conferência em dev no seletor de 'React - Fundamentos', fechado com Cancelar sem vincular: os 4 primeiros cartões com 126 px; cartão de 415×126, caixa interna em (17,17) com 313×92, rádio em (30,30), texto em (58,28) com gap 6, título em 500 e selo Pronto a 17 px da direita.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Corrigido o cartão de vídeo do seletor da aula (A6.f): a lista em grid de altura fixa espremia as linhas até o min-height e colava a caixa interna às bordas do cartão. Agora usa grid-auto-rows: max-content, e a caixa interna segue o VideoOption do Figma (padding 10/12, gap 6, título em 500, rádio alinhado). Verificado com lint, build e 394 testes do admin-spa e em dev depois do deploy da PR #229.
<!-- SECTION:FINAL_SUMMARY:END -->
