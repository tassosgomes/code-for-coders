---
status: pending
task_kind: vertical
blocked_by: ["4.0"]
gate: 'dotnet test --project src/learning/tests/CodeForCoders.Learning.IntegrationTests/CodeForCoders.Learning.IntegrationTests.csproj -- --filter-class CodeForCoders.Learning.IntegrationTests.CourseLevelPublicationTests --minimum-expected-tests 11 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj -- --filter-class CodeForCoders.BffAdmin.IntegrationTests.CourseVersionLevelProxyTests --minimum-expected-tests 2 && npm --prefix src/admin-spa run test -- authoring-version-level'
gate_expect: "Pelo menos 17 testes passam: 11 de integração de learning, 2 do BFF, 4 do SPA"
---

# 5.0 Publicação leva nível e pré-requisito à versão, ao histórico e ao fato 1.1.0

**Fatia:** V-03 · **Cobre:** RF-03, RF-04 (publicar sem nível), RF-05, RF-06 (fato), US-03, US-04, US-05, RN-C06, RN-C07, RN-C08, RN-C11, RN-C12, RN-C13, RN-C18, RN-O03, DP-01, C-02, C-03 · **Spec:** `techspec.md#v-03-publicação-leva-nível-e-pré-requisito-à-versão-ao-histórico-e-ao-fato` · **ADR:** —

## Comportamento

- **Publicar/republicar:** a publicação copia para a versão nova o nível, o texto do pré-requisito e os
  recomendados na ordem, cada um como `{courseId, title}` com o **título da versão vigente** do
  recomendado, lido na mesma transação — não o título do rascunho dele. `current_level` do curso passa
  a ser o nível da versão publicada. Nenhuma validação nova impede publicar (RN-C11): rascunho completo
  sem nível publica, com `level` nulo na versão. Republicar quando só o nível ou só o pré-requisito
  mudou gera a versão seguinte, o ato `versao-publicada` na trilha e o fato, com a mesma estrutura de
  aulas (RN-C07). Após publicar, `hasUnpublishedChanges` é `false`. A versão é imutável: o
  recomendado renomeado e republicado depois continua com o título antigo na versão.
- **Descarte:** restaura nível, texto e recomendados do retrato da versão vigente e realinha
  `current_level`; versões anteriores a esta entrega restauram nível nulo e pré-requisito vazio.
- **`getCourseVersionInternal`:** devolve `level` e `prerequisite` do retrato (texto e recomendados com
  o título da época); versão anterior à entrega → `level: null`, texto nulo, lista vazia.
- **Fato `conteudo.versao-publicada.v1` 1.1.0:** o payload gravado no outbox pela publicação passa a
  levar `description` (texto vazio quando nula), `level` (`null` quando ausente) e `prerequisite`
  (`text: null` e `recommendedCourses: []` quando ausentes) — sempre presentes, nulos serializados
  explicitamente. `eventId` continua sendo o `Id` da versão; ato de publicação sem mudança. O teste
  captura o payload do outbox e o valida contra `VersaoPublicadaPayload` 1.1.0 do AsyncAPI (com nível,
  sem nível e sem pré-requisito, dois recomendados na ordem).
- **Migration EF** (gerada por `dotnet ef migrations add`): cria `level` e `prerequisite` (jsonb com
  texto e `{courseId, title}`) em `content.course_versions`; versões existentes ficam nulas.
- **`bff-admin`:** `getCourseVersion` repassa `level` e `prerequisite`; o tipo `CourseVersion` deixa de
  descartá-los. Cliente HTTP real de `learning` contra a fronteira controlada do teste.
- **`admin-spa`**, conforme o Figma aprovado em 2.0: a página da versão
  (`/admin/autoria/{courseId}/versoes/{versionNumber}`) mostra o nível e o pré-requisito da versão, com
  os recomendados pelo título da época; versão anterior à entrega mostra "Sem nível" e "Sem
  pré-requisito". Depois de publicar com nível, o editor deixa de mostrar o aviso de sem nível e a
  lista deixa de mostrar "Sem nível". O schema `.strict()` da versão aceita os campos novos: teste
  parseia o exemplo de `getCourseVersion` do OpenAPI 1.1.0.
