---
status: pending
task_kind: vertical
blocked_by: ["4.0"]
gate: 'dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.VideoUploadTagTests --minimum-expected-tests 6 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/CodeForCoders.BffAdmin.EndToEndTests.csproj -- --filter-class CodeForCoders.BffAdmin.EndToEndTests.VideoUploadTagTests --minimum-expected-tests 3 && npm --prefix src/admin-spa run test -- video-upload-tags'
gate_expect: "Pelo menos 9 testes .NET passam (6 de integração de media, 3 E2E do bff-admin) e os testes do admin-spa com o fragmento video-upload-tags passam"
---

# 5.0 O professor informa tags ao enviar um vídeo

**Fatia:** V-03 · **Cobre:** RF-01; RN-T02, RN-T03; US-01; C-05, C-06 · **Spec:** `techspec.md` § V-03, § Bloco Backend — `media` (Casos de uso) · **ADR:** 0006

## Comportamento

**Ponta a ponta:** o formulário de envio tem o campo de tags de 4.0 (modo texto livre), opcional,
abaixo do título. Ao concluir o envio, o vídeo aparece na tela Vídeos com essas tags desde
`received`. Sem tags, o envio segue como hoje.

**`media` — `createVideoUploadInternal`:**

- Aceita `tags` (ausente = `[]`) e aplica a mesma normalização e validação de 3.0. `TAG_INVALID`
  e `TAGS_LIMIT_EXCEEDED` são respondidos **antes** de iniciar o multipart no armazenamento:
  nenhum envio, nenhuma URL de parte.
- As tags, já na grafia canônica, ficam guardadas no envio (coluna nova, `[]` nos envios
  existentes; migration pelo EF) e voltam em `VideoUpload.tags`.
- O hash de idempotência inclui `tags`: mesma chave com outras tags → 422
  `IDEMPOTENCY_KEY_REUSED`.
- **Retomada** (mesmo ator, mesma `fingerprint`, envio pendente) → 200 com as tags do envio
  **pendente**; as do novo pedido são ignoradas (C-05).

**`media` — `completeVideoUploadInternal`:**

- Na transação de `VideoUploadRepository.CompleteAsync`, depois de criar o vídeo, resolve de novo
  as tags do envio para a grafia canônica vigente e cria os vínculos. Uma tag que outro vídeo criou
  com outra grafia entre o início e a conclusão do envio fica com a grafia canônica.
- Concluir de novo devolve o mesmo vídeo com as tags que ele tem hoje.
- Conta `media.video_tags.assigned` como em 3.0.

**`bff-admin`:** `CreateVideoUploadRequest` e `CreateVideoUploadInternalRequest` ganham `tags`
opcional; o BFF repassa sem validar regra de negócio.

**`admin-spa`:** o formulário envia `tags`, mostra os erros 422 de tag no campo e, na retomada,
exibe as tags do envio pendente.

**Contratos:** acrescentar `tags` (`TagInputList`) ao pedido de `createVideoUpload`/`*Internal`
e as descrições de retomada e conclusão aos dois arquivos de `contracts/`.

## Fora do escopo desta task

- Editar tags depois do envio (pronto em 3.0). Filtros → 6.0 e 7.0.
- Copiar tags de um vídeo que falhou para o reenvio (non-goal do PRD).

## Decisões fechadas

- Na retomada valem as tags do envio pendente (C-05).
- Validação das tags antes do multipart (`techspec.md` § Bloco Backend — `media`).
- Resolução canônica de novo na conclusão (`techspec.md` § Bloco Backend — `media`).

## Modificar / Referenciar

- **modificar:** `src/media/src/CodeForCoders.Media.Domain/Entities/VideoUpload.cs`; `src/media/src/CodeForCoders.Media.Infra.Data/Videos/VideoUploadConfiguration.cs`; `src/media/src/CodeForCoders.Media.Infra.Data/Videos/VideoUploadRepository.cs`; `src/media/src/CodeForCoders.Media.Application/UseCases/VideoUploads/CreateVideoUpload/CreateVideoUpload.cs` (e input); `src/media/src/CodeForCoders.Media.Application/UseCases/VideoUploads/CompleteVideoUpload/CompleteVideoUpload.cs`; `src/media/src/CodeForCoders.Media.Api/Endpoints/VideoUploadEndpoints.cs`; `src/media/src/CodeForCoders.Media.Api/ApiModels/VideoUploadRequests.cs`; `src/bff-admin/src/CodeForCoders.BffAdmin.Api/ApiModels/VideoUploadResponses.cs`; `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/VideoUploadEndpoints.cs`; `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Clients/VideoUploadClient.cs`; `src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/VideoUploadHandler.cs`; `src/admin-spa/src/features/videos/api/create-video-upload.ts`; `src/admin-spa/src/features/videos/components/video-upload-dialog.tsx`; `src/admin-spa/src/testing/handlers.ts`; `contracts/bff-admin/openapi-midia.yaml`; `contracts/media/openapi-internal.yaml`
- **ref:** `src/media/src/CodeForCoders.Media.Application/UseCases/VideoUploads/CreateVideoUpload/CreateVideoUpload.cs:39` (hash de idempotência); `src/media/src/CodeForCoders.Media.Infra.Data/Videos/VideoUploadRepository.cs:200` (ponto de criação do vídeo na transação); `src/media/tests/CodeForCoders.Media.IntegrationTests/VideoLibraryApiFactory.cs` (dublê de `IMediaStoragePort`); skills `dotnet` e `react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/media` | `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.ArchitectureTests/CodeForCoders.Media.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/bff-admin` | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/media` | `dotnet ef migrations has-pending-model-changes --project src/media/src/CodeForCoders.Media.Infra.Data --startup-project src/media/src/CodeForCoders.Media.Api` | exit 0, sem mudança pendente | AGENTS.md |
| `src/bff-admin` | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/CodeForCoders.BffAdmin.EndToEndTests.csproj -- --filter-class CodeForCoders.BffAdmin.EndToEndTests.VideoUploadResumeTests` | exit 0 (retomada existente intacta) | `ci-dotnet.yml` Testes |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run test -- video-upload` | exit 0 (envio e retomada existentes) | `ci-react-ts.yml` |
| Contratos | `npx --yes @stoplight/spectral-cli lint contracts/bff-admin/openapi-midia.yaml contracts/media/openapi-internal.yaml --ruleset .claude/skills/tsg-flow-contract-creator/rulesets/openapi.yaml --fail-severity=error` | exit 0 | `contracts/README.md` |

## Pronto quando

- [ ] Gate focalizado passa (exit 0).
- [ ] Envio com tags → o vídeo concluído aparece em `received` já com as tags.
- [ ] Tags inválidas → 422 e nenhum multipart iniciado no armazenamento.
- [ ] Retomada com outras tags → 200 com as tags originais do envio pendente.
- [ ] Concluir duas vezes → um vídeo, mesmas tags.
- [ ] Tag criada por outro vídeo com outra grafia durante o envio → o vídeo fica com a grafia canônica.
- [ ] Em dev, um vídeo curto enviado com tags aparece rotulado em `http://localhost:8081/admin/videos`.
