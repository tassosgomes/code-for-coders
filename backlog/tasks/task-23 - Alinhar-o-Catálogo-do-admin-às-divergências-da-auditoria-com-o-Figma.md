---
id: TASK-23
title: Alinhar o Catálogo do admin às divergências da auditoria com o Figma
status: In Progress
assignee:
  - '@claude'
created_date: '2026-10-11 15:40'
updated_date: '2026-10-11 15:51'
labels:
  - frontend
dependencies: []
references:
  - >-
    https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=205-5898
  - admin-spa
modified_files:
  - src/admin-spa/src/assets/catalog.css
  - src/admin-spa/src/features/catalog-courses/components/catalog-badges.tsx
  - src/admin-spa/src/features/catalog-courses/components/catalog-alert.tsx
  - src/admin-spa/src/features/catalog-courses/components/catalog-pagination.tsx
  - src/admin-spa/src/features/catalog-courses/api/get-catalog-courses.ts
  - >-
    src/admin-spa/src/features/catalog-courses/components/catalog-courses-screen.tsx
  - >-
    src/admin-spa/src/features/catalog-courses/components/catalog-course-record-screen.tsx
  - >-
    src/admin-spa/src/features/catalog-courses/components/catalog-tagline-form.tsx
  - src/admin-spa/src/features/catalog-courses/hooks/use-catalog-tagline.ts
  - src/admin-spa/src/features/catalog-courses/components/catalog-offers.tsx
  - >-
    src/admin-spa/src/features/catalog-courses/components/catalog-offer-actions.tsx
  - src/admin-spa/src/features/catalog-courses/types/catalog-offer.ts
  - src/admin-spa/src/features/catalog-courses/hooks/use-offer-write.ts
  - src/admin-spa/src/features/catalog-courses/hooks/use-offer-publication.ts
  - src/admin-spa/src/features/catalog-courses/hooks/use-offer-unpublication.ts
  - src/admin-spa/src/features/catalog-courses/components/catalog-offer-form.tsx
  - >-
    src/admin-spa/src/features/catalog-courses/components/catalog-offer-change-confirmation.tsx
  - >-
    src/admin-spa/src/features/catalog-courses/components/catalog-offer-publication.tsx
  - >-
    src/admin-spa/src/features/catalog-courses/components/catalog-offer-unpublication.tsx
  - >-
    src/admin-spa/src/features/catalog-courses/components/catalog-offer-delete.tsx
  - src/admin-spa/src/assets/app.css
  - src/admin-spa/src/app/routes/catalog-courses.test.tsx
  - src/admin-spa/src/app/routes/catalog-course-record.test.tsx
  - src/admin-spa/src/app/routes/catalog-offer-form.test.tsx
  - src/admin-spa/src/app/routes/catalog-offer-price-change.test.tsx
  - src/admin-spa/src/app/routes/catalog-offer-publication.test.tsx
  - src/admin-spa/src/app/routes/catalog-offer-unpublish.test.tsx
  - src/admin-spa/src/app/routes/catalog-purchase-intents.test.tsx
