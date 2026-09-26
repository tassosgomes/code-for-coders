---
status: pending
task_kind: vertical
blocked_by: ["4.0"]
gate: 'dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.VideoTitleSearchTests --minimum-expected-tests 6 && dotnet test --project src/media/tests/CodeForCoders.Media.UnitTests/CodeForCoders.Media.UnitTests.csproj -- --filter-class CodeForCoders.Media.UnitTests.VideoTitleNormalizationTests --minimum-expected-tests 3 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/CodeForCoders.BffAdmin.EndToEndTests.csproj -- --filter-class CodeForCoders.BffAdmin.EndToEndTests.VideoTitleTests --minimum-expected-tests 2 && npm --prefix src/admin-spa run test -- video-library-filter'
gate_expect: 'Pelo menos 14 testes passam: 6 integração e 3 unidade de Media, 2 BFF, 3 SPA'
---

# 9.0 Professor corrige o título e encontra vídeos por estado e por nome

**Fatia:** V-06 · **Cobre:** RF-06 (filtro e busca), RF-07, RN-M04, DP-03, US-04 · **Spec:** `techspec.md` § V-06 · **ADR:** —

## Comportamento

- **Editar título** (`updateVideoTitle` → `updateVideoTitleInternal`): qualquer ator com `midia.enviar`
  da escola renomeia qualquer vídeo da escola, inclusive de colega (DP-03); só título e título
  normalizado mudam — estado, autor e data não. Título só com espaços → 422 `TITLE_REQUIRED`. Vídeo de
  outro tenant → 404 `VIDEO_NOT_FOUND`. Repetir com a mesma `Idempotency-Key` e o mesmo corpo devolve o
  mesmo resultado; mesma chave com outro corpo → 422 `IDEMPOTENCY_KEY_REUSED`. Media fora → BFF 502.
- **Filtrar e buscar** (`listVideos`): `status` filtra por estado (repetível, conforme o contrato);
  `q` compara com o título normalizado (minúsculas, sem acento), então "injecao" encontra "Injeção de
  dependência"; sempre dentro do tenant do token; ordem por envio decrescente.
- **SPA:** filtro por estado, busca por título e edição de título no desenho aprovado; o título novo
  aparece na lista; erro de título em branco exibido no campo.

## Fora do escopo desta task

Busca por autor ou por conteúdo. Excluir ou arquivar vídeo (fora do PRD).

## Decisões fechadas

- Busca por título normalizado gravado pela aplicação, sem extensão `unaccent`: D-05.
- Todo ator com `midia.enviar` vê e titula todos os vídeos da escola: DP-03.
- Migrations pelo tooling do EF, se o título normalizado ainda não existir na tabela.

## Modificar / Referenciar

- **modificar:** `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/EndpointExtensions.cs`; `src/admin-spa/src/testing/handlers.ts`
- **ref:** `internal-api-contract.yaml` 1.0.1 e `api-contract.yaml` 1.1.1 (`listVideos` parâmetros, `updateVideoTitle`); Figma aprovado; skills `dotnet` e `react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/media` | `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/media` | `dotnet build src/media/CodeForCoders.Media.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.ArchitectureTests/CodeForCoders.Media.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/bff-admin` | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-admin` | `dotnet build src/bff-admin/CodeForCoders.BffAdmin.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run build` | exit 0 | `ci-react-ts.yml` |

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 14 testes.
- [ ] "injecao" encontra "Injeção de dependência"; filtro `failed` devolve só os falhados; nada cruza tenant.
- [ ] Renomear vídeo de colega mantém o estado; título em branco → `TITLE_REQUIRED`.
- [ ] Smoke no Compose: renomear um vídeo e buscá-lo pelo nome sem acento.
