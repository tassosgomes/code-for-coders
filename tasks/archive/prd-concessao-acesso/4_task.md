---
status: done
task_kind: vertical
blocked_by: ["3.0"]
gate: 'dotnet test --project src/commerce/tests/CodeForCoders.Commerce.UnitTests/CodeForCoders.Commerce.UnitTests.csproj -- --filter-class CodeForCoders.Commerce.UnitTests.EntitlementCourseViewTests --minimum-expected-tests 6 && dotnet test --project src/commerce/tests/CodeForCoders.Commerce.UnitTests/CodeForCoders.Commerce.UnitTests.csproj -- --filter-class CodeForCoders.Commerce.UnitTests.CourtesyTitleSearchTests --minimum-expected-tests 4 && dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.CourtesyCourseListingTests --minimum-expected-tests 9 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj -- --filter-class CodeForCoders.BffAdmin.IntegrationTests.CourtesyCoursesProxyTests --minimum-expected-tests 4 && npm --prefix src/admin-spa run test -- courtesy-course-step'
gate_expect: "Pelo menos 26 testes passam: 6 e 4 unitários e 9 de integração de commerce, 4 do BFF, 3 do SPA"
---

# 4.0 O financeiro escolhe o curso entre os publicados da escola

**Fatia:** V-02 · **Cobre:** RF-03 (curso), RF-04 (escola), US-01, RN-D17, RN-C17, DP-05, C-01 · **Spec:** `techspec.md#v-02-o-financeiro-escolhe-o-curso-entre-os-publicados-da-escola` · **ADR:** [ADR-0001](../../../docs/adr/0001-monorepo-de-codigo.md)

## Comportamento

- **`commerce` — visão mínima do curso:** a primeira migration do schema `entitlement` cria `course_views`
  (`(tenant_id, course_id)`, título, título normalizado, número de versão). Uma fila nova de `commerce`, com DLX,
  DLQ e limite de entregas do padrão do serviço, ligada a `conteudo.versao-publicada.v1` do exchange de `learning`
  (declarado de forma idempotente na partida), alimenta a visão por um consumidor no formato do consumidor do
  Catálogo, reaproveitando a leitura do fato já existente. Aplica só se `versionNumber` for **maior** que o
  aplicado; igual ou menor é confirmado sem efeito; serializa por (escola, curso). Guarda **só** título e versão
  (nada de estrutura, nível ou descrição). Erro permanente (JSON inválido, `tenantId` ausente) vai à DLQ sem
  retry; falha transitória não confirma. Nenhuma chamada a `learning` existe nesse caminho (C-01).
- **Busca sem acento:** o título normalizado (minúsculas, sem diacríticos) segue a mesma regra de
  `CourseTitleSearch.Normalize` de `learning`, com **cópia própria** em `commerce` (serviços com deploy
  independente) e teste de conformidade com os mesmos casos.
- **`commerce` — `listCourtesyCoursesInternal`:** lista, por título, paginada (`_page`/`_size`), só os cursos da
  escola do claim `tenantId`, filtrando por `title` (trecho, sem distinguir maiúsculas nem acentos). **Não
  depende de o curso ter oferta** (DP-05). Política nova `cortesia.conceder` no claim `permissions`; sem ela 403
  `PERMISSION_DENIED`; JWT ausente ou inválido 401 `TOKEN_INVALID`. Curso nunca publicado não aparece; curso de
  outra escola nunca aparece.
- **`bff-admin` — `listCourtesyCourses`:** valida a sessão em Identity com audiência `commerce`, exige
  `cortesia.conceder` na sessão e repassa o JWT de ator; 502 `COMMERCE_UNAVAILABLE`, 504 `UPSTREAM_TIMEOUT`.
- **`admin-spa`:** o passo *Curso* do formulário, com a busca por título, a lista paginada, o estado vazio e o erro,
  conforme o Figma aprovado em 2.0. A lista é estado do servidor (cache de consultas); o curso escolhido vive no
  estado do formulário.
- **Observabilidade:** contadores de fato aplicado, ignorado e enviado à DLQ e o atraso do fato, sem título de
  curso em log, span ou métrica.
- **Ordem de implantação:** o consumidor de Entitlement precisa estar declarado **antes** do reenvio de `learning`
  (`CatalogInitialLoad:Enabled`, de execução única; hoje desligado em todos os Compose).

## Fora do escopo desta task

