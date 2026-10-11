---
status: pending
task_kind: vertical
blocked_by: ["3.0"]
gate: 'dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.VideoTagSuggestionTests --minimum-expected-tests 6 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/CodeForCoders.BffAdmin.EndToEndTests.csproj -- --filter-class CodeForCoders.BffAdmin.EndToEndTests.VideoTagSuggestionTests --minimum-expected-tests 3 && npm --prefix src/admin-spa run test -- video-tag-suggestions'
gate_expect: "Pelo menos 9 testes .NET passam (6 de integração de media, 3 E2E do bff-admin) e os testes do admin-spa com o fragmento video-tag-suggestions passam"
---

# 4.0 Ao digitar uma tag, o professor vê as tags que a escola já usa

**Fatia:** V-02 · **Cobre:** RF-03; RN-T02, RN-T05, RN-T06; US-03; C-04 · **Spec:** `techspec.md` § V-02, § Bloco Frontend · **ADR:** 0005

## Comportamento

**Ponta a ponta:** no diálogo de edição de tags de 3.0, digitar abre a lista de sugestões com as
tags da escola que contêm o trecho, sem diferenciar maiúsculas nem acentos. Escolher uma sugestão
cria a etiqueta na grafia canônica. Texto sem sugestão correspondente ainda pode ser confirmado
(Enter ou vírgula) e vira tag nova ao salvar.

O campo é o **componente de tags** desenhado em 2.0, feito para ser reaproveitado no envio (5.0) e
nos filtros (6.0, 7.0). Ele tem um modo de **texto livre** (envio e edição) e um modo **só
sugestão** (filtros). Operável só por teclado: setas, Enter ou vírgula, Esc, e Backspace para
remover a última etiqueta. A lista é anunciada a leitor de tela.

**`media` — `listVideoTagsInternal` (`GET /internal/v1/video-tags?q=`):**

- só tags do catálogo da escola com **pelo menos um vídeo** vinculado (RN-T05), em qualquer estado
  do vídeo;
- a forma normalizada contém a de `q`;
- ordem: as que **começam** por `q`, depois as demais, cada grupo em ordem alfabética;
- no máximo 20; sem `q`, as 20 primeiras em ordem alfabética;
- `q` vazio ou com mais de 100 caracteres → 400 `INVALID_REQUEST`;
- `%` e `_` em `q` são texto, nunca curinga;
- tag de outra escola nunca aparece (RN-T06).

**`bff-admin` — `GET /api/v1/video-tags` (`listVideoTags`):** mesma checagem de sessão e
`midia.enviar`; repassa `q`.

**Contratos:** acrescentar `listVideoTags`/`*Internal`, o parâmetro `TagQuery` e o schema
`VideoTagList` aos dois arquivos de `contracts/`.

## Fora do escopo desta task

- O campo no envio → 5.0. O campo nos filtros → 6.0 e 7.0. Aqui só o diálogo de edição usa as sugestões.

## Decisões fechadas

- Teto fixo de 20, sem paginação; as tags que começam pelo trecho vêm primeiro (C-04).
- Nos filtros, só se escolhe tag existente (PRD RF-03).
- Nenhuma dependência de UI nova (`techspec.md` § Bloco Frontend).

## Modificar / Referenciar

- **modificar:** `src/media/src/CodeForCoders.Media.Api/Endpoints/VideoEndpoints.cs`; `src/media/src/CodeForCoders.Media.Infra.Data/Videos/VideoQueries.cs` (ou a consulta de tags criada em 3.0); `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/VideoLibraryEndpoints.cs`; `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Clients/IVideoLibraryClient.cs`; `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Clients/VideoLibraryClient.cs`; `src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/VideoLibraryHandler.cs`; `src/admin-spa/src/features/videos/components/videos-area-screen.tsx` (o diálogo de 3.0 passa a usar o componente); `src/admin-spa/src/testing/handlers.ts`; `contracts/bff-admin/openapi-midia.yaml`; `contracts/media/openapi-internal.yaml`
- **ref:** `src/media/src/CodeForCoders.Media.Infra.Data/Videos/VideoQueries.cs:20` (busca normalizada que escapa o parâmetro); `docs/design/wireframes-videos.md` (adendo aprovado); skills `dotnet` e `react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/media` | `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.ArchitectureTests/CodeForCoders.Media.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/bff-admin` | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run test -- video-tags-edit` | exit 0 (edição de 3.0 intacta) | `ci-react-ts.yml` |
| Contratos | `npx --yes @stoplight/spectral-cli lint contracts/bff-admin/openapi-midia.yaml contracts/media/openapi-internal.yaml --ruleset contracts/.spectral-exception.yaml --fail-severity=error` | exit 0 | `contracts/README.md` |

## Pronto quando

- [ ] Gate focalizado passa (exit 0).
- [ ] "dock" sugere "Docker" e "Docker Compose" antes de "Moby Docker".
- [ ] Uma tag cujo último vídeo perdeu a tag deixa de ser sugerida.
- [ ] Uma tag de outra escola nunca é sugerida; nunca vêm mais de 20; "dock%" não age como curinga.
- [ ] Digitar "introducao" e escolher a sugestão cria a etiqueta "Introdução".
- [ ] O campo funciona só com teclado, e a lista é anunciada.
