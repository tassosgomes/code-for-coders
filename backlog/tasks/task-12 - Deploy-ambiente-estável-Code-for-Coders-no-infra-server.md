---
id: TASK-12
title: Deploy ambiente estável Code for Coders no infra-server
status: Done
assignee: []
created_date: '2026-10-09 00:35'
updated_date: '2026-10-09 00:56'
labels: []
dependencies: []
references:
  - codeforcoders-notification-infra-data-dependencyinjection
  - codeforcoders-notification-api-program
modified_files:
  - docker-compose.stable.infra.yml
  - docker-compose.stable.yml
  - deploy/stable/otel-collector.yaml
  - deploy/stable/Caddyfile.snippet
  - deploy/stable/README.md
  - deploy/stable/minio.Dockerfile
  - scripts/generate-local-env.sh
  - scripts/remote-infra.sh
priority: high
type: task
ordinal: 10000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Preparar um ambiente estável, isolado do desenvolvimento, no host infra-server para a aplicação Code for Coders. Publicar os SPAs no domínio code4coders.tasso.dev.br, preservando /admin/ e /students/, com TLS e roteamento pelo proxy de borda existente. As variáveis AWS_MEDIA_BUCKET, BILLING_WEBHOOK_TUNNEL_CREDENTIALS e MEDIA_EDGE_PUBLIC_URL permanecem vazias conforme orientação do usuário; serviços que dependam delas devem seguir o comportamento seguro definido para a implantação.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Serviços da aplicação e infraestrutura própria do ambiente estável executam no infra-server sem compartilhar bancos ou filas com desenvolvimento.
- [x] #2 https://code4coders.tasso.dev.br/admin/ e /students/ e suas rotas internas entregam os respectivos SPAs via HTTPS.
- [x] #3 As APIs ficam prontas após provisionar os recursos e aplicar todas as migrations.
- [x] #4 As variáveis AWS_MEDIA_BUCKET, BILLING_WEBHOOK_TUNNEL_CREDENTIALS e MEDIA_EDGE_PUBLIC_URL permanecem vazias.
- [x] #5 A configuração estável não interrompe dev-code4coders.tasso.dev.br nem os demais serviços do host de borda.
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Inspecionar o proxy de borda, DNS, recursos disponíveis e configuração atual da aplicação.
2. Preparar os arquivos Compose e env do ambiente isolado no infra-server, com as variáveis pendentes vazias.
3. Provisionar dependências, aplicar migrations, subir os serviços de aplicação e habilitar o roteamento TLS para o domínio estável.
4. Validar health checks, TLS, SPAs, rotas profundas e os domínios já ativos.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Implantacao validada em 2026-10-09. No infra-server, as dependencias isoladas (PostgreSQL, RabbitMQ, Valkey, MinIO, OTel Collector e smtp4dev) passaram scripts/remote-infra.sh check; as migrations dos nove bancos foram aplicadas. Os dez containers de aplicacao ficaram saudaveis e /health/ready retornou 200 para identity, learning, commerce, billing, notification, audit, bff-admin e bff-student.

O DNS A de code4coders.tasso.dev.br aponta para 192.168.0.5, seguindo o padrao dos outros hosts; Caddy obteve certificado ACME DNS-01 e encaminha /admin/ e /students/ para o infra-server. Validacao HTTPS: / redireciona para /admin/ (302); /admin e /students (308); os dois SPAs, deep links, runtime-env.js e asset JS retornam 200. A rota admin sem sessao retorna 401 e o catalogo publico de estudantes retorna 200. Depois do reload, dev-code4coders retornou 302 e komo.tasso.dev.br retornou 200.

AWS_MEDIA_BUCKET, BILLING_WEBHOOK_TUNNEL_CREDENTIALS e MEDIA_EDGE_PUBLIC_URL continuam vazias; media-config e stripe-webhook nao iniciam. Billing usa chaves Stripe de teste e nao recebe eventos sem o tunel. O servico de notificacoes inicia em Production via HTTP, mas o endpoint padrao termina em .invalid; e-mail depende de configurar um provedor. O OTel Collector usa exporter debug com retencao apenas nos logs Docker. Os builds npm dos SPAs reportaram alertas de seguranca no npm ci (admin: 4 high; students: 4 high e 3 moderate).

Limite de rede: o registro DNS A aponta para 192.168.0.5, um IP privado, seguindo o padrão atual do Caddy. A validação foi feita na rede local; curl para 177.37.142.176:443 expirou, então acesso pela internet pública não foi confirmado. Para expor publicamente, falta configurar NAT/firewall de entrada e apontar o DNS para o IP público.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Ambiente estavel isolado implantado no infra-server, com dependencias proprias, migrations aplicadas e oito APIs em Production. O DNS code4coders.tasso.dev.br serve /admin e /students com TLS pelo Caddy; foram validados deep links, assets, BFFs, health checks e continuidade de dev/Komo. As tres variaveis solicitadas permanecem vazias. Midia, eventos Stripe, entrega de e-mail e consulta persistente de telemetria aguardam configuracao dos respectivos provedores/credenciais. npm ci reportou alertas de seguranca nas dependencias dos SPAs.

A resolução DNS permanece em IP privado (192.168.0.5); a validação foi interna e o acesso pela internet pública requer rota de entrada/NAT e ajuste de DNS.
<!-- SECTION:FINAL_SUMMARY:END -->
