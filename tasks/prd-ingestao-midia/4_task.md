---
status: pending
task_kind: vertical
blocked_by: ["3.0"]
gate: 'dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.VideoUploadTests --minimum-expected-tests 10 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/CodeForCoders.BffAdmin.EndToEndTests.csproj -- --filter-class CodeForCoders.BffAdmin.EndToEndTests.VideoUploadTests --minimum-expected-tests 4 && npm --prefix src/admin-spa run test -- video-upload'
gate_expect: 'Pelo menos 19 testes passam: 10 Media (com MinIO real), 4 BFF, 5 SPA'
---

# 4.0 Professor envia um vídeo e ele aparece na lista como recebido

**Fatia:** V-02 (inclui EN-01) · **Cobre:** RF-02, RF-06, RF-08, RF-10 (original privado), RN-M01, RN-M04, RN-M15, US-01, US-06, DP-02 (formato e tamanho), C-11, C-13, C-16 · **Spec:** `techspec.md` § V-02, § Habilitadores (EN-01), § Media API — regras · **ADR:** [ADR-0002](../../docs/adr/0002-plataforma-de-runtime-coolify.md)

## Comportamento

- **Ambiente (EN-01):** MinIO no `docker-compose.yml` com bucket privado criado na subida e CORS que
  aceita `PUT` só da origem `http://localhost:8081`; Media fala com o MinIO pelo endpoint interno e
  **assina** URLs com o endpoint público (`http://localhost:9000`), path-style; credenciais só locais.
  O `docker-compose.coolify.yml` recebe as variáveis de armazenamento sem segredo no arquivo.
- **Iniciar envio** (`createVideoUpload` → `createVideoUploadInternal`): título só com espaços →
  422 `TITLE_REQUIRED`; tamanho acima de 5 GiB → 422 `FILE_TOO_LARGE`; tipo ou extensão fora de
  {mp4 → `video/mp4`, mov → `video/quicktime`, mkv → `video/x-matroska`} → 422 `FORMAT_NOT_SUPPORTED`.
  Nas três recusas nenhum upload é iniciado no armazenamento e nenhuma URL é emitida. Válido → inicia o
  upload em várias partes e grava o envio (201), `partCount = ceil(fileSize / 64 MiB)`. O BFF acrescenta
  `uploaderName` com o `name` da sessão validada; a identidade do autor vem do `sub` do JWT, nunca do
  corpo (C-13).
- **URLs de parte:** número fora de 1..`partCount` → 422 `PART_OUT_OF_RANGE`; URLs de `PUT` de uma
  parte, válidas por 60 minutos; `expiresAt` = validade da última URL + 24 h. A URL não serve para ler
  nada.
- **Consultar envio:** `receivedParts` vem da listagem de partes do próprio armazenamento.
- **Concluir:** partes faltando ou com tamanho divergente → 422 `UPLOAD_INCOMPLETE`; completas → um
  commit marca o envio concluído e cria o vídeo `received` (201, `Location`), com autor e nome retratado.
  Repetir a conclusão devolve o **mesmo** `videoId`; conclusão concorrente gera um vídeo só.
- Envio de outro ator ou já concluído → 404 `UPLOAD_NOT_FOUND` em todas as operações de envio.
- **Armazenamento indisponível** em iniciar, consultar ou concluir → Media 503 `STORAGE_UNAVAILABLE`
  sem mudar estado; BFF → 502 `MEDIA_UNAVAILABLE` (C-16).
- **Nada público:** o original fica em `{prefix}/{tenantId}/{videoId}/original`, sem título, nome ou
  e-mail na chave; GET anônimo no objeto é negado. Nenhuma resposta traz bucket, host de armazenamento
  ou identificador do upload do provedor, exceto a `url` de parte. URL de parte, título e nome do autor
  não aparecem em log nem span.
