---
id: TASK-22
title: Alinhar a tela de Vídeos às divergências da auditoria com o Figma
status: Done
assignee:
  - '@claude'
created_date: '2026-10-11 14:25'
updated_date: '2026-10-11 14:55'
labels:
  - frontend
dependencies: []
references:
  - >-
    https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=109-5071
  - videos-area-screen
  - admin-dashboard-components-dashboard-screen
modified_files:
  - src/admin-spa/src/features/videos/components/videos-area-screen.tsx
  - src/admin-spa/src/features/videos/components/video-transfer-panel.tsx
  - src/admin-spa/src/features/admin-dashboard/components/dashboard-screen.tsx
  - >-
    src/admin-spa/src/features/admin-dashboard/components/dashboard-screen.test.tsx
  - src/admin-spa/src/app/routes/videos-area-route.test.tsx
  - src/admin-spa/src/assets/videos.css
  - src/admin-spa/src/assets/app.css
  - src/admin-spa/src/assets/toast.css
type: bug
ordinal: 29000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Auditoria de 2026-10-11 comparou dev (conta de professor) com o Figma Screens — Vídeos (node 109:5071). A lista, os filtros e a busca já batiam; ficaram 12 divergências em estados secundários (sem permissão, selos, vazios, erro, envio incompleto, carregando, paginação, diálogos, painel de envio, card do Início) e no aviso de sucesso. Decisões do usuário: (A) alinhar o aviso compartilhado .app-toast ao componente Toast do DS (51:536) e usá-lo em Vídeos, o que também muda os avisos de Acessos, Cortesias e logout; (B) paginação como no Figma, páginas logo após o texto. Fora de escopo: botão de tema encostando em Backoffice no cabeçalho mobile (shell geral). Não alterar a troca de tema claro/escuro.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Vídeos sem permissão usa a tela compartilhada de sem permissão
- [x] #2 Selos Recebido e Pronto têm 20 px de altura; Em preparação e Falhou mantêm a borda
- [x] #3 Busca sem resultado, biblioteca vazia, erro ao carregar e envio incompleto seguem espaços, cantos e tipografia do Figma (V1.b, V1.c, V1.e, V4.a)
- [x] #4 Carregando usa linhas de 51 px com barras de 14 px nas larguras 360/120/120/90 (V1.d)
- [x] #5 Paginação fica 24 px abaixo da tabela com as páginas logo após o texto; ponto de atualização com 8 px
- [x] #6 Diálogos de enviar, editar título e sair durante o envio têm título 20/28 semibold e cantos de 16; Escolher arquivo e arquivo escolhido seguem V2.a e V2.c
- [x] #7 Painel de envio usa trilha muted, sombra leve, botão com cantos de 10 e reticências tipográficas (V3)
- [x] #8 Aviso de sucesso compartilhado segue o Toast do DS (14/20 médio, 16 de espaço, cantos de 12, 356 px, centralizado no mobile) e Vídeos o usa
- [x] #9 Card Vídeos do Início do professor diz 'Envie as gravações das aulas e acompanhe a preparação.'
- [x] #10 Lint, build e testes do admin-spa passam e as telas foram conferidas em dev
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. videos.css novo importado em app.css com overrides escopados (.videos-page, dialogs de vídeo) para selos, vazios, erro, envio incompleto, skeleton, paginação, ponto, diálogos e painel.
2. videos-area-screen: ForbiddenScreen compartilhada, Toast compartilhado, skeleton com 4 barras.
3. video-transfer-panel: reticências tipográficas.
4. toast.css: alinhar .app-toast ao Toast do DS (51:536).
5. get-staff-areas: texto do card Vídeos.
6. Ajustar testes, rodar lint/build/vitest; conferir em dev após o deploy.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Implementado: videos.css (selos, vazios, erro, envio incompleto, skeleton, paginação, ponto, diálogos, painel), Toast compartilhado alinhado ao DS e usado em Vídeos, tela de sem permissão compartilhada, reticências do painel e texto do card do Início. Diálogo de envio tinha 415 px porque o input de arquivo oculto herdava largura de .dialog-card input e criava rolagem horizontal; agora 400 px. Conferido por CSS injetado em dev antes do deploy: diálogo de envio 520x400, editar 520x262, selo Pronto 20 px, busca sem resultado 146 px, paginação 24 px abaixo com páginas a 12 px do texto, toast 356x54. Lint, build e vitest (52 arquivos, 313 testes) passam.

Conferência em dev após o deploy da #214 (conta de professor). As APIs de dev ficaram fora (502) por esgotamento de conexões no Postgres: a stack local --remote desta máquina segurava ~190 conexões ociosas. Com o ok do usuário, parei a stack local (scripts/apps.sh stop --remote) e encerrei as 191 conexões órfãs vindas de 192.168.0.3/.4 que o Postgres não soltou. Medidas em dev com o CSS publicado: selo Pronto 20 px; paginação 24 px abaixo, páginas a 12 px do texto; diálogo de envio 520x400 com título 20/28 w600 e cantos 16; Escolher arquivo 12/16 w500 cantos 10; editar título 520x262; busca sem resultado 146 px; erro ao carregar com 16 de espaço, cantos 12, texto a 28 do ícone, ação 12 px sem sublinhado; card Vídeos do Início com o texto novo. Amostras com a marcação dos componentes dentro de .videos-page: biblioteca vazia 266 px (40x32, intervalo 16, cantos 16, ícone 48 com cantos 12, título 20/28 w600); envio incompleto com 16 de espaço, cantos 12, título w500, texto a 28 do ícone; carregando com linhas de 51 e barras 360/120/120/90x14; painel com sombra SM e botão de cantos 10; toast 356x54, 14/20 w500, cantos 12, 24 px do rodapé. Sem permissão coberto pelo teste videos-area-route (a conta de professor tem a permissão).
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Tela de Vídeos alinhada ao Figma 109:5071: tela de sem permissão compartilhada, selos sem borda em Recebido/Pronto, estados vazio/sem resultado/erro/envio incompleto/carregando, paginação, ponto de atualização, diálogos (título 20/28 e cantos 16; diálogo de envio sem a rolagem horizontal que somava 15 px), painel de envio e texto do card no Início. O aviso compartilhado .app-toast passou a seguir o Toast do DS e Vídeos o usa (muda também Acessos, Cortesias e logout, por decisão do usuário). Verificado com lint, build e vitest (52 arquivos, 313 testes), CI da #214 verde e medidas em dev após o deploy, incluindo amostras dos estados que dependem de um envio real. Painel, envio incompleto e diálogo de sair não foram vistos num envio de verdade.
<!-- SECTION:FINAL_SUMMARY:END -->
