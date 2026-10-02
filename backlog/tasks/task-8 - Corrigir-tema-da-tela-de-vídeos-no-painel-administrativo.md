---
id: TASK-8
title: Corrigir tema da tela de vídeos no painel administrativo
status: Done
assignee:
  - '@codex'
created_date: '2026-10-02 22:16'
updated_date: '2026-10-02 22:28'
labels:
  - frontend
  - bug
dependencies: []
modified_files:
  - src/admin-spa/src/assets/app.css
type: bug
ordinal: 6000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Ao abrir a tela de vídeos, o conteúdo aparece escuro enquanto o restante do painel está claro. Investigar os estilos da biblioteca e dos diálogos para manter a aparência consistente com o painel.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 A biblioteca de vídeos usa as mesmas superfícies claras e cores legíveis do painel administrativo.
- [x] #2 Filtros, estados e diálogos de vídeos mantêm contraste e consistência visual com o painel.
- [x] #3 Lint, build e testes relevantes passam, com validação visual no navegador.
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Reproduzir a divergência no navegador com preferência escura e comparar com o painel claro.
2. Remover a media query exclusiva de vídeos, preservando os estilos claros existentes.
3. Validar biblioteca, filtros e diálogos em desktop/mobile com preferências clara/escura; executar lint, build e testes da SPA administrativa.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Causa identificada: @media(prefers-color-scheme:dark) em src/admin-spa/src/assets/app.css altera o shell e todos os tokens de vídeos. Groma não está inicializado nesta worktree; registrar arquivos modificados sem inventar referências arquiteturais.

Reproduzido no Chromium com preferência escura: o painel mantém texto rgb(22,19,31), mas vídeos altera o fundo para rgb(36,32,51) e o texto para rgb(248,247,252). Removida a media query que aplicava essa troca somente em vídeos.

Validação de CSS computado no Chromium aprovada em 1440px e 390px, com prefers-color-scheme light/dark: fundo #f8f7fc, superfícies #fff, texto #16131f na biblioteca, filtros e diálogos de edição/envio. Capturas em .playwright-mcp/videos-theme-{desktop,mobile}.png na worktree original. APIs simuladas somente no navegador; nenhuma escrita em dados reais. Lint sem erros (aviso preexistente de react-hooks/incompatible-library); build aprovado com avisos existentes de runtime-env e tamanho do bundle. A primeira suíte teve 229/231 testes aprovados; dois falharam por faltar api-contract.yaml na cópia remota. Corrigido o layout remoto e adicionada a fixture necessária; suíte completa em repetição.

Suíte completa repetida com estrutura correta e contrato presente: npm test -- --maxWorkers=2 --reporter=dot encerrou com código 0 no desenv-server (192.168.0.5). Lint e build passaram. git diff --check passou. Conferidas capturas desktop/mobile; a tela mantém o padrão claro mesmo com preferência escura no navegador.

Preparação da PR: commit isolado sobre origin/main (304e67d), removendo do histórico da branch os commits da entrega de cortesias. Validação repetida na base atualizada: lint aprovado sem erros/avisos, build aprovado e 209 testes em 39 arquivos aprovados no desenv-server (77,69s). Playwright repetido em 1440px/390px com preferências light/dark e diálogos de edição/envio: todas as combinações aprovadas. A contagem anterior de 231 testes corresponde à base original feature/concessao-acesso; a PR usa a suíte atual de main.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Removida a media query prefers-color-scheme:dark que mudava somente vídeos e seu shell para escuro. Biblioteca, filtros e diálogos mantêm o tema claro do painel. PR isolada sobre main, com validação repetida por Playwright em desktop/mobile com preferências light/dark, lint, build e 209 testes em 39 arquivos aprovados no servidor de testes.
<!-- SECTION:FINAL_SUMMARY:END -->
