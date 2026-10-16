---
id: TASK-37
title: Captcha Cloudflare Turnstile nos endpoints anônimos dos SPAs admin e aluno
status: In Progress
assignee:
  - '@claude'
created_date: '2026-10-16 20:43'
updated_date: '2026-10-16 20:44'
labels:
  - security
  - frontend
  - backend
dependencies: []
priority: high
type: feature
ordinal: 47000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Login, cadastro e pedido de redefinição de senha nos BFFs são anônimos e hoje só têm proteção de senha: robôs podem fazer força bruta de login, criar contas falsas e disparar e-mails em massa pelo Notification. Escolhemos o Cloudflare Turnstile (gratuito e sem limite de verificações; a escola já usa Cloudflare para tunnel, DNS e proxy) no lugar do reCAPTCHA. Decisões do usuário em 2026-10-16: falha da siteverify deixa o login passar e registra (fail-open), mas bloqueia o cadastro e o pedido de redefinição (fail-closed); o login do admin pede captcha sempre, não só após N falhas. Rotas protegidas por token de e-mail (redefinir senha, aceitar convite) e rotas autenticadas ficam fora.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 As subtasks estão concluídas
- [ ] #2 O QA geral (skill flow-qa) passou sobre a branch
<!-- AC:END -->
