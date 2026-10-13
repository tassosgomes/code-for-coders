---
id: TASK-32.4
title: 'Compra: Financeiro do backoffice (F) conforme Figma'
status: To Do
assignee:
  - '@claude'
created_date: '2026-10-13 20:42'
labels:
  - frontend
dependencies: []
references:
  - >-
    https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=292-1223
  - >-
    https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=292-2048
  - >-
    https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=292-2049
  - admin-spa
  - docs/design/wireframes-compra.md
parent_task_id: TASK-32
type: bug
ordinal: 45000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
A lista e o detalhe de pedidos do backoffice seguem a estrutura do Figma, mas o caminho do detalhe é um link único, a concessão aparece em texto e a lista, filtros e estados precisam de conferência visual (seções F).
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Lista, filtros e estados F1.a a F1.f e F2 conferidos e alinhados ao Figma
- [ ] #2 Detalhe com trilha Financeiro / Pedidos / #NNNNNN, badges de situação e concessão e referência do pagamento em mono com Copiar (F3)
- [ ] #3 Estados F3.b1 a F3.e e mobile 390 seguem o Figma sem rolagem horizontal
- [ ] #4 Lint, build e testes do admin-spa passam e as telas foram conferidas em dev
<!-- AC:END -->
