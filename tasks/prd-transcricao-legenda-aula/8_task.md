---
status: pending
task_kind: vertical
blocked_by: ["2.0", "5.0"]
gate: 'dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.TranscriptionFailurePolicyTests --minimum-expected-tests 14 && dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.TranscriptionRestartTests --minimum-expected-tests 8 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/CodeForCoders.BffAdmin.EndToEndTests.csproj -- --filter-class CodeForCoders.BffAdmin.EndToEndTests.VideoTranscriptionRestartTests --minimum-expected-tests 4 && npm --prefix src/admin-spa run test -- video-transcription-restart'
gate_expect: "Pelo menos 26 testes .NET passam (14 de política de falha e 8 de Refazer em media, 4 E2E do bff-admin) e os testes do admin-spa com o fragmento video-transcription-restart passam"
---

# 8.0 Falhas aparecem com motivo e o professor refaz sem perder a transcrição vigente

**Fatia:** V-04 · **Cobre:** RF-02 (falha, Refazer, `canStart`, 409), RF-07 (limite de espera), RN-M18, RN-M24, RN-TL06 (motivo), RN-TL07 · **Spec:** `techspec.md#v-04-falhas-aparecem-com-motivo-e-o-professor-refaz-sem-perder-a-transcrição-vigente` · **ADR:** 0020

## Comportamento

**`media`:** a política de erro e de nova tentativa por situação, sem reabrir:

- vídeo > 7200 s ou `MEDIA_DURATION_LIMIT_EXCEEDED` → `failed`/`duration-limit`, sem nova tentativa;
- `413` no envio ou `MEDIA_SIZE_LIMIT_EXCEEDED` → `failed`/`size-limit`, sem nova tentativa;
- rede, timeout, 429, 5xx, 401, 403 ou 422 no envio → continua `queued` com atraso de 1, 5, 15 e 30 min;
  na 5ª falha → `failed`/`service-unavailable`;
- job `failed` com `SOURCE_UNAVAILABLE`, ou resultado expirado (404 após `completed`) → volta a `queued`
  (reextrai o áudio e reenvia), até 3 envios (atrasos de 5 e 30 min), depois `failed`/`service-unavailable`;
- `UNSUPPORTED_MEDIA_FORMAT`, `TRANSCRIPTION_FAILED`, ffmpeg que falha e resultado inválido →
  `failed`/`processing-failed`; extração com até 3 tentativas (1 e 5 min), o resto sem nova tentativa;
- `deadline_at` vencido sem estado terminal → `failed`/`timed-out`.

Os dois motivos de limite deixam `canStart: false` e o POST responde 409
`TRANSCRIPTION_NOT_RETRYABLE`. O áudio é apagado em toda saída terminal. Um sinal próprio é emitido para
integração rejeitada pelo serviço (401/403).

**Refazer:** acionar `requestVideoTranscription` sobre um vídeo com transcrição `ready` ou `failed`
(e `canStart`) inicia **nova** tentativa **sem tocar** na vigente: enquanto roda, o estado é
`in-progress` com `readable: true`; quando fica `ready`, a troca acontece numa **única transação** (a
nova vira vigente, a antiga deixa de ser, o conteúdo antigo é apagado). Se a nova **falha**, o estado é
`failed` com `readable: true` e a vigente fica intacta. A tentativa `failed` anterior é apagada quando
outra começa. A vigente continua legível para o aluno durante todo o Refazer.

**`bff-admin`:** repasse dos códigos de erro (`VIDEO_NOT_READY`, `TRANSCRIPTION_NOT_RETRYABLE`) com o mesmo
`code`.

**`admin-spa`:** a linha mostra `falhou` com o **motivo** em texto de professor, pelo `failureReason`
(os cinco do desenho aprovado em 2.0), a ação certa (*Refazer*, ou nenhuma nos limites), e *Refazer* em
vídeo `ready`. Com a refeita em andamento ou falha, a tela indica que a anterior continua valendo.

**Contratos:** acrescentar, se ainda faltarem em `contracts/bff-admin/openapi-midia.yaml` e
`contracts/media/openapi-internal.yaml`, os exemplos de 409 e os valores de `failureReason` que esta task
passa a produzir.

## Fora do escopo desta task

- Leitura da transcrição pelo professor e o aviso de "refeita em andamento" no painel: 9.0.
- Calibragem do prazo de 30 h e da cadência: piloto de 30 dias (configuração).

## Decisões fechadas

- Cinco motivos de falha fixos, sem mais (Q-03); dois de limite sem Refazer.
- Refazer não tira a vigente do ar (RN-TL07); a troca é atômica.
- Aviso de job que não é o gravado é ignorado (janela de idempotência do serviço de 120 s).
- Valores de atraso, tentativas e prazo são configuráveis; os acima são os padrões.

## Modificar / Referenciar

- **modificar:** `src/media/src/CodeForCoders.Media.Application/UseCases` (`RequestVideoTranscription` e fluxo do worker: política de erro); `src/media/src/CodeForCoders.Media.Infra.Messaging` (laço do worker: atrasos e prazo); `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/VideoLibraryEndpoints.cs`; `src/admin-spa/src/features/videos/components/videos-area-screen.tsx`; `src/admin-spa/src/features/videos/components/video-status-badge.tsx`; `src/admin-spa/src/testing/handlers.ts`; `contracts/bff-admin/openapi-midia.yaml`; `contracts/media/openapi-internal.yaml`
- **ref:** `docs/design/wireframes-transcricao.md`; `tasks/prd-transcricao-legenda-aula/api-contract.md` (motivos de falha); `src/media/tests/CodeForCoders.Media.IntegrationTests/VideoPreparationFailureTests.cs` (padrão); skills `dotnet` e `react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/media` | `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.ArchitectureTests/CodeForCoders.Media.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.TranscriptionPipelineTests --minimum-expected-tests 1` | exit 0 (regressão intacta) | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.VideoTranscriptionRequestTests --minimum-expected-tests 1` | exit 0 (regressão intacta) | `ci-dotnet.yml` Testes |
| `src/bff-admin` | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-admin` | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/CodeForCoders.BffAdmin.EndToEndTests.csproj -- --filter-class CodeForCoders.BffAdmin.EndToEndTests.VideoTranscriptionTests --minimum-expected-tests 1` | exit 0 (regressão intacta) | `ci-dotnet.yml` Testes |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run test -- video authoring-video-picker` | exit 0 (telas e fluxos existentes intactos) | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run build` | exit 0 | `ci-react-ts.yml` (build) |
| Contratos | `npx --yes @stoplight/spectral-cli lint contracts/bff-admin/openapi-midia.yaml contracts/media/openapi-internal.yaml --ruleset contracts/.spectral-exception.yaml --fail-severity=error` | exit 0 | `contracts/README.md` |

## Pronto quando

- [ ] Gate focalizado passa (exit 0).
- [ ] Cada linha da política de erro (inclusive resultado expirado e `schemaVersion` desconhecida) termina no estado e no motivo previstos.
- [ ] Dois pedidos simultâneos de Refazer resultam em uma tentativa; falha no meio da troca mantém a vigente.
- [ ] Refazer mantém `readable: true`; refeita que falha deixa `failed` com a vigente intacta.
- [ ] `duration-limit` e `size-limit` mostram o motivo, não oferecem Refazer e o POST responde 409 `TRANSCRIPTION_NOT_RETRYABLE`.
