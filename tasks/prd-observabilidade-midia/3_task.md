---
status: done
task_kind: vertical
blocked_by: []
gate: 'dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.MediaQueueMetricsTests --minimum-expected-tests 4'
gate_expect: "Pelo menos 4 testes passam sobre Postgres real: oldest_waiting com fila de idades conhecidas, fila vazia → 0 (não ausência), claim incrementa claimed e wait, recollect incrementa retried"
---

# 3.0 Profundidade e idade da fila de preparação visíveis no painel

**Fatia:** V-03 · **Cobre:** RF-03, US-02, DP-03, DP-04 · **Spec:** `techspec.md#v-03` e § Tabela
de instrumentos · **ADR:** [ADR-0006](../../docs/adr/0006-preparacao-de-video-e-custodia-de-chave.md)

## Comportamento

A pergunta "a fila está andando?" passa a ter resposta no painel:

- `media.videos.oldest_waiting` (gauge `s`, worker) — snapshot: `min(uploaded_at)` entre vídeos
  `received`; **0 quando a fila está vazia** (zero explícito, não série ausente — distinção que o
  painel de staleness de 1.0 complementa);
- `media.videos.claimed` (`{video}`, worker) — no claim do `ClaimNextAsync`;
- `media.videos.retried` (`{video}`, worker) — quando o claim pega vídeo com
  `preparation_attempts > 1`;
- `media.videos.wait` (histograma `s`, buckets 1 s–1 h, worker) — no claim: `now - uploaded_at`.

Profundidade continua vindo de `media.videos.count{status=received}` (sem instrumento novo — a
seção **Fila** do dashboard combina profundidade, idade e preso, este último já visível desde 1.0).
Vídeo em preparação de arquivo grande não conta como preso (a régua existente de 4×duração/12 h é
a mesma — nada muda nela).

A instrumentação do claim não entra na transação de claim nem adiciona consulta: os valores vêm do
que o claim já sabe; o snapshot nova usa os índices parciais da fila
(`ix_videos_preparation_queue`) e `IgnoreQueryFilters` como o padrão existente.

## Fora do escopo desta task

Métricas de resultado da preparação (4.0), outbox (5.0), alerta de fila parada (6.0 — regra A4
consome `oldest_waiting` desta task). Nenhuma mudança na régua de preso nem na lease.

## Decisões fechadas

- A fila observada é o estado no banco com lease (ADR-0006 — conformada; sem evento de
  mensageria).
- Gauges/histogramas só no worker; sem tenant, sem Id.

## Modificar / Referenciar

- **modificar:** `src/media/src/CodeForCoders.Media.Application/Common/MediaTelemetry.cs`
  (instrumentos da fila); `.../UseCases/Videos/PrepareVideo/PrepareVideo.cs` (claim);
  `src/media/src/CodeForCoders.Media.Infra.Messaging/MediaVolumeMetricsWorker.cs` (gauge
  `oldest_waiting` no snapshot)
- **ref:** `src/media/src/CodeForCoders.Media.Infra.Data/Videos/VideoPreparationRepository.cs`
  (invariantes do claim — `FOR UPDATE SKIP LOCKED`, orçamento de disco; instrumentação fora da
  transação); `techspec.md` § Tabela de instrumentos

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/media` | `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/media` | `dotnet build src/media/CodeForCoders.Media.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.UnitTests/CodeForCoders.Media.UnitTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.ArchitectureTests/CodeForCoders.Media.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `scripts/kibana/` | `python3 scripts/kibana/import_observabilidade.py --verify-only` | exit 0 (seção Fila referenciando os instrumentos novos) | Gate de 1.0 |

## Pronto quando

- [x] Gate passa (exit 0) com pelo menos 4 testes.
- [x] Fila com dois `received` (10 min e 2 h) → `oldest_waiting` ≈ 2 h; fila vazia → 0.
- [ ] Smoke: vídeo enviado aparece na profundidade, sai dela ao ser reclamado, e `wait` registra
      uma observação coerente com a espera.
