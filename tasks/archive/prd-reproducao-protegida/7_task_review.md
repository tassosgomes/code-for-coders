# Revisão — Task 7.0 (revalidation, tentativa 2/3)

Run: run.MxOfvRL7

Resultado: **VALIDAÇÃO APROVADA** (0 bloqueantes, 0 recomendações abertas)

HEAD revisado: b6a51ef26b28d3d6bc22acbea9b6cb7c5c3fbb21 (árvore de trabalho com as alterações não commitadas da task; `git status --porcelain` e HEAD inalterados durante a revisão).

## Gate e verificações (primeiro plano, em sequência, via `rtk proxy`)

| Comando | Exit | Resultado |
|---|---|---|
| media `PlaybackProgressTests` (mín. 10) | 0 | 15 passaram |
| media `RetainedOutboxFactTests` (mín. 6) | 0 | 6 passaram |
| bff `PlaybackProgressProxyTests` (mín. 4) | 0 | 6 passaram |
| spa `test -- playback-progress` | 0 | 7 passaram |
| `gate_expect` (≥ 27) | ok | 15 + 6 + 6 + 7 = 34 |
| dotnet format media / bff `--verify-no-changes` | 0 / 0 | |
| dotnet build media / bff | 0 / 0 | 0 warnings, 0 erros |
| ArchitectureTests media / bff | 0 / 0 | 8 / 8 |
| spa lint / typecheck | 0 / 0 | |
| spa `test --` (suíte inteira) | 0 | 20 arquivos, 124 testes |
| spa `build -- --base=/student/` | 0 | |

Cobertura agregada, `dotnet publish` e imagens ficam para a validação full.

## Revalidação dos bloqueantes anteriores

### B1 — atomicidade do avanço: RESOLVIDO

- `PlaybackSessionConfiguration.cs`: `last_sequence` e `last_progress_at` agora são tokens de concorrência (`IsConcurrencyToken`), com migration gerada pelo EF (`20261003203118_AddPlaybackProgressFields`) e snapshot atualizado. O `UPDATE` da sessão só vale se os dois valores lidos ainda forem os atuais.
- `MediaUnitOfWork.TryCommitAsync` agora devolve `false` em `ConcurrencyConflictException`, `DbUpdateConcurrencyException` e violação de unicidade da PK do outbox (aninhada ou não); `RecordPlaybackProgress` responde `recorded: false` em vez de 500.
- Novos testes `ConcurrentProgressRequestsWithSameSequence…` e `ConcurrentProgressRequestsWithDistinctSequencesLessThan10SecondsApart…` disparam dois pedidos simultâneos (`Task.WhenAll`), exigem 200 nos dois, exatamente um `recorded: true` (XOR) e exatamente uma linha no outbox.

### B2 — ordem do reenvio: RESOLVIDO

- `OutboxPublisherWorker.ReplayProgressFactsAsync` ordena e pagina por (`OccurredOn`, `Id`), com `BatchSize = 1` no teste para exercitar a paginação.
- `ReplayAndAssertAsync` agora confere a ordem de chegada por posição (`expected[j]` contra `MessageId` e payload), e `ReplayProgressFactsPublishesRetainedFactsInOrder…` semeia três fatos da mesma sessão com `OccurredOn` crescente. O teste discrimina a ordem.

## Recomendações da tentativa 1

1. `OutboxMessageWriter` agora recebe `IOptions<OutboxOptions>` obrigatório (sem `IConfiguration?` opcional): atendida.
2. Vídeo sem duração: a decisão está comentada e logada em `RecordPlaybackProgress` (aviso com `SessionId`/`VideoId`): atendida.

## Regressões no diff novo

Nenhuma encontrada. `OutboxOptions` mudou para `Infra.Data/Outbox` e os consumidores (worker, métricas, reenvio de vídeo, testes de métricas) compilam e passam; as ArchitectureTests continuam verdes.

## Critérios "Pronto quando"

- Gate focalizado com os mínimos: atendido.
- 10 min → ~20 fatos e pausa a 252 s → posição 252: coberto no SPA (`playback-progress.test.tsx`).
- Saúde do outbox com fatos retidos e reenvio com broker real, cada `eventId` uma vez, em ordem de gravação: coberto.
- Fato conforme `asyncapi-contract.yaml`, sem e-mail, nome, título, `videoId` ou percentual: coberto.
- Atomicidade e regra de 10 s sob concorrência: coberto.

---

## Histórico — tentativa 1/3 (run.sxDxvx4T): VALIDAÇÃO REPROVADA

B1 (aceitação do avanço não atômica) e B2 (reenvio ordenado por `Id` hash, não por ordem de gravação), mais duas recomendações. Todos tratados acima.
