---
status: done
task_kind: vertical
blocked_by: ["2.0"]
gate: 'dotnet test --project src/media/tests/CodeForCoders.Media.UnitTests/CodeForCoders.Media.UnitTests.csproj -- --filter-class CodeForCoders.Media.UnitTests.VideoTagNormalizationTests --minimum-expected-tests 6 && dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.VideoTagReplacementTests --minimum-expected-tests 10 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/CodeForCoders.BffAdmin.EndToEndTests.csproj -- --filter-class CodeForCoders.BffAdmin.EndToEndTests.VideoTagTests --minimum-expected-tests 5 && npm --prefix src/admin-spa run test -- video-tags-edit'
gate_expect: "Pelo menos 21 testes .NET passam (6 unitários e 10 de integração de media, 5 E2E do bff-admin) e os testes do admin-spa com o fragmento video-tags-edit passam"
---

# 3.0 O professor edita as tags de um vídeo na tela Vídeos e as vê na lista

**Fatia:** V-01 · **Cobre:** RF-02; RN-T01, RN-T02, RN-T03, RN-T04, RN-T05 (escrita), RN-T06; US-02; C-01, C-02, C-03, C-06, C-08 · **Spec:** `techspec.md` § V-01, § Bloco Backend — `media`, § Bloco Backend — `bff-admin`, § Bloco Frontend · **ADR:** 0005, 0006

## Comportamento

**Ponta a ponta:** na tela Vídeos (`/admin/videos`), cada linha mostra as tags do vídeo como
etiquetas, na grafia canônica e em ordem alfabética, sem diferenciar maiúsculas nem acentos. A ação
de editar tags abre a superfície desenhada em 2.0. O professor digita tags em **texto livre**
(a sugestão chega em 4.0), remove etiquetas e salva. A linha passa a mostrar exatamente o conjunto
gravado, e salvar `[]` deixa a linha sem etiquetas. Título, estado e vínculos do vídeo não mudam.

**`media` — `replaceVideoTagsInternal` (`PUT /internal/v1/videos/{videoId}/tags`):**

- **Normalização** (domínio, `techspec.md` § Bloco Backend — `media`):
  - a grafia de exibição tira os espaços das pontas e reduz os repetidos;
  - a forma normalizada é minúscula e sem acentos;
  - tags equivalentes no mesmo pedido são juntadas sem erro, e fica a primeira.
- **Validação:**
  - depois de normalizar: vazia, com mais de 30 caracteres ou com caractere de controle → 422
    `TAG_INVALID`;
  - mais de 10 distintas → 422 `TAGS_LIMIT_EXCEEDED`;
  - acima de 50 itens ou 100 caracteres por item → 400 `INVALID_REQUEST`.
- **Grafia canônica:** cada tag é garantida no catálogo da escola por inserção que não sobrescreve
  (`ON CONFLICT DO NOTHING` na escola + forma normalizada). Se "Docker" já existe, "docker" grava
  "Docker".
- **Transação:**
  - trava a linha do vídeo (`SELECT … FOR UPDATE`); vídeo inexistente ou de outra escola → 404
    `VIDEO_NOT_FOUND`;
  - apaga os vínculos do vídeo e insere os novos;
  - grava o registro de idempotência `replaceVideoTags`.

  **Não** grava a linha de `videos` e **não** grava outbox.
- **Concorrência:** duas trocas concorrentes no mesmo vídeo resultam no conjunto de **uma** delas
  por inteiro, nunca na união.
- **Durante a preparação:** trocar as tags não faz falhar `MarkPreparing`/`MarkReady`, cujo token
  de concorrência é `PreparationLeaseId`.
- **Idempotência:**
  - mesma chave e mesmo corpo → mesma resposta;
  - mesma chave com outro corpo → 422 `IDEMPOTENCY_KEY_REUSED`;
  - registro gravado **antes** desta entrega (resposta sem `tags`) → o replay devolve `tags: []`.
- **Telemetria:** `media.video_tags.assigned` com `origin` = `existing` | `new` por tag atribuída,
  sem o texto da tag nos atributos.

