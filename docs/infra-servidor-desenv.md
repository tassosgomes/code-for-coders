# Infra de desenvolvimento — servidor 192.168.0.5 (`desenv`)

Stack compartilhada de desenvolvimento hospedada no servidor `192.168.0.5`. Fonte de verdade operacional: `~/infra` no servidor (compose, `.env`, scripts e README próprios).

## Serviços de dados (atuais)

| Serviço | Versão | Acesso | Observações |
|---|---|---|---|
| PostgreSQL | 18 | `192.168.0.5:5432` | Tunado: shared_buffers 2GB, 200 conns, pg_stat_statements. Provisionamento de app: `~/infra/scripts/new-db.sh` |
| RabbitMQ | 4.3 | `192.168.0.5:5672` / UI [rabbit.tasso.dev.br](https://rabbit.tasso.dev.br) | Watermark 0.5, disk free 2GB, nofile 65536 |
| Valkey | 8.1 | `192.168.0.5:6379` (senha no `.env`) | maxmemory 1GB allkeys-lru, AOF everysec |

## Plataforma (atuais)

| Serviço | Versão | Acesso | Papel |
|---|---|---|---|
| LogTo | latest | [auth.tasso.dev.br](https://auth.tasso.dev.br) / [auth-admin](https://auth-admin.tasso.dev.br) | Identidade |
| Komodo core+periphery | 2 | [komo.tasso.dev.br](https://komo.tasso.dev.br) | Deploy/monitoração de containers |
| FerretDB + DocumentDB | 2.7 | interno | Backend Mongo do Komodo (MongoDB oficial não sobe em kernel >= 6.19) |
| pgAdmin | 9 | [pg.tasso.dev.br](https://pg.tasso.dev.br) | Admin do Postgres |
| Valkey Admin | 1.2 | [valkey.tasso.dev.br](https://valkey.tasso.dev.br) | Basic auth no Caddy |
| Caddy | custom (plugin Cloudflare) | 80/443 | Edge TLS (DNS-01), roteia `*.tasso.dev.br` |

## Observabilidade e email (novas — set/2026)

| Serviço | Versão | Acesso | Papel |
|---|---|---|---|
| Elasticsearch | 9.5.4 | interno (basic auth) | Storage de telemetria; single-node, heap 1g / limite 2g |
| Kibana | 9.5.4 | [kibana.tasso.dev.br](https://kibana.tasso.dev.br) | Visualização (Discover/Logs); login `elastic` + `ELASTIC_PASSWORD` do `.env` |
| OTel Collector | 0.145.0 | gRPC `192.168.0.5:4317` / HTTP `:4318` | Ingestão OTLP dos apps → Elasticsearch |
| smtp4dev | v3 | SMTP `192.168.0.5:25` / UI [smtp.tasso.dev.br](https://smtp.tasso.dev.br) | Caixa de email dev compartilhada; UI com basic auth (`admin`) |

### Roteamento de telemetria

```
apps (OTLP) ──► collector:4317/4318 ──► traces  ──► traces-generic.otel-default  (Discover)
                                       ├─ metrics ──► metrics-generic.otel-default (Discover)
                                       └─ logs    ──► logs-generic.otel-default    (Logs/Discover)
```

- APM UI do Kibana (traces-apm.*) é evolução futura: exigiria apm-server ou o modo `ecs` do exporter (instável no collector 0.145).
- Os endpoints OTLP são idênticos aos do compose local (`http://otel-collector:4317` → `http://192.168.0.5:4317`).

## Transversal

- **Backup** diário 03:00: `pg_dumpall` (postgres + komodo-db) + definições RabbitMQ; retenção 7 dias em `~/infra/backups/daily`. ES/Kibana/smtp4dev ficam fora (dados efêmeros de dev).
- **Segredos**: todos em `~/infra/.env` (chmod 600), interpolados pelo compose. Valores com `$` (ex.: bcrypt) devem ficar entre aspas simples.
- **DNS**: registros A DNS-only no Cloudflare (`*.tasso.dev.br` → 192.168.0.5) via `~/infra/scripts/cloudflare-dns.sh`.
- **Consumo**: ~4.9 Gi usados de 15 Gi (novos serviços ~2.9 Gi).
- **Paridade com o compose local do repo**: completa — Postgres, RabbitMQ, Valkey, OTel Collector e smtp4dev. Falta apenas repontear as apps (quando decidirmos migrar o runtime de dev).

## Notas conhecidas

- **pgAdmin**: a imagem `pgadmin4:9` não inclui `curl`, o que quebrava o healthcheck do compose (app funcionava normalmente). Corrigido em set/2026 — check via `python3`/urllib.
- Firewall (`ufw`) do servidor desativado — serviços expostos confiam na LAN. Ativação é pré-condição recomendada antes de ampliar o uso do servidor.
