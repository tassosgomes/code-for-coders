---
id: TASK-37.2
title: 'Captcha: verificação Turnstile e rate limit por IP nos BFFs'
status: Done
assignee:
  - '@claude'
created_date: '2026-10-16 20:44'
updated_date: '2026-10-16 21:52'
labels:
  - security
  - backend
dependencies:
  - TASK-37.1
modified_files:
  - src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/SessionEndpoints.cs
  - >-
    src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/StaffPasswordRecoveryEndpoints.cs
  - >-
    src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/PipelineExtensions.cs
  - >-
    src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/ServiceConfigurationExtensions.cs
  - src/bff-admin/src/CodeForCoders.BffAdmin.Api/appsettings.json
  - >-
    src/bff-admin/src/CodeForCoders.BffAdmin.Application/Common/BffAdminTelemetry.cs
  - >-
    src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/BffAdminApiFactory.cs
  - >-
    src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CourseBffApiFactory.cs
  - >-
    src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CourtesyAuditBffApiFactory.cs
  - >-
    src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CourtesyBffApiFactory.cs
  - >-
    src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/FinanceOrdersBffApiFactory.cs
  - >-
    src/bff-admin/tests/CodeForCoders.BffAdmin.UnitTests/CodeForCoders.BffAdmin.UnitTests.csproj
  - >-
    src/bff-student/src/CodeForCoders.BffStudent.Api/Endpoints/SessionEndpoints.cs
  - >-
    src/bff-student/src/CodeForCoders.BffStudent.Api/Endpoints/StudentPasswordRecoveryEndpoints.cs
  - >-
    src/bff-student/src/CodeForCoders.BffStudent.Api/Endpoints/StudentRegistrationEndpoints.cs
  - >-
    src/bff-student/src/CodeForCoders.BffStudent.Api/Extensions/PurchaseIntentRateLimitExtensions.cs
  - >-
    src/bff-student/src/CodeForCoders.BffStudent.Api/Extensions/ServiceConfigurationExtensions.cs
  - src/bff-student/src/CodeForCoders.BffStudent.Api/appsettings.json
  - >-
    src/bff-student/src/CodeForCoders.BffStudent.Application/Common/BffStudentTelemetry.cs
  - >-
    src/bff-student/tests/CodeForCoders.BffStudent.EndToEndTests/BffStudentApiFactory.cs
  - >-
    src/bff-student/tests/CodeForCoders.BffStudent.EndToEndTests/StudentPasswordRecoveryTests.cs
  - >-
    src/bff-student/tests/CodeForCoders.BffStudent.IntegrationTests/ShowcaseBffFactory.cs
  - >-
    src/bff-admin/src/CodeForCoders.BffAdmin.Api/Captcha/CaptchaEndpointExtensions.cs
  - >-
    src/bff-admin/src/CodeForCoders.BffAdmin.Api/Captcha/CaptchaEndpointFilter.cs
  - src/bff-admin/src/CodeForCoders.BffAdmin.Api/Captcha/CaptchaFailurePolicy.cs
  - src/bff-admin/src/CodeForCoders.BffAdmin.Api/Captcha/ClientIpResolver.cs
  - src/bff-admin/src/CodeForCoders.BffAdmin.Api/Captcha/ITurnstileVerifier.cs
  - src/bff-admin/src/CodeForCoders.BffAdmin.Api/Captcha/TurnstileOptions.cs
  - >-
    src/bff-admin/src/CodeForCoders.BffAdmin.Api/Captcha/TurnstileVerification.cs
  - src/bff-admin/src/CodeForCoders.BffAdmin.Api/Captcha/TurnstileVerifier.cs
  - >-
    src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/AnonymousRateLimitExtensions.cs
  - src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/CaptchaExtensions.cs
  - >-
    src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/RateLimitingExtensions.cs
  - >-
    src/bff-admin/src/CodeForCoders.BffAdmin.Api/Security/AnonymousRateLimitOptions.cs
  - src/bff-admin/src/CodeForCoders.BffAdmin.Api/appsettings.Development.json
  - >-
    src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/CaptchaProtectionTests.cs
  - >-
    src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/FakeTurnstileHandler.cs
  - >-
    src/bff-admin/tests/CodeForCoders.BffAdmin.UnitTests/Captcha/CaptchaEndpointFilterTests.cs
  - >-
    src/bff-admin/tests/CodeForCoders.BffAdmin.UnitTests/Captcha/TurnstileVerifierTests.cs
  - >-
    src/bff-student/src/CodeForCoders.BffStudent.Api/Captcha/CaptchaEndpointExtensions.cs
  - >-
    src/bff-student/src/CodeForCoders.BffStudent.Api/Captcha/CaptchaEndpointFilter.cs
  - >-
    src/bff-student/src/CodeForCoders.BffStudent.Api/Captcha/CaptchaFailurePolicy.cs
  - src/bff-student/src/CodeForCoders.BffStudent.Api/Captcha/ClientIpResolver.cs
  - >-
    src/bff-student/src/CodeForCoders.BffStudent.Api/Captcha/ITurnstileVerifier.cs
  - src/bff-student/src/CodeForCoders.BffStudent.Api/Captcha/TurnstileOptions.cs
  - >-
    src/bff-student/src/CodeForCoders.BffStudent.Api/Captcha/TurnstileVerification.cs
  - >-
    src/bff-student/src/CodeForCoders.BffStudent.Api/Captcha/TurnstileVerifier.cs
  - >-
    src/bff-student/src/CodeForCoders.BffStudent.Api/Extensions/AnonymousRateLimitExtensions.cs
  - >-
    src/bff-student/src/CodeForCoders.BffStudent.Api/Extensions/CaptchaExtensions.cs
  - >-
    src/bff-student/src/CodeForCoders.BffStudent.Api/Extensions/RateLimitingExtensions.cs
  - >-
    src/bff-student/src/CodeForCoders.BffStudent.Api/Security/AnonymousRateLimitOptions.cs
  - >-
    src/bff-student/src/CodeForCoders.BffStudent.Api/appsettings.Development.json
  - >-
    src/bff-student/tests/CodeForCoders.BffStudent.EndToEndTests/CaptchaProtectionTests.cs
  - >-
    src/bff-student/tests/CodeForCoders.BffStudent.EndToEndTests/FakeTurnstileHandler.cs
  - >-
    src/bff-student/tests/CodeForCoders.BffStudent.UnitTests/Captcha/CaptchaEndpointFilterTests.cs
  - >-
    src/bff-student/tests/CodeForCoders.BffStudent.UnitTests/Captcha/TurnstileVerifierTests.cs
