---
id: TASK-39
title: >-
  media-edge responde 403 a segmentos HLS servidos pelo tunnel com prefixo
  /media
status: In Progress
assignee:
  - '@claude'
created_date: '2026-10-20 12:24'
updated_date: '2026-10-20 12:27'
labels:
  - backend
  - bug
dependencies: []
priority: high
ordinal: 54000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Desde a TASK-36 o hostname dev-code4coders.tasso.dev.br passa pelo Cloudflare Tunnel, cuja regra ^/media.* encaminha o caminho completo ao media-edge sem remover o prefixo (o Caddy na LAN removia via handle_path). O nginx do media-edge só casa segmentos em /<tenant>/<video>/hls/..., então /media/... cai em location / e responde 403, e o aluno não reproduz vídeos.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 media-edge aceita o caminho do segmento com e sem o prefixo /media sem afrouxar regex, secure_link nem limit_except
- [x] #2 Segmento assinado válido responde 200 via https://dev-code4coders.tasso.dev.br/media/... e assinatura inválida continua 403
- [ ] #3 Player do aluno reproduz o vídeo em dev
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Acrescentar rewrite ^/media(/.*)$ $1 (sem flag) no início do server do src/media-edge/default.conf.template, antes dos set que usam $uri. 2. Validar localmente com a imagem do edge: nginx -t e curl com/sem prefixo, assinatura válida e inválida. 3. Sincronizar o template no desenv-server, rebuild e recriar só o media-edge. 4. Validar via https público e no player do aluno.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Causa: o ingress ^/media.* do tunnel c4c-billing-webhook (TASK-36) repassa /media/... ao media-edge sem remover o prefixo; o nginx caía em location / (403) antes do secure_link. Correção: rewrite ^/media(/.*)$ $1 sem flag no início do server. Validação local com origem falsa: assinatura válida com e sem /media chega à origem em /<bucket>/media/<tenant>/...; assinatura inválida, expiração adulterada, /media/media, %2F e /mediax continuam 403. Deploy no desenv-server (scp do template + compose up -d --build --no-deps media-edge), container healthy. Via https://dev-code4coders.tasso.dev.br/media/...: assinatura válida 200 video/MP2T, Range 206, assinatura inválida 403; /students/ 200 e webhook Stripe 400 sem assinatura (sem regressão). Pendente: conferir o player com conta de aluno.
<!-- SECTION:NOTES:END -->
