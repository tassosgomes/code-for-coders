---
id: TASK-16
title: Unificar shell e cabeçalho das telas de Autoria com Vídeos no SPA admin
status: In Progress
assignee: []
created_date: '2026-10-10 19:20'
updated_date: '2026-10-10 19:23'
labels:
  - frontend
  - bug
dependencies: []
type: bug
ordinal: 14000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Ao navegar entre Vídeos e Autoria no SPA admin, a casca muda: o alinhamento do início com o logo muda, a ordem entre Vídeos e Autoria na sidebar inverte em Autoria e no Início, e o botão '+ Novo curso' fica fora do padrão (centralizado verticalmente e dentro do container de 990px). As regras de shell de Autoria (authoring.css, seletor :has(.authoring-page)) e a ordenação condicional em app-shell.tsx vieram do alinhamento ao Figma (TASK-7). A tela de Vídeos é a referência de shell e de posição do botão de ação do cabeçalho.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Sidebar, logo e início têm o mesmo alinhamento e espaçamento em Vídeos e Autoria (desktop e mobile)
- [x] #2 A ordem de Vídeos e Autoria na sidebar é a mesma em todas as telas
- [ ] #3 O botão '+ Novo curso' fica alinhado à direita e na mesma linha do bloco de título, como 'Enviar vídeo' em Vídeos
- [x] #4 Lint, build e testes do admin-spa passam
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Remover a ordenação condicional de Vídeos/Autoria em app-shell.tsx: a sidebar segue a ordem de staff-areas em todas as telas, e o cabeçalho mobile mostra a marca do backoffice também em Autoria (sem o rótulo 'Autoria').
2. Remover de authoring.css as regras de shell escopadas em .app-shell:has(.authoring-page) (padding, gaps, margin-top, brand-mark, account-trigger, cabeçalho mobile) e o .authoring-mobile-label, para que Autoria herde o mesmo shell de Vídeos em app.css.
3. Promover as regras de cabeçalho mobile de .app-shell:has(.videos-page) em app.css para .app-shell, de modo que todas as telas do shell usem a mesma marca em telas pequenas.
4. Alinhar o cabeçalho de página .course-page-heading ao .page-heading-row de Vídeos: sem limite de 990px e com align-items: end, para que o botão '+ Novo curso' fique à direita e na mesma linha do título.
5. Verificar com lint, typecheck/build e testes do admin-spa; conferir visualmente desktop e mobile se o ambiente permitir.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Causa: a casca de Autoria vinha de regras .app-shell:has(.authoring-page) em authoring.css (padding da sidebar, gaps, margin-top, marca e conta), da ordenação condicional em app-shell.tsx que colocava Autoria antes de Vídeos e do limite de 990px em .course-page-heading. Tudo veio do alinhamento ao Figma (TASK-7).
Mudanças: removidas as regras de shell de Autoria; ordem da sidebar fixa pela ordem de staff-areas (Vídeos, Autoria) em todas as telas; cabeçalho mobile usa a marca do backoffice também em Autoria; regras mobile de cabeçalho de .app-shell:has(.videos-page) promovidas a .app-shell; .course-page-heading usa align-items: end, sem limite de 990px, como .page-heading-row.
Teste novo: admin-layout-route.test.tsx verifica Vídeos antes de Autoria em / e /autoria.
Validação: lint e typecheck sem erros; build ok; vitest 254 testes passando (47 arquivos).
Pendente: AC 1 e 3 são visuais. Não foi possível subir a stack completa aqui (gateway em 8080 fora do ar; 8081 serve bundle antigo do container). Conferir Vídeos e Autoria em desktop e mobile antes de concluir.
<!-- SECTION:NOTES:END -->
