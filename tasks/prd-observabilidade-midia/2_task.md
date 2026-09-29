---
status: done
task_kind: vertical
blocked_by: []
gate: 'dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.MediaUploadFunnelMetricsTests --minimum-expected-tests 4'
gate_expect: "Pelo menos 4 testes passam lendo instrumentos por MeterListener sobre Postgres/MinIO reais: sessão nova conta created, retomada não conta, conclusão conta completed+size, expiração conta expired"
---

# 2.0 Funil e tamanho dos envios visíveis no painel

**Fatia:** V-02 · **Cobre:** RF-02, RF-01 (sessões pendentes), US-01 (parcial), DP-03, DP-04 ·
**Spec:** `techspec.md#v-02` e § Tabela de instrumentos · **ADR:** —

## Comportamento

O papel `api` do `media` passa a contar o funil do envio no instante do evento, e o worker passa a
reportar as sessões pendentes:

- `media.upload.created` (`{upload}`) — só quando `CreateVideoUpload` cria sessão **nova**;
  retomada por fingerprint **não** incrementa (caso negativo explícito);
- `media.upload.completed` (`{upload}`) e `media.upload.size` (histograma `By`, buckets
  explícitos cobrindo 1 MiB–5 GiB) — quando `CompleteVideoUpload` completa o multipart, com
  `original_size_bytes` do vídeo;
- `media.upload.expired` (`{upload}`) — quando `ExpirePendingVideoUploads` expira sessão
  abandonada; abandono não é falha de preparação e aparece contado separadamente;
- `media.uploads.pending` (gauge `{upload}`) — snapshot do worker: sessões com
  `completed_at IS NULL AND expired_at IS NULL`, consultadas como o snapshot atual
  (`AsNoTracking`, `IgnoreQueryFilters`).

Incremento ocorre após o efeito (nunca antes). A seção **Envio** do dashboard mostra criadas,
concluídas e expiradas comparáveis na mesma janela, distribuição de tamanho e pendentes —
atualizando os NDJSON versionados e revalidando com o script de 1.0.

## Fora do escopo desta task

Fila de preparação (3.0), métricas de preparação (4.0), outbox (5.0), alertas (6.0). Nenhuma
mudança de comportamento do envio em si.

## Decisões fechadas

- Gauges só no worker (D-07 herdado); counters/histogram no papel que executa o evento.
- Sem dimensão de tenant e sem Id em métrica (DE10/D-06; DP-03 do PRD).
- Estado vem do serviço (DP-04): snapshot do banco, não consulta do painel.

## Modificar / Referenciar

- **modificar:** `src/media/src/CodeForCoders.Media.Application/Common/MediaTelemetry.cs`
  (instrumentos do funil ao lado dos existentes);
  `.../UseCases/VideoUploads/CreateVideoUpload/CreateVideoUpload.cs` (created, sessão nova);
  `.../UseCases/VideoUploads/CompleteVideoUpload/CompleteVideoUpload.cs` (completed + size);
  `.../UseCases/VideoUploads/ExpirePendingVideoUploads/ExpirePendingVideoUploads.cs` (expired);
  `src/media/src/CodeForCoders.Media.Infra.Messaging/MediaVolumeMetricsWorker.cs` (gauge
  `uploads.pending` no snapshot)
- **ref:** `MediaVolumeMetricsWorker.cs` atual (padrão de snapshot/gauge); skill `dotnet`
  (observabilidade); `techspec.md` § Tabela de instrumentos (nomes/unidades contratuais)

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/media` | `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/media` | `dotnet build src/media/CodeForCoders.Media.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.UnitTests/CodeForCoders.Media.UnitTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.ArchitectureTests/CodeForCoders.Media.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `scripts/kibana/` | `python3 scripts/kibana/import_observabilidade.py --verify-only` | exit 0 (seção Envio referenciando os instrumentos novos) | Gate de 1.0 |

## Pronto quando

- [x] Gate passa (exit 0) com pelo menos 4 testes, incluindo o caso negativo da retomada.
- [x] Snapshot: 2 sessões pendentes no banco → gauge `media.uploads.pending` = 2, sem dimensão.
- [ ] Smoke: um envio concluído e um abandonado (expirado) aparecem como tal na seção Envio do
      painel, nas mesmas janela e unidade.
