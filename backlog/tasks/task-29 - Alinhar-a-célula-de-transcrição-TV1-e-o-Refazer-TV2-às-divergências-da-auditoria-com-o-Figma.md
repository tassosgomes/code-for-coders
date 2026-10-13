---
id: TASK-29
title: >-
  Alinhar a célula de transcrição (TV1) e o Refazer (TV2) às divergências da
  auditoria com o Figma
status: Done
assignee:
  - '@claude'
created_date: '2026-10-13 15:34'
updated_date: '2026-10-13 18:29'
labels:
  - frontend
dependencies: []
references:
  - >-
    https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=338-9
  - >-
    https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=338-10
  - docs/design/wireframes-transcricao.md
  - admin-spa
modified_files:
  - src/admin-spa/src/features/videos/components/video-transcription-status.tsx
  - src/admin-spa/src/features/videos/components/video-status-badge.tsx
  - src/admin-spa/src/assets/videos.css
priority: medium
type: bug
ordinal: 36000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Em dev, as ações da coluna Transcrição da tela Vídeos (Transcrever, Ler, Refazer) aparecem como links sublinhados empilhados, e não como os botões do Figma (seções TV1 338:9 e TV2 338:10). A auditoria também achou diferenças no selo de estado, nos textos de apoio e no estado Pedindo do Refazer. O comportamento (RF-02, RN-TL07) não muda.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Ações da célula como botões sm de 32 px em linha com gap 8: Ler, Refazer e Tentar de novo em outline; Transcrever em primário
- [x] #2 Mobile (VideoCard): as mesmas ações em botões lg de 48 px (16/24 peso 500, padding 24)
- [x] #3 Selo de transcrição: ícone de 12 px; Pronta com Check; Transcrevendo com fundo surface, borda e texto info; padding 2/8
- [x] #4 Textos de apoio 12/16 muted; o motivo da falha em danger, antes de 'A anterior continua valendo para os alunos.'
- [x] #5 TV2: o botão Refazer, enquanto pede, mostra o ícone de carregamento girando antes de 'Pedindo…'
- [x] #6 Lint, build e testes do admin-spa passam; conferência em dev
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Ler TV1 (338:9: 338:17, 339:157, 339:41463) e TV2 (338:10) no Figma e comparar com video-transcription-status.tsx, video-status-badge.tsx e videos.css.
2. Trocar os links por Button sm (outline / primário) num grupo de ações; Button lg no mobile.
3. Ajustar o selo (ícone 12, Check, Transcrevendo info), os textos de apoio (16 de linha, motivo em danger) e o spinner do Pedindo.
4. Lint, build e testes; prévia em dev com o CSS do build injetado no navegador; conferência em dev depois do deploy.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Prévia em dev (CSS do build injetado só no navegador, sem gravar nada): Ler 44×32, Refazer 70×32 e Transcrever em primário, 12 px peso 500; selo de 20 px com padding 2/8; linha da tabela com 105 px, igual à TV1.a.
Achado na prévia: regressão da PR #222 (TASK-25). A célula do título virou flex em coluna e herdou align-items:center de app.css, o que centralizava título e tags. Corrigido aqui com align-items:flex-start em videos.css.
Lint, build e 394 testes do admin-spa passam.

PR #228 aberta.

Conferência da TASK-25 em dev achou também: sugestões do filtro de tags no fluxo, empurrando a tabela cerca de 47 px. Viraram popover absoluto alinhado ao campo (prévia em dev: 4 px abaixo do campo, mesma borda esquerda, tabela parada). Lint e 394 testes passam.

PR #228 mergeada em 2026-10-13 (c9d6981) e publicada em dev (index-B-W4AwWt.css). Conferência em dev como professor:
- Desktop: Ler 44×32 e Refazer 70×32 em outline, na mesma linha; Transcrever 95×32 em primário, 12 px peso 500, sem sublinhado.
- Selo Pronta: 20 px, padding 2/8, lucide-check de 12 px. Textos de apoio com 16 de linha. Título alinhado à esquerda. Linhas com 105 px (Pronta) e 85 px (Sem transcrição), como na TV1.a.
- Mobile 390 (iframe na própria página): rótulo Transcrição visível, divisória e botões de 48 px, 16 px peso 500, padding 24.
- Transcrevendo e motivo de falha: não há vídeos nesses estados em dev; conferidos por estilo computado de elementos de prova na própria página (Transcrevendo com fundo branco e borda/texto rgb(37,99,235); motivo de falha em rgb(220,38,38)).
- TV2.b: teste temporário (não commitado) com o pedido pendurado confirmou o svg .video-status-spinner antes de 'Pedindo…' e o Cancelar desabilitado.
- Sugestões do filtro de tags: popover absoluto, 4 px abaixo do campo, mesma borda esquerda, tabela parada.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Alinhadas a célula de transcrição (TV1) e a confirmação do Refazer (TV2) ao Figma. As ações viraram Button sm em linha (lg no mobile); o selo usa ícone de 12 px, Check em Pronta e Transcrevendo em info; os textos de apoio passaram a 12/16, com o motivo da falha em danger; o Pedindo ganhou spinner. Na mesma PR foram corrigidas duas regressões da #222 achadas na conferência da TASK-25: título e tags centralizados na tabela, e sugestões do filtro empurrando a tabela. Verificado com lint, build e os 394 testes do admin-spa, um teste temporário do Pedindo e conferência em dev depois do deploy (desktop, mobile 390 em iframe e estilos computados). PR #228.
<!-- SECTION:FINAL_SUMMARY:END -->
