---
id: TASK-37.5
title: 'Captcha: configuração dos ambientes e smoke'
status: In Progress
assignee:
  - '@claude'
created_date: '2026-10-16 20:44'
updated_date: '2026-10-16 21:52'
labels:
  - security
  - infra
dependencies:
  - TASK-37.3
  - TASK-37.4
modified_files:
  - docker-compose.yml
  - docker-compose.komodo.web.yml
  - scripts/generate-local-env.sh
  - deploy/topology.md
  - deploy/stable/README.md
parent_task_id: TASK-37
type: chore
ordinal: 52000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Os novos segredos e sitekeys precisam chegar a todos os modos de execução (local, remoto, Cloud Agents e deploy) sem quebrar quem sobe a stack, e o fluxo precisa ser provado de ponta a ponta.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 scripts/generate-local-env.sh, .env de exemplo, docker-compose.yml e docker-compose.remote.yml levam as variáveis novas, usando por padrão as chaves de teste oficiais do Turnstile
- [ ] #2 A configuração de deploy recebe a sitekey e o secret reais de cada widget
- [x] #3 Smoke local com scripts/apps.sh start --remote: login, cadastro e esqueci a senha funcionam nos dois SPAs com as chaves de teste, e uma chamada direta sem token aos BFFs recebe 400
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1) Variáveis nos composes (local com default de teste; komodo.web com :? sem default, herdado pelo stable). 2) generate-local-env.sh grava chaves de teste (novo e existente). 3) Docs de deploy (topology.md, stable/README). 4) Smoke com remote-infra + apps.sh start --remote, curl nos 5 endpoints e Playwright se disponível.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Variáveis: TURNSTILE_{STUDENT,ADMIN}_{SITE,SECRET}_KEY. Local: default de teste no compose e no generate-local-env.sh. komodo.web (dev e estável): ':?' sem default; stable.yml e remote.yml herdam por merge (sem override). Smoke: curl sem token 400 CAPTCHA_REQUIRED nos 5 endpoints; com token dummy 401 INVALID_CREDENTIALS (logins) e 202 (cadastro, resets). UI via Playwright nos dois SPAs: widget de teste resolve, submit libera, respostas 401/202. Infra remota deu 502 IDENTITY_UNAVAILABLE intermitente (conexão resetada pelo Postgres compartilhado).

Orquestrador: AC2 aguarda os widgets reais e as chaves no .env de desenv-server e infra-server; o compose já exige as variáveis sem default. Ajuste posterior: widget do student-spa com size flexible.
<!-- SECTION:NOTES:END -->
