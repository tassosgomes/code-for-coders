---
status: in_progress
task_kind: vertical
blocked_by: ["4.0"]
gate: 'dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.StudentCourseAccessListTests --minimum-expected-tests 8 && dotnet test --project src/learning/tests/CodeForCoders.Learning.IntegrationTests/CodeForCoders.Learning.IntegrationTests.csproj -- --filter-class CodeForCoders.Learning.IntegrationTests.StudentCourseAccessClientTests --minimum-expected-tests 4 && dotnet test --project src/learning/tests/CodeForCoders.Learning.IntegrationTests/CodeForCoders.Learning.IntegrationTests.csproj -- --filter-class CodeForCoders.Learning.IntegrationTests.StudentCoursesListingTests --minimum-expected-tests 9 && dotnet test --project src/bff-student/tests/CodeForCoders.BffStudent.IntegrationTests/CodeForCoders.BffStudent.IntegrationTests.csproj -- --filter-class CodeForCoders.BffStudent.IntegrationTests.MyCoursesProxyTests --minimum-expected-tests 4 && npm --prefix src/student-spa run test -- my-courses-active'
gate_expect: "Pelo menos 31 testes passam: 8 de integração de commerce, 4 e 9 de integração de learning, 4 do BFF, 6 do SPA"
---

# 5.0 O aluno vê os cursos vigentes em "meus cursos" e continua de onde parou

**Fatia:** V-03 · **Cobre:** RF-04 (vigentes, vazio, login), RF-05 (Continuar/Começar), RN-P08, RN-P10, DP-08, US de ver os cursos num só lugar, US do botão Continuar, US de ver quanto falta · **Spec:** `techspec.md#v-03-o-aluno-vê-os-cursos-vigentes-em-meus-cursos-e-continua-de-onde-parou` · **ADR:** [ADR-0015](../../docs/adr/0015-escopos-de-servico-media-learning-em-commerce-por-rota.md)

## Comportamento

`listMyCourses` → `listStudentCoursesInternal` → `listStudentCourseAccessInternal` (`api-contract.yaml`, `internal-api-contract-learning.yaml`, `internal-api-contract-commerce.yaml`).

- **`commerce`:** `GET /internal/v1/course-access?studentId=` com asserção de serviço e escopo **`course-access:read`** (ADR-0015); `access-decision:read` não basta. Um item por curso, na escola da asserção: `active` pela **mesma** regra de `decideAccessInternal` (concessão com status ativo e `expires_at` nulo ou futuro), com `since` = concessão ativa mais recente; senão `ended`, com `endedOn` = maior `ends_on`, `endedAt` = maior `expires_at` das terminadas e `endedReason` = `grant-ended`. Nunca motivo, origem nem `grantId`. `Cache-Control: private, no-store`. A rota nova não conta como leitura de vitrine na telemetria (`ServiceAssertionAuthenticationHandler.cs:87`). Escopo de `learning` acrescentado nos três compose.
- **`learning`:** cliente da lista com a asserção de `learning` e escopo como parâmetro (hoje fixo em `AccessDecisionAssertionFactory.cs:23`), timeout de 2 s, **sem cache**. Sem resposta → 503 `COURSE_ACCESS_UNAVAILABLE`. Com resposta: versões vigentes dos cursos numa consulta (curso sem versão vigente é omitido), progresso do aluno nesses cursos numa consulta, e por curso título, contagem e percentual, `started`, `lastActivityAt` e `continueLessonId` pela RN-P10 sobre a ordem do currículo vigente (avanço mais recente não concluído → próxima não concluída depois dela → primeira não concluída → a mais recente; mais recente removida → primeira não concluída; nunca começado → primeira aula). `active` ordenado por `lastActivityAt` decrescente, nunca começados por último por `since` decrescente. Progresso nunca acrescenta curso (RN-P08).
- **`bff-student`:** `GET /api/v1/my-courses` autenticado, audiência `learning`, repasse e mapeamento de erros; falha de Identity → 502/504, nunca 503.
- **`student-spa`, Início (`/`):** o lugar reservado de `dashboard-screen.tsx:63` vira "Seus cursos": título, "37% · 3 de 8 aulas" e Continuar, ou Começar; Continuar/Começar abrem `/aulas/{continueLessonId}`, que retoma pela 4.0. Estado vazio "Você ainda não tem cursos" com caminho para `/cursos`. Carregando com skeleton. O card de conta continua. Desenho conforme `docs/design/wireframes-progresso.md`.

Casos que os testes provam: `commerce` — asserção de `media` → 403 `SCOPE_DENIED`; asserção de `learning` só com `access-decision:read` → 403; aluno de outra escola → lista vazia; aluno sem concessão → lista vazia; duas concessões ativas no mesmo curso → um item; uma vencida e uma ativa → `active`; só vencidas → `ended` com o `ends_on` da que terminou por último; resposta sem motivo, origem ou `grantId` (conforme `internal-api-contract-commerce.yaml`). `learning` (cliente real contra fronteira controlada) — sucesso, sem resposta → indisponível, 403 → indisponível, escopo `course-access:read` na asserção. `learning` (listagem) — os cinco casos de RN-P10, ordenação, curso sem versão vigente omitido, progresso sem concessão ausente, título da versão vigente.

