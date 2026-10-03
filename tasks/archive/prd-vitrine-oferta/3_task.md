---
status: done
task_kind: vertical
blocked_by: ["2.0"]
gate: 'dotnet test --project src/identity/tests/CodeForCoders.Identity.UnitTests/CodeForCoders.Identity.UnitTests.csproj -- --filter-class CodeForCoders.Identity.UnitTests.StaffRoleCatalogOfferPermissionTests --minimum-expected-tests 3 && dotnet test --project src/commerce/tests/CodeForCoders.Commerce.UnitTests/CodeForCoders.Commerce.UnitTests.csproj -- --filter-class CodeForCoders.Commerce.UnitTests.CatalogCourseViewTests --minimum-expected-tests 6 && dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.CatalogCourseListingTests --minimum-expected-tests 9 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj -- --filter-class CodeForCoders.BffAdmin.IntegrationTests.CatalogCoursesProxyTests --minimum-expected-tests 5 && npm --prefix src/admin-spa run test -- catalog-courses'
gate_expect: "Pelo menos 27 testes passam: 3 de identity, 6 unitários e 9 de integração de commerce, 5 do BFF, 4 do SPA"
---

# 3.0 O financeiro abre o Catálogo e vê os cursos publicados da escola

**Fatia:** V-01 · **Cobre:** RF-01, RF-02 (lista e aviso de curso sem nível), US-01 (pré-condição), RN-O01, RN-O14, RN-O18, RN-C17, RN-12, RN-16, RN-17, RN-18, DP-01, C-01 · **Spec:** `techspec.md#v-01` · **ADR:** [ADR-0005](../../../docs/adr/0005-sessao-e-servico-do-backoffice.md)

## Comportamento

- **`identity`:** o papel financeiro passa a carregar `oferta.editar` (além de `financeiro.ler`); professor,
  suporte e administrador não a recebem.
- **`commerce` — visão do curso:** a fila do Catálogo, ligada a `conteudo.versao-publicada.v1` do exchange de
  `learning` (declarado de forma idempotente na partida, com DLX/DLQ e limite de entregas), cria e atualiza
  uma linha de visão por curso e escola. Aplica o fato só se `versionNumber` for maior que o aplicado; igual
  ou menor é confirmado sem efeito. **Exceção de compatibilidade:** fato 1.1.0 (propriedades `description`,
  `level`, `prerequisite` presentes, mesmo nulas) de mesma `versionNumber` substitui a linha derivada de um
  1.0.0; a linha guarda o formato de origem. Fato 1.0.0 vira nível nulo, pré-requisito vazio e descrição
  vazia. A estrutura guardada são módulos e títulos de aula, **sem `videoId`**. A aplicação é atômica,
  idempotente e trava a linha do curso. Erro permanente (JSON inválido, `tenantId` ausente) vai à DLQ sem
  retry; falha transitória não confirma. Nenhuma chamada a `learning` existe nesse caminho (C-01).
- **`commerce` — `listCatalogCoursesInternal`:** lista por título, paginada, só os cursos da escola do claim
  `tenantId`, com nível (ou nulo), `inShowcase` e a contagem de ofertas por estado (zeros nesta fatia).
  Política `oferta.editar` no claim `permissions`; sem ela, 403 `PERMISSION_DENIED`; JWT ausente ou inválido,
  401. Curso de outra escola nunca aparece. A elegibilidade de vitrine é um único predicado de domínio
  (nível não nulo e ao menos uma oferta publicada), reutilizado pelas fatias seguintes; o momento em que o
  curso passa a estar na vitrine é guardado na linha do curso.
- **`bff-admin` — `listCatalogCourses`:** valida a sessão em Identity com audiência `commerce`, exige
  `oferta.editar` na sessão e repassa o JWT de ator a `commerce` (5 s por tentativa, 20 s no total, nova
  tentativa só em método seguro); 502/504 com `COMMERCE_UNAVAILABLE`/`COMMERCE_TIMEOUT`. Revogar o papel
  recusa a próxima ação da sessão aberta (RN-17); as ofertas existentes não mudam.
- **`admin-spa`:** item de menu **Catálogo**, visível só com `oferta.editar`, e a rota `/catalogo` com a
  lista (título, nível ou o texto "Sem nível", se está na vitrine, contagem de ofertas por estado,
  paginação). Sem permissão, o item não aparece e a rota direta mostra o estado de acesso negado; o estado do
  servidor vive no cache de consultas. Conforme o Figma aprovado em 2.0.
- **Observabilidade:** contadores de fato aplicado, ignorado e enviado à DLQ e o atraso do fato, sem texto
  de curso em log, span ou métrica.
- **Caso negativo principal:** curso nunca publicado não aparece; papel sem a permissão → 403 direto em
  `commerce` e no BFF; fato duplicado ou de versão menor não altera a linha.

## Fora do escopo desta task

