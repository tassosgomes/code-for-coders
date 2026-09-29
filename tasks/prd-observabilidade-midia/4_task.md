---
status: done
task_kind: vertical
blocked_by: []
gate: 'dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.MediaPreparationMetricsTests --minimum-expected-tests 5'
gate_expect: "Pelo menos 5 testes passam sobre Postgres/ffmpeg reais: falha conta failed{reason} e não completed, retentativa que chega a pronto conta retried sem failed, prepare_duration{stage} observa cada etapa, time_to_ready observa o pronto, e todo span media.video.* carrega video.id"
---

# 4.0 Resultado da preparação com motivo, e investigação por vídeo até o trace

**Fatia:** V-04 · **Cobre:** RF-04, RF-07, US-03, US-05, DP-03 · **Spec:** `techspec.md#v-04` e
§ Tabela de instrumentos · **ADR:** —

## Comportamento

O worker passa a reportar o **resultado** da preparação no instante do commit de status (fonte:
`failure_reason` persistido — não na exceção, para que o caminho de exceção genérica engolida pelo
workflow também conte):

- `media.videos.completed` (`{video}`) — no commit `ready`;
- `media.videos.failed{reason}` (`{video}`, `reason` ∈ {unreadable-file, unsupported-format,
  duration-exceeded, attempts-exhausted}) — no commit `failed`; o caminho sem motivo determinístico
  vira `attempts-exhausted` ao esgotar;
- `media.videos.time_to_ready` (histograma `s`, buckets 10 s–24 h) — no commit `ready`:
  `now - uploaded_at`;
- `media.videos.prepare_duration{stage}` (histograma `s`, `stage` ∈ {download, probe, transcode,
  publish}) — nos mesmos pontos dos spans `media.video.*` já existentes.

Falha passageira com retry que chega a pronto: `retried` +1 (já instrumentado em 3.0), `failed` 0,
`time_to_ready` com uma observação — tentativa intermediária não é falha.

**RF-07:** o helper único `StartActivity` (`PrepareVideo.cs:399`) passa a fixar também a tag
`video.id`, e o workflow/`CompleteVideoUpload` carregam `VideoId` no escopo de log estruturado.
Com isso, o Discover filtra `video.id` em traces e logs e a investigação de um vídeo não reproduz
o envio (Id é permitido em trace/log; a proibição é dimensão de métrica — DP-03).

A seção **Preparação** do dashboard mostra percentis por etapa, tempo até pronto, tentativas e
falhas por motivo em janela.

## Fora do escopo desta task

Claim/wait/retried (3.0), outbox (5.0), alerta de taxa de falha (6.0 — A2 consome
`failed{reason}`/`completed` daqui). Nenhuma mudança na máquina de estados, na escada de qualidades
nem nos motivos de falha.

## Decisões fechadas

- Contagem de falha vem do status persistido, não da exceção (risco CA1031 mapeado na TechSpec).
- `reason` e `stage` são conjuntos fechados (sem cardinalidade aberta).
- `video.id` em span/log apenas — nunca em métrica.

## Modificar / Referenciar

- **modificar:** `src/media/src/CodeForCoders.Media.Application/Common/MediaTelemetry.cs`
  (instrumentos de resultado); `.../UseCases/Videos/PrepareVideo/PrepareVideo.cs` (durations no
  ponto dos spans, counters no commit, tag `video.id` no helper, `VideoId` no log);
  `.../UseCases/VideoUploads/CompleteVideoUpload/CompleteVideoUpload.cs` (`VideoId` no log)
- **ref:** `tasks/prd-ingestao-midia/techspec.md` D-01..D-05 (escada, motivos e retries — não
  reabrir); `src/media/src/CodeForCoders.Media.Domain/Entities/Video.cs` (status/failure_reason);
  skill `dotnet` (observabilidade)

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/media` | `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/media` | `dotnet build src/media/CodeForCoders.Media.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.UnitTests/CodeForCoders.Media.UnitTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.ArchitectureTests/CodeForCoders.Media.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `scripts/kibana/` | `python3 scripts/kibana/import_observabilidade.py --verify-only` | exit 0 (seção Preparação referenciando os instrumentos novos) | Gate de 1.0 |

## Pronto quando

- [x] Gate passa (exit 0) com pelo menos 5 testes.
- [x] Vídeo ilegível → +1 `failed{reason=unreadable-file}`, nenhum `completed`; vídeo válido →
      `completed` + `time_to_ready` + uma observação por `stage`.
- [ ] Smoke: falha provocada (arquivo corrompido) aparece com motivo na seção Preparação no dia em
      que acontece, e o `video.id` dela é achado no Discover (spans + logs) sem abrir o banco.
