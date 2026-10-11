---
id: TASK-21.4
title: >-
  Acesso interno: corrigir logo duplicado no cabeçalho, rodapés de Acessos e
  terminal de sem permissão em dev
status: Done
assignee:
  - '@claude'
created_date: '2026-10-11 01:51'
updated_date: '2026-10-11 14:08'
labels:
  - frontend
dependencies: []
modified_files:
  - src/admin-spa/src/assets/backoffice-shell.css
  - src/admin-spa/src/assets/staff-access.css
  - src/admin-spa/src/components/forbidden-screen.tsx
  - src/admin-spa/src/features/staff-access/components/staff-access-screen.tsx
parent_task_id: TASK-21
type: bug
ordinal: 28000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Conferência em dev após a PR #210 achou: no desktop o logo de mobile aparece também no cabeçalho (a regra nova forçava display:flex em .mobile-backoffice-brand); rodapés das abas de Acessos ficaram dentro da borda da tabela (no Figma ficam abaixo); na tela de sem permissão os pontos e o título 'terminal' vão para a direita e os números de linha não ficam apagados; o badge 'Sem papel' aparece como 'Sem Papel'.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 No desktop o cabeçalho não mostra o logo de mobile; no mobile ele continua aparecendo
- [x] #2 Rodapés 'N pessoas · página X de Y' e 'N convites pendentes · cada convite vale 7 dias' ficam abaixo da tabela, sem borda
- [x] #3 Terminal da tela de sem permissão tem pontos e 'terminal' à esquerda e números de linha apagados
- [x] #4 Badge 'Sem papel' aparece com p minúsculo
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
Tirar o display forçado do logo de mobile, mover rodapés para fora da tabela, ajustar pontos e números do terminal e o badge Sem papel.
<!-- SECTION:PLAN:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Corrigidos logo duplicado no cabeçalho, rodapés de Acessos, terminal da tela de sem permissão e capitalização de Sem papel (#211). Verificado em dev após o deploy (logo de mobile com display none no desktop, rodapé sem borda fora da tabela, terminal com pontos à esquerda, Sem papel) e por admin-spa em main (2026-10-11): lint ok, build ok, vitest 52 arquivos/313 testes.
<!-- SECTION:FINAL_SUMMARY:END -->
