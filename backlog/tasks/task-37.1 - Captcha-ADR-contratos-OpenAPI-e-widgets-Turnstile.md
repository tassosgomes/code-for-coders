---
id: TASK-37.1
title: 'Captcha: ADR, contratos OpenAPI e widgets Turnstile'
status: Done
assignee:
  - '@claude'
created_date: '2026-10-16 20:44'
updated_date: '2026-10-16 23:36'
labels:
  - security
dependencies: []
modified_files:
  - docs/adr/0023-captcha-cloudflare-turnstile-nos-endpoints-anonimos.md
  - docs/adr/index.md
  - contracts/bff-student/openapi-conta.yaml
  - contracts/bff-admin/openapi-acesso-interno.yaml
parent_task_id: TASK-37
type: docs
ordinal: 48000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Antes do código, a decisão precisa ficar registrada e o contrato vigente precisa mostrar o header e as respostas novas, para BFFs e SPAs implementarem o mesmo acordo. Os widgets da Cloudflare (um para o admin, outro para o aluno) fornecem as sitekeys e os secrets reais.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 ADR nova em docs/adr registra o Turnstile, a comparação com o reCAPTCHA, os endpoints cobertos e excluídos e a política fail-open no login e fail-closed no cadastro e no pedido de redefinição
- [x] #2 docs/adr/index.md lista a ADR nova
- [x] #3 Os OpenAPI de bff-student e bff-admin em contracts/ declaram o header X-Captcha-Token nos endpoints cobertos e as respostas 400 CAPTCHA_REQUIRED, 403 CAPTCHA_INVALID, 503 CAPTCHA_UNAVAILABLE e 429 RATE_LIMITED
- [x] #4 Há dois widgets Turnstile (admin e aluno) em modo Managed com os hostnames dos ambientes, e a sitekey e o secret de cada um estão onde o deploy os lê
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Criar ADR-0023 (Turnstile) e listar em docs/adr/index.md. 2. Acrescentar X-Captcha-Token e respostas 400/403/429(/503) aos 5 endpoints em contracts/bff-student/openapi-conta.yaml e contracts/bff-admin/openapi-acesso-interno.yaml, de forma aditiva. 3. Validar YAML com yq e procurar testes de contrato. 4. Registrar notas.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
ADR-0023 criada e listada no índice (o índice já usa ADR-0022 para o segundo arquivo 0020). Contratos: bff-student/openapi-conta.yaml (registerStudent, createStudentSession, requestPasswordReset) e bff-admin/openapi-acesso-interno.yaml (createStaffSession, requestStaffPasswordReset) ganharam o parâmetro X-Captcha-Token e as respostas 400 CAPTCHA_REQUIRED, 403 CAPTCHA_INVALID, 429 RATE_LIMITED (Retry-After) e 503 CAPTCHA_UNAVAILABLE (só nos fail-closed). YAMLs validados com yq. Não há teste de contrato que leia esses OpenAPI. Critério #4 (widgets Cloudflare) fica com o orquestrador/usuário.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
ADR-0023 e contratos OpenAPI dos dois BFFs com X-Captcha-Token e respostas de captcha. Widgets Turnstile reais criados pelo usuário; deploy em dev em 2026-10-16 mostrou o widget e o login funcionou.
<!-- SECTION:FINAL_SUMMARY:END -->
