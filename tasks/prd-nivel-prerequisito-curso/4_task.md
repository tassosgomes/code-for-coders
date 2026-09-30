---
status: done
task_kind: vertical
blocked_by: ["3.0"]
gate: 'dotnet test --project src/learning/tests/CodeForCoders.Learning.UnitTests/CodeForCoders.Learning.UnitTests.csproj -- --filter-class CodeForCoders.Learning.UnitTests.CoursePrerequisiteRuleTests --minimum-expected-tests 5 && dotnet test --project src/learning/tests/CodeForCoders.Learning.IntegrationTests/CodeForCoders.Learning.IntegrationTests.csproj -- --filter-class CodeForCoders.Learning.IntegrationTests.CoursePrerequisiteTests --minimum-expected-tests 13 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj -- --filter-class CodeForCoders.BffAdmin.IntegrationTests.CoursePrerequisiteProxyTests --minimum-expected-tests 4 && npm --prefix src/admin-spa run test -- authoring-prerequisite'
gate_expect: "Pelo menos 28 testes passam: 5 unitários e 13 de integração de learning, 4 do BFF, 6 do SPA"
---

# 4.0 Professor recomenda o pré-requisito em texto e cursos publicados da escola

**Fatia:** V-02 · **Cobre:** RF-02, US-02, RN-C03, RN-C05, RN-C17, RN-C18, RN-O05, RN-18, DP-02, C-04, C-05 · **Spec:** `techspec.md#v-02-professor-recomenda-pré-requisito-em-texto-e-cursos-da-escola` · **ADR:** [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md)

## Comportamento

- **`learning` — `updateCourseInternal`:** o `PATCH` do curso aceita `prerequisiteText` (nulo ou 1–1 000
  caracteres) e `recommendedCourseIds` (lista de 0–5 ids, sem repetição, na ordem enviada), com a mesma
  semântica de 3.0: ausente não muda, `null` limpa, lista inteira substitui. Texto acima de 1 000 →
  422 `FIELD_INVALID` com o campo em `detail`; mais de 5 ids, id repetido ou id que não é UUID violam o
  schema (`maxItems`/`uniqueItems`) → 400 `INVALID_REQUEST`, como as demais violações de schema do
  parser; nada muda.
- **Validação dos recomendados:** na mesma transação e sob o filtro de tenant, cada id precisa existir,
  ter versão vigente e ser diferente do próprio curso; senão 422 `RECOMMENDED_COURSE_INVALID` e nada
  muda. Curso de outra escola responde exatamente como inexistente. A recusa de um id invalida a
  requisição inteira (nível e texto enviados junto também não são gravados). A recomendação mútua (A→B,
  B→A) é aceita. Apagar o texto e esvaziar a lista deixa o rascunho sem pré-requisito, válido.
- **Impressão de conteúdo:** texto e lista ordenada de ids entram na impressão; mudar só a ordem dos
  recomendados de um curso publicado torna `hasUnpublishedChanges: true` e incrementa `draftRevision`.
  Descartar alterações devolve texto e lista ao estado da versão vigente (vazios até 5.0).
- **Leitura:** `getCourseInternal` devolve `prerequisite` com o texto e os recomendados na ordem, cada um
  com o **título atual do rascunho** do recomendado.
- **Busca por título — `listCoursesInternal`:** aceita `title` (2–100) combinado com `status`; compara
  sem diferenciar maiúsculas nem acentos contra `title_search`, mantida pelo domínio na criação e na
  alteração do título; o termo é escapado para `LIKE` (`%` e `_` literais). "fundamentos" e
  "fundaméntos" encontram "Fundamentos de C#"; curso de outro tenant nunca aparece.
- **Migration EF** (gerada por `dotnet ef migrations add`): cria `prerequisite_text`,
  `recommended_course_ids` (jsonb ordenado) e `title_search` em `content.courses`; preenche
  `title_search` dos cursos existentes com `lower` + `translate` dos acentos do português, sem a
  extensão `unaccent`; zera `published_fingerprint` de novo, porque a fórmula da impressão muda outra vez.
- **`bff-admin`:** `listCourses` valida `title` (2–100) — fora do limite → 400 sem chamar `learning` —
  e o repassa codificado; `getCourse` e `updateCourse` repassam `prerequisite`/`prerequisiteText`/
  `recommendedCourseIds`; 422 `RECOMMENDED_COURSE_INVALID` chega ao SPA com o mesmo `code`. O cliente
  HTTP real de `learning` é exercitado contra a fronteira controlada do teste, incluindo termo com
  acento, espaço e `&`.
