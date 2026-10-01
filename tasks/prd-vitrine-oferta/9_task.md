---
status: done
task_kind: vertical
blocked_by: ["6.0", "2.0"]
gate: 'dotnet test --project src/commerce/tests/CodeForCoders.Commerce.UnitTests/CodeForCoders.Commerce.UnitTests.csproj -- --filter-class CodeForCoders.Commerce.UnitTests.ServiceAssertionVerifierTests --minimum-expected-tests 8 && dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.ShowcaseListingTests --minimum-expected-tests 11 && dotnet test --project src/bff-student/tests/CodeForCoders.BffStudent.IntegrationTests/CodeForCoders.BffStudent.IntegrationTests.csproj -- --filter-class CodeForCoders.BffStudent.IntegrationTests.ShowcaseAnonymousRouteTests --minimum-expected-tests 7 && npm --prefix src/student-spa run test -- student-showcase'
gate_expect: "Pelo menos 33 testes passam: 8 unitários e 11 de integração de commerce, 7 do BFF do aluno, 7 do SPA"
---

# 9.0 O visitante abre a vitrine, filtra por nível e só vê o que está à venda

**Fatia:** V-07 · **Cobre:** RF-07, US-05, RN-O03, RN-O04, RN-O05, RN-O12, RN-O13, RN-O18, RN-C18, DP-04, C-04, C-07 · **Spec:** `techspec.md#v-07` · **ADR:** [ADR-0009](../../docs/adr/0009-autenticacao-de-servico-bff-aluno-em-servicos-de-dominio.md) (Proposed), [ADR-0004](../../docs/adr/0004-autenticacao-de-servico-bff-identity.md)

## Comportamento

- **`bff-student` — rota anônima:** `/api/v1/showcase/**` não exige sessão nem CSRF e **ignora** o cookie de
  aluno (válido, inválido ou ausente → a mesma vitrine). A fábrica de asserção de serviço passa a ser por
  destino: a asserção para `commerce` tem audiência `commerce`, escopo `showcase:read`, `tenantId` da
  configuração do BFF, vida de até 60 s, `jti` único e chave privada própria (par dedicado, ADR-0009);
  a asserção de Identity continua como antes. O cliente HTTP de `commerce` usa 3 s por tentativa, 8 s no
  total e uma nova tentativa só em `GET`; 502/504 com `SHOWCASE_UNAVAILABLE`/`SHOWCASE_TIMEOUT`.
- **`commerce` — segundo esquema de autenticação:** rota pública aceita **só** a asserção de serviço (assinatura,
  `kid`, emissor, audiência, escopo, tenant, validade e `jti` consumido em Valkey até `exp`); rota de ator
  aceita **só** o JWT de ator. Sem asserção → 401 `SERVICE_ASSERTION_INVALID`; escopo insuficiente → 403
  `SCOPE_DENIED`; `jti` repetido → 401; JWT de aluno na rota pública → 401. O tenant do contexto vem da
  asserção verificada; asserção de outra escola só enxerga dados daquela escola. O verificador é próprio de
  `commerce`, seguindo o comportamento de referência de `identity`, sem cópia de código de outro serviço.
- **`commerce` — `listShowcaseCoursesInternal`:** devolve só cursos **na vitrine** (nível não nulo e ao menos
  uma oferta publicada, pelo predicado único de 3.0), da escola da asserção, filtrados por `level`
  (`beginner`, `intermediate`, `advanced`; valor fora da lista → 400 `INVALID_REQUEST`), ordenados por
  `in_showcase_since` decrescente com desempate por `courseId`, paginados (`_size` ≤ 48), com o menor preço e
  o número de ofertas publicadas. `summary` do cartão é a `tagline` ou o início da descrição cortado em 200
  caracteres. Lista de campos **explícita**: nada de autor, e-mail, identificador de pessoa nem `videoId`.
  Resposta `Cache-Control: no-store`. Curso cuja versão vigente perdeu o nível some **sem** despublicar as
  ofertas e volta quando uma versão com nível é aplicada (DP-04).
- **`student-spa`:** rotas públicas `/cursos` e `/cursos/:courseId` **fora** de `requireStudentSession`, em
  layout público próprio (marca, links para entrar e cadastrar); o aluno autenticado vê o mesmo (RN-O13); o
  hook global de eventos de sessão não redireciona a página pública. A vitrine mostra cartões, o filtro de
  nível como grupo de opções com rótulo e estado anunciado (Todos, Iniciante, Intermediário, Avançado)
  guardado no endereço como `?nivel=iniciante|intermediario|avancado` (traduzido para o enum do contrato no
  cliente), "a partir de R$ X" quando houver mais de uma oferta, estado vazio que oferece "Todos" e paginação
  de 12. O link do cartão usa o `courseId`, nunca o título (C-07). Conforme o Figma aprovado em 2.0.
- **Composição e ambiente:** `docker-compose.yml`, `docker-compose.coolify.yml` e
  `scripts/generate-local-env.sh` ganham o par de chaves do `bff-student` para `commerce`, o emissor e os
  escopos aceitos em `commerce` e as URLs do BFF para `commerce`; segredos pelo secret manager do ambiente
  (ADR-0002).
- **Observabilidade:** contagem de leituras públicas por operação e resultado; nenhum dado de visitante,
  preço ou texto comercial em log, span ou métrica.
