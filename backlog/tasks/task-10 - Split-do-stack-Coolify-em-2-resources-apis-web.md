---
id: TASK-10
title: Split do stack Coolify em 2 resources (apis + web)
status: To Do
assignee: []
created_date: '2026-10-06 20:15'
labels:
  - infra
  - deploy
dependencies: []
ordinal: 8000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Deploys via Coolify falham com 'Argument list too long' (issue coollabsio/coolify#11737): compose renderizado de 119KB excede MAX_ARG_STR_SZ. Split em docker-compose.coolify.apis.yml (9 serviços backend) e docker-compose.coolify.web.yml (4 serviços frontend), rede externa c4c-shared para DNS cross-app, novos resources code4coders-apis e code4coders-web no Coolify, migração de envs/domínios e cutover.
<!-- SECTION:DESCRIPTION:END -->