- **`admin-spa`**, conforme o Figma aprovado em 2.0: na seção **Para quem é este curso**, texto do
  pré-requisito com contador e limite de 1 000; cursos recomendados em lista ordenada com seletor que
  chama `listCourses` com `status=published` e `title`, exclui o próprio curso e os já escolhidos,
  deixa de oferecer "adicionar" com 5 escolhidos (motivo em texto), reordena por teclado sem arrastar e
  remove; grava por `updateCourse` com a chave de idempotência da intenção; `422
  RECOMMENDED_COURSE_INVALID`/`FIELD_INVALID` viram mensagem no campo. Leitor sem `autoria.editar` vê
  texto e recomendados sem controles.

## Fora do escopo desta task

Levar pré-requisito à versão com o título da época, página da versão, fato 1.1.0 (5.0). Links para os
recomendados na vitrine (`CAP-003`). Revalidar recomendados na publicação (não há: OD45).

## Decisões fechadas

- Pré-requisito é texto e/ou até 5 cursos publicados da escola, nunca o próprio (DP-02); recomendação
  não é dependência e não restringe compra (DE04, RN-O05).
- Filtro `title` em `listCourses`, sem operação nova de busca (C-04).
- Busca sem acento por coluna `title_search`, sem `unaccent` (`techspec.md#decisões-técnicas`).
- Texto do pré-requisito é texto do autor: nunca em log, span, métrica ou mensagem de erro.

## Modificar / Referenciar

- **modificar:** `src/learning/src/CodeForCoders.Learning.Domain/Entities/Course.cs`, `CourseChanges.cs`, `CourseContentFingerprint.cs`
- **modificar:** `src/learning/src/CodeForCoders.Learning.Api/Endpoints/CourseChangesRequest.cs`, `CourseEndpoints.cs` (parâmetro `title`)
- **modificar:** `src/learning/src/CodeForCoders.Learning.Application/UseCases/Courses/UpdateCourse/UpdateCourse.cs` (validação dos recomendados), `UseCases/Courses/Common/CourseDetailOutput.cs`, `Interfaces/CourseListQuery.cs`
- **modificar:** `src/learning/src/CodeForCoders.Learning.Infra.Data/Queries/CourseQueries.cs`, `Configurations/CourseConfiguration.cs`; migration via `dotnet ef migrations add`
- **modificar:** `src/bff-admin/src/CodeForCoders.BffAdmin.Application/Interfaces/CourseDetail.cs`, `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/CourseAuthoringEndpoints.cs`
- **modificar:** `src/admin-spa/src/features/course-authoring/types/course.ts`, `api/update-course.ts`, `api/get-courses.ts`, `components/course-curriculum.tsx`, `src/admin-spa/src/testing/authoring-course-handlers.ts`
- **ref:** `internal-api-contract-learning.yaml` e `api-contract.yaml` (`listCourses.title`, `prerequisite`, `RECOMMENDED_COURSE_INVALID`); `src/learning/src/CodeForCoders.Learning.Application/UseCases/Courses/Common/CourseEditSession.cs`; `docs/design/wireframes-autoria-curso.md` (adendo) e Figma aprovado; skills `dotnet` e `react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/learning` | `dotnet format src/learning/CodeForCoders.Learning.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/learning` | `dotnet build src/learning/CodeForCoders.Learning.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/learning` | `dotnet test --project src/learning/tests/CodeForCoders.Learning.IntegrationTests/CodeForCoders.Learning.IntegrationTests.csproj` | exit 0 (sem regressão, inclusive `CourseLevelTests`) | `ci-dotnet.yml` Testes |
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

- [ ] Gate passa (exit 0) com pelo menos 28 testes.
- [ ] Recomendado de outro tenant, nunca publicado, inexistente ou o próprio curso → 422 `RECOMMENDED_COURSE_INVALID` e o rascunho fica igual; resposta para outro tenant indistinguível da de inexistente.
- [ ] 6 ids e id repetido → 400 `INVALID_REQUEST`; texto com 1 001 caracteres → 422 `FIELD_INVALID`; A→B e B→A aceitos; texto e lista vazios aceitos.
- [ ] "fundaméntos" e "FUNDAMENTOS" encontram "Fundamentos de C#"; `%` no termo não casa tudo; `title` com 1 caractere → 400 no BFF sem chamada a `learning`.
- [ ] SPA: seletor sem o próprio curso e sem nunca publicados; sexto recomendado não oferecido; ordem alterável por teclado; erro legível no campo.
- [ ] Smoke no Compose: o professor do seed recomenda "Fundamentos de C#" buscando "fundamentos" e reordena os recomendados; o curso publicado mostra "alterações não publicadas".
