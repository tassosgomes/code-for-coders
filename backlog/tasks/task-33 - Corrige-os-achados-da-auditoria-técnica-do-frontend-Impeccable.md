---
id: TASK-33
title: Corrige os achados da auditoria técnica do frontend (Impeccable)
status: In Progress
assignee:
  - '@claude'
created_date: '2026-10-13 23:20'
updated_date: '2026-10-13 23:41'
labels:
  - frontend
  - a11y
  - performance
dependencies: []
ordinal: 46000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
A auditoria técnica do Impeccable de 2026-10-13 (nota 14/20) apontou nos dois SPAs: bundle único sem divisão por rota (aluno 497 KB gzip com hls.js em todas as telas), contraste da borda de input 1,29:1 (WCAG 1.4.11), skeleton da auditoria sem prefers-reduced-motion, áreas de toque de 24–32 px no admin, cores de status fora da paleta do DESIGN.md, sombras repetidas sem token, fonte fixa no CSS do Financeiro, fontes do admin via @import, borda igual ao fundo no tema escuro do admin, :focus no lugar de :focus-visible e @testing-library/dom em dependencies. O admin também tem um sistema de tokens próprio não documentado no DESIGN.md.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 student-spa e admin-spa carregam as telas por rota sob demanda e hls.js só é baixado na tela de aula
- [x] #2 Fontes do admin carregadas por <link> com preconnect no index.html
- [x] #3 Skeletons com animação respeitam prefers-reduced-motion
- [x] #4 Controles interativos do Financeiro e das Cortesias têm pelo menos 44 px no celular
- [x] #5 Cores de status do admin usam a paleta do DESIGN.md e o tema escuro tem borda distinta do fundo
- [x] #6 Sombras do admin vêm de tokens e o Financeiro não fixa a fonte no CSS
- [x] #7 Foco de módulos e aulas usa :focus-visible
- [x] #8 Borda de input atinge 3:1 nos dois SPAs
- [x] #9 @testing-library/dom fica em devDependencies nos dois SPAs
- [x] #10 DESIGN.md documenta o sistema de tokens do admin
- [x] #11 Lint, tsc, build e testes passam nos dois SPAs
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Sonnet (student-spa): rotas com lazy do react-router e hls.js por import dinâmico.
2. Sonnet (admin-spa): rotas com lazy do react-router.
3. Sonnet (admin-spa CSS): fontes via <link>, tokens de sombra, status na paleta, borda escura, reduced-motion nos skeletons, :focus-visible, fonte fixa no Financeiro, toque de 44 px no celular em Financeiro e Cortesias.
4. Haiku: @testing-library/dom para devDependencies e DESIGN.md com o sistema do admin.
5. Borda de input 3:1: decisão de valor com o usuário (afeta o Figma).
6. Verificação: lint, tsc, build, vitest nos dois SPAs e detector do Impeccable.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Orquestrado com 3 agentes Sonnet (rotas do aluno, rotas do admin, CSS do admin) e 1 Haiku (token do aluno, devDependencies, DESIGN.md).
- Entrada do aluno: 497 → 174 KB gzip; hls.js em chunk próprio (179 KB gzip), baixado só na aula. Entrada do admin: 318 → 116 KB gzip.
- Borda de input: usuário escolheu ink/400 no claro (2,91:1, exceção consciente registrada no DESIGN.md) e ink/500 no escuro (3,69:1). Todos os campos do admin passaram a usar --input-border.
- Admin: tokens --shadow-xs/sm/md/overlay/dialog/sheet e --font-sans/heading/mono em theme.css; status na paleta; --border escuro ink/700 e --border-strong #4b4560.
- Falha ao baixar hls.js mostra 'Não foi possível iniciar a aula.' (não culpa o navegador).
- Verificação: lint, tsc, vitest (aluno 231, admin 396, duas rodadas padrão) e build nos dois SPAs; detector do Impeccable só aponta a marca de citação de .audit-detail-reason (legítima). Conferência visual local dos logins nos dois temas.
- Pendente fora do código: atualizar a variável input no Figma.
<!-- SECTION:NOTES:END -->
