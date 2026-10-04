---
status: pending
task_kind: vertical
blocked_by: ["2.0", "3.0"]
gate: 'dotnet test --project src/learning/tests/CodeForCoders.Learning.IntegrationTests/CodeForCoders.Learning.IntegrationTests.csproj -- --filter-class CodeForCoders.Learning.IntegrationTests.CourseProgressReadTests --minimum-expected-tests 12 && dotnet test --project src/bff-student/tests/CodeForCoders.BffStudent.IntegrationTests/CodeForCoders.BffStudent.IntegrationTests.csproj -- --filter-class CodeForCoders.BffStudent.IntegrationTests.CourseProgressProxyTests --minimum-expected-tests 5 && npm --prefix src/student-spa run test -- lesson-resume-progress'
gate_expect: "Pelo menos 25 testes passam: 12 de integração de learning, 5 do BFF, 8 do SPA"
---

# 4.0 Ao abrir a aula, o aluno retoma de onde parou e vê as concluídas e o percentual

**Fatia:** V-02 · **Cobre:** RF-03, RF-05 (na aula), RF-06, RN-P07, RN-P09, DP-03, DP-04, DP-05, DP-07, US de retomar a aula, US de ver concluídas e quanto falta, US de dois aparelhos · **Spec:** `techspec.md#v-02-ao-abrir-a-aula-o-aluno-retoma-de-onde-parou-e-vê-as-concluídas-e-o-percentual` · **ADR:** [ADR-0013](../../docs/adr/0013-jwt-de-aluno-validado-por-servicos-de-dominio.md)

## Comportamento

`getCourseProgress` → `getStudentCourseProgressInternal` (`api-contract.yaml`, `internal-api-contract-learning.yaml`), e a tela da aula passa a usá-lo.

- **`learning`:** política de aluno existente, aluno = `sub`. Ordem: versão vigente do curso na escola (404 `COURSE_NOT_AVAILABLE`, indistinto para inexistente, de outra escola, sem versão) → decisão de acesso pelo `AccessDecisionClient` existente (403 `ACCESS_DENIED` com `reason`/`accessEndedAt`; 503 `ACCESS_DECISION_UNAVAILABLE`; nada do curso antes da decisão positiva) → resposta calculada **na leitura** sobre as aulas da versão vigente: contagem, percentual inteiro arredondado para baixo (RN-P07), e em `lessons` só as aulas da versão vigente com registro: `completed`, `lastPositionSeconds`, `resumeAtSeconds` (RN-P09: 0 se o último avanço foi `ended`, se faltam menos de 10 s para a duração do vídeo atual, ou se a posição passa dela; sem duração conhecida, a própria posição, salvo `ended`). `Cache-Control: private, no-store`.
- **`bff-student`:** `GET /api/v1/courses/{courseId}/progress` autenticado, audiência `learning` na tabela de rotas, repasse e mapeamento dos erros do contrato; falha de Identity nesta rota → 502/504, **nunca** 503.
- **`student-spa`, tela da aula:** lê o progresso em paralelo com `getStudentLesson`. O player começa em `resumeAtSeconds` da aula aberta; a abertura espera o progresso responder ou falhar por **até 2 s** e, passado o teto, começa do início **sem saltar depois**. Falha no progresso nunca impede o vídeo. Com retomada acima de 0, aviso "Retomando de m:ss" com *Começar do início* (volta a 0:00), numa região de status, alcançável por teclado e que não some com foco. A lista marca as concluídas (texto além do ícone) e mostra o percentual com a contagem. Relê o progresso ao pausar, ao chegar ao fim e a cada 60 s de reprodução; passar dos 90% marca a aula sem recarregar. Caminho de volta para o Início. Progresso só no cache do react-query, nunca em armazenamento do navegador. Desenho conforme `docs/design/wireframes-progresso.md`.

