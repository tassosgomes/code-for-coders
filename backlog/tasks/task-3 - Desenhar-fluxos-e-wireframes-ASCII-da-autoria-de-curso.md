---
id: TASK-3
title: Desenhar fluxos e wireframes ASCII da autoria de curso
status: Done
assignee:
  - '@codex'
created_date: '2026-09-29 21:27'
updated_date: '2026-09-29 21:44'
labels: []
dependencies: []
references:
  - tasks/prd-autoria-curso/prd.md
  - tasks/prd-autoria-curso/techspec.md
  - tasks/prd-autoria-curso/api-contract.md
  - docs/design/wireframes-videos.md
modified_files:
  - docs/design/wireframes-autoria-curso.md
type: docs
ordinal: 3000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
O PRD CAP-005 exige a etapa de wireframe ASCII antes do desenho no Figma e da implementação do admin-spa. A TechSpec e os contratos já foram aprovados; falta um artefato visual revisável que cubra a jornada do professor e os estados de erro e acesso.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Documento em docs/design segue a organização do wireframes-videos.md e mapeia RF-01 a RF-09 às telas e fluxos de autoria.
- [x] #2 Wireframes ASCII mostram lista, criação, editor de módulos e aulas, seletor de vídeo, publicação com pendências, republicação, descarte, histórico e exclusão, com estados relevantes.
- [x] #3 Documento explicita permissões, rotas, navegação por pendências, acessibilidade, mobile e alinhamento com AppShell/Design System, PRD, TechSpec e contratos.
- [x] #4 Links locais e referências a rotas e limites são conferidos; decisões de desenho para aprovação ficam identificadas.
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Conferir PRD, TechSpec, contratos e padrões dos wireframes do backoffice. 2. Mapear telas, estados e transições de RF-01 a RF-09 sem introduzir escopo novo. 3. Criar docs/design/wireframes-autoria-curso.md com fluxos e desenhos ASCII, incluindo mobile, acessibilidade e decisões pendentes. 4. Validar links, rotas, limites, consistência com fontes e revisar o documento.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Wireframe ASCII criado em docs/design; groma view do wireframes-videos.md informa que docs/design não tem componente proprietário no mapa, portanto não há ID de arquitetura a associar.

Revisão do documento: seções 1–6 conferidas contra PRD v1.0, TechSpec v1.0, OpenAPI 1.0.0, wireframes de Vídeos/Acesso e Components.md. Verificação estática passou para links locais, cercas ASCII, RF-01 a RF-12, A1 a A12, ausência de espaços finais e coerência das pendências e do videoId na versão histórica. Não há lint/build/testes de aplicação pertinentes a esta alteração somente documental.

O responsável aprovou os wireframes ASCII em 2026-09-29. O documento agora registra o estado aprovado, as decisões aprovadas e o handoff para o Figma; a aprovação do desenho visual continua sendo a etapa seguinte.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Wireframes ASCII de Autoria (CAP-005) criados, conferidos e aprovados pelo responsável em 2026-09-29. O documento está pronto como entrada para o desenho no Figma; validação visual e implementação são etapas posteriores.
<!-- SECTION:FINAL_SUMMARY:END -->
