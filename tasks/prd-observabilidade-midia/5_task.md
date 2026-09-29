---
status: pending
task_kind: vertical
blocked_by: []
gate: 'dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.MediaOutboxMetricsTests --minimum-expected-tests 5'
gate_expect: "Pelo menos 5 testes passam sobre Postgres/RabbitMQ reais: publicação conta published{event}, RegisterFailure conta publish_failed, snapshot reporta pending/oldest_pending/exhausted, gauge de outbox não é emitido pelo papel api, e a profundidade da DLQ chega ao gauge via Management API real"
---

# 5.0 Saúde do outbox e da DLQ no mesmo painel

**Fatia:** V-05 · **Cobre:** RF-05, US-06, DP-03, DP-04 · **Spec:** `techspec.md#v-05` e § Tabela
de instrumentos · **ADR:** —

## Comportamento

Os sinais obrigatórios do baseline para o `media` ficam visíveis:

- `media.outbox.published{event}` e `media.outbox.publish_failed{event}` (counters `{message}`,
  `event` ∈ {ativo-pronto, preparacao-falhou}) — incrementados em `PublishOneAsync` por **quem
  publica**: o worker roda nos dois papéis e cada publicação é contada uma vez pela instância que
  a fez (aditivo, sem dupla contagem);
- `media.outbox.pending`, `media.outbox.oldest_pending`, `media.outbox.exhausted` (gauges,
  **apenas worker**): snapshot — `processed_on IS NULL`, idade do pendente mais antigo (0 quando
  vazio) e `processed_on IS NULL AND attempts >= max`. O papel `api` não emite esses gauges: duas
  réplicas reportando o mesmo pendente dobrariam o número;
- `media.messaging.dlq.messages{queue}` (gauge `{message}`, `queue` =
  `media.platform-heartbeat.dlq`) — novo poller BackgroundService do papel worker consultando a
  **RabbitMQ Management API real** (`GET /api/queues/{vhost}/{name}`, basic auth do serviço) a cada
  ciclo de snapshot; falha de consulta vira warning e gap de série (visível), nunca exceção não
  tratada.

Configuração: `RabbitMq:ManagementUri` nova em `RabbitMqOptions` (validada), com fio no compose —
`RabbitMq__ManagementUri: ${RABBITMQ_MANAGEMENT_URI:-http://rabbitmq:15672}` no local e a variável
do `.env` (`https://rabbit.tasso.dev.br`, já adicionada pelo usuário) no remoto/produção. A seção
**Outbox e DLQ** do dashboard fecha o RF-05.

## Fora do escopo desta task

Mudar limites/attempts do outbox, topologia de filas, alerta A3/A5 (6.0 — consomem os gauges
daqui). O comportamento de publicação não muda: só passa a se reportar.

## Decisões fechadas

- Counters nos dois papéis, gauges só no worker (TechSpec, princípios de emissão 1–2).
- DLQ via Management API (decisão da TechSpec; alternatives rejeitadas lá) — adaptador real com
  fronteira controlada no teste (Testcontainers RabbitMQ management).
- Credenciais/URI por variável de ambiente; senha não vai para log, métrica ou painel.

## Modificar / Referenciar

- **modificar:** `src/media/src/CodeForCoders.Media.Application/Common/MediaTelemetry.cs`
  (instrumentos de outbox/DLQ); `src/media/src/CodeForCoders.Media.Infra.Messaging/OutboxPublisherWorker.cs`
  (counters em sucesso/falha); `.../MediaVolumeMetricsWorker.cs` (gauges de outbox no snapshot);
  `.../Configuration/RabbitMqOptions.cs` (`ManagementUri` validada);
  `.../DependencyInjection.cs` (poller de DLQ no papel worker); `docker-compose.yml` e
  `docker-compose.coolify.yml` (fio `RabbitMq__ManagementUri`)
- **ref:** `.../RabbitMqTopologyInitializer.cs` (nomes canônicos fila/DLQ); `.../Configuration/OutboxOptions.cs`
  (`MaxAttempts` — mesma fonte do esgotado); `.env` (`RABBITMQ_MANAGEMENT_URI`, `REMOTE_RABBITMQ_PASSWORD`
  — sem logar valores); skill `dotnet` (observabilidade)

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/media` | `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/media` | `dotnet build src/media/CodeForCoders.Media.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.UnitTests/CodeForCoders.Media.UnitTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.ArchitectureTests/CodeForCoders.Media.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `docker-compose.yml` | `docker compose -f docker-compose.yml config --quiet` | exit 0 (interpolação válida; default preservado) | Sem CI de compose — limitação registrada |
| `scripts/kibana/` | `python3 scripts/kibana/import_observabilidade.py --verify-only` | exit 0 (seção Outbox/DLQ referenciando os instrumentos novos) | Gate de 1.0 |

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 5 testes, incluindo o caso negativo do papel api (não
      emite gauge de outbox) e o adaptador real de Management API.
- [ ] Snapshot com 3 pendentes (1 esgotada) → pending 3, exhausted 1, oldest_pending coerente;
      outbox saudável → pending ≈ 0 abaixo da janela de polling.
- [ ] Smoke: mensagem na DLQ do heartbeat → gauge > 0 na seção Outbox e DLQ do painel.
