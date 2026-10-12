---
status: pending
task_kind: vertical
blocked_by: ["3.0"]
gate: 'dotnet test --project src/media/tests/CodeForCoders.Media.UnitTests/CodeForCoders.Media.UnitTests.csproj -- --filter-class CodeForCoders.Media.UnitTests.TranscriptionResultValidationTests --minimum-expected-tests 8 && dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.TranscriptionServiceClientTests --minimum-expected-tests 8 && dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.TranscriptionPipelineTests --minimum-expected-tests 12 && dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.TranscriptionWorkerCompositionTests --minimum-expected-tests 2'
gate_expect: "Pelo menos 30 testes .NET passam (8 unitários, 8 do cliente do serviço, 12 do pipeline e 2 de composição do worker em media)"
---

# 4.0 O worker leva a transcrição da fila ao texto pronto, a partir do vídeo cifrado

**Fatia:** V-01 (pipeline) · **Cobre:** RF-02 (estado `in-progress` → `ready`), RF-07 (cópia própria, apagar áudio, conciliação por consulta), RN-M18, RN-M23, RN-M24 (caminho feliz), RN-M26 (spans) · **Spec:** `techspec.md#pipeline-do-worker-uma-transcrição` · **ADR:** 0006, 0014, 0019, 0020

## Comportamento

**O que passa a funcionar:** uma tentativa `queued` (de 3.0) é reivindicada pelo worker do `media`,
percorre o pipeline inteiro e termina `ready`, com o texto guardado e o estado do vídeo
`transcription.status = ready`, `readable: true`, `readyAt` preenchido. Nenhum objeto de áudio resta
ao fim.

**Pipeline** (ordem e regras da TechSpec, sem reabrir):

1. **Pré-checagem local:** duração acima de 7200 s → falha `duration-limit` sem extrair nada.
2. **Extração do áudio** da Versão de Reprodução: lê o `master.m3u8`, escolhe a variante de **menor
   `BANDWIDTH`** (não a lista fixa de nomes de `PlaylistRewriter`), baixa playlist e segmentos para
   o diretório de trabalho da tentativa validando cada linha de recurso contra
   `^[A-Za-z0-9]+/segment_[0-9]+\.ts$` e rejeitando URI absoluta; obtém a chave pela custódia
   existente, grava `key.bin` (somente dono) e troca o marcador `c4c-key:{videoId}` pelo caminho; roda o
   ffmpeg com `-allowed_extensions ALL -protocol_whitelist file,crypto`, `-vn -ac 1 -ar 16000 -c:a aac
   -b:a 48k -movflags +faststart` e 1 thread; sobe o `.m4a` para
   `{tenantId}/{videoId}/transcription/{transcriptionId}/audio.m4a` (fora de `hls/`). O diretório de
   trabalho inteiro, **inclusive a chave**, é apagado em `finally`, com sucesso ou falha, e a chave é
   zerada na memória. Nada da chave vai a log, span, objeto, mensagem ou resposta.
3. **Envio:** URL pré-assinada de leitura do áudio (90 min, em memória, nunca persistida nem
   logada, assinada com `AwsMedia__EndpointTranscription`) e `POST /v1/transcriptions` com
   `X-API-Key`, `Idempotency-Key = c4c-{transcriptionId}-{n}` e `{sourceUrl, clientReference =
   transcriptionId}`; grava `whisper_job_id`, `submitted_at`, `deadline_at` (+30 h) e passa a
   `waiting`. O cliente segue o **comportamento** do serviço (aceita destino `http://` em lista), não o
   `pattern` documental.
4. **Espera e conciliação por consulta** (caminho obrigatório): cadência de 60 s nos 10 primeiros
   minutos, 5 min até 2 h, depois 15 min. Quando o job sai de `downloading`, o áudio é apagado.
   Estado desconhecido do serviço é tratado como em andamento e registrado.
5. **Cópia:** em `completed`, busca o resultado, valida e grava conteúdo e vigente numa transação
   (`schemaVersion` ≠ 1, trechos aparados, vazios descartados, `startMs ≥ 0`, `endMs ≥ startMs`,
   ordenação, limite de 20.000 trechos e 8 MiB de JSON) e apaga o áudio se ainda existir.
6. Reivindicação com concessão e `FOR UPDATE SKIP LOCKED` ordenada por `requested_at`; concessão
   vencida volta a elegível e conta tentativa; concorrência configurável (padrão 1); orçamento de
   disco antes de extrair; manutenção a cada ciclo (concessões vencidas, sobras de áudio).

Os caminhos de **falha** e a política de nova tentativa por situação são de 8.0; aqui basta que um
resultado inválido (`schemaVersion` desconhecida, excedente de tamanho) **não** vire texto e termine
`failed` / `processing-failed`.

**Configuração e implantação:** `Transcription__Service__BaseAddress`, `Transcription__Service__ApiKey`
(segredo), `AwsMedia__EndpointTranscription`, `Transcription__SourceUrlLifetimeMinutes`,
`Transcription__MaxWaitHours`, `__MaxConcurrency`, `__MaxSubmissions` e cadências, replicados em
`docker-compose.yml` (pilha local com `Enabled=false`), **`docker-compose.remote.yml` (override
obrigatório: `AwsMedia__EndpointTranscription=http://192.168.0.5:9000`, `Transcription__Service__*` e a
chave via `${…:?}`)**, `docker-compose.komodo.apis.yml`, `docker-compose.stable.yml` e
`docker-compose.stable.infra.yml`. A chave do serviço não é gerada por `scripts/generate-local-env.sh`.

