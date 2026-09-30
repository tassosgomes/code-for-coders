---
status: done
task_kind: vertical
blocked_by: ["2.0"]
gate: 'dotnet test --project src/learning/tests/CodeForCoders.Learning.UnitTests/CodeForCoders.Learning.UnitTests.csproj -- --filter-class CodeForCoders.Learning.UnitTests.CourseLevelRuleTests --minimum-expected-tests 4 && dotnet test --project src/learning/tests/CodeForCoders.Learning.IntegrationTests/CodeForCoders.Learning.IntegrationTests.csproj -- --filter-class CodeForCoders.Learning.IntegrationTests.CourseLevelTests --minimum-expected-tests 9 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj -- --filter-class CodeForCoders.BffAdmin.IntegrationTests.CourseLevelProxyTests --minimum-expected-tests 4 && npm --prefix src/admin-spa run test -- authoring-course-level'
gate_expect: "Pelo menos 24 testes passam: 4 unitários e 9 de integração de learning, 4 do BFF, 7 do SPA"
---

# 3.0 Professor declara o nível do curso e vê o aviso de curso sem nível

**Fatia:** V-01 · **Cobre:** RF-01, RF-04 (editor, janela de publicação, lista), US-01, US-03, RN-C03, RN-C05, RN-C10, RN-C18, RN-O03, RN-12, RN-18, DP-01, C-05 · **Spec:** `techspec.md#v-01-professor-declara-o-nível-e-vê-o-aviso-de-curso-sem-nível` · **ADR:** [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md)

## Comportamento

- **`learning` — `updateCourseInternal`:** o `PATCH` do curso aceita `level` com `beginner`,
  `intermediate`, `advanced` ou `null`. Campo ausente não muda o nível; `null` limpa; valor fora da
  lista → 422 `FIELD_INVALID` com a indicação do campo, nada muda. A gravação usa o caminho existente
  do rascunho (bloqueio do curso, recibo de idempotência, registro da edição): repetir a mesma chave de
  idempotência devolve a mesma resposta sem nova revisão. Ator sem `autoria.editar` → 403, nada muda;
  curso de outro tenant → 404 como inexistente.
- **Impressão de conteúdo e revisão (C-05):** o nível entra na impressão de conteúdo, calculada a
  partir do curso e da versão. Mudar só o nível de um curso publicado incrementa `draftRevision`, torna
  `hasUnpublishedChanges: true` e não toca a versão vigente. Voltar o nível ao valor da versão vigente
  torna `hasUnpublishedChanges: false` de novo — inclusive num curso publicado **antes** da migration,
  cuja impressão gravada foi zerada e é recalculada pelo caminho preguiçoso existente.
- **Descarte:** descartar alterações devolve o nível ao da versão vigente (nulo, porque nenhuma versão
  carrega nível até 5.0) e deixa `hasUnpublishedChanges: false`.
- **Leitura:** `getCourseInternal` devolve `level` e `currentLevel`; `listCoursesInternal` devolve
  `currentLevel` por item. `currentLevel` vem de `current_level` em `courses` (nulo para todos até 5.0).
- **Migration EF** (gerada por `dotnet ef migrations add`): cria `level` e `current_level` em
  `content.courses` e zera `published_fingerprint`; `has_unpublished_changes` não é tocado.
- **`bff-admin`:** `getCourse`, `listCourses` e `updateCourse` repassam `level` e `currentLevel`; os
  tipos que hoje descartam campo desconhecido passam a carregá-los. O cliente HTTP real de `learning` é
  exercitado contra a fronteira controlada do teste; 422 e 403 de `learning` chegam ao SPA com o mesmo
  `code`.
- **`admin-spa`**, conforme o Figma aprovado em 2.0:
  - seção **Para quem é este curso** no editor com o nível como grupo de opções rotulado (três níveis
    e "Sem nível"); a escolha grava por `updateCourse` com a chave de idempotência da intenção e a
    resposta integral substitui o curso exibido; `FIELD_INVALID` vira mensagem no campo;
  - quem tem `autoria.ler` sem `autoria.editar` vê o nível sem controle de alteração;
  - aviso permanente, em texto e anunciado a leitor de tela, quando `currentLevel` é nulo, com atalho
    que leva o foco ao campo de nível; quando o nível existe só no rascunho, o aviso diz que ele só
    vale depois de publicar;
  - a janela de publicação repete o aviso e mantém o botão de publicar habilitado;
  - a lista mostra "Sem nível" em texto para curso publicado com `currentLevel` nulo;
  - a ajuda contextual passa a dizer que preço e vigência são da oferta, e nível e pré-requisito são
    do professor; nenhum texto sugere que o nível impede compra ou acesso;
  - os schemas `.strict()` aceitam os campos novos: teste parseia os exemplos de `getCourse` e
    `listCourses` do OpenAPI 1.1.0.

