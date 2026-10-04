# Revisão da task 5.0 — A reprodução continua sem interrupção e para quando o direito acaba

Run: run.weBwC7Bu
Modo: focused · Tentativa: 1/3 · HEAD revisado: 853c367b2e03fe9cda3790a0896328e9f8e666fb (árvore de trabalho com mudanças da task, não commitadas)

## Resultado: VALIDAÇÃO APROVADA (0 bloqueantes, 2 recomendações)

## Gate da task (primeiro plano, sequencial)

| Comando | Exit | Saída |
|---|---|---|
| `dotnet test --project …Media.IntegrationTests… -- --filter-class …PlaybackRenewalTests --minimum-expected-tests 8` | 0 | 11 passaram (mínimo 8) |
| `npm --prefix src/student-spa run test -- playback-renewal` | 0 | 10 passaram (mínimo 7) |

`gate_expect` (≥ 15: 8 media + 7 SPA): atendido (21).

## Verificações do projeto

| Componente | Comando | Exit |
|---|---|---|
| media | `dotnet format … --verify-no-changes` | 0 |
| media | `dotnet build src/media/CodeForCoders.Media.slnx` | 0 |
| media | ArchitectureTests | 0 |
| bff-student | `dotnet format … --verify-no-changes` | 0 |
| bff-student | `dotnet build …BffStudent.slnx` | 0 |
| bff-student | ArchitectureTests | 0 |
| student-spa | `lint` | 0 |
| student-spa | `typecheck` | 0 |
| student-spa | `test --` (suíte inteira: 18 arquivos, 109 testes) | 0 |
| student-spa | `build -- --base=/student/` | 0 |

Extras: `Media.UnitTests` 22/22 e `BffStudent.IntegrationTests` (PlaybackProxyTests) 18/18, exit 0.
Cobertura agregada, `dotnet publish` e imagens ficam para a validação full.

## Revisão semântica

- `media`: `RenewPlaybackSession` aceita só sessão do aluno do JWT (404 para desconhecida/de outro aluno, 410 para vencida antes de consultar `commerce`), repete a decisão sem cache (`DecideFreshAsync`), 403 com `reason`/`accessEndedAt`, 503 quando indisponível sem estender, 422 sem e-mail. Reconfere o vencimento após a decisão (`TryRenew`) e a validade é `agora + 5 min` (não acumula em renovações simultâneas). Mesma `sessionId`, nova `segmentAccess` pela porta de distribuição da 4.0 (reusada). Rota `/internal/v1/playback-sessions/{id}/renewals` conforme `internal-api-contract-media.yaml`.
- `bff-student`: rota `/api/v1/playback-sessions/{id}/renewals` conforme `api-contract.yaml`, com teste de CSRF/audiência `media`, repasse de `reason`/`accessEndedAt` e códigos de erro contratuais.
- `student-spa`: renovação só com o vídeo tocando, a partir de `renewAfter`, sem recarregar a playlist; 503 repete a cada 5 s até `expiresAt`; 403/410 param com mensagem; pausa além de `expiresAt` abre sessão nova e retoma da posição; resposta tardia após pausa/vencimento é descartada. Cenários negativos da task cobertos pelos testes listados.
- Script de apoio `scripts/expire-playback-grant.sh` + `docs/playback-renewal-scenario.md` presentes (parâmetros validados, limitado a concessões de cortesia, `ROLLBACK` se não encontrar).

## Bloqueantes

Nenhum.

## Recomendações (não bloqueiam)

1. Os itens manuais de "Pronto quando" (antecipar `expires_at` no banco de `commerce` com o aluno assistindo; 20 min seguidos reais) dependem do servidor remoto e não foram executados aqui. O comportamento é coberto por testes com relógio/sessão simulados (inclusive "20 minutos, uma playlist, sem pausa"); vale rodar o roteiro de `docs/playback-renewal-scenario.md` no checkpoint/QA.
2. Em `protected-video-player`, `onPlay` nativo de um vídeo com sessão já vencida chama `schedule()`→`refresh()` e para com a mensagem de sessão terminada, em vez de abrir sessão nova como o `togglePlayback`. Só ocorre se houver controle nativo/atalho que dispare `play` fora do botão do player; hoje o caminho principal está correto.

## Integridade

HEAD e conjunto de arquivos alterados não mudaram durante a revisão. Nenhum código, status, task ou commit foi alterado pelo validador (apenas este relatório e o result.json).