**Evidências exigidas (regra de cliente de rede e de composição):**

- Extração **real** com o ffmpeg da imagem sobre HLS AES-128 gerado pelo transcodificador do próprio
  `media`, gerando `.m4a` mono 16 kHz.
- Cliente do serviço testado como **adaptador real** (`HttpClient`) contra um simulador HTTP do
  OpenAPI do serviço, incluindo `schemaVersion` desconhecida e resultado expirado.
- Teste de que a aplicação sobe com os registros reais no papel `worker` no ambiente de teste.
- Dois workers disputando a mesma fila não processam a mesma tentativa; worker morto no meio devolve a
  tentativa à fila por concessão vencida.
- Ao fim: prefixo `transcription/` vazio, diretório de trabalho vazio, `key.bin` ausente, e nenhum
  vestígio de chave, URL assinada ou texto em log.

**Contratos:** nada novo a promover (as operações do serviço externo não entram em `contracts/`).

## Fora do escopo desta task

- Receptor do aviso: 7.0 (aqui só a consulta periódica). Disparo automático: 6.0.
- Política de falha por situação, motivos e Refazer: 8.0.
- Métricas e painel: 12.0 (os spans por estágio nascem aqui).
- Smoke contra o serviço real: 13.0.

## Decisões fechadas

- Fonte do áudio é a Versão de Reprodução lida pelo worker (D-01, ADR-0020); o original não existe mais.
- Menor qualidade como fonte (D-03). Conteúdo em `jsonb` no banco do `media` (D-04). O aviso é gatilho; a consulta é a verdade (D-05).
- A chave só existe em `key.bin` (0600) no diretório temporário e some em `finally` (ADR-0006).
- O prefixo do áudio está fora de `hls/` (ADR-0014). O texto não vai a log, métrica, span nem URL.
- Falha herdada de vídeos abaixo de 480p em `PlaylistRewriter` é preexistente (TASK-27) e **não** é corrigida aqui.

## Modificar / Referenciar

- **modificar:** `src/media/src/CodeForCoders.Media.Application/Interfaces/IMediaStoragePort.cs`; `src/media/src/CodeForCoders.Media.Infra.Data/Adapters/S3MediaStorageAdapter.cs`, `S3MediaClientPair.cs`; `src/media/src/CodeForCoders.Media.Infra.Data/Configuration/AwsMediaOptions.cs`; `src/media/src/CodeForCoders.Media.Infra.Data/DependencyInjection.cs`; `src/media/src/CodeForCoders.Media.Infra.Messaging/DependencyInjection.cs`; `src/media/src/CodeForCoders.Media.Application/DependencyInjection.cs`; `docker-compose.yml`; `docker-compose.remote.yml`; `docker-compose.komodo.apis.yml`; `docker-compose.stable.yml`; `docker-compose.stable.infra.yml`
- **ref:** `src/media/src/CodeForCoders.Media.Infra.Messaging/VideoPreparationWorker.cs` e `FfmpegVideoTranscoder.cs:148-199` (laço, orçamento de disco, ffmpeg); `src/media/src/CodeForCoders.Media.Application/UseCases/PlaybackSessions/GetPlaybackResource/GetPlaybackResource.cs:18-23` (obtenção e zeragem da chave); `src/media/src/CodeForCoders.Media.Application/UseCases/Videos/PrepareVideo/PrepareVideo.cs`; `src/media/tests/CodeForCoders.Media.IntegrationTests/FfmpegTools.cs` e `VideoPreparationTestContext.cs`; `~/github-tassosgomes/whisper/tasks/prd-api-transcricao-assincrona/api-contract.yaml` (contrato do serviço, 1.0.0); `docs/adr/0006`, `0014`, `0019`, `0020`; skill `dotnet`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/media` | `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.ArchitectureTests/CodeForCoders.Media.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.VideoPreparationTests --minimum-expected-tests 1` | exit 0 (regressão intacta) | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.FfmpegAvailabilityTests --minimum-expected-tests 1` | exit 0 (regressão intacta) | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.VideoTranscriptionRequestTests --minimum-expected-tests 1` | exit 0 (regressão intacta) | `ci-dotnet.yml` Testes |

## Pronto quando

- [ ] Gate focalizado passa (exit 0).
- [ ] Uma tentativa `queued` sobre um vídeo preparado de verdade chega a `ready`, com os trechos aparados, ordenados e o estado do vídeo `ready`/`readable`.
- [ ] Ao fim não restam objeto de áudio, diretório de trabalho nem `key.bin`, nem em caso de falha no meio.
- [ ] Nenhum log, span ou atributo contém a chave, a URL assinada ou o texto.
- [ ] Resultado com `schemaVersion` desconhecida ou acima dos limites termina `failed` sem gravar texto.
- [ ] Os cinco parâmetros de serviço aparecem em `docker-compose.remote.yml` e nos demais ambientes listados.