- **Media sem mudança:** no Compose, a Media consome o fato 1.1.0 e registra as Referências de Uso como
  antes, sem erro nem mensagem em DLQ.

## Fora do escopo desta task

Reenvio da versão vigente e `message_id` no outbox (6.0). Histórico em lista (`listCourseVersions`
não mudou no contrato). Consumidor do Catálogo em `commerce` (`CAP-003`).

## Decisões fechadas

- Título do recomendado no retrato = título da versão vigente dele (C-02, `techspec.md#decisões-técnicas`).
- Fato 1.1.0 no mesmo canal `v1`, com `description`, `level` e `prerequisite` sempre presentes (C-03,
  `contracts.md#evolução-e-compatibilidade`); `eventId` = `Id` da versão.
- Sem revalidação de recomendados na publicação: curso publicado não é excluído nem despublicado (OD45).
- Descrição e texto do pré-requisito vão ao outbox e ao broker, nunca a log, span, métrica ou erro.

## Modificar / Referenciar

- **modificar:** `src/learning/src/CodeForCoders.Learning.Domain/Entities/Course.cs` (`Publish`, `DiscardDraft`, `current_level`), `CourseVersion.cs`, `CourseContentFingerprint.cs` (impressão a partir da versão)
- **modificar:** `src/learning/src/CodeForCoders.Learning.Application/UseCases/Courses/PublishCourse/PublishCourse.cs` (títulos dos recomendados; payload 1.1.0), `UseCases/Courses/Common/CourseVersionOutput.cs`
- **modificar:** `src/learning/src/CodeForCoders.Learning.Infra.Data/Configurations/CourseVersionConfiguration.cs`, `CourseConfiguration.cs`; migration via `dotnet ef migrations add`
- **modificar:** `src/bff-admin/src/CodeForCoders.BffAdmin.Application/Interfaces/CourseVersion.cs`
- **modificar:** `src/admin-spa/src/features/course-authoring/types/course-version.ts`, `src/admin-spa/src/app/routes/authoring-version-route.tsx`, `src/admin-spa/src/testing/authoring-version-handlers.ts`
- **ref:** `asyncapi-contract.yaml` (`VersaoPublicadaPayload` 1.1.0, exemplos `versao4` e `reenvioSemNivel`); `internal-api-contract-learning.yaml` e `api-contract.yaml` (`getCourseVersion`); `src/media/src/CodeForCoders.Media.Infra.Messaging/PublishedCourseFact.cs` (campos lidos pela Media); `src/learning/tests/CodeForCoders.Learning.IntegrationTests/CoursePublicationTests.cs` (captura do outbox); `docs/design/wireframes-autoria-curso.md` (adendo) e Figma aprovado; skills `dotnet` e `react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/learning` | `dotnet format src/learning/CodeForCoders.Learning.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/learning` | `dotnet build src/learning/CodeForCoders.Learning.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/learning` | `dotnet test --project src/learning/tests/CodeForCoders.Learning.IntegrationTests/CodeForCoders.Learning.IntegrationTests.csproj` | exit 0 (sem regressão em publicação, versões e descarte) | `ci-dotnet.yml` Testes |
| `src/learning` | `dotnet test --project src/learning/tests/CodeForCoders.Learning.UnitTests/CodeForCoders.Learning.UnitTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
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

- [ ] Gate passa (exit 0) com pelo menos 17 testes.
- [ ] Payload capturado do outbox conforme `VersaoPublicadaPayload` 1.1.0 nos três casos, com `level: null` e `prerequisite.text: null` presentes no JSON quando ausentes.
- [ ] Republicar só o nível gera versão N+1, ato `versao-publicada` e fato; estrutura de aulas igual.
- [ ] Recomendado renomeado e republicado depois: a versão e a página da versão mostram o título antigo; o rascunho mostra o título atual do rascunho dele.
- [ ] Descartar restaura nível e pré-requisito da versão vigente; versão anterior à entrega restaura nulo/vazio.
- [ ] Smoke no Compose: publicar com nível e dois recomendados, abrir a página da versão, conferir que a Media consumiu o fato sem erro e sem mensagem em DLQ, e que o aviso de sem nível sumiu do editor e da lista.
