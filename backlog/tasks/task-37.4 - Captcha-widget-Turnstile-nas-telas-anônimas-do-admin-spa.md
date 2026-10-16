---
id: TASK-37.4
title: 'Captcha: widget Turnstile nas telas anônimas do admin-spa'
status: Done
assignee:
  - '@claude'
created_date: '2026-10-16 20:44'
updated_date: '2026-10-16 21:52'
labels:
  - security
  - frontend
dependencies:
  - TASK-37.2
modified_files:
  - src/admin-spa/package.json
  - src/admin-spa/package-lock.json
  - src/admin-spa/public/runtime-env.template.js
  - src/admin-spa/docker/40-runtime-env.sh
  - src/admin-spa/src/config/env.ts
  - src/admin-spa/src/lib/captcha.ts
  - src/admin-spa/src/components/captcha-field.tsx
  - src/admin-spa/src/assets/auth-screens.css
  - src/admin-spa/src/features/staff-session/api/staff-session.ts
  - src/admin-spa/src/features/staff-session/components/staff-login-screen.tsx
  - >-
    src/admin-spa/src/features/staff-session/components/staff-login-screen.test.tsx
  - >-
    src/admin-spa/src/features/staff-password-recovery/api/request-staff-password-reset.ts
  - >-
    src/admin-spa/src/features/staff-password-recovery/components/staff-password-recovery-screen.tsx
  - >-
    src/admin-spa/src/features/staff-password-recovery/components/staff-password-recovery-screen.test.tsx
  - src/admin-spa/src/testing/handlers.ts
  - src/admin-spa/src/testing/setup.ts
  - src/admin-spa/src/testing/turnstile-mock.tsx
parent_task_id: TASK-37
type: feature
ordinal: 51000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
A equipe interna precisa obter o token do Turnstile nas telas que chamam os endpoints protegidos do bff-admin.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Entrar e recuperar senha mostram o widget Turnstile e enviam o token no header X-Captcha-Token
- [x] #2 A sitekey vem de TURNSTILE_SITE_KEY em runtime-env, sem rebuild
- [x] #3 O widget segue o tema claro ou escuro ativo do admin
- [x] #4 O botão de envio só habilita com token; o widget é reiniciado após cada envio, erro ou expiração do token
- [x] #5 Os códigos CAPTCHA_REQUIRED, CAPTCHA_INVALID, CAPTCHA_UNAVAILABLE e RATE_LIMITED mostram mensagens em pt-BR
- [x] #6 Testes do SPA cobrem o fluxo com o widget mockado; lint, typecheck e build passam
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1) Adicionar @marsidev/react-turnstile ao admin-spa. 2) TURNSTILE_SITE_KEY no runtime-env (template, env.ts com fallback de teste em dev, envsubst do entrypoint). 3) CaptchaField reutilizavel (tema efetivo via next-themes, pt-BR, reset). 4) Header X-Captcha-Token nas APIs de login e pedido de redefinicao; mensagens pt-BR por codigo. 5) Mock do widget, handlers MSW exigindo o header e testes. 6) Gate lint/typecheck/test/build.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Widget @marsidev/react-turnstile ^1.6.1. Entrypoint docker/40-runtime-env.sh listava as variaveis do envsubst: incluida TURNSTILE_SITE_KEY (default vazio; sem a variavel o SPA falha na validacao do env, TASK-37.5 deve defini-la nos ambientes). Gate: lint, typecheck, test (60 arquivos, 410 testes) e build verdes.

Gate reconferido pelo orquestrador: lint, typecheck, build e vitest 60 arquivos/410 testes verdes.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
CaptchaField em entrar e recuperar senha do admin-spa, seguindo o tema efetivo, com sitekey de runtime-env, botão travado até haver token, reset após erro ou expiração e mensagens pt-BR por código. Verificado com lint, typecheck, build, vitest 410/410 e smoke Playwright da TASK-37.5.
<!-- SECTION:FINAL_SUMMARY:END -->
