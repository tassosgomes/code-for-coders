---
id: TASK-31.2
title: >-
  Aula do aluno: link de volta, título, painel com abas, lista de aulas e menu
  lateral conforme Figma
status: In Progress
assignee:
  - '@claude'
created_date: '2026-10-13 19:54'
updated_date: '2026-10-13 20:05'
labels:
  - frontend
dependencies: []
references:
  - >-
    https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=343-368
  - >-
    https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=273-36456
  - >-
    https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=337-64
  - student-spa
modified_files:
  - >-
    src/student-spa/src/features/student-lessons/components/student-lesson-screen.tsx
  - >-
    src/student-spa/src/features/student-lessons/components/student-lesson-nav.tsx
  - >-
    src/student-spa/src/features/student-lessons/components/lesson-transcript-panel.tsx
  - src/student-spa/src/components/app-shell.tsx
  - src/student-spa/src/assets/app.css
parent_task_id: TASK-31
priority: medium
type: bug
ordinal: 40000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Coluna da aula (257:442), painel de transcrição TA1 (343:631), LessonList+Progress (273:36456), LessonItem (256:89), ModuleHeader (256:98) e TranscriptSegment (337:64). O menu lateral hoje acaba logo após os itens.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Link 'Voltar para o Início' com ícone arrow-left 16, 14/20 peso 500 em primary, gap 6; título em H2 (30/36); coluna com gap 16
- [ ] #2 Painel à direita em card (borda, raio 12, padding 16, gap 12) com abas segmentadas (ativa em fundo background com sombra sm)
- [ ] #3 Lista de aulas: sobretítulo 'Aulas do curso', progresso '37% · 3 de 8 aulas' 14/20 muted e barra de 8 px; módulos como acordeão com chevron; itens com marcador de 28 px (atual: play em primary; concluída: ✓ em success-soft), número mono 12, título 14/20 e 'Aula atual' 12/16
- [ ] #4 Transcrição: rótulo 'Buscar na aula' acima do campo de 40 px com ícone de busca e placeholder 'Palavra ou trecho'; trechos com barra de 3 px, play 12 no atual, tempo mono 12/18 e texto 14/20
- [ ] #5 Menu lateral do aluno vai até o fim da página, como no admin
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Ler a coluna da aula (257:442), TA1.a (343:631), LessonList+Progress (273:36456) e TranscriptSegment (337:64).
2. Link de volta com ícone, título H2, painel em card com abas segmentadas, lista com acordeão e marcadores, trechos da transcrição com barra e play.
3. Menu lateral do aluno esticado até o fim da página.
4. Testes e prévia local; conferência em dev.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Prévia local: painel de 360 com a altura da coluna (839 px) e rolagem interna; abas segmentadas; lista com sobretítulo, "0% · 0 de 2 aulas", barra de 8 px, Módulo 1 · Fundamentos com chevron, aula atual com marcador play em primary e "Aula atual"; transcrição com rótulo, lupa, placeholder "Palavra ou trecho" e trecho atual com barra de 3 px e play 12. Menu lateral com 971 px, a altura da página.
Decisões: "Voltar para o Início" continua (G9 do PRD de progresso), só ganha o visual do Figma; dentro do painel a lista fica sem borda própria, para não repetir card dentro de card como no TA1.i; todos os módulos começam abertos (o acordeão pode fechar), para nenhuma aula ficar escondida; o número da aula entra no nome do link só para leitor de tela ("2. Persistência").
Não entra aqui: estados de carregamento e mensagens no lugar do player (A1.d, A1.f a A1.n, TA1.d a TA1.h).
<!-- SECTION:NOTES:END -->
