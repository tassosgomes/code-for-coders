---
id: TASK-10
title: Split do stack Coolify em 2 resources (apis + web)
status: Done
assignee: []
created_date: '2026-10-06 20:15'
updated_date: '2026-10-06 20:34'
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

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Executado em 2026-10-06: PR #179 (merge b0b7c0a) splitou docker-compose.coolify.yml em docker-compose.coolify.apis.yml (9 servicos) e docker-compose.coolify.web.yml (4 servicos) com rede externa c4c-shared. Resources Coolify code4coders-apis (qki6sxkyy5wu7sp5krcjdevw) e code4coders-web (bqoiqurbo23u8qeqxro3wi2e) criados via API + DB (docker_compose_raw), envs migradas por resource (69 apis / 23 web), dominios replicados. Ambos deploys via Coolify API finalizaram 'finished'; 13/13 containers healthy; 11/11 URLs publicas 200. Resource antigo code4coders-dev deletado; rede jjun1b8nxnz0vqjaumwhi4ro removida. Ambos resources apontando para main. Correcoes colaterais do dia: envs com valor junk (run scripts/generate-local-env.sh) corrigidas (NOTIFICATION_IDENTITY_*, COMMERCE_BILLING_*) e SERVICE_*_MEDIA_EDGE adicionadas.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Split em 2 resources concluido e validado: deploys via Coolify API retornam finished (antes falhavam com Argument list too long / issue #11737); 13 containers healthy e 11 endpoints publicos 200 apos cutover. PR #179 merged.
<!-- SECTION:FINAL_SUMMARY:END -->
