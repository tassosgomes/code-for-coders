---
id: TASK-21.1
title: >-
  Acesso interno: logo, menu lateral, menu da conta, Início e tela de sem
  permissão
status: Done
assignee:
  - '@claude'
created_date: '2026-10-11 00:22'
updated_date: '2026-10-11 14:07'
labels:
  - frontend
dependencies: []
modified_files:
  - src/admin-spa/src/components/app-shell.tsx
  - src/admin-spa/src/components/forbidden-screen.tsx
  - src/admin-spa/src/components/forbidden-screen.test.tsx
  - src/admin-spa/src/features/admin-dashboard/components/dashboard-screen.tsx
  - >-
    src/admin-spa/src/features/admin-dashboard/components/dashboard-screen.test.tsx
  - src/admin-spa/src/app/routes/courtesies-route.tsx
  - src/admin-spa/src/app/routes/catalog-courses-route.tsx
  - src/admin-spa/src/app/routes/catalog-course-record-route.tsx
  - src/admin-spa/src/app/routes/admin-layout-route.test.tsx
  - src/admin-spa/src/app/routes/catalog-course-record.test.tsx
  - src/admin-spa/src/app/routes/catalog-courses.test.tsx
  - src/admin-spa/src/app/routes/courtesy-student-lookup.test.tsx
  - src/admin-spa/src/app/routes/audit-trail-list.test.tsx
  - src/admin-spa/src/app/routes/videos-area-route.test.tsx
  - src/admin-spa/src/app/routes/authoring-courses.test.tsx
  - src/admin-spa/src/assets/backoffice-shell.css
parent_task_id: TASK-21
type: bug
ordinal: 25000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Logo achatado no menu (marca encolhe para 18 px porque a linha não cabe em 232 px), primeiro item colado no logo, espaços do menu fora do ritmo do Figma, itens em peso 400, menu da conta com nome em negrito e papéis como badge e Sair sem ícone. No Início, cards de Catálogo (e qualquer área sem texto próprio) caem na descrição 'Área financeira reservada…' com ícone de pessoas; subtítulo inexistente no Figma; 'Seus papéis:' escondido; cards com 520 px. Rotas sem permissão mostram só um h1.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Marca do logo tem 32×32 com raio 12 em todas as larguras; logo, badge Backoffice e menu usam os espaços do Figma (logo + ~40 px até o 1º item, 12 px entre títulos de grupo e itens); a organização dos grupos não muda
- [x] #2 Itens do menu usam Inter 14/20 peso 500 (ativo 600) com as cores sidebar do tema
- [x] #3 Menu da conta mostra nome (14/500), papéis em texto cinza 12 px, separador e item Sair com ícone LogOut que ocupa a linha com destaque no hover/foco; gatilho com nome em peso 500 e iniciais em 14 px
- [x] #4 Início mostra 'Seus papéis:' visível ao lado dos badges, sem o subtítulo extra; cada área tem ícone e descrição próprios (Catálogo, Autoria, Vídeos, Financeiro, Acessos, Auditoria); cards de 360 px com botão em peso 600
- [x] #5 Rotas sem permissão usam uma tela compartilhada com janela de terminal '403 Forbidden', título 'Esta área não é do seu papel', texto do Figma e botão 'Voltar para o início'
- [x] #6 Testes do admin-spa cobrem descrições por área no Início, menu da conta e tela de sem permissão
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
Logo sem encolher e espaços do menu pelo ritmo de 12 px (Cortesias 231:86) em backoffice-shell.css; menu da conta e Início conforme B5; ForbiddenScreen compartilhada nas rotas de Catálogo e Cortesias; testes.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Marca não encolhe (flex:none), 40px logo→1º item e 12px entre elementos do menu, organização mantida. Cards de 520px vinham de .authoring-page .area-card (authoring.css); sobrescrito com .dashboard-page.authoring-page. Não há token --sidebar-foreground; mantido --foreground-secondary. Auditoria tem tela própria de sem permissão (audit-trail-forbidden.tsx), não migrada. Terminal da tela sem permissão usa window.location.pathname para incluir o /admin.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Logo, menu lateral, menu da conta, Início e tela de sem permissão alinhados ao Figma (#210, ajustes em #211). Verificado em dev medindo o DOM (marca 32×32; Início y=88, título y=136, item y=164 como no Figma; itens 500/600), menu da conta, cards de 360 px com descrição por área e /admin/cortesias como admin mostrando o terminal 403; e por admin-spa em main (2026-10-11): lint ok, build ok, vitest 52 arquivos/313 testes.
<!-- SECTION:FINAL_SUMMARY:END -->
