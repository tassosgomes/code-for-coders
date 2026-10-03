---
tsg_artifact: tasks
product: code-4-coders
capability: CAP-006
version: 1.0
status: approved
updated: 2026-09-28
sources: tasks/prd-observabilidade-midia/prd.md@1.0, tasks/prd-observabilidade-midia/techspec.md@1.0
---

# Plano de Implementação — Observabilidade operacional da ingestão de mídia (CAP-006)

> **TechSpec de origem:** [techspec.md](techspec.md), aprovada em 2026-09-28
> **Escopo:** Backend + infraestrutura de telemetria (`media` api/worker, Kibana do servidor dev, Compose)
> **ADRs pertinentes:** [0006](../../../docs/adr/0006-preparacao-de-video-e-custodia-de-chave.md), [0008](../../../docs/adr/0008-observabilidade-kibana-como-codigo.md)
> **Contratos:** nenhum contrato HTTP/mensageria/dados novo; a tabela de instrumentos da TechSpec (§ Resumo Executivo) é o contrato aplicação ↔ dashboard/alertas
> **Status do plano:** Confirmado para implementação

## Visão Geral

Ao final, a equipe abre o Kibana e responde em um minuto — sem abrir o banco — quantos envios e
preparações estão em andamento, há quanto tempo a fila espera, quais preparações falharam e por
quê, e se os eventos de mídia estão saindo (outbox/DLQ). Cinco alertas acendem sozinhos no Kibana
quando o pipeline para, e resolvem sozinhos. Nada muda para o professor: nenhuma tela, nenhuma API,
nenhum evento novo — o pipeline apenas passa a se reportar, com o provisionamento do Kibana vindo
do repositório (ADR-0008).

## Fases

### Fase 1 — Retrato imediato

1.0 dashboard "Pipeline de Mídia" v1 com os sinais já emitidos + provisionamento como código;
7.0 paridade do ambiente local (endpoint OTLP interpolável). Checkpoint: dashboard mostra vídeos
por estado com idade da última atualização, importável do repo por segunda pessoa; a stack local
aponta ao Elastic do servidor dev e o painel reflete um vídeo enviado localmente.

### Fase 2 — Funil e fila

2.0 funil de envio (criadas, concluídas, expiradas, tamanho, pendentes); 3.0 profundidade e idade
da fila + claimed/retried/espera. Checkpoint: um envio concluído e um abandonado aparecem como
tais no painel; a fila responde "está andando?" com profundidade e idade do mais antigo.

### Fase 3 — Preparação e outbox

4.0 resultado da preparação (duração por etapa, tempo até pronto, tentativas, falhas por motivo) +
`video.id` em span/log; 5.0 outbox (pendentes, idade, esgotados, publicados) + DLQ via Management
API. Checkpoint: uma falha provocada aparece com motivo no painel e é investigada no Discover pelo
`video.id` sem reproduzir o envio; outbox saudável mostra pendentes ≈ 0.

### Fase 4 — Alertas

6.0 regras A1–A5 versionadas e importadas. Checkpoint: lote de 4 vídeos corrompidos ativa A2 e
resolve após a janela; `media-worker` parado ativa A4; ambos resolvem sozinhos ao normalizar.

## Mapa de Entrega

| Fatia | Task | Comportamento observável | Gate | Bloqueado por |
|---|---|---|---|---|
| V-01 | [1.0](1_task.md) | Dashboard "Pipeline de Mídia" com vídeos por estado, preso e volume, com staleness visível, importado do repo | `python3 scripts/kibana/import_observabilidade.py --verify-only` | Nenhum |
| V-02 | [2.0](2_task.md) | Funil do envio: criadas, concluídas, expiradas, tamanho e pendentes no painel | Media `MediaUploadFunnelMetricsTests` (≥ 4) | Nenhum |
| V-03 | [3.0](3_task.md) | Fila: profundidade, idade do mais antigo, claimed/retried/espera no painel | Media `MediaQueueMetricsTests` (≥ 4) | Nenhum |
| V-04 | [4.0](4_task.md) | Preparação: duração por etapa, tempo até pronto, tentativas, falhas por motivo; `video.id` em span/log | Media `MediaPreparationMetricsTests` (≥ 5) | Nenhum |
| V-05 | [5.0](5_task.md) | Outbox (pendentes, idade, esgotados, publicados) e DLQ no mesmo painel | Media `MediaOutboxMetricsTests` (≥ 5) | Nenhum |
| V-06 | [6.0](6_task.md) | Alertas A1–A5 ativos/resolvidos sozinhos no Kibana, sem conector externo | `python3 scripts/kibana/import_observabilidade.py --verify-only` | 2.0, 3.0, 4.0, 5.0 |
| V-07 | [7.0](7_task.md) | Stack local enviando telemetria ao Elastic do servidor dev via variável | `docker compose config` + interpolação do override | 1.0 |

### Habilitadores

Nenhum: a TechSpec declara a fatia V-01 como comportamento observável (dashboard) e todas as
demais carregam instrumentação + painel na mesma linha.

