---
status: done
task_kind: vertical
blocked_by: ["1.0", "5.0"]
gate: "dotnet test --project src/learning/tests/CodeForCoders.Learning.IntegrationTests/CodeForCoders.Learning.IntegrationTests.csproj -- --filter-class CodeForCoders.Learning.IntegrationTests.CourseVersionTests --minimum-expected-tests 8"
gate_expect: "8 testes de republicação, descarte e histórico passam; menos de 8 reprova"
---

# 6.0 Professor republica, descarta alterações e consulta versões imutáveis

**Fatia:** V-05 · **Cobre:** RF-07, RF-08, RF-12; US-04, US-06;
RN-C05/06/07/08/09/17, RN-M09; DP-01, DP-04 ·
**Spec:** [V-05](techspec.md#v-05-professor-corrige-republica-descarta-e-consulta-versões) ·
**ADR:** [ADR-0005](../../../docs/adr/0005-sessao-e-servico-do-backoffice.md)

## Comportamento

Após a primeira publicação, professor continua editando o **rascunho** por item. A lista e o
editor mostram “Publicado · v1 · alterações não publicadas” somente quando a estrutura/campos
editáveis diferem do retrato vigente; editar e voltar ao valor anterior remove o indicador
mesmo se `draftRevision` cresceu. A versão vigente permanece inalterada até uma nova
publicação. Republicar com a revisão exibida cria v2 sequencial, torna-a única vigente e
preserva v1 como retrato imutável; aula mantida conserva `lessonId`, aula removida sai da vigente.
Media substitui as referências do curso pelo conjunto da maior versão, de modo que aula removida
não continue autorizada. Fato v1 atrasado ou duplicado não reverte v2.

O histórico mostra versões em ordem decrescente, autor, momento, nota opcional e indicação da
vigente. Abrir `/admin/autoria/{courseId}/versoes/{versionNumber}` mostra título, descrição,
ordem e IDs do retrato histórico, em leitura sem controles de edição; refresh direto preserva a
versão solicitada. **Descartar alterações** pede confirmação, restaura o rascunho da vigente
preservando IDs e atualiza o indicador. Curso nunca publicado recusa descarte. Edição feita por
colega desde a revisão exibida devolve `DRAFT_CHANGED` em publicar/descartar; a UI atualiza o
curso e pede revisão/confirmação nova, sem retry automático. Duas publicações concorrentes da
mesma revisão com chaves distintas recebem números sequenciais; reuso da mesma chave/corpo não
cria versão extra.

Os testes exercitam leitura histórica em Learning, BFF com cliente HTTP real e DI real,
substituição de referências em Media e navegação SPA por URL direta. Smoke no Compose publica
duas vezes com aula removida e confere v1 imutável, v2 vigente, trilha com os dois atos e
referências só da v2.

## Fora do escopo desta task

Efeito da versão nova no progresso do aluno (CAP-017), despublicar ou excluir curso publicado.

## Decisões fechadas

- O retrato de versão é independente do rascunho; `hasUnpublishedChanges` compara conteúdo,
  não apenas `draftRevision` (TechSpec, Conteúdo e Currículo).
- Publicar/descartar conferem `draftRevision`; demais edições não conferem (C-03, OD44).
- Nota de versão não é motivo de auditoria; versão antiga nunca é alterada, mesmo com título de
  vídeo renomeado em Media. Media mantém apenas referências da maior `versionNumber` (C-11).

## Modificar / Referenciar

- **modificar:** `src/learning/src/CodeForCoders.Learning.Infra.Data/LearningDbContext.cs`,
  `src/learning/src/CodeForCoders.Learning.Api/Extensions/EndpointExtensions.cs`.
- **modificar:** `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/EndpointExtensions.cs`.
- **modificar:** `src/admin-spa/src/config/paths.ts`, `src/admin-spa/src/app/router.tsx`.
- **ref:** [PRD](prd.md), [OpenAPI público](api-contract.yaml),
  [OpenAPI interno](internal-api-contract-learning.yaml),
  [Figma aprovado em 1.0](../../../docs/design/wireframes-autoria-curso.md),
  `src/media/src/CodeForCoders.Media.Infra.Data/MediaDbContext.cs`,
  `.agents/skills/dotnet/SKILL.md`, `.agents/skills/react/SKILL.md`.

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| Learning | `dotnet restore src/learning/CodeForCoders.Learning.slnx` | exit 0 | `.github/workflows/learning.yml` → `ci-dotnet.yml@v1` |
| Learning | `dotnet format src/learning/CodeForCoders.Learning.slnx --verify-no-changes` | exit 0 | mesmo CI, lint |
| Learning | `dotnet test --project src/learning/tests/CodeForCoders.Learning.IntegrationTests/CodeForCoders.Learning.IntegrationTests.csproj -- --filter-class CodeForCoders.Learning.IntegrationTests.CourseVersionTests --minimum-expected-tests 8` | exit 0, ≥ 8 testes | mesmo CI, testes; gate focalizado |
| Learning | `dotnet publish src/learning/CodeForCoders.Learning.slnx -c Release` | exit 0 | mesmo CI, publish |
| Media | `dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.CourseReferenceVersionTests --minimum-expected-tests 4` | exit 0, ≥ 4 testes; v2 substitui v1 | `.github/workflows/media.yml` → `ci-dotnet.yml@v1`, testes |
| BFF | `dotnet restore src/bff-admin/CodeForCoders.BffAdmin.slnx` | exit 0 | `.github/workflows/bff-admin.yml` → `ci-dotnet.yml@v1` |
| BFF | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | mesmo CI, lint |
| BFF | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj -- --filter-class CodeForCoders.BffAdmin.IntegrationTests.CourseVersionProxyTests --minimum-expected-tests 4` | exit 0, ≥ 4 testes; HTTP/DI reais | mesmo CI, testes |
| BFF | `dotnet publish src/bff-admin/CodeForCoders.BffAdmin.slnx -c Release` | exit 0 | mesmo CI, publish |
| SPA | `npm --prefix src/admin-spa run lint` | exit 0 | `.github/workflows/admin-spa.yml` → `ci-react-ts.yml@v1` |
| SPA | `npm --prefix src/admin-spa run typecheck` | exit 0 | mesmo CI, Typecheck |
| SPA | `npm --prefix src/admin-spa test -- authoring-versions` | exit 0; arquivo selecionado cobre histórico, descarte e URL | mesmo CI, Vitest |
| SPA | `npm --prefix src/admin-spa run build -- --base=/admin/` | exit 0 | mesmo CI, Build |

## Pronto quando

- [ ] Gate focalizado passa (exit 0) com ao menos 8 testes de Learning.
- [ ] Edição do publicado muda só rascunho; v1 histórica segue idêntica; v2 é a única vigente e
  o histórico mostra autor, momento e nota.
- [ ] Descarte confirmado torna rascunho igual à vigente; revisão desatualizada devolve
  `DRAFT_CHANGED` e requer nova confirmação; curso nunca publicado recusa descarte.
- [ ] Media conserva só referências da v2, mesmo após fato v1 atrasado; URL histórica abre após
  refresh e não oferece edição. Cliente BFF/DI reais são exercitados.
