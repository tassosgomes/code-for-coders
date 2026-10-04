# Revisão da task 8.0 — A equipe vê custo, erros e cache da entrega

Run: run.g3XBnxD1 (tentativa 1/3, histórico)
Modo: focused · Tentativa: 1/3 · HEAD: 2d83da10a6da1c73645865c0c7bf53f8e213b253 (inalterado durante a revisão)

## Resultado: VALIDAÇÃO REPROVADA (1 bloqueante, 3 recomendações)

## Gate e verificações (todas em primeiro plano; o exit code é o veredito)

| Comando | Exit | Evidência |
|---|---|---|
| gate 1: `dotnet test` UnitTests `PlaybackTelemetryTests` (mín. 5) | 0 | 15 passaram |
| gate 2: `dotnet test` IntegrationTests `PlaybackSignalsTests` (mín. 4) | 0 | 7 passaram |
| gate 3: `npm --prefix src/student-spa run test -- playback-telemetry` | 0 | 3 passaram (esperado ≥ 2) |
| gate 4: `python3 scripts/kibana/import_observabilidade.py --verify-only` | 0 | 8 saved objects, instrumentos novos no manifesto |
| `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | 0 | |
| `dotnet build src/media/CodeForCoders.Media.slnx` | 0 | 0 warnings, 0 erros |
| `dotnet test` ArchitectureTests | 0 | 8 passaram |
| `npm run lint` / `typecheck` | 0 / 0 | |
| `npm run test --` (suíte inteira) | 0 | sem regressão |
| `npm run build -- --base=/student/` | 0 | |

O gate passa e satisfaz o `gate_expect`. A reprovação vem da revisão semântica, abaixo.

## Escopo revisado
Diff desde o checkpoint 7.0 e os untracked do escopo: `MediaTelemetry.cs`, `OpenPlaybackSession.cs`,
`AccessDecisionClient.cs`, `MediaVolumeMetricsWorker.cs`, `telemetry.ts`, `use-protected-playback.ts`,
`import_observabilidade.py`, `observabilidade-midia.ndjson` e os três arquivos de teste novos.

## Bloqueante

### B1. O painel "Tempo até começar (mediana e p95)" não tem produtor: nenhum código emite `media.playback.time_to_start`
- `src/media/src/CodeForCoders.Media.Application/Common/MediaTelemetry.cs:107,138` declara o histograma e `RecordPlaybackTimeToStart`.
  Nenhum código de produção chama o método; só o teste unitário (`PlaybackTelemetryTests.cs:93-97`).
- O SPA emite um **span de trace** `playback.time-to-start` (`src/student-spa/src/lib/telemetry.ts:36`), não uma métrica.
  O painel `playback-time-to-start` (`scripts/kibana/import_observabilidade.py:1354-1355`) consulta
  `FROM metrics-generic* ... metrics.media.playback.time_to_start`, campo que nada alimenta. O repositório também não tem
  conector `spanmetrics` (busca sem ocorrências) que converta o span em métrica.
- Cenário de falha: o aluno abre uma aula, o vídeo começa e o span é exportado para traces. O painel continua sem dados.
  O `--verify-only` passa porque só confere a forma da query e o nome do instrumento no manifesto, não a existência de um produtor.
- Critérios afetados: RF-08 (tempo até o vídeo começar, mediana e p95), "Pronto quando" item 2 ("uma reprodução de teste aparece
  ... no tempo até começar") e o contrato "instrumentos como contrato", em que o instrumento do manifesto precisa ser emitido
  por algum serviço.
- Correção esperada (a escolha do desenho é do implementer, dentro das decisões fechadas): ligar o intervalo do SPA a uma
  métrica/histograma efetivamente exportada pelo OpenTelemetry do SPA, ou fazer o painel ler a fonte que existe e ajustar o
  manifesto e a verificação. Acrescentar um teste que prove que o dado que o painel consulta é emitido, sem e-mail, nome ou
  identificador de aluno, e fazer a verificação do painel recusar instrumento sem produtor.

## Recomendações (não bloqueiam)
1. Em `use-protected-playback.ts` (cleanup do efeito), `timing.end()` sem primeiro quadro encerra o intervalo como sucesso ao
   desmontar ou trocar de aula. Esse caso abandonado entraria no p50/p95 como início bem-sucedido; descarte-o ou marque-o
   com status distinto.
2. A integração de `PlaybackSignalsTests` cobre `negada`, `indisponivel` e `referencia_ausente`. Os motivos `video_nao_pronto`
   e `sem_email` só têm cobertura unitária (normalização), não pelo caso de uso real.
3. O registro de que, sem a fonte da distribuição, o alerta de cache (G22) não dispara em desenvolvimento não aparece no
   código nem no ADR-0008. Registre-o no script/painel ou na task.

## Verificado e sem problemas
- Nenhum atributo de e-mail, nome ou identificador de aluno nos instrumentos novos: `reason` é o único rótulo, normalizado.
- Aberturas e recusas por motivo, falha e latência da decisão, e alunos ativos em 30 dias (distintos, `IgnoreQueryFilters`) emitem e
  têm teste.
- Alerta G22 com limiar 85% em 15 minutos, sem conector de notificação.
- Arquitetura, format e build sem regressão.

Limites: o `--import` no Kibana de desenvolvimento e a busca do e-mail na telemetria exportada de ponta a ponta não foram
exercitados nesta revisão (sem rede/Kibana no escopo do validator). A reprovação não depende deles.


---

# Revalidação da task 8.0 (tentativa 2/3)

Run: run.uWcNbDib
Modo: revalidation · Tentativa: 2/3 · HEAD: 2d83da10a6da1c73645865c0c7bf53f8e213b253 (inalterado durante a revisão)

## Resultado: VALIDAÇÃO APROVADA (0 bloqueantes, 2 recomendações)

## Gate e verificações (primeiro plano, sequenciais; o exit code é o veredito)

| Comando | Exit | Evidência |
|---|---|---|
| gate 1: UnitTests `PlaybackTelemetryTests` (mín. 5) | 0 | 15 passaram |
| gate 2: IntegrationTests `PlaybackSignalsTests` (mín. 4) | 0 | 9 passaram (eram 7) |
| gate 3: `npm --prefix src/student-spa run test -- playback-telemetry` | 0 | 4 passaram (esperado ≥ 2) |
| gate 4: `python3 scripts/kibana/import_observabilidade.py --verify-only` | 0 | 8 saved objects; inclui checagem nova de produtor |
| `dotnet format ... --verify-no-changes` | 0 | |
| `dotnet build src/media/CodeForCoders.Media.slnx` | 0 | 0 warnings, 0 erros |
| `dotnet test` ArchitectureTests | 0 | 8 passaram |
| `npm run lint` / `typecheck` | 0 / 0 | |
| `npm run test --` (suíte inteira) | 0 | 21 arquivos, 128 testes, sem regressão |
| `npm run build -- --base=/student/` | 0 | |

O gate satisfaz o `gate_expect` (≥ 11 testes: 15 + 9 + 4) e a verificação do painel sai com exit 0.

## Bloqueio anterior

### B1 (sem produtor de `media.playback.time_to_start`): RESOLVIDO
- `src/student-spa/src/lib/telemetry.ts` agora cria o histograma `media.playback.time_to_start` (unidade `s`, sem atributos) e
  o registra em `end()` quando o primeiro quadro chega. O `initTelemetry` configura `MeterProvider` com
  `OTLPMetricExporter` (URL derivada de `/v1/traces` para `/v1/metrics`), com o mesmo `resource` dos traces.
- O histograma morto de `media` foi removido: `RecordPlaybackTimeToStart` não existe mais em código de produção. O único
  produtor é o SPA, e o nome bate com o campo `metrics.media.playback.time_to_start` consultado pelo painel.
- `playback-telemetry.test.tsx` prova com `InMemoryMetricExporter` que a métrica é emitida com unidade `s` e sem
  atributos. Prova também que falha e abandono não gravam no histograma, e que o e-mail de teste não aparece em spans nem
  em métricas exportados.
- `import_observabilidade.py` ganhou `find_instrument_producers`, e o `--verify-only` agora reprova instrumento do manifesto sem
  produtor em `src/`. Mutação feita em cópia no scratchpad, trocando o nome do instrumento em `telemetry.ts`: a busca devolve
  `[]` para `time_to_start`, o que dispararia o `ValueError`. Os demais instrumentos continuam com produtor. A árvore real não
  foi tocada.

## Recomendações anteriores
1. Abandono contava como sucesso: **resolvido**. O cleanup chama `timing.cancel()` (span com `playback.abandoned=true`, sem
   histograma), com teste.
2. Cobertura de `video_nao_pronto` e `sem_email` pelo caso de uso real: a integração subiu de 7 para 9 testes; mantida como
   verificação do gate (não bloqueia).
3. Registro de que o alerta G22 não dispara sem a fonte da distribuição: **resolvido**, em `PLATFORM_PROVIDED_INSTRUMENTS` e
   na `limitation` da regra no script.

## Recomendações novas (não bloqueiam)
1. A checagem de produtor é busca textual do nome do instrumento: uma declaração sem chamada ainda a satisfaria (foi o caso do
   histograma morto de `media`, agora removido). Vale endurecê-la no futuro para exigir uso (`.Record`/`.Add`).
2. O endpoint de métricas é derivado de `OTEL_ENDPOINT` trocando `/v1/traces` por `/v1/metrics`. Confirme na plataforma
   que o collector aceita `/v1/metrics` por CORS vindo do navegador e que a CSP `connect-src` do SPA o permite; sem isso o
   painel de tempo até começar fica vazio em ambiente real.

## Regressões no diff novo
Nenhuma encontrada: format, build, arquitetura, lint, typecheck, suíte inteira do SPA e build passam.

Limites: `--import` no Kibana de desenvolvimento e a busca do e-mail na telemetria exportada de ponta a ponta não foram
exercitados (sem rede/Kibana no escopo do validator). Sem esses, o item 2 de "Pronto quando" fica para a validação full/QA.
A aprovação não depende deles.
