---
status: pending
task_kind: vertical
blocked_by: ["4.0"]
gate: 'dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.MediaTranscriptionMetricsTests --minimum-expected-tests 7 && python3 scripts/kibana/import_observabilidade.py --verify-only'
gate_expect: "Pelo menos 7 testes de integração de media passam e a verificação estrutural dos saved objects sai com exit 0"
---

# 12.0 A operação enxerga os sinais da transcrição no painel de mídia

**Fatia:** V-08 · **Cobre:** RF-07 (sinais), RN-M26 · **Spec:** `techspec.md#v-08-a-operação-enxerga-os-sinais-da-transcrição` · **ADR:** 0008, 0020

## Comportamento

Os instrumentos e spans da TechSpec ficam completos e o painel de mídia no Kibana mostra, **sem texto
de aula nem dado pessoal**: tempo entre "pronto" e "transcrição pronta", taxa de falha por motivo,
duração transcrita e fila em espera por estado com a idade da mais antiga.

- **Instrumentos** (`MediaTelemetry.cs`): tempo da tentativa até pronta (rótulo de origem), pedidos por
  origem, resultados por motivo de falha, duração transcrita, fila em espera por estado e idade da mais
  antiga (`MediaVolumeMetricsWorker`), desfecho do receptor, conciliadas por consulta versus aviso e
  duração de cada estágio. Spans `media.transcription.extract-audio`, `.upload-audio`, `.submit`,
  `.poll`, `.fetch-result`, `.webhook.receive`, só com ids.
- **Proibido** em atributo, métrica, rótulo ou log: URL assinada, texto, `jobId` junto de dado pessoal,
  e-mail. Teste que varre os atributos emitidos.
- **Painel e alertas** como saved objects versionados (ADR-0008) no mesmo script de importação:
  alertas sugeridos para idade da fila acima do limite, `service-unavailable` acima do limite e tentativa
  sem avanço. Os nomes dos instrumentos são contrato com os saved objects.

## Fora do escopo desta task

- Calibrar limites de alerta e prazos com dados reais: piloto de 30 dias.
- Importar no servidor Kibana real é o smoke de ambiente, não o gate.

## Decisões fechadas

- Painel e alertas como código, junto da instrumentação (ADR-0008).
- Todo sinal é agregado e sem texto (RN-M26).

## Modificar / Referenciar

- **modificar:** `src/media/src/CodeForCoders.Media.Application/Common/MediaTelemetry.cs`; `src/media/src/CodeForCoders.Media.Infra.Messaging/MediaVolumeMetricsWorker.cs`; `scripts/kibana/observabilidade-midia.ndjson`; `scripts/kibana/import_observabilidade.py`
- **ref:** `src/media/tests/CodeForCoders.Media.IntegrationTests/MediaPreparationMetricsTests.cs` e `MediaQueueMetricsTests.cs` (padrão de teste de métricas); `docs/adr/0008-observabilidade-kibana-como-codigo.md`; skill `dotnet`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/media` | `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.ArchitectureTests/CodeForCoders.Media.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.MediaPreparationMetricsTests --minimum-expected-tests 1` | exit 0 (regressão intacta) | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.MediaVolumeMetricsTests --minimum-expected-tests 1` | exit 0 (regressão intacta) | `ci-dotnet.yml` Testes |
| Kibana como código | `python3 scripts/kibana/import_observabilidade.py --verify-only` | exit 0 | ADR-0008 (sem CI dedicado; a verificação estrutural é o check do próprio script) |

## Pronto quando

- [ ] Gate focalizado passa (exit 0).
- [ ] Os instrumentos listados são emitidos com os rótulos esperados e nenhum atributo contém URL assinada, texto ou dado pessoal.
- [ ] O NDJSON versionado inclui as visualizações e os alertas da transcrição e a verificação estrutural passa.