- **SPA:** fluxo de envio do Figma aprovado: validação de extensão, tipo e tamanho **antes** da primeira
  chamada; fingerprint de nome, tamanho e data de modificação; URLs em lotes de até 100; até 3 partes em
  paralelo, cada `PUT` sem credencial e recortada do `File` (nunca o arquivo inteiro em memória); parte
  com falha de rede repetida até 3 vezes; aviso antes de sair durante a transferência; `UPLOAD_INCOMPLETE`
  → reconsulta e reenvia só as faltantes; ao fim, a linha *recebido* com título, autor e data. Nenhum
  texto promete proteção contra cópia (RN-M15).

## Fora do escopo desta task

Retomada por fingerprint, lista de pendentes e expiração (6.0). Preparação (7.0). Filtro, busca e
edição de título (9.0).

## Decisões fechadas

- Bytes direto ao armazenamento por URL assinada: C-11. Parte de 64 MiB, URL de 60 min: D-02.
- `IMediaCdnPort` e `DeliveryUri` saem do uso nesta entrega (techspec, Riscos); a porta é redesenhada sem URL de entrega.
- Idempotência pelo mecanismo do serviço, janela de 24 h, escopo tenant + operação + ator.
- Migrations pelo tooling do EF.

## Modificar / Referenciar

- **modificar:** `docker-compose.yml`, `docker-compose.coolify.yml` (MinIO, bucket, CORS, variáveis de armazenamento de Media)
- **modificar:** `Directory.Packages.props` (SDK de S3 da AWS; Testcontainers para MinIO)
- **modificar:** `src/media/src/CodeForCoders.Media.Application/Interfaces/IMediaStoragePort.cs`, `src/media/src/CodeForCoders.Media.Infra.Data/Adapters/S3MediaStorageAdapter.cs`, `src/media/src/CodeForCoders.Media.Infra.Data/Configuration/AwsMediaOptions.cs`, `src/media/src/CodeForCoders.Media.Infra.Data/DependencyInjection.cs`, `src/media/src/CodeForCoders.Media.Api/appsettings.json`
- **modificar:** `src/media/tests/CodeForCoders.Media.UnitTests/MediaStorageBoundaryTests.cs` (testa a porta antiga), `src/media/tests/CodeForCoders.Media.IntegrationTests/MediaIntegrationFixture.cs` (MinIO)
- **modificar:** `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/EndpointExtensions.cs`; `src/admin-spa/src/testing/handlers.ts`
- **ref:** `internal-api-contract.yaml` 1.0.1, `api-contract.yaml` 1.1.1 e exemplos; `docs/design/wireframes-videos.md` e Figma; skills `dotnet` e `react`; Context7 para o SDK S3 (multipart e presign)

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/media` | `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/media` | `dotnet build src/media/CodeForCoders.Media.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.UnitTests/CodeForCoders.Media.UnitTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.ArchitectureTests/CodeForCoders.Media.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.EndToEndTests/CodeForCoders.Media.EndToEndTests.csproj` | exit 0 (parte com o cliente de armazenamento real registrado) | `ci-dotnet.yml` Testes |
| `src/bff-admin` | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-admin` | `dotnet build src/bff-admin/CodeForCoders.BffAdmin.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run build` | exit 0 | `ci-react-ts.yml` |

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 19 testes.
- [ ] Integração com MinIO real: 3 partes enviadas pelas URLs assinadas → conclusão → vídeo `received`; parte faltando → `UPLOAD_INCOMPLETE`; conclusão repetida → mesmo `videoId`.
- [ ] 6 GB e `.avi` recusados sem upload iniciado nem URL emitida; armazenamento parado → 503 sem estado novo.
- [ ] GET anônimo no original negado; nenhuma resposta traz host de armazenamento fora da `url` de parte.
- [ ] Smoke no Compose pelo navegador: o professor de 3.0 envia um MP4 em `http://localhost:8081/admin/videos`, o `PUT` ao MinIO passa pelo CORS, e a linha *recebido* aparece; o objeto existe em `{tenant}/{videoId}/original`.
