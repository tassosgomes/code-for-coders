---
status: done
task_kind: vertical
blocked_by: []
gate: 'dotnet test --project src/media/tests/CodeForCoders.Media.UnitTests/CodeForCoders.Media.UnitTests.csproj -- --filter-class CodeForCoders.Media.UnitTests.TranscriptionEligibilityTests --minimum-expected-tests 6 && dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.VideoTranscriptionRequestTests --minimum-expected-tests 10 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/CodeForCoders.BffAdmin.EndToEndTests.csproj -- --filter-class CodeForCoders.BffAdmin.EndToEndTests.VideoTranscriptionTests --minimum-expected-tests 5'
gate_expect: "Pelo menos 21 testes .NET passam (6 unitários e 10 de integração de media, 5 E2E do bff-admin)"
---

# 3.0 O professor pede a transcrição de um vídeo pronto e o estado aparece no vídeo

**Fatia:** V-01 (pedido e estado) · **Cobre:** RF-02 (pedido, estado, `canStart`), RN-M18, RN-M20, RN-M23, RN-TL06 (estado), US transcrever vídeo antigo; T-01, T-03 · **Spec:** `techspec.md#v-01-professor-transcreve-um-vídeo-pronto-e-vê-a-transcrição-ficar-pronta` · **ADR:** 0006, 0019, 0020

## Comportamento

**Ponta a ponta (HTTP):** o `bff-admin` expõe `POST /api/v1/videos/{videoId}/transcription-requests`
(`requestVideoTranscription`), que repassa a `media` (`requestVideoTranscriptionInternal`). O pedido
de um vídeo `ready` sem transcrição devolve 202 com o estado `in-progress` e grava uma tentativa
`queued` (origem solicitada, com o solicitante). Esta task **não** processa a tentativa: o worker
chega em 4.0.

**`Video.transcription`** (`status`, `readable`, `readyAt`, `canStart`, motivo quando houver) passa a
vir em toda resposta que devolve `Video`: `listVideos`, `getVideo`, `updateVideoTitle` e
`replaceVideoTags`, e nas respectivas operações internas. O estado é **derivado** da tentativa
(regra do ciclo de vida da TechSpec); vídeo que não está `ready` é sempre `not-requested`,
`readable: false`, `canStart: false`. Vídeos existentes devolvem `not-requested` sem migração de
dados. A leitura em lote do estado acompanha a das tags (sem N+1).

**Regras do pedido:**

- Duas pessoas pedindo ao mesmo tempo, ou o pedido com tentativa em andamento, resultam em **uma**
  tentativa (índice único parcial, não código); o segundo pedido responde 202 com o estado real.
- Vídeo não pronto → 409 `VIDEO_NOT_READY`.
- Última falha `duration-limit` ou `size-limit` → 409 `TRANSCRIPTION_NOT_RETRYABLE` (a falha em si
  nasce em 4.0 e 8.0; aqui vale a regra sobre uma tentativa `failed` preparada em teste).
- Recurso desligado (`Transcription__Enabled=false`, decisão Q-A): estado derivado
  `not-requested`, `canStart: false`, e o POST responde 409 `TRANSCRIPTION_NOT_RETRYABLE`; nenhuma
  linha é criada.
- Ator sem `midia.enviar` → 403; vídeo de outra escola → 404 (G07). `Idempotency-Key` e CSRF
  exigidos como nas demais rotas de vídeo; mesma chave e corpo → mesma resposta.

**Migration** das tabelas da fila e do conteúdo (`video_transcriptions` com os índices únicos
parciais de "uma em andamento" e "uma vigente" e o índice de reivindicação; `video_transcription_contents`;
tabela de avisos), gerada **só** pela ferramenta do EF Core, com filtro de escola como as demais
entidades.

**Configuração:** `Transcription__Enabled` (api e worker), replicada em `docker-compose.yml`
(`false` na pilha local completa), `docker-compose.remote.yml`, `docker-compose.komodo.apis.yml` e
`docker-compose.stable.yml`.

**Contratos:** acrescentar a `contracts/bff-admin/openapi-midia.yaml` e
`contracts/media/openapi-internal.yaml` o que esta task implementa: `requestVideoTranscription` e
`*Internal`, o objeto `transcription` em `Video`, e os schemas e exemplos de erro usados. O conteúdo
vem dos recortes `api-contract.yaml` e `internal-api-contract-media.yaml`. Como `Video.transcription`
é obrigatório e os testes validam `Video` contra o schema vigente (`additionalProperties: false`), a
promoção é parte desta task.

## Fora do escopo desta task

- Processar a tentativa (extração, envio, cópia): 4.0.
- Tela Vídeos: 5.0. Disparo automático na preparação: 6.0.
- `getVideoTranscription` (leitura do professor): 9.0.
- Motivos de falha produzidos pelo pipeline e o Refazer: 8.0 (aqui só a regra de elegibilidade).

