---
id: TASK-18
title: Ajustar telas de Autoria às divergências da auditoria com o Figma
status: In Progress
assignee:
  - '@claude'
created_date: '2026-10-10 20:19'
updated_date: '2026-10-10 20:19'
labels:
  - frontend
  - bug
dependencies: []
type: bug
ordinal: 16000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Auditoria de 2026-10-10 comparando o SPA admin em dev (/admin/autoria) com o Figma (arquivo kKNfxTqSFT5IfQHcoTh5gn, página Screens — Autoria, node 155:7451, inclui o adendo Nível e pré-requisito) encontrou bugs, partes faltando, desalinhamentos e textos divergentes. Fora de escopo: temas claro/escuro e contraste (centralizados na TASK-15) e o shell/sidebar (ordem Vídeos→Autoria e marca vêm da TASK-16, referência é Vídeos). Decisões do usuário: a descrição do curso é renderizada como markdown; o estado publicado sem alterações segue o Figma; o modal de publicação segue o Figma (A7, A7.h).
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 As subtarefas de currículo/publicação e de cabeçalho/lista/versão estão concluídas
- [ ] #2 Lint, build e testes do admin-spa passam
<!-- AC:END -->
