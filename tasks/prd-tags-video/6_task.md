---
status: done
task_kind: vertical
blocked_by: ["4.0"]
gate: 'dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.VideoTagFilterTests --minimum-expected-tests 8 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/CodeForCoders.BffAdmin.EndToEndTests.csproj -- --filter-class CodeForCoders.BffAdmin.EndToEndTests.VideoTagFilterTests --minimum-expected-tests 3 && npm --prefix src/admin-spa run test -- video-library-tag-filter'
gate_expect: "Pelo menos 11 testes .NET passam (8 de integração de media, 3 E2E do bff-admin) e os testes do admin-spa com o fragmento video-library-tag-filter passam"
---

# 6.0 O professor filtra a tela Vídeos por tags, com E ou OU

**Fatia:** V-04 · **Cobre:** RF-04, RF-06; RN-T05, RN-T06; US-05, US-06; C-07 · **Spec:** `techspec.md` § V-04, § Bloco Backend — `media` (Filtro em SQL) · **ADR:** 0005

## Comportamento

**Ponta a ponta:** a tela Vídeos ganha o filtro por tags (componente de 4.0, modo só sugestão)
junto dos filtros de estado e título.

- Com duas ou mais tags aparece a escolha **E / OU**, com **E** como padrão. Com uma tag ou nenhuma,
  a escolha não aparece.
- O filtro fica na URL (`tag` repetido e `tagMatch`), junto de `status`, `q` e `page`: um
  link como `/admin/videos?tag=docker&tag=m%C3%B3dulo%202&tagMatch=any` abre a lista filtrada e
  sobrevive ao recarregamento.
- Mudar o filtro volta para a página 1.
- A atualização automática enquanto há vídeo em preparação mantém o filtro.
- Lista vazia com filtro de tag → mensagem de que nenhum vídeo atende aos filtros, com a ação de
  limpar.

**`media` — `listVideosInternal` com `tag` (até 10) e `tagMatch`:**

- `all`: o vídeo tem todas as tags do filtro; `any`: tem pelo menos uma.
- Combina com `status` e `q` (todos ao mesmo tempo), com a ordenação e a paginação atuais e o
  total correto.
- Valor que não é tag da escola não casa: com `all` → `data: []`; com `any` ele só não
  contribui.
- Mais de 10 `tag`, valor acima de 100 caracteres ou `tagMatch` fora de `all`/`any` → 400
  `INVALID_REQUEST`.
- **Telemetria:** `media.videos.listed` com `tag_filter` = `none` | `all` | `any` e
  `ready_only`.

**`bff-admin`:** `listVideos` repassa `tag` e `tagMatch`, com os mesmos limites de proteção.

**Contratos:** acrescentar os parâmetros `Tag` e `TagMatch` e o schema `TagMatch` a
`listVideos`/`*Internal` nos dois arquivos de `contracts/`.

## Fora do escopo desta task

- O filtro no seletor da aula → 7.0 (usa a mesma listagem com `status=ready`).

## Decisões fechadas

- `tagMatch` `all` (padrão) ou `any`; tag desconhecida não dá erro (C-07).
- Na tela Vídeos o filtro fica na URL (`techspec.md` § Decisões Técnicas).
- Tag, estado e título combinam sempre como "todos ao mesmo tempo" (PRD RF-06).

## Modificar / Referenciar

- **modificar:** `src/media/src/CodeForCoders.Media.Infra.Data/Videos/VideoQueries.cs`; `src/media/src/CodeForCoders.Media.Application/UseCases/Videos/ListVideos/ListVideos.cs` (e input); `src/media/src/CodeForCoders.Media.Application/Common/MediaTelemetry.cs`; `src/media/src/CodeForCoders.Media.Api/Endpoints/VideoEndpoints.cs`; `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/VideoLibraryEndpoints.cs`; `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Clients/IVideoLibraryClient.cs`; `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Clients/VideoLibraryClient.cs`; `src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/VideoLibraryHandler.cs`; `src/admin-spa/src/app/routes/videos-area-route.tsx`; `src/admin-spa/src/features/videos/api/get-videos.ts`; `src/admin-spa/src/features/videos/components/videos-area-screen.tsx`; `src/admin-spa/src/testing/handlers.ts`; `contracts/bff-admin/openapi-midia.yaml`; `contracts/media/openapi-internal.yaml`
- **ref:** `src/media/src/CodeForCoders.Media.Infra.Data/Videos/VideoQueries.cs:9` (filtros e paginação atuais); `src/admin-spa/src/app/routes/videos-area-route.tsx:59` (estado na URL); `src/admin-spa/src/app/routes/video-library-filter.test.tsx` (padrão dos testes de filtro); skills `dotnet` e `react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/media` | `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.ArchitectureTests/CodeForCoders.Media.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/bff-admin` | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.VideoTitleSearchTests` | exit 0 (busca por título intacta) | `ci-dotnet.yml` Testes |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run test -- video-library-filter videos-area-route` | exit 0 (filtros existentes intactos) | `ci-react-ts.yml` |
| Contratos | `npx --yes @stoplight/spectral-cli lint contracts/bff-admin/openapi-midia.yaml contracts/media/openapi-internal.yaml --ruleset contracts/.spectral-exception.yaml --fail-severity=error` | exit 0 | `contracts/README.md` |

## Pronto quando

- [ ] Gate focalizado passa (exit 0).
- [ ] Com A(`docker`, `módulo 2`), B(`docker`) e C(`módulo 2`): `all` → A; `any` → A, B e C.
- [ ] Tag desconhecida com `all` → lista vazia; com `any` junto de "docker" → A e B.
- [ ] Combinação com `status=failed` e `q` traz só quem atende aos três; a paginação tem o total certo.
- [ ] A escolha E/OU só aparece com duas tags ou mais; remover até sobrar uma a esconde.
- [ ] O link com `tag` e `tagMatch` abre a lista filtrada depois de recarregar.