Casos que os testes provam (`learning`): 3 de 8 → 37; 2 de 3 → 66; 10 de 10 republicado com 12 → 83; aula concluída removida → fora do numerador e do total; de volta numa versão seguinte → conta; RN-P09 nos três casos de reinício e no caso sem duração; aula sem registro ausente de `lessons`; sem concessão → 403 sem dado do curso; vencida → 403 com `accessEndedAt`; `commerce` sem resposta → 503; curso de outra escola → 404; JWT de ator → 401.

## Fora do escopo desta task

- "Meus cursos" e Continuar → 5.0 e 6.0.
- Qualquer bloqueio de aula por conclusão: não existe (RN-R06, G20).

## Decisões fechadas

- Percentual por contagem de aulas da versão vigente, arredondado para baixo (DP-03); retomada pela RN-P09 (DP-04); conclusão permanente (DP-05); removida fora do percentual (DP-07).
- Progresso exige a mesma decisão da tela da aula (C-04); leitura separada de `getStudentLesson` (C-01); escopo `lessons:read` (C-06).
- Teto de 2 s sem salto tardio (`techspec.md`, Decisões Técnicas). O SPA não recalcula RN-P09 (C-10).

## Modificar / Referenciar

Arquivos a criar não são listados: a estrutura vem das skills `dotnet` e `react`.

- **modificar:** `src/learning/src/CodeForCoders.Learning.Api/Extensions/EndpointExtensions.cs` (rota de progresso)
- **modificar:** `src/bff-student/src/CodeForCoders.BffStudent.Application/Common/BffSecurityOptions.cs` (`RouteAudiences`: `/api/v1/courses` → `learning`); `src/bff-student/src/CodeForCoders.BffStudent.Api/Security/BffSecurityMiddleware.cs` (linha 117: rotas novas → 502/504); `src/bff-student/src/CodeForCoders.BffStudent.Api/Extensions/EndpointExtensions.cs`
- **modificar:** `src/student-spa/src/features/student-lessons/components/student-lesson-screen.tsx`, `student-lesson-nav.tsx`, `hooks/use-protected-playback.ts` (linha 228, posição inicial)
- **ref:** `src/learning/src/CodeForCoders.Learning.Application/UseCases/StudentLessons/GetStudentLesson/` (ordem 404 → decisão, versão vigente); `src/learning/src/CodeForCoders.Learning.Api/Clients/AccessDecisionClient.cs`; `src/bff-student/src/CodeForCoders.BffStudent.Api/Clients/StudentLessonLearningClient.cs` (padrão de repasse); `src/student-spa/src/features/student-lessons/utils/known-course-store.ts` (não guardar progresso ali); `docs/design/wireframes-progresso.md`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/learning` | `dotnet format src/learning/CodeForCoders.Learning.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/learning` | `dotnet build src/learning/CodeForCoders.Learning.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/learning` | `dotnet test --project src/learning/tests/CodeForCoders.Learning.ArchitectureTests/CodeForCoders.Learning.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/bff-student` | `dotnet format src/bff-student/CodeForCoders.BffStudent.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-student` | `dotnet build src/bff-student/CodeForCoders.BffStudent.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/bff-student` | `dotnet test --project src/bff-student/tests/CodeForCoders.BffStudent.ArchitectureTests/CodeForCoders.BffStudent.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/student-spa` | `npm --prefix src/student-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run test --` | exit 0 (suíte inteira, sem regressão nos testes de `playback-*`) | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run build -- --base=/student/` | exit 0 | `ci-react-ts.yml` (`build-args: --base=/student/`) |

Testcontainers pesados rodam **em sequência** (AGENTS.md).

## Pronto quando

- [ ] Gate focalizado passa (exit 0).
- [ ] No navegador, com cortesia real: assistir até 4:12, fechar o navegador, voltar à aula → começa em 4:12 com o aviso; Começar do início volta a 0:00.
- [ ] Passar dos 90% marca a aula na lista em até 1 minuto, sem recarregar.
- [ ] Com o progresso fora do ar, o vídeo toca do início e a lista fica sem marcas.
- [ ] Aluno sem concessão não recebe nada do curso pela rota de progresso.
