---
id: TASK-21
title: >-
  Ajustar telas de Acesso interno, logo e menu às divergências da auditoria com
  o Figma
status: Done
assignee:
  - '@claude'
created_date: '2026-10-11 00:21'
updated_date: '2026-10-11 14:08'
labels:
  - frontend
dependencies: []
references:
  - >-
    https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=66-2
modified_files:
  - src/admin-spa/src/components/toast.tsx
  - src/admin-spa/src/assets/toast.css
  - src/admin-spa/src/assets/app.css
  - src/admin-spa/src/config/session-markers.ts
  - src/admin-spa/src/app/routes/admin-layout-route.tsx
type: bug
ordinal: 24000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Auditoria de 2026-10-10 comparou dev com o Figma Screens — Acesso interno (node 66:2) e achou 23 divergências no logo, menu lateral, menu da conta, Início, telas de entrada/senha/convite, Acessos e tela de sem permissão. Decisões do usuário: manter a organização atual do menu (Início sem título, grupos abaixo) e só acertar os espaços pelo ritmo de 12 px do Figma mais novo (Cortesias, 231:86); tela de sem permissão compartilhada nas rotas que hoje mostram 'Você não tem acesso a esta área.'; revogar papel com uma linha 'Revogar <papel>' por papel no menu ⋯. Não alterar a troca de tema claro/escuro. B11 (Financeiro reservado) fica fora: o módulo financeiro real substituiu.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 As subtarefas de shell/Início, telas de entrada e Acessos estão concluídas
- [x] #2 Lint, build e testes do admin-spa passam e as telas foram conferidas em dev
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Antes da divisão: Toast compartilhado (components/toast.tsx + assets/toast.css), marcador de sessão encerrada por saída (config/session-markers.ts, gravado em admin-layout-route.tsx) e arquivos CSS por subtarefa importados no app.css.
2. Três subtarefas em paralelo com arquivos exclusivos: 21.1 shell/Início/sem permissão, 21.2 telas de entrada, 21.3 Acessos.
3. Integração: lint, build, vitest completo; PR; conferência em dev após deploy.
<!-- SECTION:PLAN:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Acesso interno, logo e menu alinhados ao Figma em #210 e #211 (subtarefas 21.1–21.4). Verificado em dev com as contas admin e financeira e por admin-spa em main (2026-10-11): lint ok, build ok, vitest 52 arquivos/313 testes.
<!-- SECTION:FINAL_SUMMARY:END -->
