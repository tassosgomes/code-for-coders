---
id: TASK-22
title: Alinhar a tela de Vídeos às divergências da auditoria com o Figma
status: In Progress
assignee:
  - '@claude'
created_date: '2026-10-11 14:25'
updated_date: '2026-10-11 14:31'
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
- [ ] #1 Vídeos sem permissão usa a tela compartilhada de sem permissão
- [ ] #2 Selos Recebido e Pronto têm 20 px de altura; Em preparação e Falhou mantêm a borda
- [ ] #3 Busca sem resultado, biblioteca vazia, erro ao carregar e envio incompleto seguem espaços, cantos e tipografia do Figma (V1.b, V1.c, V1.e, V4.a)
- [ ] #4 Carregando usa linhas de 51 px com barras de 14 px nas larguras 360/120/120/90 (V1.d)
- [ ] #5 Paginação fica 24 px abaixo da tabela com as páginas logo após o texto; ponto de atualização com 8 px
- [ ] #6 Diálogos de enviar, editar título e sair durante o envio têm título 20/28 semibold e cantos de 16; Escolher arquivo e arquivo escolhido seguem V2.a e V2.c
- [ ] #7 Painel de envio usa trilha muted, sombra leve, botão com cantos de 10 e reticências tipográficas (V3)
- [ ] #8 Aviso de sucesso compartilhado segue o Toast do DS (14/20 médio, 16 de espaço, cantos de 12, 356 px, centralizado no mobile) e Vídeos o usa
- [ ] #9 Card Vídeos do Início do professor diz 'Envie as gravações das aulas e acompanhe a preparação.'
- [ ] #10 Lint, build e testes do admin-spa passam e as telas foram conferidas em dev
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
<!-- SECTION:NOTES:END -->
