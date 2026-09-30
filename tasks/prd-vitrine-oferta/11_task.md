---
status: pending
task_kind: vertical
blocked_by: ["10.0"]
gate: 'dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.PurchaseIntentTests --minimum-expected-tests 9 && dotnet test --project src/bff-student/tests/CodeForCoders.BffStudent.IntegrationTests/CodeForCoders.BffStudent.IntegrationTests.csproj -- --filter-class CodeForCoders.BffStudent.IntegrationTests.PurchaseIntentRateLimitTests --minimum-expected-tests 4 && npm --prefix src/student-spa run test -- student-purchase-intent && npm --prefix src/admin-spa run test -- catalog-purchase-intents'
gate_expect: "Pelo menos 19 testes passam: 9 de integração de commerce, 4 do BFF do aluno, 4 do SPA do aluno, 2 do SPA do backoffice"
---

# 11.0 O visitante clica em Comprar, vê o aviso e a escola tem um primeiro sinal de demanda

**Fatia:** V-09 · **Cobre:** RF-09, RN-O05 (intenção por oferta), C-08, G10, G26, OD53, DP-02 · **Spec:** `techspec.md#v-09` · **ADR:** [ADR-0009](../../docs/adr/0009-autenticacao-de-servico-bff-aluno-em-servicos-de-dominio.md) (Proposed)

## Comportamento

- **`student-spa`:** o clique em *Comprar* **sempre** abre o aviso "compra em breve", focável e anunciado e
  sem pedir dado, **independentemente** do resultado de `registerPurchaseIntent`; 404, 429 ou 5xx na contagem
  não escondem o aviso nem mostram erro ao visitante. A `Idempotency-Key` é gerada por clique e não é
  persistida no navegador.
- **`bff-student` — `registerPurchaseIntent`:** sem sessão e sem CSRF (C-08). Aplica o limite de taxa **por
  oferta**, em memória, com 429 `RATE_LIMITED` e `Retry-After`; assina a asserção de serviço com escopo
  `purchase-intent:write` e chama `commerce`; o clique **não** é repetido pelo cliente HTTP. Sem IP, sem
  cookie, sem `User-Agent` nem identificador de navegador em nenhum ponto (G26).
- **`commerce` — `registerPurchaseIntentInternal`:** verifica a asserção (9.0) e, numa só transação, grava o
  recibo e incrementa o contador diário (UTC) por escola, oferta e dia **se** a oferta está `published` na
  escola, numa única instrução de verificação e incremento (nada de ler e depois gravar). Oferta
  despublicada, inexistente ou de outra escola → 404 `OFFER_NOT_AVAILABLE`, sem contar. `Idempotency-Key`
  guardada só como **hash SHA-256** num recibo com validade de 24 horas; repetição devolve 202 sem contar de
  novo. Clique concorrente com a despublicação conta no máximo uma vez e nunca em oferta já despublicada.
  O armazenamento contém só escola, oferta, dia e contagem, mais o recibo com hash e validade.
- **Backoffice:** a ficha do curso mostra, por oferta, o total como a soma de todos os dias ("14 cliques em
  Comprar", RF-09, "desde a publicação"); `getCatalogCourseInternal` passa a devolver esse total (zero antes
  de qualquer clique).
- **Observabilidade:** cliques contados, repetidos e recusados **sem rótulo de oferta** (ids só em span); nenhum
  dado de visitante em log, span ou métrica.
- **Caso negativo principal:** oferta despublicada → 404 e contagem igual; mesma chave → 202 sem nova
  contagem; limite estourado → 429 e, na tela, o aviso ainda aparece.

## Fora do escopo desta task

Destino real do botão, pedido, checkout e pagamento (`CAP-011`); cliques por pessoa, sessão ou origem.

## Decisões fechadas

- Contador diário por oferta, sem linha por clique; recibo com hash da chave por 24 h
  (`techspec.md#decisões-técnicas`).
- Limite de taxa por oferta em memória do BFF; o aviso nunca depende da contagem; valores iniciais são
  configuração a calibrar (`techspec.md#decisões-técnicas`, `#questões-em-aberto`).
- Chave de idempotência nunca em log, span, métrica nem armazenamento do navegador
  (`techspec.md#contratos-e-fronteiras`).

## Modificar / Referenciar

- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Api/Extensions/EndpointExtensions.cs`, `ExceptionHandlers/GlobalExceptionHandler.cs` (`OFFER_NOT_AVAILABLE`)
- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Infra.Data/CommerceDbContext.cs` (contador diário e recibo de 24 h); migration via `dotnet ef migrations add`
- **modificar:** `src/bff-student/src/CodeForCoders.BffStudent.Api/Extensions/EndpointExtensions.cs`, `ServiceConfigurationExtensions.cs` (rota, limite de taxa por oferta)
- **ref:** `internal-api-contract-commerce.yaml` e `api-contract-student.yaml` (`registerPurchaseIntent`); `api-contract.yaml` (total de cliques na ficha); `src/admin-spa/src/features/**` e `src/student-spa/src/features/**` (padrões); `docs/design/wireframes-catalogo-vitrine.md` e Figma aprovado; skills `dotnet` e `react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/commerce` | `dotnet format src/commerce/CodeForCoders.Commerce.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/commerce` | `dotnet build src/commerce/CodeForCoders.Commerce.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.ArchitectureTests/CodeForCoders.Commerce.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/bff-student` | `dotnet format src/bff-student/CodeForCoders.BffStudent.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-student` | `dotnet build src/bff-student/CodeForCoders.BffStudent.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/bff-student` | `dotnet test --project src/bff-student/tests/CodeForCoders.BffStudent.IntegrationTests/CodeForCoders.BffStudent.IntegrationTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/student-spa` | `npm --prefix src/student-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run build -- --base=/student/` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run build -- --base=/admin/` | exit 0 | `ci-react-ts.yml` |

`bff-admin` não muda: o total de cliques já é campo de `getCatalogCourse` desde 4.0. Cobertura agregada ≥ 70%,
`dotnet publish` e imagens ficam para a validação full.

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 19 testes.
- [ ] Duas ofertas, clicar na vitalícia → só ela sobe; repetir a requisição com a mesma chave → 202 sem nova contagem; despublicar e chamar direto → 404 e contagem igual; clique concorrente com despublicação nunca conta em oferta despublicada.
- [ ] Inspeção das tabelas e dos logs e spans do clique: nenhuma sessão, cookie, IP, `User-Agent` nem identificador de navegador; a chave de idempotência aparece só como hash.
- [ ] Estourar o limite → 429 com `Retry-After` e, na tela, o aviso ainda aparece; total da ficha = soma dos dias.
- [ ] Smoke no Compose: clicar em *Comprar* em `http://localhost:8082/student/cursos/{courseId}` abre o aviso e a ficha em `http://localhost:8081/admin/catalogo/{courseId}` mostra o total da oferta clicada incrementado em um.
