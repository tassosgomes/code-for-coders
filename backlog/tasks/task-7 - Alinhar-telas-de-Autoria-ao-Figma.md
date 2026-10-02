---
id: TASK-7
title: Alinhar telas de Autoria ao Figma
status: Done
assignee:
  - '@codex'
created_date: '2026-10-02 20:59'
updated_date: '2026-10-02 22:12'
labels:
  - frontend
  - design
dependencies: []
references:
  - 'https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn?node-id=155-7451'
documentation:
  - docs/validation/autoria-figma.md
modified_files:
  - src/admin-spa/src/app/routes/audit-trail-list.test.tsx
  - src/admin-spa/src/app/routes/authoring-course-route.tsx
  - src/admin-spa/src/app/routes/authoring-courses-route.tsx
  - src/admin-spa/src/app/routes/authoring-courses.test.tsx
  - src/admin-spa/src/app/routes/authoring-delete.test.tsx
  - src/admin-spa/src/app/routes/authoring-publish.test.tsx
  - src/admin-spa/src/app/routes/authoring-version-route.tsx
  - src/admin-spa/src/app/routes/authoring-versions.test.tsx
  - src/admin-spa/src/app/routes/authoring-video-picker.test.tsx
  - src/admin-spa/src/app/routes/videos-area-route.test.tsx
  - src/admin-spa/src/assets/app.css
  - src/admin-spa/src/components/app-shell.tsx
  - src/admin-spa/src/components/ui/dialog.tsx
  - src/admin-spa/src/components/ui/form/single-choice-form.tsx
  - src/admin-spa/src/components/ui/form/text-details-form.tsx
  - >-
    src/admin-spa/src/features/admin-dashboard/components/dashboard-screen.test.tsx
  - src/admin-spa/src/features/admin-dashboard/components/dashboard-screen.tsx
  - src/admin-spa/src/features/course-authoring/components/course-curriculum.tsx
  - >-
    src/admin-spa/src/features/course-authoring/components/course-edit-dialog.tsx
  - src/admin-spa/src/features/course-authoring/components/course-history.tsx
  - >-
    src/admin-spa/src/features/course-authoring/components/course-level-notice.tsx
  - >-
    src/admin-spa/src/features/course-authoring/components/course-prerequisite.tsx
  - >-
    src/admin-spa/src/features/course-authoring/components/course-publication.tsx
  - >-
    src/admin-spa/src/features/course-authoring/components/course-recommendation-picker.tsx
  - >-
    src/admin-spa/src/features/course-authoring/components/course-video-picker.tsx
  - >-
    src/admin-spa/src/features/course-authoring/components/create-course-dialog.tsx
  - src/admin-spa/src/stores/use-shell-store.ts
  - docs/validation/autoria-figma.md
  - docs/validation/autoria-figma/criar-curso.png
  - docs/validation/autoria-figma/descartar.png
  - docs/validation/autoria-figma/editar-mobile.png
  - docs/validation/autoria-figma/editor-dark.png
  - docs/validation/autoria-figma/editor-desktop.png
  - docs/validation/autoria-figma/editor-mobile.png
  - docs/validation/autoria-figma/historico.png
  - docs/validation/autoria-figma/inicio-professor.png
  - docs/validation/autoria-figma/lista-desktop.png
  - docs/validation/autoria-figma/lista-erro.png
  - docs/validation/autoria-figma/lista-mobile.png
  - docs/validation/autoria-figma/lista-vazia.png
  - docs/validation/autoria-figma/publicacao.png
  - docs/validation/autoria-figma/seletor-cursos.png
  - docs/validation/autoria-figma/seletor-video-mobile.png
  - docs/validation/autoria-figma/seletor-video.png
  - docs/validation/autoria-figma/versao-publicada.png
  - src/admin-spa/src/assets/authoring.css
  - src/admin-spa/src/components/ui/dialog.test.tsx
  - src/admin-spa/src/features/course-authoring/components/course-pagination.tsx
type: enhancement
ordinal: 6000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
As telas implementadas de Autoria precisam reproduzir o desenho aprovado na página Screens — Autoria do Design System Code4Coders, mantendo os fluxos existentes.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Lista, editor, diálogos, seletores e versões seguem a hierarquia, componentes e espaçamentos do Figma.
- [x] #2 Layouts mobile e tema escuro permanecem utilizáveis, com ações e textos acessíveis.
- [x] #3 Lint, build e testes de Autoria passam; comparação visual dos fluxos é registrada.
- [x] #4 O início do professor (A12) segue os cartões, textos e navegação do frame aprovado.
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Comparar os frames de Autoria com rotas, componentes e CSS existentes. 2. Ajustar hierarquia do editor, listagem, cartões, diálogos e seletores reutilizando os componentes e tokens atuais. 3. Conferir desktop, mobile e Dark com Playwright. 4. Executar lint, build e testes da SPA e registrar evidências.

5. Alinhar o início do professor A12 e estados vazios/erro da lista. 6. Corrigir e verificar foco dos diálogos, navegação das abas e cópia de referências.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Groma não está inicializado na worktree; não foram inventados IDs arquiteturais. Referências Figma mapeiam ícones para lucide-react, reutilizados sem novos assets rasterizados.

Incluído o frame A12 (Início do professor), presente na mesma página Screens — Autoria, no alinhamento visual.

Validação em desenv-server: lint e build aprovados; 235 testes aprovados em 44 arquivos. Playwright conferiu desktop 1440x900, mobile 390x844, larguras 320/768/1024, Light/Dark, teclado, diálogos, seletores e recuperação de erro com APIs simuladas. Evidências e limites: docs/validation/autoria-figma.md.

Preparação da PR: o commit foi reaplicado sobre origin/main f0db614 para excluir os commits de Cortesias ainda não integrados. Revalidação em desenv-server: lint e build aprovados, 209 testes aprovados em 39 arquivos. Playwright confirmou edição, Escape, histórico por teclado e início do professor nessa base.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Alinhadas lista e estados vazios/erro, editor e público do curso, diálogos, seletores, histórico, versão publicada e início do professor aos frames Screens — Autoria do Figma. Ajustados tokens Light/Dark, composição mobile e acessibilidade de abas, foco e cópia. Lint/build e 209 testes aprovados sobre main; comparação visual registrada em docs/validation/autoria-figma.md com 17 capturas. Branch feature/figma-screen-alignment contém somente esta entrega.
<!-- SECTION:FINAL_SUMMARY:END -->