Ficha do curso e chamada comercial (4.0). Criação de ofertas (5.0) e a escrita no outbox (6.0). Rotas
públicas e segundo esquema de autenticação (9.0). Saída de cursos por perda de nível com ofertas publicadas é
exercitada em 9.0; aqui só o predicado e o momento guardado.

## Decisões fechadas

- Visão do curso é réplica derivada, substituída por inteiro; nada no Catálogo a edita
  (`techspec.md#decisões-técnicas`, C-01).
- Exceção de formato 1.1.0 × 1.0.0 de mesma versão (`techspec.md#decisões-técnicas`); o reenvio de `learning`
  (CAP-005) não existe ainda: a evidência usa fatos 1.0.0 reais e fixtures conformes ao schema 1.1.0.
- Autorização em `commerce` pelas claims do JWT de ator (ADR-0005); `bff-admin` e `admin-spa` implantados juntos.
- Migrations só por `dotnet ef migrations add`; `tenant_id` no filtro global de toda consulta (G07).

## Modificar / Referenciar

- **modificar:** `src/identity/src/CodeForCoders.Identity.Domain/Entities/StaffRoleCatalog.cs` (permissão no papel financeiro)
- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Api/Extensions/ServiceConfigurationExtensions.cs` (política `oferta.editar`), `EndpointExtensions.cs` (grupo do Catálogo)
- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Infra.Data/CommerceDbContext.cs` (entidades do Catálogo com filtro de tenant); migration via `dotnet ef migrations add`
- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Infra.Messaging/RabbitMqTopologyInitializer.cs`, `Configuration/RabbitMqOptions.cs` (fila do Catálogo ligada a `learning.events`, DLQ)
- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Application/Common/ITenantContext.cs`, `TenantContext.cs` (tenant do claim e do payload do consumidor)
- **modificar:** `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Clients/CommerceApiOptions.cs`, `Extensions/ServiceConfigurationExtensions.cs`, `Extensions/EndpointExtensions.cs` (cliente e grupo `/api/v1/catalog/**`)
- **modificar:** `src/admin-spa/src/features/staff-session/utils/get-staff-areas.ts`, `src/admin-spa/src/config/paths.ts`, `src/admin-spa/src/app/app-routes.tsx`
- **ref:** `src/identity/src/CodeForCoders.Identity.Api/Security/ServiceAssertionVerifier.cs` (não usado aqui; comportamento de referência para 9.0); `src/learning/src/CodeForCoders.Learning.Infra.Messaging/**` (precedente de consumidor e topologia); `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/CourseAuthoringEndpoints.cs` (validação de sessão por audiência); `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Clients/CommerceFinanceAreaClient.cs` (cliente existente de `commerce`); `src/admin-spa/src/features/course-authoring/**` (padrão de tela); `internal-api-contract-commerce.yaml` e `api-contract.yaml` (`listCatalogCourses`); `asyncapi-contract.yaml` (`receberVersaoPublicada`); `docs/design/wireframes-catalogo-vitrine.md` e Figma aprovado; skills `dotnet` e `react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/identity` | `dotnet format src/identity/CodeForCoders.Identity.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/identity` | `dotnet build src/identity/CodeForCoders.Identity.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/identity` | `dotnet test --project src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj` | exit 0 (papéis e convites sem regressão) | `ci-dotnet.yml` Testes |
| `src/commerce` | `dotnet format src/commerce/CodeForCoders.Commerce.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/commerce` | `dotnet build src/commerce/CodeForCoders.Commerce.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj` | exit 0 (área financeira sem regressão) | `ci-dotnet.yml` Testes |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.ArchitectureTests/CodeForCoders.Commerce.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/bff-admin` | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-admin` | `dotnet build src/bff-admin/CodeForCoders.BffAdmin.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/bff-admin` | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run test -- staff-session` | exit 0 (menu por permissão sem regressão) | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run build -- --base=/admin/` | exit 0 | `ci-react-ts.yml` |

Cobertura agregada ≥ 70%, `dotnet publish` e imagens ficam para a validação full.

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 27 testes.
- [ ] Composição: a aplicação `commerce` inicia no ambiente de teste com os registros reais (política, consumidor, topologia e contexto de tenant) e o cliente HTTP real de `bff-admin` é exercitado contra a fronteira controlada do teste, sem dublê do adaptador.
- [ ] `listCatalogCoursesInternal` direto: sem a permissão → 403; com JWT de outra escola → lista só daquela escola; papel administrador, professor e suporte → 403.
- [ ] Reentrega do mesmo fato e fato de versão menor não alteram a linha; fato 1.1.0 de mesma versão substitui o 1.0.0 (nível e descrição passam a existir); fato inválido vai à DLQ sem retry.
- [ ] Smoke no Compose (Identity, Learning, RabbitMQ, PostgreSQL, Valkey): publicar um curso em `learning` (fato 1.0.0 real) e abrir `http://localhost:8081/admin/catalogo` como financeiro → curso listado com "Sem nível"; como professor, suporte e administrador, o menu não tem o item e a rota direta nega; revogar o papel e repetir a ação → 401/403.