**`media` — saídas:** `listVideosInternal`, `getVideoInternal`, `updateVideoTitleInternal`,
`completeVideoUploadInternal` e as operações de envio passam a devolver `tags`, nunca `null`.
Vídeos e envios existentes devolvem `[]`.

**Migration** gerada pelo EF (`dotnet ef migrations add`): catálogo de tags da escola (único por
escola + forma normalizada) e vínculo vídeo–tag (PK vídeo+tag, índice escola+tag), com filtro de
tenant como as demais entidades.

**`bff-admin`:**

- `PUT /api/v1/videos/{videoId}/tags` (`replaceVideoTags`) com a mesma checagem de sessão e
  `midia.enviar` das rotas de vídeo, e exigindo `Idempotency-Key`. O CSRF já é coberto pelo
  middleware.
- Repassa os 422 de `media` com o mesmo `code`.
- `VideoResponse` e `VideoUploadResponse` ganham `tags`; sem isso o campo some.

**`admin-spa`:**

- Os schemas zod estritos de vídeo e envio aceitam `tags` (lista de string).
- As etiquetas aparecem na lista, e o diálogo de edição aplica os mesmos limites antes de enviar.
- Os 422 mostram a mensagem pelo `code`.
- Sem `midia.enviar` a área inteira já é bloqueada, como hoje.

**Contratos:** acrescentar a `contracts/bff-admin/openapi-midia.yaml` e
`contracts/media/openapi-internal.yaml` o que esta task implementa: `replaceVideoTags`/`*Internal`,
`tags` em `Video` e `VideoUpload`, `Tag`, `TagList`, `TagInputList`,
`ReplaceVideoTagsRequest` e os exemplos de erro. O conteúdo vem dos recortes
`api-contract.yaml` e `internal-api-contract.yaml`.

## Fora do escopo desta task

- Sugestão das tags da escola e `listVideoTags` → 4.0.
- Tags no pedido de envio e na conclusão do envio → 5.0. Aqui o envio só **devolve** `tags: []`.
- Filtro por tag → 6.0 e 7.0.
- Consolidação dos contratos (versão, origem, README) → encerramento do PRD.

## Decisões fechadas

- Tags em tabelas próprias, nunca na linha de `videos` (`techspec.md` § Decisões Técnicas).
- Troca do conjunto inteiro; a última gravação vale por inteiro; sem `ETag` (C-02, C-03).
- A tag é uma string na grafia canônica, sem id (C-01); a normalização junta as equivalentes sem erro (C-06).
- `Idempotency-Key` e CSRF exigidos (C-08).
- Uma tag sem vídeo continua no catálogo, mas é invisível (RN-T05).
- `admin-spa`, `bff-admin` e `media` são publicados juntos (`contracts.md`, Evolução).

## Modificar / Referenciar

