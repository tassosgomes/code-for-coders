---
id: TASK-37.3
title: 'Captcha: widget Turnstile nas telas anônimas do student-spa'
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
  - src/student-spa/docker/40-runtime-env.sh
  - src/student-spa/package-lock.json
  - src/student-spa/package.json
  - src/student-spa/public/runtime-env.template.js
  - src/student-spa/src/config/env.ts
  - >-
    src/student-spa/src/features/student-password-recovery/api/request-student-password-reset.ts
  - >-
    src/student-spa/src/features/student-password-recovery/components/student-password-recovery-screen.tsx
  - src/student-spa/src/features/student-registration/api/register-student.ts
  - >-
    src/student-spa/src/features/student-registration/components/student-registration-screen.tsx
  - src/student-spa/src/features/student-session/api/student-session.ts
  - >-
    src/student-spa/src/features/student-session/components/student-login-screen.tsx
  - src/student-spa/src/testing/handlers.ts
  - src/student-spa/src/testing/setup.ts
  - src/student-spa/src/testing/turnstile-mock.tsx
  - src/student-spa/src/app/student-captcha-flows.integration.test.tsx
  - src/student-spa/src/components/captcha-field.tsx
  - src/student-spa/src/hooks/use-captcha.ts
  - src/student-spa/src/utils/captcha-error.ts
parent_task_id: TASK-37
type: feature
ordinal: 50000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
O aluno precisa obter o token do Turnstile nas telas que chamam os endpoints protegidos do bff-student.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Login, cadastro e esqueci a senha mostram o widget Turnstile e enviam o token no header X-Captcha-Token
- [x] #2 A sitekey vem de TURNSTILE_SITE_KEY em runtime-env, sem rebuild
- [x] #3 O botão de envio só habilita com token; o widget é reiniciado após cada envio, erro ou expiração do token
- [x] #4 Os códigos CAPTCHA_REQUIRED, CAPTCHA_INVALID, CAPTCHA_UNAVAILABLE e RATE_LIMITED mostram mensagens em pt-BR
- [x] #5 Testes do SPA cobrem o fluxo com o widget mockado; lint, typecheck e build passam
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Dependência react-turnstile. 2. TURNSTILE_SITE_KEY no runtime-env (template, env.ts com fallback de teste em dev, envsubst do entrypoint). 3. Hook useCaptcha e CaptchaField (pt-br, tema next-themes). 4. Camada de API: captchaToken vai no header X-Captcha-Token em login, cadastro e esqueci a senha. 5. Botão só habilita com token; reset do widget no finally e na expiração. 6. Mensagens pt-BR para CAPTCHA_* e RATE_LIMITED com Retry-After. 7. Testes: mock do widget, handlers MSW exigindo o header, teste de integração novo.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
student-spa concluído; gate verde (lint, typecheck, 253 testes, build). Lib @marsidev/react-turnstile ^1.6.1 (lock em 1.6.1; a 1.6.2 tem menos de 2 semanas). O entrypoint docker/40-runtime-env.sh lista as variáveis do envsubst, então TURNSTILE_SITE_KEY entrou lá (default vazio). Sem valor em produção o SPA falha na validação do env: a TASK-37.5 deve definir a variável nos ambientes.

Gate reconferido pelo orquestrador: lint, typecheck, build e vitest 35 arquivos/253 testes verdes.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
CaptchaField (@marsidev/react-turnstile 1.6.1) em login, cadastro e esqueci a senha do student-spa, com sitekey de runtime-env, botão travado até haver token, reset após envio ou expiração e mensagens pt-BR por código. Widget em largura total, igual ao admin. Verificado com lint, typecheck, build, vitest 253/253 e smoke Playwright da TASK-37.5.
<!-- SECTION:FINAL_SUMMARY:END -->