## Tasks

- [x] 1.0 Dashboard "Pipeline de Mídia" v1 provisionado como código sobre os sinais existentes
- [x] 2.0 Funil e tamanho dos envios visíveis no painel
- [x] 3.0 Profundidade e idade da fila de preparação visíveis no painel
- [x] 4.0 Resultado da preparação com motivo, e investigação por vídeo até o trace
- [x] 5.0 Saúde do outbox e da DLQ no mesmo painel
- [x] 6.0 Alertas A1–A5 no Kibana, versionados e resolvendo sozinhos
- [x] 7.0 Paridade local: endpoint OTLP interpolável no Compose

## Caminho crítico e lanes

`1.0 → 7.0` fecha a Fase 1; `2.0 → 3.0 → 4.0 → 5.0 → 6.0` fecha o resto, na ordem numérica.
2.0/3.0/4.0/5.0 são independentes entre si (árvores de instrumento distintas); a execução
sequencial do orquestrador segue a numeração e 6.0 só nasce depois das quatro.

## Artefatos compartilhados

| Artefato | Produzido em | Evolui em |
|---|---|---|
| Saved objects NDJSON + script de import/verificação (`scripts/kibana/`) | 1.0 | 2.0–5.0 (seções), 6.0 (alertas) |
| Instrumentos em `MediaTelemetry` | 2.0 | 3.0, 4.0, 5.0 |
| Snapshot do worker (`MediaVolumeMetricsWorker`) | 2.0 (uploads pendentes) | 3.0 (oldest_waiting), 5.0 (outbox) |
| Opção `RabbitMq:ManagementUri` + fio `.env` → compose | 5.0 | — |
| `docs/infra-servidor-desenv.md` | 1.0 (provisionamento) | 7.0 (override local) |

## Verificação herdada

| Componente | Fonte | Checks e limites | Estado da base | Resolução planejada |
|---|---|---|---|---|
| `src/media` | `.github/workflows/media.yml` → `tassosgomes/template-pipeline/.github/workflows/ci-dotnet.yml@v1` (revisito do plano de ingestão, 2026-09-26) | `dotnet restore`; `dotnet format --verify-no-changes`; `dotnet test` (MTP, Debug) cobertura ≥ 70%; `dotnet publish -c Release`; imagem `src/media/Dockerfile` | Base integrada com validação full aprovada (CAP-006, PR #68) | Nenhuma falha herdada conhecida; cobertura medida no CI a cada PR |
| `scripts/kibana/` | Sem CI dedicado | Gate é o próprio `import_observabilidade.py --verify-only` (validação estrutural determinística) | Não existia; nasce no 1.0 | Limitação registrada: paridade de import real no servidor dev é o smoke de 1.0/6.0 |
| `docker-compose.yml` / `docker-compose.coolify.yml` | Sem CI para compose | `docker compose config` local como verificação (gate de 7.0 e check de 5.0) | Válido em main (stack sobe) | Limitação registrada: sem CI de compose |

Pré-requisitos de ambiente declarados na TechSpec: `RABBITMQ_MANAGEMENT_URI` já no `.env` do
checkout principal (adicionada pelo usuário em 2026-09-28, `https://rabbit.tasso.dev.br`) —
copiar para o `.env` da worktree no início da execução (padrão usado em ingestão); ILM do índice
`metrics-generic` com 30 dias aplicada no servidor dev antes do checkpoint da Fase 2 (ação do time
de plataforma); rede alcançando `192.168.0.5:4317` e o Kibana para os smokes.

## Cobertura

| Requisito | Task(s) |
|---|---|
| RF-01 | 1.0, 2.0 |
| RF-02 | 2.0 |
| RF-03 | 1.0, 3.0 |
| RF-04 | 4.0 |
| RF-05 | 5.0 |
| RF-06 | 6.0 |
| RF-07 | 4.0 |
| US-01 | 1.0 |
| US-02 | 3.0 |
| US-03 | 4.0 |
| US-04 | 6.0 |
| US-05 | 4.0 |
| US-06 | 5.0 |
| DP-01 | 1.0 |
| DP-02 | 6.0 |
| DP-03 | 2.0, 3.0, 4.0, 5.0 |
| DP-04 | 2.0, 3.0, 4.0, 5.0 |

Termos canônicos (vocabulário do painel) nascem no 1.0; métricas de sucesso do PRD são
acompanhadas no painel produzido (1.0–6.0), não em task dedicada — não há artefato de software
faltando. Migração de dados: nenhuma. Auditoria: fora de escopo pelo PRD.

**Ajuste em relação à TechSpec:** `docker-compose.yml` (e `docker-compose.coolify.yml`) entram
também em 5.0, além de V-07 — a decisão "URI e credenciais via `.env`" exige o fio
`RabbitMq__ManagementUri` no compose; a TechSpec listava compose apenas em V-07.

> Verificado por `python3 .agents/skills/tsg-flow-task-creator/scripts/validate_plan.py tasks/prd-observabilidade-midia/` antes do handoff.
