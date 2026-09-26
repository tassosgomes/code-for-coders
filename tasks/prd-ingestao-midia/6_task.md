---
status: pending
task_kind: vertical
blocked_by: ["4.0", "5.0"]
gate: 'dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.VideoUploadResumeTests --minimum-expected-tests 7 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/CodeForCoders.BffAdmin.EndToEndTests.csproj -- --filter-class CodeForCoders.BffAdmin.EndToEndTests.VideoUploadResumeTests --minimum-expected-tests 2 && npm --prefix src/admin-spa run test -- video-upload-resume'
gate_expect: 'Pelo menos 12 testes passam: 7 Media (MinIO real, relógio controlado), 2 BFF, 3 SPA'
---

# 6.0 Envio interrompido continua de onde parou, e envio abandonado some em 24 h

**Fatia:** V-03 · **Cobre:** RF-03, US-02, DP-02 (retomada por 24 h), C-16 · **Spec:** `techspec.md` § V-03, § Media worker (passo 7a) · **ADR:** [ADR-0006](../../docs/adr/0006-preparacao-de-video-e-custodia-de-chave.md)

## Comportamento

- **Retomada:** `createVideoUpload` com a mesma `fingerprint` de um envio pendente e não expirado **do
  mesmo ator e tenant** → 200 com esse envio e `receivedParts` vindo da listagem de partes do
  armazenamento. Arquivo diferente → envio novo (201). A mesma `fingerprint` de **outro** ator não
  retoma nada: cria envio novo para quem pediu. Duas retomadas simultâneas (duas abas) → um envio só.
- **Pendentes:** `listPendingVideoUploads` devolve só os envios pendentes e não expirados do próprio
  ator, com nome do arquivo, partes recebidas e `expiresAt`.
- **Expiração:** a varredura periódica do papel `worker` encontra envio pendente com `expiresAt`
  vencido, aborta o upload em várias partes no armazenamento (as partes somem) e marca o envio como
  expirado. Depois disso, qualquer operação sobre ele → 404 `UPLOAD_NOT_FOUND`, ele não aparece nos
  pendentes e escolher o mesmo arquivo recomeça do zero. Uma parte em voo quando o envio expira falha, e
  a chamada seguinte responde `UPLOAD_NOT_FOUND`. Armazenamento indisponível na retomada → 503
  `STORAGE_UNAVAILABLE` / BFF 502 (C-16).
- **SPA:** ao abrir a área com envio pendente, o aviso do design aprovado — *"envio incompleto de
  aula-3.mp4 — selecione o mesmo arquivo para continuar"* — com o prazo; ao escolher o mesmo arquivo, o
  progresso começa das partes já recebidas (ex.: 10/48) e só as faltantes são enviadas; outro arquivo
  inicia envio novo; envio expirado não aparece.

## Fora do escopo desta task

Preparação (7.0). Regra de ciclo de vida do bucket em produção (plataforma).

## Decisões fechadas

- Retomada até a validade da última URL + 24 h: D-02. Índice único de pendente por (tenant, ator, fingerprint): techspec, Media — modelo.
- `receivedParts` sempre do armazenamento, nunca de memória do servidor.
- A varredura roda só no papel `worker` (ADR-0006).
- Relógio controlado por `TimeProvider` nos testes de expiração.

## Modificar / Referenciar

- **modificar:** `src/media/src/CodeForCoders.Media.Application/Interfaces/IMediaStoragePort.cs`, `src/media/src/CodeForCoders.Media.Infra.Data/Adapters/S3MediaStorageAdapter.cs` (abortar upload, se ainda não coberto em 4.0)
- **modificar:** `src/media/src/CodeForCoders.Media.Infra.Messaging/DependencyInjection.cs` (varredura no papel `worker`)
- **modificar:** `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/EndpointExtensions.cs`; `src/admin-spa/src/testing/handlers.ts`
- **ref:** `internal-api-contract.yaml` 1.0.1 (`listPendingVideoUploadsInternal`, `getVideoUploadInternal`); `docs/design/wireframes-videos.md` e Figma; skills `dotnet` e `react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/media` | `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/media` | `dotnet build src/media/CodeForCoders.Media.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.UnitTests/CodeForCoders.Media.UnitTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.ArchitectureTests/CodeForCoders.Media.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/bff-admin` | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-admin` | `dotnet build src/bff-admin/CodeForCoders.BffAdmin.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run build` | exit 0 | `ci-react-ts.yml` |

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 12 testes.
- [ ] Mesma fingerprint retoma com as partes do armazenamento; fingerprint de outro ator cria envio novo; pendente de um professor não aparece para outro.
- [ ] Depois de `expiresAt`, a varredura aborta o upload, as partes somem do MinIO e a operação responde `UPLOAD_NOT_FOUND`.
- [ ] Smoke no Compose: interromper o envio no navegador, voltar à área, ver o aviso e continuar do ponto em que parou.