## Decisões fechadas

- A Transcrição é entidade-fila própria, nunca estado do `Video` (RN-M19, D-02, ADR-0020).
- Estado do produto derivado de quatro valores; o estado interno é mais fino (`queued`, `working`, `waiting`, `ready`, `failed`).
- Invariantes por índice único parcial: uma em andamento por vídeo e uma vigente por vídeo (RN-M20).
- `Idempotency-Key` pelo mecanismo existente de 24 h; CSRF pelo middleware do `bff-admin`.
- Migration só pelo `dotnet ef`; override remoto obrigatório (AGENTS.md).

## Modificar / Referenciar

- **modificar:** `src/media/src/CodeForCoders.Media.Domain` (entidade Transcrição e invariantes); `src/media/src/CodeForCoders.Media.Infra.Data/MediaDbContext.cs`, `DependencyInjection.cs` e `Migrations/` (gerada); `src/media/src/CodeForCoders.Media.Infra.Data/Videos/VideoQueries.cs` (estado em lote); `src/media/src/CodeForCoders.Media.Application/UseCases/Videos/VideoOutput.cs`, `ListVideos`, `GetVideo`, `UpdateVideoTitle`, `ReplaceVideoTags`; `src/media/src/CodeForCoders.Media.Application/DependencyInjection.cs`; `src/media/src/CodeForCoders.Media.Api/Endpoints/VideoEndpoints.cs`; `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/VideoLibraryEndpoints.cs`, `Clients/IVideoLibraryClient.cs`, `Clients/VideoLibraryClient.cs`, `ApiModels/VideoResponses.cs`; `src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/VideoLibraryHandler.cs`; `docker-compose.yml`; `docker-compose.remote.yml`; `docker-compose.komodo.apis.yml`; `docker-compose.stable.yml`; `contracts/bff-admin/openapi-midia.yaml`; `contracts/media/openapi-internal.yaml`
- **ref:** `src/media/src/CodeForCoders.Media.Infra.Data/Videos/VideoPreparationRepository.cs:28-56` (padrão de concessão e `SKIP LOCKED`); `src/media/src/CodeForCoders.Media.Application/UseCases/Videos/UpdateVideoTitle/UpdateVideoTitle.cs` (idempotência); `src/media/tests/CodeForCoders.Media.IntegrationTests/VideoLibraryApiFactory.cs` e `MediaIntegrationFixture.cs`; `tasks/prd-transcricao-legenda-aula/api-contract.yaml`, `internal-api-contract-media.yaml`; `docs/adr/0020-transcricao-de-aula-derivada-da-versao-de-reproducao.md`; skill `dotnet`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/media` | `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.ArchitectureTests/CodeForCoders.Media.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet ef migrations has-pending-model-changes --project src/media/src/CodeForCoders.Media.Infra.Data --startup-project src/media/src/CodeForCoders.Media.Api` | exit 0, sem mudança pendente | AGENTS.md (migrations pela ferramenta do EF Core) |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.VideoTitleSearchTests --minimum-expected-tests 1` | exit 0 (regressão intacta) | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.VideoTagReplacementTests --minimum-expected-tests 1` | exit 0 (regressão intacta) | `ci-dotnet.yml` Testes |
| `src/bff-admin` | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-admin` | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/CodeForCoders.BffAdmin.EndToEndTests.csproj -- --filter-class CodeForCoders.BffAdmin.EndToEndTests.VideoLibraryTests --minimum-expected-tests 1` | exit 0 (regressão intacta) | `ci-dotnet.yml` Testes |
| `src/bff-admin` | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/CodeForCoders.BffAdmin.EndToEndTests.csproj -- --filter-class CodeForCoders.BffAdmin.EndToEndTests.VideoTagTests --minimum-expected-tests 1` | exit 0 (regressão intacta) | `ci-dotnet.yml` Testes |
| Contratos | `npx --yes @stoplight/spectral-cli lint contracts/bff-admin/openapi-midia.yaml contracts/media/openapi-internal.yaml --ruleset contracts/.spectral-exception.yaml --fail-severity=error` | exit 0 | `contracts/README.md` |

## Pronto quando

- [ ] Gate focalizado passa (exit 0).
- [ ] Pedido válido → 202 `in-progress` e uma tentativa `queued`; dois pedidos simultâneos → exatamente uma tentativa.
- [ ] Vídeo não pronto → 409 `VIDEO_NOT_READY`; sem permissão → 403; outra escola → 404; sem CSRF → 403 `CSRF_INVALID`.
- [ ] Com o recurso desligado, o estado é `not-requested` com `canStart: false` e o POST responde 409 `TRANSCRIPTION_NOT_RETRYABLE` sem criar linha.
- [ ] Toda resposta de `Video` traz `transcription`, inclusive `updateVideoTitle` e `replaceVideoTags`.
- [ ] Spectral sem erros nos dois contratos tocados.
