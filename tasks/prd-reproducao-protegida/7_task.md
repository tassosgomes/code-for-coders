---
status: done
task_kind: vertical
blocked_by: ["4.0"]
gate: 'dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.PlaybackProgressTests --minimum-expected-tests 10 && dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.RetainedOutboxFactTests --minimum-expected-tests 6 && dotnet test --project src/bff-student/tests/CodeForCoders.BffStudent.IntegrationTests/CodeForCoders.BffStudent.IntegrationTests.csproj -- --filter-class CodeForCoders.BffStudent.IntegrationTests.PlaybackProgressProxyTests --minimum-expected-tests 4 && npm --prefix src/student-spa run test -- playback-progress'
gate_expect: "Pelo menos 27 testes passam: 10 e 6 de integração de media, 4 do BFF, 7 do SPA"
---

# 7.0 O avanço da reprodução chega como fato, sem e-mail e sem perder nada

**Fatia:** V-05 · **Cobre:** RF-07, RN-R05, RN-M14, DP-02, US de `CAP-017` receber o avanço · **Spec:** `techspec.md#v-05` · **ADR:** [ADR-0006](../../docs/adr/0006-preparacao-de-video-e-custodia-de-chave.md)

## Comportamento

A reprodução informa o avanço, e o fato fica guardado até existir quem o consuma. Operação: `recordPlaybackProgress` →
`recordPlaybackProgressInternal`; mensagem `midia.reproducao-avancou.v1` (`asyncapi-contract.yaml`).

- **`student-spa` (player):** envia `heartbeat` a cada `progress.intervalSeconds` de reprodução contínua, `paused`, `ended` e `left`
  (saída da aula ou da aba, com `fetch` em modo `keepalive`, porque a escrita exige `X-CSRF-Token` e `sendBeacon` não o permite), com
  `sequence` crescente a partir de 1. Nunca um avanço por segundo.
- **`bff-student`:** rota com `X-CSRF-Token`, audiência `media`, timeout de 3 s, mapeamento de erros conforme o contrato.
- **`media`:** aceita o avanço de sessão **do aluno do JWT**, mesmo depois do fim da validade, **até 60 minutos depois de `expiresAt`**
  (depois, 410). `positionSeconds` acima da duração do vídeo + 10 s → 400 `VALIDATION_ERROR`. Atomicamente: só se `sequence` for maior
  que a última aceita **e** houver ao menos 10 s desde o último avanço aceito da sessão; então grava `last_sequence`,
  `last_progress_at`, `last_position_seconds` e o fato no outbox, **na mesma transação** (G06). Responde 200 com `recorded`: `false`
  para `sequence` repetida ou menor e para avanço a menos de 10 s do anterior, sem erro. A negação posterior **não** apaga nem
  invalida avanço anterior.
- **O fato:** `eventId` determinístico por (`sessionId`, `sequence`); `tenantId`, `sessionId`, `studentId`, `courseId`, `lessonId`,
  `sequence`, `positionSeconds`, `reason`, `occurredAt`; **sem e-mail, nome, título, percentual ou `videoId`**.
- **Retenção:** `Outbox:RetainedRoutingKeys` lista as chaves de roteamento cujas mensagens nascem **já processadas e não são
  publicadas**; com `midia.reproducao-avancou.v1` na lista o fato fica retido, **não conta como pendente nem esgotado** na saúde do
  outbox e não aciona o alarme. Existe uma **operação de reenvio**, no padrão do reenvio dos fatos de vídeo
  (`Outbox:ProgressFactReplayTenantId` e `Outbox:ProgressFactReplayQueue`), que publica os fatos retidos de uma escola, em ordem de
  gravação, numa fila indicada que já exista, sem alterar o registro nem gerar fato novo. Tirar a chave da lista faz o fato sair ao
  vivo. A ordem de virada para o consumidor é: o consumidor declara a fila; a chave sai da lista; **depois** o reenvio roda (o que
  publica de novo o que já saiu ao vivo, seguro pelo `eventId` determinístico).

