---
id: TASK-36
title: Expor frontend e APIs de dev via Cloudflare Tunnel com URL publica estavel
status: Done
assignee: []
created_date: '2026-10-16 20:37'
updated_date: '2026-10-20 12:31'
labels:
  - backend
  - frontend
dependencies: []
priority: medium
ordinal: 49000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
O frontend de dev (admin-spa e student-spa) e as APIs que o servem via nginx hoje so sao acessiveis na LAN, pois dev-code4coders.tasso.dev.br resolve para 192.168.0.5. Para permitir uso externo do frontend sem trocar API_URL, CORS e links gerados, o acesso publico deve usar o mesmo hostname via Cloudflare Tunnel, que ja e o padrao do webhook da Stripe neste repo.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Ingress do tunnel no repo expoe dev-code4coders.tasso.dev.br para /admin, /students e /media mantendo a regra restrita do webhook Stripe
- [x] #2 DNS publico de dev-code4coders.tasso.dev.br aponta para o tunnel (CNAME proxied), nao mais para 192.168.0.5
- [x] #3 Home do admin e do student respondem 200 via internet e chamadas de API mesma-origem funcionam
- [x] #4 Webhook Stripe continua restrito ao proprio path e hostname, sem regressao
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Estender infra/cloudflared/billing-webhook.yml com host dev-code4coders.tasso.dev.br e paths /admin, /students, /media para os servicos na rede c4c-shared, mantendo a regra Stripe em primeiro. 2. Atualizar comentarios no docker-compose.komodo.apis.yml. 3. Validar YAML e compose config. 4. Sincronizar yml no desenv-server e recriar so o container cloudflared-billing-webhook com profile stripe-webhook. 5. Trocar DNS dev-code4coders de A 192.168.0.5 para CNAME proxied do tunnel via API Cloudflare no servidor. 6. Verificar HTTPS publico, logs do cloudflared e regressao do webhook. 7. Documentar e registrar rollback.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Ingress estendido em infra/cloudflared/billing-webhook.yml (mesmo tunnel c4c-billing-webhook, sem novas credenciais): dev-code4coders.tasso.dev.br com /admin*->admin-spa:8080, /students*->student-spa:8080, /media*->media-edge:8080; regra Stripe mantida em primeiro. Compose config validado no servidor, container recriado com --profile stripe-webhook --force-recreate, logs com Registered tunnel connection e sem erro de ingress. DNS dev-code4coders convertido de A 192.168.0.5 para CNAME proxied do tunnel via API Cloudflare. Verificado via internet: /admin/ 200, /students/ 200, /admin/api/v1/videos 401 problem+json SESSION_REQUIRED, webhook sem assinatura 400, paths invalidos 404 do tunnel. Doc operacional atualizada em docs/infra-servidor-desenv.md.

2026-10-20: a rota /media deste ingress repassava o prefixo ao media-edge e os segmentos HLS respondiam 403; corrigido no media-edge pela TASK-39 (PR #242).
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Reutilizado o tunnel c4c-billing-webhook (sem novas credenciais): ingress agora expoe dev-code4coders.tasso.dev.br (/admin, /students, /media) alem da regra restrita do webhook Stripe; DNS trocado de A 192.168.0.5 para CNAME proxied do tunnel; container recriado no desenv-server com logs limpos. Verificacao via internet: /admin/ e /students/ 200, /admin/api/v1/videos 401 SESSION_REQUIRED (BFF alcancado mesma-origem), webhook sem assinatura 400 sem regressao, paths invalidos 404 do tunnel. Rollback: reverter o DNS para A 192.168.0.5 e/ou o yml.
<!-- SECTION:FINAL_SUMMARY:END -->