## Fora do escopo desta task

Pré-requisito, seletor de recomendados e busca por título (4.0). Levar o nível à versão, manter
`current_level` na publicação e no descarte, página da versão e fato 1.1.0 (5.0): até lá, publicar
não leva o nível à versão — estado intermediário aceito no plano, sem implantação intermediária.

## Decisões fechadas

- Nível opcional para publicar (DP-01); nenhuma validação nova na publicação.
- Nível entra em `draftRevision`/`hasUnpublishedChanges` (C-05).
- Migration zera `published_fingerprint` e confia no recálculo preguiçoso existente
  (`techspec.md#bloco-backend`, "Impressão de conteúdo e revisão").
- `current_level` guardado em `courses`, não lido do JSON das versões (`techspec.md#decisões-técnicas`).
- Autorização em `learning` a partir das claims do JWT de ator (ADR-0005); `bff-admin` e `admin-spa` implantados juntos.

## Modificar / Referenciar

- **modificar:** `src/learning/src/CodeForCoders.Learning.Domain/Entities/Course.cs`, `CourseChanges.cs`, `CourseContentFingerprint.cs`
- **modificar:** `src/learning/src/CodeForCoders.Learning.Api/Endpoints/CourseChangesRequest.cs` (só a lista permitida do curso)
- **modificar:** `src/learning/src/CodeForCoders.Learning.Application/UseCases/Courses/Common/CourseDetailOutput.cs`, `src/learning/src/CodeForCoders.Learning.Application/Interfaces/CourseSummary.cs`, `src/learning/src/CodeForCoders.Learning.Infra.Data/Queries/CourseQueries.cs` (projeção de `currentLevel`), `src/learning/src/CodeForCoders.Learning.Infra.Data/Configurations/CourseConfiguration.cs`; migration via `dotnet ef migrations add`
- **modificar:** `src/bff-admin/src/CodeForCoders.BffAdmin.Application/Interfaces/CourseDetail.cs`, `CourseSummary.cs`
- **modificar:** `src/admin-spa/src/features/course-authoring/types/course.ts`, `api/update-course.ts`, `components/course-curriculum.tsx`, `components/course-publication.tsx`, `src/admin-spa/src/app/routes/authoring-courses-route.tsx`, `src/admin-spa/src/testing/authoring-course-handlers.ts`
- **ref:** `src/learning/src/CodeForCoders.Learning.Application/UseCases/Courses/Common/CourseEditSession.cs` (bloqueio, idempotência, recálculo preguiçoso); `internal-api-contract-learning.yaml` e `api-contract.yaml` (`getCourse`, `listCourses`, `updateCourse`); `docs/design/wireframes-autoria-curso.md` (adendo) e Figma aprovado; `src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CourseLearningHandler.cs`; skills `dotnet` e `react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/learning` | `dotnet format src/learning/CodeForCoders.Learning.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/learning` | `dotnet build src/learning/CodeForCoders.Learning.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/learning` | `dotnet test --project src/learning/tests/CodeForCoders.Learning.IntegrationTests/CodeForCoders.Learning.IntegrationTests.csproj` | exit 0 (sem regressão em edição, publicação, versões) | `ci-dotnet.yml` Testes |
| `src/learning` | `dotnet test --project src/learning/tests/CodeForCoders.Learning.ArchitectureTests/CodeForCoders.Learning.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/bff-admin` | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-admin` | `dotnet build src/bff-admin/CodeForCoders.BffAdmin.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/bff-admin` | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run test -- authoring` | exit 0 (sem regressão nas telas de Autoria) | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run build -- --base=/admin/` | exit 0 | `ci-react-ts.yml` |

Cobertura agregada ≥ 70%, `dotnet publish` e imagens ficam para a validação full.

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 24 testes.
- [ ] Curso publicado: mudar só o nível → `hasUnpublishedChanges: true`, `draftRevision` cresce, versão vigente intacta; voltar ao valor da versão → `false`, inclusive em curso publicado antes da migration.
- [ ] `updateCourseInternal` direto: `level: "expert"` → 422 `FIELD_INVALID`; JWT sem `autoria.editar` → 403; curso de outro tenant → 404; nada muda em nenhum dos três.
- [ ] SPA: leitor vê o nível sem controle; aviso com atalho para o campo; janela de publicação com aviso e botão habilitado; "Sem nível" em texto na lista.
- [ ] Smoke no Compose: em `http://localhost:8081/admin/autoria/{courseId}` o professor do seed declara nível num curso publicado, vê "alterações não publicadas" e o aviso "só vale depois de publicar"; a lista mostra "Sem nível".
