---
id: TASK-31
title: Alinhar a tela da aula do aluno ao Figma e ajustar o player
status: In Progress
assignee:
  - '@claude'
created_date: '2026-10-13 19:54'
labels:
  - frontend
dependencies: []
references:
  - >-
    https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=255-3
  - >-
    https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=343-368
  - >-
    https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=273-36456
  - docs/design/wireframes-aula.md
  - docs/design/wireframes-transcricao.md
  - docs/design/wireframes-progresso.md
  - student-spa
priority: medium
type: bug
ordinal: 38000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Auditoria da tela da aula (student-spa) contra o Figma: A1 (255:3), progresso A1.P (273:36456) e painel de transcrição TA1 (343:368). Inclui pedidos do responsável para o player: clique na imagem toca e pausa, clique duplo entra e sai da tela cheia, o player cabe na parte visível da tela, e o menu lateral vai até o fim da página. O cabeçalho do Figma (StudentHeader) não entra: a casca com menu lateral da Conta do aluno continua, por decisão do responsável.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Subtasks de player e de layout (lista, painel e casca) concluídas
- [ ] #2 Lint, build e testes do student-spa passam; conferência em dev
<!-- AC:END -->
