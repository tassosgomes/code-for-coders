---
id: TASK-19.1
title: 'Cortesias: expor se o curso tem oferta na listagem de cursos concedíveis'
status: In Progress
assignee:
  - '@claude'
created_date: '2026-10-10 22:29'
updated_date: '2026-10-10 22:34'
labels:
  - backend
dependencies: []
modified_files:
  - >-
    src/commerce/src/CodeForCoders.Commerce.Application/Interfaces/CourtesyCourse.cs
  - >-
    src/commerce/src/CodeForCoders.Commerce.Infra.Data/Queries/CourtesyCourseQueries.cs
  - >-
    src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CourtesyCourseListingTests.cs
  - >-
    src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CourtesyCoursesHttpHandler.cs
  - >-
    src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CourtesyCoursesProxyTests.cs
  - contracts/commerce/openapi-internal.yaml
  - contracts/bff-admin/openapi-cortesias.yaml
parent_task_id: TASK-19
type: feature
ordinal: 20000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
O passo Curso do Figma (T2.a) mostra a coluna Oferta com selo "Com oferta"/"Sem oferta". GET /courtesy-courses (bff-admin, proxy do Commerce) devolve só courseId e title. O Commerce já guarda as ofertas do catálogo (CatalogOffers), então o dado existe no serviço dono. Cortesia continua não dependendo de oferta (RN-D11): o campo é só informativo.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Cada item de GET /courtesy-courses no Commerce e no bff-admin traz o booleano obrigatório `hasOffer`, verdadeiro quando o curso tem oferta publicada
- [ ] #2 Os contratos contracts/commerce/openapi-internal.yaml e contracts/bff-admin/openapi-cortesias.yaml descrevem `hasOffer` com exemplo
- [ ] #3 Testes de integração do Commerce e do bff-admin cobrem curso com e sem oferta publicada
- [ ] #4 A listagem continua incluindo cursos sem oferta
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Commerce: CourtesyCourse ganha HasOffer calculado por existência de oferta publicada em CatalogOffers (sem migration).
2. bff-admin: DTO/cliente repassam hasOffer.
3. Contratos commerce interno e bff-admin descrevem hasOffer.
4. Testes de integração com curso com e sem oferta publicada.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
hasOffer = existe CatalogOffer do curso com Status 'published' (mesma regra de PurchaseOfferReader.cs:9 e CatalogCourseView.cs:36). Uma SQL com Any no Select; sem migration. bff-admin repassa o JSON do Commerce sem reserializar, então só testes e contrato mudaram. Commerce: CourtesyCourseListingTests + OrderHttpContract 15/15; bff-admin filtro Courtes 40/40.
<!-- SECTION:NOTES:END -->