- **modificar:** `src/media/src/CodeForCoders.Media.Domain/Entities/Video.cs` (expor a dobra de normalização); `src/media/src/CodeForCoders.Media.Infra.Data/MediaDbContext.cs`; `src/media/src/CodeForCoders.Media.Infra.Data/Videos/VideoQueries.cs` (tags por vídeo); `src/media/src/CodeForCoders.Media.Application/UseCases/Videos/VideoOutput.cs`; `src/media/src/CodeForCoders.Media.Application/UseCases/VideoUploads/VideoUploadOutputs.cs`; `src/media/src/CodeForCoders.Media.Application/UseCases/Videos/ListVideos/ListVideos.cs`; `src/media/src/CodeForCoders.Media.Application/UseCases/Videos/GetVideo/GetVideo.cs`; `src/media/src/CodeForCoders.Media.Application/UseCases/Videos/UpdateVideoTitle/UpdateVideoTitle.cs`; `src/media/src/CodeForCoders.Media.Application/UseCases/VideoUploads/VideoUploadUseCaseHelpers.cs` (replay sem `tags` → `[]`); `src/media/src/CodeForCoders.Media.Application/Common/MediaTelemetry.cs`; `src/media/src/CodeForCoders.Media.Api/Endpoints/VideoEndpoints.cs`; `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/VideoLibraryEndpoints.cs`; `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Clients/IVideoLibraryClient.cs`; `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Clients/VideoLibraryClient.cs`; `src/bff-admin/src/CodeForCoders.BffAdmin.Api/ApiModels/VideoResponses.cs`; `src/bff-admin/src/CodeForCoders.BffAdmin.Api/ApiModels/VideoUploadResponses.cs`; `src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/VideoLibraryHandler.cs`; `src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/VideoUploadHandler.cs`; `src/admin-spa/src/features/videos/api/get-videos.ts`; `create-video-upload.ts`; `get-pending-video-uploads.ts`; `get-video-upload.ts`; `complete-video-upload.ts`; `update-video-title.ts`; `src/admin-spa/src/features/videos/components/videos-area-screen.tsx`; `src/admin-spa/src/testing/handlers.ts`; `src/admin-spa/src/testing/authoring-video-handlers.ts` (vídeos com `tags`); `contracts/bff-admin/openapi-midia.yaml`; `contracts/media/openapi-internal.yaml`
- **ref:** `src/media/src/CodeForCoders.Media.Infra.Data/Videos/VideoConfiguration.cs:73` (token de concorrência da preparação); `src/media/src/CodeForCoders.Media.Infra.Data/Videos/VideoUploadRepository.cs:167` (padrão de transação com `FOR UPDATE`); `src/media/tests/CodeForCoders.Media.IntegrationTests/VideoLibraryApiFactory.cs` e `MediaIntegrationFixture.cs` (aplicação real com PostgreSQL em contêiner); `src/media/tests/CodeForCoders.Media.IntegrationTests/VideoTitleSearchTests.cs` (padrão de teste da biblioteca); `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Security/BffSecurityMiddleware.cs:194`; `api-contract.yaml`, `internal-api-contract.yaml`; skills `dotnet` e `react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/media` | `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.ArchitectureTests/CodeForCoders.Media.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet ef migrations has-pending-model-changes --project src/media/src/CodeForCoders.Media.Infra.Data --startup-project src/media/src/CodeForCoders.Media.Api` | exit 0, sem mudança pendente | AGENTS.md (migrations pela ferramenta) |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.VideoTitleSearchTests` | exit 0 (busca e título intactos) | `ci-dotnet.yml` Testes |
| `src/bff-admin` | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-admin` | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/CodeForCoders.BffAdmin.EndToEndTests.csproj -- --filter-class CodeForCoders.BffAdmin.EndToEndTests.VideoLibraryTests` | exit 0 (rotas existentes com `tags`) | `ci-dotnet.yml` Testes |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run test -- video authoring-video-picker` | exit 0 (telas de vídeo e seletor existentes com `tags`) | `ci-react-ts.yml` |
| Contratos | `npx --yes @stoplight/spectral-cli lint contracts/bff-admin/openapi-midia.yaml contracts/media/openapi-internal.yaml --ruleset contracts/.spectral-exception.yaml --fail-severity=error` | exit 0 | `contracts/README.md` |

## Pronto quando

- [ ] Gate focalizado passa (exit 0).
- [ ] Salvar "docker", " Docker " e "DOCKER" grava uma tag; com "Docker" já na escola, "docker" aparece como "Docker".
- [ ] 11 tags distintas, uma tag de 31 caracteres ou uma tag só de espaços → 422 com o `code` do contrato, e o vídeo não muda.
- [ ] Vídeo de outra escola → 404 `VIDEO_NOT_FOUND`; sem CSRF → 403 `CSRF_INVALID`.
- [ ] Duas trocas concorrentes no mesmo vídeo deixam exatamente o conjunto de uma delas.
- [ ] Trocar as tags durante a preparação não interrompe a preparação.
- [ ] Toda resposta de vídeo e de envio traz `tags` (`[]` nos existentes), inclusive o replay de idempotência antigo.
- [ ] Em dev, depois de `scripts/remote-infra.sh migrate`, as tags editadas em `http://localhost:8081/admin/videos` sobrevivem ao recarregamento.