Casos negativos que a task prova: `sequence` repetida e menor (`recorded: false`, um fato só); dois avanços a menos de 10 s (o
segundo com `recorded: false`); avanço de sessão de outro aluno (404); depois da janela (410); posição absurda (400); corpo com
e-mail, aula ou curso (400, o contrato não os aceita); avanço de sessão que terminou por decisão negada (aceito e não invalida o
anterior).

## Fora do escopo desta task

- Consumir o fato, calcular progresso, concluir aula e retomar de onde parou → `CAP-017`. Limpeza do registro retido → `CAP-017`.
- Medir sobreposição de aulas distintas → QA-01 do PRD (decisão antes de `CAP-017`).
- Sinais e painel → 8.0.

## Decisões fechadas

- Cadência de 30 s mais pausa, saída e fim; mínimo de 10 s entre dois avanços da mesma sessão (DP-02, RN-R05).
- Janela de aceitação de 60 min (`techspec.md` D-09); `eventId` determinístico (D-08); fato retido com reenvio (D-07).
- Nenhum dado pessoal no fato (G23); o `studentId` do fato é o opaco da conta.
- O fato **não** é publicado nesta entrega: nenhuma fila nova no RabbitMQ até `CAP-017`.

## Modificar / Referenciar

Arquivos a criar não são listados: a estrutura vem das skills `dotnet` e `react`.

- **modificar:** `src/media/src/CodeForCoders.Media.Api/Extensions/EndpointExtensions.cs`, `Extensions/VideoReplayOperationsExtensions.cs` (operação de reenvio de avanço, no padrão da existente); `Infra.Data/Outbox/OutboxMessageWriter.cs`; `Infra.Messaging/Configuration/OutboxOptions.cs`, `OutboxPublisherWorker.cs` (`ReplayVideoFactsAsync` como modelo), `RabbitMqPublisher.cs`; `Infra.Data/MediaDbContext.cs` (campos de avanço da sessão, por migration do EF)
- **modificar:** `src/bff-student/src/CodeForCoders.BffStudent.Api/Extensions/EndpointExtensions.cs` (rota de avanço)
- **modificar:** os componentes do player de 4.0 em `src/student-spa/src/features/` e `src/testing/` (heartbeat, pausa, fim, saída com `keepalive`)
- **ref:** `src/media/src/CodeForCoders.Media.Infra.Data/Health/OutboxHealthCheck.cs` (a saúde só conta o não processado); `src/media/tests/CodeForCoders.Media.IntegrationTests/VideoFactReplayTests.cs` e `VideoFactReplayFixture.cs` (padrão de teste do reenvio); `asyncapi-contract.yaml`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/media` | `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/media` | `dotnet build src/media/CodeForCoders.Media.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.ArchitectureTests/CodeForCoders.Media.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/bff-student` | `dotnet format src/bff-student/CodeForCoders.BffStudent.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-student` | `dotnet build src/bff-student/CodeForCoders.BffStudent.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/bff-student` | `dotnet test --project src/bff-student/tests/CodeForCoders.BffStudent.ArchitectureTests/CodeForCoders.BffStudent.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/student-spa` | `npm --prefix src/student-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run test --` | exit 0 (suíte inteira, sem regressão) | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run build -- --base=/student/` | exit 0 | `ci-react-ts.yml` (`build-args: --base=/student/`) |

Cobertura agregada ≥ 70%, `dotnet publish` e imagens ficam para a validação full. Testcontainers pesados rodam **em sequência**, nunca em paralelo (AGENTS.md).

## Pronto quando

- [x] Gate focalizado passa (exit 0), com os mínimos do `gate`.
- [ ] 10 minutos contínuos de reprodução simulada produzem cerca de 20 fatos; pausa aos 252 s produz um fato com posição 252.
- [ ] A saúde do outbox não acusa pendência nem esgotamento com fatos retidos; a operação de reenvio publica numa fila de teste, com o broker real, cada `eventId` **uma vez**, também de uma sessão que terminou por decisão negada.
- [ ] O fato publicado confere com `asyncapi-contract.yaml` e não contém e-mail, nome, título ou percentual.
