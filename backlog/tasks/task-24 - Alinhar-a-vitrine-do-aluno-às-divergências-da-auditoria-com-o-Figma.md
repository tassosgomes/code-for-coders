---
id: TASK-24
title: Alinhar a vitrine do aluno às divergências da auditoria com o Figma
status: In Progress
assignee:
  - '@claude'
created_date: '2026-10-11 15:40'
updated_date: '2026-10-11 15:55'
labels:
  - frontend
dependencies: []
references:
  - >-
    https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=205-5898
  - student-spa
modified_files:
  - src/student-spa/src/components/public-layout.tsx
  - >-
    src/student-spa/src/features/student-showcase/components/showcase-pagination.tsx
  - >-
    src/student-spa/src/features/student-showcase/components/showcase-level-filter.tsx
  - >-
    src/student-spa/src/features/student-showcase/components/showcase-course-card.tsx
  - >-
    src/student-spa/src/features/student-showcase/components/student-showcase-screen.tsx
  - >-
    src/student-spa/src/features/student-showcase/components/student-course-page.tsx
  - src/student-spa/src/features/student-showcase/components/offer-option.tsx
  - >-
    src/student-spa/src/features/student-showcase/components/student-showcase-screen.test.tsx
  - >-
    src/student-spa/src/features/student-showcase/components/student-course-page.test.tsx
type: bug
ordinal: 31000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Auditoria de 2026-10-11 comparou a vitrine pública de dev (/students/cursos) com o Figma Screens — Catálogo e vitrine (node 205:5898, seções P1, P2 e P4). A estrutura bate, mas tipografia, espaçamentos, filtro, cartão, paginação e estados vazios/erro/carregando divergem. P3 (aviso de compra em breve) foi superado pela compra avulsa (CAP-011) e fica fora. Não alterar a troca de tema claro/escuro.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Cabeçalho da vitrine e da página do curso: título 36/42 (-1), subtítulo 18/28, intervalo 12, conteúdo com 48 px no topo
- [ ] #2 Filtro de nível em cartões de rádio de 180 px (P1.a) e escondido quando a vitrine não tem nenhum curso (P1.d)
- [ ] #3 Cartão (ShowcaseCard) com miniatura de 160 px e ícone de 48, corpo com intervalo 8, título 16/24, preço 14 semibold, opções 12/16 e Ver curso → em linha própria; grade com intervalo de 32
- [ ] #4 Paginação '‹ 1 2 › · 12 por página' alinhada à direita
- [ ] #5 Vazio com filtro (P1.c), vitrine vazia (P1.d), carregando (P1.e) e erro (P1.f) seguem o Figma: cards centralizados, skeleton em cartões com miniatura, alerta com 'Tente de novo em instantes.' e Tentar de novo como link
- [ ] #6 Página do curso (P2): colunas com 48 de intervalo, opções em 360 px com título 16/24, módulos em cards com contagem de aulas à direita; carregando, erro (P2.c, P2.d) e indisponível (P4, centralizado) seguem o Figma
- [ ] #7 Item Cursos do cabeçalho público em destaque (primary) nas páginas da vitrine
- [ ] #8 Mobile 390 (P1.h, P2.e, P4.m) sem rolagem horizontal
- [ ] #9 Lint, build e testes do student-spa passam e as telas foram conferidas em dev
- [ ] #10 Rodapé público inverso de 64 px com '© Code4Coders · cursos de programação' alinhado ao conteúdo (P1.a)
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. public-layout: largura de 1120 de conteúdo, cabeçalho de 72, Cursos ativo em primary (NavLink), main com 48 no topo, rodapé inverso de 64.
2. Vitrine: cabeçalho 36/42 e 18/28, filtro em cartões de rádio, filtro e contagem escondidos sem cursos, cartão ShowcaseCard, grade com 32, paginação compacta, vazios em cartão centralizado, skeleton com miniatura, alerta com Tentar de novo como link.
3. Página do curso: título 36/42, colunas com 48 e 360, seções em H4, opções em H5, módulos em cartões com seta à esquerda e contagem à direita, OfferOption do DS, carregando/erro/indisponível do Figma.
4. Ajustar testes; lint, tsc, build e vitest; conferir em dev após o deploy.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Implementado: layout público (conteúdo de 1120, cabeçalho 72, Cursos ativo em primary, 48 no topo, rodapé inverso de 64 sem o ano), vitrine (cabeçalho 36/42 e 18/28, filtro em cartões de rádio de 180, filtro e contagem escondidos quando a vitrine não tem curso, ShowcaseCard com miniatura 160 e ícone 48 em code-accent, grade com 32 e cartões sem esticar, paginação '‹ 1 2 › · 12 por página' à direita no desktop e à esquerda no mobile, vazios em cartão centralizado, skeleton com miniatura, alerta com 'Tente de novo em instantes.' e Tentar de novo como link) e página do curso (título 36/42, colunas 48 e 360, H4 nas seções, H5 em Opções de acesso, módulos em cartões com seta à esquerda e contagem à direita, OfferOption com preço H4, carregando do P2.c, erro do P2.d, indisponível centralizado). Fora de escopo: o CodeWindow compartilhado usa ícone no lugar das três bolinhas do Figma (bloco usado em outras telas). Lint, tsc, build e vitest (30 arquivos, 188 testes) passam.
<!-- SECTION:NOTES:END -->
