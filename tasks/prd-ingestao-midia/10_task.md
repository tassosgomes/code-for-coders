---
status: pending
task_kind: vertical
blocked_by: ["7.0"]
gate: 'dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.MediaVolumeMetricsTests --minimum-expected-tests 4'
gate_expect: 'Pelo menos 4 testes passam, lendo os instrumentos por MeterListener sobre Postgres real'
---

# 10.0 Volume guardado e vídeos por estado visíveis na telemetria

**Fatia:** V-07 · **Cobre:** RF-11, RN-M17, métrica "vídeo preso" do PRD · **Spec:** `techspec.md` § V-07, D-06, D-07 · **ADR:** —

## Comportamento

O papel `worker` emite, a partir do banco a cada 60 s:

- `media.storage.used` — bytes guardados de todos os vídeos, sem dimensão;
- `media.videos.count` — vídeos por estado, dimensão `status`;
- `media.videos.stuck` — vídeos em `received`/`preparing` há mais de 4× a duração estimada ou, sem
  duração, há mais de 12 h.

Os valores batem com o banco: com dois vídeos prontos de 10 MB e 20 MB, um falhado e um recebido há
13 h, o uso é 30 MB, a contagem por estado é 2/1/1 e há 1 preso. Nenhuma dimensão traz id de tenant,
vídeo ou autor. O papel `api` não emite esses instrumentos, para que réplicas da API não dupliquem o
gauge.

## Fora do escopo desta task

Painel e alerta no backend de métricas (plataforma). Detalhe por escola (D-06: vem de consulta quando
houver multi-tenant).

## Decisões fechadas

- Métricas sem dimensão de tenant: D-06, decidido pelo usuário em 2026-09-25.
- Emitidas só pelo papel `worker`: D-07.

## Modificar / Referenciar

- **modificar:** `src/media/src/CodeForCoders.Media.Infra.Messaging/DependencyInjection.cs` (registro no papel `worker`)
- **ref:** `domains/entrega-de-midia-e-protecao/domain.md` (RN-M17); `context/architecture-baseline.md` (proibição de id como dimensão); skill `dotnet` (observabilidade)

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/media` | `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/media` | `dotnet build src/media/CodeForCoders.Media.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.UnitTests/CodeForCoders.Media.UnitTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.ArchitectureTests/CodeForCoders.Media.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 4 testes.
- [ ] Os três instrumentos refletem o cenário do banco descrito acima, sem dimensão com id.
- [ ] Smoke no Compose: os três instrumentos aparecem no coletor OTLP local com valores coerentes com o banco.
