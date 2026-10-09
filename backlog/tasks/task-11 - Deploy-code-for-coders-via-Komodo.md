---
id: TASK-11
title: Deploy code-for-coders via Komodo
status: Done
assignee:
  - '@codex'
created_date: '2026-10-08 23:23'
updated_date: '2026-10-08 23:44'
labels:
  - infra
  - deploy
  - frontend
dependencies: []
modified_files:
  - docker-compose.komodo.apis.yml
  - docker-compose.komodo.web.yml
  - src/admin-spa/nginx.conf.template
  - src/student-spa/nginx.conf.template
type: task
ordinal: 9000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Publicar a aplicação code-for-coders no servidor desenv, usando a infraestrutura já disponível em ~/infra e o Komodo. As duas SPAs devem compartilhar https://dev-code4coders.tasso.dev.br, com o painel administrativo em /admin e a área de estudantes em /students. O usuário pediu para deixar AWS_MEDIA_BUCKET, BILLING_WEBHOOK_TUNNEL_CREDENTIALS e MEDIA_EDGE_PUBLIC_URL vazias até confirmar seus valores.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 O stack da aplicação é gerenciado pelo Komodo no servidor desenv, sem interromper o painel em https://komo.tasso.dev.br.
- [x] #2 https://dev-code4coders.tasso.dev.br/admin/ e /students/ carregam as SPAs, inclusive em navegação direta para rotas internas e seus assets.
- [x] #3 Cada SPA alcança seu BFF pelo mesmo domínio usando seu próprio prefixo, sem colisão entre as rotas /admin e /students.
- [x] #4 As três variáveis que o usuário pediu para deixar vazias permanecem vazias; serviços ou recursos dependentes delas e suas limitações ficam documentados.
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Criar uma configuração de stack própria para Komodo, preservando os Compose existentes do Coolify. Manter AWS_MEDIA_BUCKET, BILLING_WEBHOOK_TUNNEL_CREDENTIALS e MEDIA_EDGE_PUBLIC_URL vazias e deixar fora da subida automática os serviços que não iniciam sem elas. 2. Configurar as SPAs para compartilhar o domínio: /admin e /students, com API de cada BFF no prefixo da SPA e fallback de deep links; manter os paths atuais do Coolify. 3. Adicionar o host dev-code4coders.tasso.dev.br no Caddy e DNS, conectar Caddy e stack à rede compartilhada sem alterar os demais hosts da infraestrutura. 4. Criar/implantar o stack pelo Komodo e validar HTTPS, assets, deep links, isolamento dos BFFs e disponibilidade do painel Komodo; registrar limitações dos recursos com variáveis vazias.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Deploy validado pelo Komodo no servidor desenv: stack code-for-coders em running, 10 containers saudáveis; Caddy permanece saudável nas redes infra_default e c4c-shared, e https://komo.tasso.dev.br continuou respondendo 200. O certificado HTTPS do novo domínio foi emitido. Via curl no servidor com --resolve: / redireciona para /admin/; /admin e /students redirecionam com 308; rotas profundas de ambas as SPAs e assets JS/CSS retornam 200. As rotas /admin/api/v1 e /students/api/v1 chegam aos BFFs correspondentes (probe sem sessão retorna 401 application/problem+json) e ambos os /health/ready estão prontos. O resolver público 1.1.1.1 aponta o domínio para 192.168.0.5; o resolver padrão desta máquina ainda não o resolvia durante a verificação. AWS_MEDIA_BUCKET, MEDIA_EDGE_PUBLIC_URL e BILLING_WEBHOOK_TUNNEL_CREDENTIALS permanecem vazias. Serviços media-edge, media e media-worker estão fora do perfil padrão media-config; cloudflared-billing-webhook está fora do perfil stripe-webhook. Recursos de mídia e recebimento de webhooks Stripe ficam indisponíveis até a configuração dessas variáveis.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Implantada pelo Komodo em dev-code4coders.tasso.dev.br, com as SPAs em /admin e /students e APIs encaminhadas a cada BFF. Validado HTTPS, deep links, assets, readiness dos BFFs e continuidade do Komodo. As três variáveis solicitadas continuam vazias; mídia e túnel de webhook permanecem desativados até sua configuração.
<!-- SECTION:FINAL_SUMMARY:END -->