## Fora do escopo desta task

- Seção "Acesso encerrado", 503 na tela e progresso indisponível → 6.0. Nesta task `learning` já devolve `ended` e o 503 corretamente; a tela só os desenha em 6.0.

## Decisões fechadas

- Composição em `learning` (C-02); escopo novo só para `learning` e lista sem cache (C-03, ADR-0015); sem paginação, teto de 500 (C-05); Continuar calculado em `learning` (C-10).
- "Meus cursos" é o Início; o login sem origem já leva para lá (DP-08, `student-login-screen.tsx:76`).

## Modificar / Referenciar

Arquivos a criar não são listados: a estrutura vem das skills `dotnet` e `react`.

- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Api/Security/ServiceAssertionScopes.cs`, `Security/ServiceAssertionAuthenticationHandler.cs` (linhas 87–92), `Extensions/ServiceAssertionExtensions.cs` (política), `Extensions/EndpointExtensions.cs` (rota)
- **modificar:** `docker-compose.yml`, `docker-compose.remote.yml`, `docker-compose.coolify.yml` (`ServiceAssertions__Issuers__learning__AllowedScopes__1: course-access:read`)
- **modificar:** `src/learning/src/CodeForCoders.Learning.Api/Security/AccessDecisionAssertionFactory.cs` (escopo por parâmetro), `Extensions/AccessDecisionConfigurationExtensions.cs` (cliente da lista), `Extensions/EndpointExtensions.cs` (rota)
- **modificar:** `src/bff-student/src/CodeForCoders.BffStudent.Application/Common/BffSecurityOptions.cs` (`/api/v1/my-courses` → `learning`), `src/bff-student/src/CodeForCoders.BffStudent.Api/Security/BffSecurityMiddleware.cs`, `src/bff-student/src/CodeForCoders.BffStudent.Api/Extensions/EndpointExtensions.cs`
- **modificar:** `src/student-spa/src/features/student-dashboard/components/dashboard-screen.tsx`, `src/student-spa/src/app/routes/dashboard-route.tsx`
- **ref:** `src/commerce/src/CodeForCoders.Commerce.Infra.Data/Entitlement/AccessDecisionQueries.cs:11` (semântica de ativa); `src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/AccessDecisionIssuersTests.cs` e `AccessDecisionContract.cs` (padrão de teste de emissor e de contrato); `src/learning/src/CodeForCoders.Learning.Api/Clients/AccessDecisionClient.cs` (padrão do cliente); `src/learning/tests/CodeForCoders.Learning.IntegrationTests/LessonCommerceBoundaryHandler.cs` (fronteira controlada); `docs/design/wireframes-progresso.md`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/commerce` | `dotnet format src/commerce/CodeForCoders.Commerce.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/commerce` | `dotnet build src/commerce/CodeForCoders.Commerce.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.AccessDecisionIssuersTests` | exit 0 (a decisão segue aceitando `access-decision:read` e recusando o escopo novo) | `ci-dotnet.yml` Testes |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.ArchitectureTests/CodeForCoders.Commerce.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/learning` | `dotnet format src/learning/CodeForCoders.Learning.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/learning` | `dotnet build src/learning/CodeForCoders.Learning.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/learning` | `dotnet test --project src/learning/tests/CodeForCoders.Learning.IntegrationTests/CodeForCoders.Learning.IntegrationTests.csproj -- --filter-class CodeForCoders.Learning.IntegrationTests.AccessDecisionClientTests` | exit 0 (a asserção da decisão segue com `access-decision:read`) | `ci-dotnet.yml` Testes |
| `src/learning` | `dotnet test --project src/learning/tests/CodeForCoders.Learning.ArchitectureTests/CodeForCoders.Learning.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/bff-student` | `dotnet format src/bff-student/CodeForCoders.BffStudent.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-student` | `dotnet build src/bff-student/CodeForCoders.BffStudent.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/bff-student` | `dotnet test --project src/bff-student/tests/CodeForCoders.BffStudent.ArchitectureTests/CodeForCoders.BffStudent.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/student-spa` | `npm --prefix src/student-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run test --` | exit 0 (suíte inteira; `dashboard-screen.test.tsx` atualizado ao novo conteúdo) | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run build -- --base=/student/` | exit 0 | `ci-react-ts.yml` |
| Compose | `docker compose -f docker-compose.yml config -q`; `docker compose -f docker-compose.yml -f docker-compose.remote.yml config -q`; `docker compose -f docker-compose.coolify.yml config -q` | exit 0 em cada um | AGENTS.md |

Testcontainers pesados rodam **em sequência** (AGENTS.md).

## Pronto quando

- [x] Gate focalizado passa (exit 0).
- [x] `commerce` e `learning` iniciam com os registros reais (política, cliente, emissor com dois escopos) no ambiente de teste.
- [x] No navegador: o financeiro concede cortesia em dois cursos; o aluno entra e cai no Início com o começado primeiro ("37% · 3 de 8 aulas", Continuar) e o outro com Começar; Continuar abre a aula certa, na posição de retomada.
- [x] Aluno sem concessão vê "Você ainda não tem cursos" com o caminho para a vitrine.
- [x] Asserção de `media` na rota da lista recebe 403.