- **Caso negativo principal:** `commerce` direto sem asserção → 401; escopo errado → 403; `jti` repetido →
  401; JWT de aluno → 401; `nivel=xyz` → 400; curso de outra escola nunca aparece.

## Fora do escopo desta task

Página do curso (10.0), clique em Comprar e limite de taxa (11.0). Cache de leitura: fora desta entrega
(`Cache-Control: no-store`).

## Decisões fechadas

- `bff-student` autentica-se em `commerce` por asserção de serviço na audiência `commerce`, escopos
  `showcase:read` e `purchase-intent:write`, com par de chaves dedicado (ADR-0009, C-04). A ADR-0009 está
  `Proposed`; a implementação segue sua decisão e a promoção a `Accepted` é do responsável.
- Filtro de nível no endereço em português, com tabela de tradução no cliente
  (`techspec.md#decisões-técnicas`).
- Sem cache de leitura nesta entrega (`techspec.md#decisões-técnicas`); tempos-limite do BFF são propostas a
  calibrar (`techspec.md#questões-em-aberto`).
- Endereço público identificado por `courseId` (C-07).

## Modificar / Referenciar

- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Api/Extensions/ServiceConfigurationExtensions.cs` (segundo esquema, opções do emissor `bff-student`, políticas por rota), `EndpointExtensions.cs`, `ExceptionHandlers/GlobalExceptionHandler.cs` (401/403/400/404 públicos)
- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Application/Common/ITenantContext.cs`, `TenantContext.cs` (tenant da asserção)
- **modificar:** `src/bff-student/src/CodeForCoders.BffStudent.Api/Security/BffSecurityMiddleware.cs` (prefixo anônimo), `ServiceAssertionTokenFactory.cs`, `StudentIdentityOptions.cs`, `Extensions/ServiceConfigurationExtensions.cs` (fábrica e opções por destino, cliente HTTP 3 s/8 s), `Extensions/EndpointExtensions.cs` (rota da vitrine)
- **modificar:** `src/student-spa/src/config/paths.ts`, `src/student-spa/src/app/router.tsx`; `src/student-spa/nginx.conf.template` (nenhuma rota nova esperada; confirmar na evidência)
- **modificar:** `docker-compose.yml`, `docker-compose.coolify.yml`, `scripts/generate-local-env.sh`
- **ref:** `src/identity/src/CodeForCoders.Identity.Api/Security/ServiceAssertionVerifier.cs`, `ServiceAssertionOptions.cs`, `src/identity/src/CodeForCoders.Identity.Infra.Data/Outbox/ServiceAssertionReplayStore.cs` (comportamento e consumo de `jti`); `internal-api-contract-commerce.yaml` e `api-contract-student.yaml` (`listShowcaseCourses`); `src/student-spa/src/features/**` (padrão de tela e de rotas públicas); `docs/design/wireframes-catalogo-vitrine.md`, `DESIGN.md` e Figma aprovado; skills `dotnet` e `react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/commerce` | `dotnet format src/commerce/CodeForCoders.Commerce.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/commerce` | `dotnet build src/commerce/CodeForCoders.Commerce.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj` | exit 0 (área financeira e Catálogo do backoffice sem regressão) | `ci-dotnet.yml` Testes |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.ArchitectureTests/CodeForCoders.Commerce.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/bff-student` | `dotnet format src/bff-student/CodeForCoders.BffStudent.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-student` | `dotnet build src/bff-student/CodeForCoders.BffStudent.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/bff-student` | `dotnet test --project src/bff-student/tests/CodeForCoders.BffStudent.IntegrationTests/CodeForCoders.BffStudent.IntegrationTests.csproj` | exit 0 (rotas com sessão sem regressão) | `ci-dotnet.yml` Testes |
| `src/bff-student` | `dotnet test --project src/bff-student/tests/CodeForCoders.BffStudent.ArchitectureTests/CodeForCoders.BffStudent.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/student-spa` | `npm --prefix src/student-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run test -- src/app` | exit 0 (fluxos de sessão e conta sem regressão) | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run build -- --base=/student/` | exit 0 | `ci-react-ts.yml` |

Cobertura agregada ≥ 70%, `dotnet publish` e imagens ficam para a validação full.

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 33 testes.
- [ ] Composição: `commerce` inicia no ambiente de teste com os dois esquemas de autenticação e o verificador reais, e cada rota aceita um único esquema; o cliente HTTP real do `bff-student` é exercitado contra a fronteira controlada do teste, e as asserções assinadas pela fábrica do BFF são aceitas pelo verificador de `commerce` (conformidade cruzada, ADR-0009).
- [ ] Só cursos com nível e ao menos uma oferta publicada aparecem; filtro `iniciante` reflete no endereço e mostra só iniciantes; filtro sem resultado mostra o estado vazio com "Todos"; nova versão sem nível tira o curso e as ofertas seguem `published` no backoffice.
- [ ] Resposta JSON da vitrine inspecionada: nenhum autor, identificador de pessoa nem `videoId`.
- [ ] Smoke no Compose com um curso de cada nível publicado em `learning` **com nível real** (fatos 1.1.0 de `learning` ou, enquanto ele não existe, mensagens conformes a `VersaoPublicadaPayload` 1.1.0 no exchange de `learning`) e ofertas publicadas: abrir `http://localhost:8082/student/cursos` e `…?nivel=iniciante`, recarregar cada URL direto (o nginx devolve o SPA), com cookie de aluno válido e inválido → mesma vitrine.
