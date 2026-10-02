---
id: TASK-9
title: Redirecionar para login ao expirar sessão
status: In Progress
assignee:
  - '@codex'
created_date: '2026-10-02 22:32'
updated_date: '2026-10-02 22:44'
labels:
  - frontend
  - auth
dependencies: []
modified_files:
  - src/admin-spa/src/config/session-markers.ts
  - src/admin-spa/src/app/routes/staff-session-loader.ts
  - src/admin-spa/src/app/routes/admin-layout-route.tsx
  - src/admin-spa/src/features/staff-session/components/staff-login-screen.tsx
  - >-
    src/student-spa/src/features/student-session/components/student-login-screen.tsx
priority: high
type: bug
ordinal: 7000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Quando a sessão do usuário expira, as chamadas autenticadas passam a retornar 401 e o frontend permanece aberto, acumulando erros em vez de encerrar a sessão e levar a pessoa ao login com uma indicação clara.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Uma resposta 401 de uma sessão autenticada expirada redireciona para a rota de login configurada.
- [ ] #2 A tela de login apresenta a mensagem exata 'Sessão Expirada' após o redirecionamento.
- [ ] #3 O redirecionamento e a mensagem ocorrem uma única vez, sem loop nem notificações duplicadas.
- [ ] #4 Uma falha 401 ocorrida no próprio fluxo de login preserva o tratamento atual de credenciais inválidas.
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Criar marcadores de sessão para a SPA administrativa e registrar sessão ativa após carregar uma sessão válida. 2. Quando o loader ou uma tela autenticada receber 401, limpar o estado da sessão, marcar expiração somente se havia sessão ativa e redirecionar uma única vez para o login. 3. Exibir 'Sessão Expirada' no login administrativo e ajustar o título equivalente da SPA do aluno. 4. Rodar lint e build das SPAs afetadas.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Implementação feita em ambas as SPAs. Lint e build de produção passaram em src/admin-spa e src/student-spa; os builds mantiveram os avisos existentes de runtime-env.js e tamanho de bundle. Testes automatizados não foram executados nesta rodada, então os critérios comportamentais seguem sem marcação.
<!-- SECTION:NOTES:END -->
