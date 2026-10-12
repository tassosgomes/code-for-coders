---
status: pending
task_kind: vertical
blocked_by: ["4.0"]
gate: 'dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.AutomaticTranscriptionTests --minimum-expected-tests 6'
gate_expect: "Pelo menos 6 testes de integração de media passam"
---

# 6.0 Todo vídeo novo é transcrito sem ação de ninguém

**Fatia:** V-02 · **Cobre:** RF-01, RN-M19, RN-M20, RN-M25 · **Spec:** `techspec.md#v-02-todo-vídeo-novo-é-transcrito-sem-ação-de-ninguém` · **ADR:** 0006, 0020

## Comportamento

Quando a preparação termina com sucesso, a **mesma transação** que marca o vídeo `ready` e grava o
fato `midia.ativo-pronto.v1` insere a tentativa `queued` (origem automática, sem solicitante). Não há
chamada remota nesse caminho. Resultados:

- vídeo `ready` com **uma** tentativa `queued`; a reentrega do mesmo fato ou o aviso duplicado de
  preparação não cria uma segunda (índice único parcial);
- vídeo que falhou na preparação não gera tentativa;
- serviço de transcrição fora do ar não altera o vídeo: continua `ready`, vinculável e reproduzível, e
  a tentativa conclui depois ou termina `failed`;
- falha na inserção da tentativa não pode derrubar a preparação (a Mídia não depende da Transcrição);
- com `Transcription__Enabled=false` nenhuma linha é criada;
- o tempo até "pronto" (`MediaTelemetry.VideoTimeToReady`) não piora.

A tela Vídeos já mostra "transcrevendo" por causa de 5.0, sem mudança de código de tela.

## Fora do escopo desta task

- Transcrição de vídeos já prontos antes da entrega é por ação do professor (3.0); não há carga em massa.
- Tela: 5.0 (já cobre o estado).

## Decisões fechadas

- A tentativa nasce na transação do `MarkReady` + outbox (`PrepareVideo.cs:211-247`), sem I/O remoto (RN-M19, ADR-0020).
- Transcrição nunca altera o estado da Mídia.

## Modificar / Referenciar

- **modificar:** `src/media/src/CodeForCoders.Media.Application/UseCases/Videos/PrepareVideo/PrepareVideo.cs` (`CompletePreparationAsync`, linhas 211-247)
- **ref:** `src/media/src/CodeForCoders.Media.Application/Common/MediaTelemetry.cs` (`VideoTimeToReady`); `src/media/tests/CodeForCoders.Media.IntegrationTests/VideoPreparationTestContext.cs` e `VideoPreparationTests.cs`; `docs/adr/0020`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/media` | `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.ArchitectureTests/CodeForCoders.Media.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.VideoPreparationTests --minimum-expected-tests 1` | exit 0 (regressão intacta) | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.VideoPreparationFailureTests --minimum-expected-tests 1` | exit 0 (regressão intacta) | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.MediaPreparationMetricsTests --minimum-expected-tests 1` | exit 0 (regressão intacta) | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.TranscriptionPipelineTests --minimum-expected-tests 1` | exit 0 (regressão intacta) | `ci-dotnet.yml` Testes |

## Pronto quando

- [ ] Gate focalizado passa (exit 0).
- [ ] Preparação bem-sucedida deixa o vídeo `ready` e exatamente uma tentativa `queued` automática.
- [ ] Reentrega do mesmo fato não duplica a tentativa; preparação que falha não cria tentativa.
- [ ] Com o serviço simulado indisponível, `Video.Status` permanece `ready`.
- [ ] Com o recurso desligado não há linha nova.
