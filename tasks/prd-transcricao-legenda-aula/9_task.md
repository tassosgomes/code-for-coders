---
status: done
task_kind: vertical
blocked_by: ["2.0", "5.0"]
gate: 'dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.VideoTranscriptionReadTests --minimum-expected-tests 7 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/CodeForCoders.BffAdmin.EndToEndTests.csproj -- --filter-class CodeForCoders.BffAdmin.EndToEndTests.VideoTranscriptionReadTests --minimum-expected-tests 4 && npm --prefix src/admin-spa run test -- video-transcription-reader'
gate_expect: "Pelo menos 11 testes .NET passam (7 de integração de media e 4 E2E do bff-admin) e os testes do admin-spa com o fragmento video-transcription-reader passam"
---

# 9.0 O professor lê a transcrição pronta na tela Vídeos, somente leitura

**Fatia:** V-05 · **Cobre:** RF-08, RN-M27, RN-TL08 · **Spec:** `techspec.md#v-05-professor-lê-a-transcrição-somente-leitura` · **ADR:** 0005, 0020

## Comportamento

**`media` — `getVideoTranscriptionInternal` e `bff-admin` — `GET /api/v1/videos/{videoId}/transcription`
(`getVideoTranscription`):** devolve a transcrição **vigente** do vídeo (idioma, duração, `revision` opaco
`tr_` + 8 últimos hexadecimais do id da tentativa vigente, e a lista de trechos). Regras:

- sem vigente → 409 `TRANSCRIPTION_NOT_READY`;
- sem `midia.enviar` → 403; vídeo de outra escola → 404 (G07);
- `Cache-Control: private, no-store`; não abre sessão de reprodução nem consulta Matrícula;
- durante o Refazer devolve a vigente; o texto não aparece em log, métrica, span nem URL;
- o `bff-admin` repassa corpo, `Content-Type` e `Cache-Control` e respeita o limite de resposta de 8 MiB.

**`admin-spa`:** com `readable: true`, a linha oferece abrir a transcrição na superfície desenhada em 2.0:
lista de trechos com tempo, **sem** busca, **sem** clique para o vídeo e **sem** edição. Se há uma refeita
em andamento, o painel avisa que o texto mostrado é o vigente e que há uma nova a caminho. Estados de
carga, erro e "sem transcrição pronta" como no design. O texto é renderizado como nós de texto (nunca
HTML) e não é guardado em cache persistente.

**Contratos:** acrescentar `getVideoTranscription` / `getVideoTranscriptionInternal` e os schemas de
trecho e transcrição aos dois contratos.

## Fora do escopo desta task

- Busca, destaque e clique para o tempo: só o aluno (10.0).
- Edição da transcrição: fora do PRD.

## Decisões fechadas

- Somente leitura, sem edição (RN-TL08); sem Matrícula nem sessão (RN-M27).
- Autorização pela política existente `midia.enviar` (`MediaAuthorization.PolicyName`).
- Nenhum texto de aula em cache persistente, telemetria ou URL.

## Modificar / Referenciar

- **modificar:** `src/media/src/CodeForCoders.Media.Api/Endpoints/VideoEndpoints.cs`; `src/media/src/CodeForCoders.Media.Application/DependencyInjection.cs`; `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/VideoLibraryEndpoints.cs`, `Clients/IVideoLibraryClient.cs`, `Clients/VideoLibraryClient.cs`, `ApiModels/VideoResponses.cs`; `src/admin-spa/src/features/videos/components/videos-area-screen.tsx`; `src/admin-spa/src/testing/handlers.ts`; `contracts/bff-admin/openapi-midia.yaml`; `contracts/media/openapi-internal.yaml`
- **ref:** `docs/design/wireframes-transcricao.md`; `src/media/src/CodeForCoders.Media.Application/UseCases/Videos/GetVideo`; `api-contract.yaml` e `internal-api-contract-media.yaml` (`getVideoTranscription*`); skills `dotnet` e `react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/media` | `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.ArchitectureTests/CodeForCoders.Media.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.VideoLibraryAuthorizationTests --minimum-expected-tests 1` | exit 0 (regressão intacta) | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.TranscriptionRestartTests --minimum-expected-tests 1` | exit 0 (regressão intacta) | `ci-dotnet.yml` Testes |
| `src/bff-admin` | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-admin` | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/CodeForCoders.BffAdmin.EndToEndTests.csproj -- --filter-class CodeForCoders.BffAdmin.EndToEndTests.VideoLibraryTests --minimum-expected-tests 1` | exit 0 (regressão intacta) | `ci-dotnet.yml` Testes |
| `src/bff-admin` | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/CodeForCoders.BffAdmin.EndToEndTests.csproj -- --filter-class CodeForCoders.BffAdmin.EndToEndTests.VideoTranscriptionTests --minimum-expected-tests 1` | exit 0 (regressão intacta) | `ci-dotnet.yml` Testes |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run test -- video authoring-video-picker` | exit 0 (telas e fluxos existentes intactos) | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run build` | exit 0 | `ci-react-ts.yml` (build) |
| Contratos | `npx --yes @stoplight/spectral-cli lint contracts/bff-admin/openapi-midia.yaml contracts/media/openapi-internal.yaml --ruleset contracts/.spectral-exception.yaml --fail-severity=error` | exit 0 | `contracts/README.md` |

## Pronto quando

- [x] Gate focalizado passa (exit 0).
- [x] Com transcrição pronta, o professor abre e lê os trechos com tempo; não há como editar, buscar ou clicar para o vídeo.
- [x] Sem vigente → 409 `TRANSCRIPTION_NOT_READY`; sem permissão → 403; outra escola → 404.
- [x] Durante o Refazer o painel mostra a vigente e o aviso de refeita em andamento.
- [x] Nenhum texto em log ou URL nos testes (inspeção do log do teste).