Concessão (5.0). A rotina de limpeza de recibos, as tabelas de concessão e o outbox de Entitlement nascem em 5.0. Qualquer alteração em `learning`: o reenvio existente basta.

## Decisões fechadas

- Visão própria de Entitlement, sem ler a do Catálogo (`techspec.md#decisões-técnicas`, D-02); nada nela é editável.
- Migrations só por `dotnet ef migrations add` (nunca à mão); `tenant_id` no filtro global de toda consulta (G07).
- Cursos sem oferta aparecem: cortesia não tem pedido (RN-D11, DP-05).
- Autorização em `commerce` pelas claims do JWT de ator (ADR-0005), com a mesma configuração de JWKS do Catálogo.

## Modificar / Referenciar

- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Infra.Data/CommerceDbContext.cs` (entidade da visão com filtro de tenant); migration via `dotnet ef migrations add`
- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Infra.Messaging/RabbitMqTopologyInitializer.cs`, `Configuration/RabbitMqOptions.cs`, `DependencyInjection.cs` (fila, DLQ, ligação e consumidor)
- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Api/Extensions/ServiceConfigurationExtensions.cs` (política `cortesia.conceder`), `EndpointExtensions.cs` (grupo de cortesias)
- **modificar:** `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Clients/CommerceApiOptions.cs`, `Extensions/EndpointExtensions.cs` (cliente e grupo `/api/v1/courtesy-courses`)
- **modificar:** `src/admin-spa/src/features` (feature de cortesias: passo *Curso*), `src/admin-spa/src/testing/handlers.ts`
- **ref:** `src/commerce/src/CodeForCoders.Commerce.Infra.Messaging/CatalogCourseConsumerWorker.cs`, `PublishedCourseFact.cs`, `Infra.Data/Catalog/CatalogCourseProjectionStore.cs` (consumidor, leitura do fato e regra de versão); `src/learning/src/CodeForCoders.Learning.Domain/Entities/CourseTitleSearch.cs` (normalização); `src/learning/src/CodeForCoders.Learning.Infra.Messaging/CatalogInitialLoad.cs` (reenvio de execução única); `src/commerce/src/CodeForCoders.Commerce.Api/Endpoints/CatalogCourseEndpoints.cs` (padrão de endpoint); `internal-api-contract-commerce.yaml` e `api-contract.yaml` (`listCourtesyCourses`); `asyncapi-contract.yaml` (`receberVersaoPublicadaEmMatricula`); `docs/design/wireframes-cortesias.md` e Figma aprovado; skills `dotnet` e `react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/commerce` | `dotnet format src/commerce/CodeForCoders.Commerce.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/commerce` | `dotnet build src/commerce/CodeForCoders.Commerce.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj` | exit 0 (sem regressão) | `ci-dotnet.yml` Testes |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.ArchitectureTests/CodeForCoders.Commerce.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/bff-admin` | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-admin` | `dotnet build src/bff-admin/CodeForCoders.BffAdmin.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/bff-admin` | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj` | exit 0 (sem regressão) | `ci-dotnet.yml` Testes |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run test -- courtesy-student-lookup` | exit 0 (sem regressão) | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run build -- --base=/admin/` | exit 0 | `ci-react-ts.yml` |

Cobertura agregada ≥ 70%, `dotnet publish` e imagens ficam para a validação full.

## Pronto quando

- [x] Gate passa (exit 0) com pelo menos 26 testes.
- [x] Composição: `commerce` inicia no ambiente de teste com os registros reais (política, consumidor, topologia, contexto de tenant) e o cliente HTTP real de `bff-admin` é exercitado contra a fronteira controlada do teste.
- [x] Reentrega do mesmo fato e fato de versão menor não alteram a linha; fato com `tenantId` ausente ou JSON inválido vai à DLQ sem retry; a busca "fundamentos" acha "Fundamentos de C#" e a busca sem acento acha título acentuado.
- [x] `listCourtesyCoursesInternal` direto: sem a permissão → 403; JWT de outra escola → lista só daquela escola; curso sem oferta aparece.
- [x] Smoke no Compose (Identity, `learning`, `commerce`, RabbitMQ, PostgreSQL, Valkey, migrations por `dotnet ef database update`): publicar um curso em `learning` (fato real) e abrir `http://localhost:8081/admin/cortesias` como financeiro → o curso é listado no passo *Curso*; com o consumidor declarado antes do reenvio de `learning`, os cursos já publicados aparecem.