type: bug
ordinal: 30000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Auditoria de 2026-10-11 comparou dev (conta Financeiro) com o Figma Screens — Catálogo e vitrine (node 205:5898, seções C1 a C7). O Catálogo foi implementado sem os componentes do DS: nível e situação aparecem como texto, a ficha não tem selos, as ofertas ficam dentro de um card sem coluna de preço, selo e ação direta, os diálogos usam rádios simples e os avisos de sucesso são parágrafos. Decisões do usuário: tirar o total de cliques do curso (fica só por oferta, como no Figma); em Carregando e Erro da ficha usar o título e o Na vitrine da lista em cache e, sem cache, uma barra no lugar do título. Fora de escopo: C8 (trilha de auditoria, telas de CAP-030), menu lateral do Financeiro (shell) e a vitrine do aluno (task própria). Não alterar a troca de tema claro/escuro.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Lista de cursos (C1.a) em tabela com cabeçalho muted, linhas de 16x20, título 16/24 médio, nível como selo (Sem nível com ícone e selo warning), Na vitrine com ícone, coluna Abrir → e paginação '‹ 1 2 › · 10 por página' alinhada à esquerda
- [ ] #2 Lista vazia, carregando e erro seguem C1.b (janela de código), C1.c (tabela com barras) e C1.d (alerta de erro com Tentar de novo como link)
- [ ] #3 Ficha (C2.a) tem voltar com ícone, selo Na vitrine/Fora da vitrine ao lado do título, card Do curso com selo somente leitura e linhas de 140 px, e Abrir na Autoria → na mesma linha da nota
- [ ] #4 Alertas de sem nível (C2.b) e de curso que perdeu o nível com ofertas publicadas (C2.c) seguem o texto e o estilo warning do Figma
- [ ] #5 Chamada comercial (C2.a e C2.h) sem rótulo visível, dica 12/16 com contador à direita na mesma linha e erro abaixo, borda vermelha acima de 160
- [ ] #6 Ofertas (C2.a, C2.b, C2.d) em cabeçalho próprio com título 20/28 e + Nova oferta, lista em card com linhas de 72 px: nome e detalhes, preço à direita, selo de situação, ação direta (Publicar, Despublicar ou Publicar de novo) e menu ⋯ de 32 px; vazio em card tracejado; sem o total de cliques do curso
- [ ] #7 Carregando, erro e não encontrado da ficha seguem C2.e, C2.f e C2.g, com o título da lista em cache quando houver
- [ ] #8 Diálogo de oferta (C3) segue o Figma: descrição 'A oferta nasce como rascunho…', contador no rótulo, preço com R$ e campo de 200 px, vigência em cartões de rádio, meses em campo de 120 px com dica, erros com ícone, alerta de limite e de erro no topo
- [ ] #9 Diálogos de publicar (C4), confirmar alteração (C5), despublicar (C6) e excluir rascunho (C7) seguem textos, alertas e larguras do Figma; Excluir é destrutivo; avisos de sucesso usam o Toast compartilhado (C4.e)
- [ ] #10 Mobile 390 (C1.e, C2.i) sem rolagem horizontal
- [ ] #11 Lint, build e testes do admin-spa passam e as telas foram conferidas em dev
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. catalog.css novo (importado em app.css) com tabela de cursos, selos, ficha, ofertas, diálogos e estados; só tokens de tema.
2. Componentes: catalog-badges (nível, situação da oferta, vitrine), catalog-alert (danger/warning/info com Tentar de novo), catalog-pagination.
3. Lista (C1): tabela em card com coluna Abrir →, selos, paginação Figma; vazio com janela de código, carregando em tabela, erro em alerta.
4. Ficha (C2): voltar com ícone, selo de vitrine ao lado do título, Do curso com somente leitura, alertas C2.b/C2.c, chamada com dica e contador na mesma linha, ofertas em lista própria com preço/selo/ação direta/menu ⋯, vazio tracejado, sem total de cliques; carregando/erro com título da lista em cache; não encontrado com subtítulo e Voltar ao Catálogo.
5. Diálogos (C3–C7): hooks passam a devolver erro com título, detalhe e se cabe repetir; formulário com contador no rótulo, R$, cartões de vigência e dica de meses; publicar/alterar/despublicar/excluir com alertas e textos do Figma; Excluir destrutivo; avisos via Toast compartilhado.
6. Atualizar testes, rodar lint/build/vitest; conferir em dev após o deploy.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Implementado: catalog.css (importado em app.css; regras antigas do Catálogo removidas, mantida .catalog-record-card que Financeiro e Cortesias usam), selos de nível/situação/vitrine, Alert do DS (erro, aviso, informação com Tentar de novo), paginação do Figma, lista em tabela com Abrir → (cartões no mobile), vazio com janela de código, carregando em tabela, ficha com voltar, selo de vitrine, Do curso somente leitura, alertas C2.b/C2.c, chamada com dica e contador na mesma linha, ofertas em lista própria com preço, selo, ação direta e menu ⋯ (Editar e Excluir), vazio tracejado, sem total de cliques do curso; carregando/erro usam título e vitrine da lista em cache; não encontrado com subtítulo e Voltar ao Catálogo. Diálogos: hooks devolvem erro com título, detalhe e retry; formulário com contador no rótulo, R$, cartões de vigência e dica de meses; publicar fecha com Fechar quando recusado; confirmar alteração com antes/depois e alerta informativo; despublicar e excluir em AlertDialog 480 sem X; avisos via Toast compartilhado com 5 s. Lint, tsc, build e vitest (52 arquivos, 313 testes) passam. Ainda não conferido em dev.
<!-- SECTION:NOTES:END -->
