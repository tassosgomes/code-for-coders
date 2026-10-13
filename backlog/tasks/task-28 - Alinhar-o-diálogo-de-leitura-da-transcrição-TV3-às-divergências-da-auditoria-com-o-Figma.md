---
id: TASK-28
title: >-
  Alinhar o diálogo de leitura da transcrição (TV3) às divergências da auditoria
  com o Figma
status: Done
assignee:
  - '@claude'
created_date: '2026-10-13 14:48'
updated_date: '2026-10-13 15:19'
labels:
  - frontend
dependencies: []
references:
  - >-
    https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=338-11
  - docs/design/wireframes-transcricao.md
  - admin-spa
modified_files:
  - src/admin-spa/src/features/videos/components/video-transcription-reader.tsx
  - src/admin-spa/src/assets/videos.css
  - src/admin-spa/src/app/routes/video-transcription-reader.test.tsx
priority: medium
type: bug
ordinal: 35000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
A auditoria do Figma (seção TV3 · Ler a transcrição, node 338:11) contra o código entregue na CAP-032 (PR #225) encontrou divergências visuais no diálogo somente leitura do backoffice: cabeçalho do vídeo, lista de trechos, Alert info, skeleton, estado vazio, Alert de erro e mobile. O comportamento (RF-08, RN-TL07/08) não muda.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Cabeçalho: nome do vídeo em 14/20 peso 500 na cor de texto, a 16 px do título, e a meta 'Pronta em … · somente leitura' 12/16 muted logo abaixo (2 px)
- [x] #2 Lista: região com borda e raio 8 e padding 4; linhas sem divisória, padding 10/12, gap 16; tempo em JetBrains Mono 12/18 muted numa coluna de 64 px; texto 14/20
- [x] #3 Alert info de nova em andamento: fundo accent-subtle, borda padrão, ícone Info 16 e texto 14/20 na cor de texto
- [x] #4 Carregando: 8 linhas de skeleton (tempo 48 px + texto de larguras variadas, barras de 14 px com raio 4) dentro da região com borda, e o texto visível 'Carregando a transcrição…'
- [x] #5 Ainda não há transcrição pronta: EmptyState centralizado com círculo de 48 px e ícone FileMinus 24, texto 14/20 muted
- [x] #6 Erro de carga: Alert destructive com CircleAlert 16, mensagem na cor danger e botão outline pequeno (32 px) 'Tentar de novo' dentro do Alert
- [x] #7 Mobile (≤ 640 px): tela cheia com padding 16 e gap 12, X de 24 px, tempo acima do texto (texto 16/24) e Fechar com 48 px na largura toda
- [x] #8 Lint, build e testes do admin-spa passam; conferência em dev
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Ler TV3 (338:11) no Figma e o video-transcription-reader.tsx/videos.css.
2. Ajustar marcação: Alert info com ícone, EmptyState com FileMinus, Alert destructive com CircleAlert e Tentar de novo dentro, texto visível no carregamento.
3. Reescrever o CSS do TV3 (cabeçalho do vídeo, região com borda, linhas, skeleton, mobile).
4. Ampliar o teste do leitor; lint, build e testes; conferir em página de teste local e em dev.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Conferido em página de teste local (CSS do build + marcação dos estados a–e e mobile 390) contra os screenshots do Figma.
Meta 'Pronta em …' só existe quando a transcrição carrega (readyAt vem da resposta); no carregamento o Figma a mostra, o código não tem o dado ainda.
O estado 'ainda não há transcrição pronta' continua com role=alert (teste existente); o visual agora é EmptyState neutro.
Lint, build e 394 testes do admin-spa passam.

PR #227 aberta.

PR #227 mergeada em 2026-10-13 (9bc757f) e publicada em dev. Conferência em dev como professor, com o vídeo '0101 - Forca Intro': diálogo de 720 px com gap 16; nome do vídeo em rgb(22,19,31) e peso 500; meta 'Pronta em 13/10, 10:56 · somente leitura'; região com borda de 1 px e raio 8; linhas de 40 px com padding 10/12, texto de 14 px e sem divisória; tempo em JetBrains Mono numa coluna de 64 px. Os estados b–e e o mobile 390 não têm como ser provocados em dev sem mexer em dados; foram conferidos na página de teste local com o CSS do build e cobertos pelos testes do leitor.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Alinhado o diálogo de leitura da transcrição (TV3, Figma 338:11) ao Figma: cabeçalho do vídeo, região com borda, linhas com tempo em mono, Alert info, skeleton com texto visível, EmptyState, Alert destructive com Tentar de novo e mobile em tela cheia. Só visual; o comportamento de RF-08 e RN-TL07/08 não mudou. Verificado com lint, build e os 394 testes do admin-spa, com a página de teste local (estados a–f e mobile 390) e, depois do deploy, em dev com o vídeo '0101 - Forca Intro' (medidas computadas conferidas). PR #227.
<!-- SECTION:FINAL_SUMMARY:END -->