parent_task_id: TASK-37
type: feature
ordinal: 49000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Os BFFs são a borda dos SPAs; validar o token lá impede que um robô chame a API direto, contornando o widget.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Em bff-student, POST student-sessions, student-accounts e password-reset-requests exigem um token Turnstile válido no header X-Captcha-Token
- [x] #2 Em bff-admin, POST staff-sessions e staff-password-reset-requests exigem um token Turnstile válido no header X-Captcha-Token
- [x] #3 Sem token a resposta é 400 CAPTCHA_REQUIRED; token reprovado pela Cloudflare dá 403 CAPTCHA_INVALID; os dois em problem+json com traceId
- [x] #4 Com a siteverify indisponível ou em timeout, os logins seguem e o evento é registrado; cadastro e pedido de redefinição respondem 503 CAPTCHA_UNAVAILABLE
- [x] #5 O IP enviado à siteverify vem de CF-Connecting-IP quando presente
- [x] #6 Os mesmos endpoints têm rate limit por IP configurável, respondendo 429 RATE_LIMITED com Retry-After
- [x] #7 Há métrica de verificações por resultado e span na chamada à siteverify
- [x] #8 Opções validadas na partida; o captcha pode ser desligado por configuração
- [x] #9 Testes de unidade e de integração cobrem token ausente, inválido, válido e siteverify indisponível, sem rede real
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1) Em cada BFF (Api): pasta Captcha/ com TurnstileOptions, ITurnstileVerifier/TurnstileVerifier (HttpClient tipado, sem retry pois o token e de uso unico), TurnstileVerification, CaptchaFailurePolicy, CaptchaEndpointFilter, RequireCaptcha, ClientIpResolver (CF-Connecting-IP -> IP remoto). 2) Rate limit por IP: AnonymousRateLimitOptions + policy AnonymousPerIp (fixed window por path+IP); no bff-student um unico AddRateLimiter com OnRejected unico que diferencia por policy do endpoint; no bff-admin AddRateLimiter + UseRateLimiter. 3) Telemetria: counter captcha.verifications{result}, counter de rate limit, Activity captcha.siteverify. 4) Aplicar nos 5 endpoints; appsettings (Enabled true, SecretKey vazio; Development com secret de teste). 5) Testes: unidade do verificador e filtro; e2e/integracao com handler falso da siteverify cobrindo ausente 400, reprovado 403, valido, indisponivel (login segue, cadastro/reset 503) e 429. 6) Gate: dotnet build e test nos dois slnx.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Implementado nos dois BFFs (Api/Captcha, Extensions, telemetria, appsettings, testes). Gate: bff-student 323 testes OK; bff-admin 331 testes OK; build 0 warnings. CF-Connecting-IP so e confiavel quando o trafego passa pela Cloudflare; usado apenas como remoteip e chave de rate limit. Sem retry no cliente da siteverify (token de uso unico). idempotency_key nao enviado.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Filtro RequireCaptcha (header X-Captcha-Token, siteverify via HttpClient tipado, fail-open nos logins e fail-closed em cadastro e redefinição) e rate limit anônimo por path|IP nos dois BFFs, com OnRejected único preservando o de purchase intent. Verificado com dotnet test: bff-student 323/323 e bff-admin 331/331, e com o smoke por curl da TASK-37.5 (400 sem token, resposta de negócio com token de teste).
<!-- SECTION:FINAL_SUMMARY:END -->
