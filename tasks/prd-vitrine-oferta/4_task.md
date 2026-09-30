---
status: pending
task_kind: vertical
blocked_by: ["3.0"]
gate: 'dotnet test --project src/commerce/tests/CodeForCoders.Commerce.UnitTests/CodeForCoders.Commerce.UnitTests.csproj -- --filter-class CodeForCoders.Commerce.UnitTests.CatalogTaglineTests --minimum-expected-tests 3 && dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.CatalogCourseRecordTests --minimum-expected-tests 8 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj -- --filter-class CodeForCoders.BffAdmin.IntegrationTests.CatalogCourseRecordProxyTests --minimum-expected-tests 4 && npm --prefix src/admin-spa run test -- catalog-course-record'
gate_expect: "Pelo menos 19 testes passam: 3 unitários e 8 de integração de commerce, 4 do BFF, 4 do SPA"
---

# 4.0 O financeiro abre a ficha do curso e escreve a chamada comercial

**Fatia:** V-02 · **Cobre:** RF-02, RN-O02, RN-O03 (aviso), RN-O06 (orientação na tela), DP-03 (160 caracteres), C-02, C-06 · **Spec:** `techspec.md#v-02` · **ADR:** [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md)

## Comportamento

- **`commerce` — `getCatalogCourseInternal`:** devolve a ficha do curso da escola: título, nível e
  pré-requisito (texto e recomendados com o título **atual** quando o Catálogo conhece o curso, senão o da
  publicação, C-02) em leitura, `tagline`, `inShowcase`, o total de cliques (zero até 11.0) e as ofertas
  (lista vazia até 5.0; no máximo 50, sem paginação, C-06). Todo curso que o Catálogo conhece tem ficha:
  não existe "criar ficha". Curso inexistente ou de outra escola → 404 `CATALOG_COURSE_NOT_FOUND`,
  indistinguível entre os dois.
- **`commerce` — `updateCatalogCourseInternal`:** grava a `tagline` validada (1 a 160 caracteres; `null`
  limpa; campo ausente não muda) sob o bloqueio do curso. `Idempotency-Key` obrigatória (ausente → 400
  `INVALID_REQUEST`); recibo por escola, ator e chave com hash do pedido e a resposta por 24 horas; mesma
  chave e mesmo corpo devolve a mesma resposta sem regravar; mesma chave com corpo diferente → 422
  `IDEMPOTENCY_KEY_REUSED`. 161 caracteres → 422 `FIELD_INVALID` com o campo e o limite; sem
  `oferta.editar` → 403.
- **`bff-admin` — `getCatalogCourse` e `updateCatalogCourse`:** mesmo desenho de 3.0, repassando o JWT de
  ator e a `Idempotency-Key`; escrita que expira devolve 504, e repetir pela mesma chave é seguro.
- **`admin-spa`:** a rota `/catalogo/:courseId` mostra título, nível e pré-requisito sem ação de alteração
  (quem os altera é o professor; link para a Autoria só com `autoria.ler`); aviso permanente quando o nível
  é nulo ("nenhuma oferta pode ser publicada até o professor declarar o nível"); campo de chamada comercial
  com contador de 160 e a orientação de não prometer exclusividade nem proteção contra cópia; salvar relê a
  ficha; a chave de idempotência nasce por intenção e é reutilizada na repetição após 504. A lista de
  ofertas aparece vazia com a chamada para criar a primeira. Conforme o Figma aprovado em 2.0.
- **Caso negativo principal:** 161 caracteres recusados com o campo indicado; curso de outra escola → 404;
  corpo diferente com a mesma chave → `IDEMPOTENCY_KEY_REUSED`.

## Fora do escopo desta task

Criar, editar e excluir ofertas (5.0). Total de cliques com valor diferente de zero (11.0). Qualquer mensagem
no outbox: alterar a `tagline` não gera fato nem ato (DP-05).

## Decisões fechadas

- Ficha implícita por curso conhecido; `tagline` opcional de 1 a 160 (DP-03, C-06).
- Nível e pré-requisito são do professor e só de leitura aqui (RN-O02); a chamada comercial não substitui a
  descrição pedagógica.
- Recibo de idempotência da escrita do backoffice segue o precedente de `learning` (G09).
- Mapa exceção → HTTP de `techspec.md#mapeamento-do-contrato-de-api`.

## Modificar / Referenciar

- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Api/Extensions/EndpointExtensions.cs`, `ExceptionHandlers/GlobalExceptionHandler.cs` (mapa exceção → HTTP)
- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Infra.Data/CommerceDbContext.cs` (`tagline` e recibos); migration via `dotnet ef migrations add`
- **modificar:** `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/EndpointExtensions.cs` (rotas da ficha)
- **modificar:** `src/admin-spa/src/config/paths.ts`, `src/admin-spa/src/app/app-routes.tsx` (rota da ficha)
- **ref:** `src/learning/src/CodeForCoders.Learning.Domain/Entities/CourseEditReceipt.cs`, `Infra.Data/Idempotency/CourseEditStore.cs` (recibo com hash e resposta por 24 h); `src/admin-spa/src/features/course-authoring/**` (idempotência por intenção); `internal-api-contract-commerce.yaml` e `api-contract.yaml` (`getCatalogCourse`, `updateCatalogCourse`); `docs/design/wireframes-catalogo-vitrine.md` e Figma aprovado; skills `dotnet` e `react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/commerce` | `dotnet format src/commerce/CodeForCoders.Commerce.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/commerce` | `dotnet build src/commerce/CodeForCoders.Commerce.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.ArchitectureTests/CodeForCoders.Commerce.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/bff-admin` | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-admin` | `dotnet build src/bff-admin/CodeForCoders.BffAdmin.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/bff-admin` | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run test -- catalog-courses` | exit 0 (lista de 3.0 sem regressão) | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run build -- --base=/admin/` | exit 0 | `ci-react-ts.yml` |

Cobertura agregada ≥ 70%, `dotnet publish` e imagens ficam para a validação full.

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 19 testes.
- [ ] Ficha com nível e pré-requisito sem ação de alteração; chamada salva e relida; 161 caracteres recusados com campo e limite; `null` limpa a chamada.
- [ ] Repetição com a mesma chave não regrava; corpo diferente com a mesma chave → `IDEMPOTENCY_KEY_REUSED`; curso inexistente ou de outra escola → 404 `CATALOG_COURSE_NOT_FOUND`.
- [ ] SPA: aviso de nível nulo, contador de 160 e orientação de RN-O06 presentes; nenhum texto da plataforma promete exclusividade ou proteção.
- [ ] Smoke no Compose: em `http://localhost:8081/admin/catalogo/{courseId}` o financeiro salva e relê a chamada comercial.
