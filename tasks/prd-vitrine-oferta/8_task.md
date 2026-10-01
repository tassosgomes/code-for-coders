---
status: done
task_kind: vertical
blocked_by: ["6.0"]
gate: 'dotnet test --project src/commerce/tests/CodeForCoders.Commerce.UnitTests/CodeForCoders.Commerce.UnitTests.csproj -- --filter-class CodeForCoders.Commerce.UnitTests.OfferUnpublishRulesTests --minimum-expected-tests 5 && dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.OfferUnpublishTests --minimum-expected-tests 7 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj -- --filter-class CodeForCoders.BffAdmin.IntegrationTests.OfferUnpublishProxyTests --minimum-expected-tests 3 && npm --prefix src/admin-spa run test -- catalog-offer-unpublish'
gate_expect: "Pelo menos 18 testes passam: 5 unitários e 7 de integração de commerce, 3 do BFF, 3 do SPA"
---

# 8.0 O financeiro despublica a oferta: ela sai da vitrine sem afetar quem comprou

**Fatia:** V-06 · **Cobre:** RF-06, US-04, RN-O11, RN-O12, RN-O15, DP-04 (efeito inverso, exercitado em 9.0), C-09 · **Spec:** `techspec.md#v-06` · **ADR:** [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md)

## Comportamento

- **`commerce` — `unpublishOfferInternal`:** só oferta `published` passa a `unpublished`, com
  `offerRevision` + 1; outro estado → 422 `OFFER_STATE_CONFLICT`. O momento de entrada na vitrine é
  recalculado pelo predicado único de 3.0: despublicar uma de duas ofertas publicadas mantém o valor;
  despublicar a última o limpa e o curso sai da vitrine (`inShowcase: false` na lista e na ficha do
  backoffice). Fato `catalogo.oferta-despublicada.v1` e ato `oferta-despublicada` são gravados no outbox
  na mesma transação. `Idempotency-Key` obrigatória: repetir pela mesma chave devolve a mesma resposta, um
  fato e um ato. A oferta segue visível no backoffice como *despublicada* e pode ser republicada (6.0).
- **`deleteOfferInternal`:** oferta `unpublished` → 422 `OFFER_STATE_CONFLICT`; só rascunho é excluído
  (RN-O11).
- **`bff-admin` — `unpublishOffer`:** repassa JWT e `Idempotency-Key`; erros de `commerce` com o mesmo `code`.
- **`admin-spa`:** despublicar pede confirmação; a ficha mostra a oferta como *despublicada* e **não** oferece
  "Excluir" nela; a lista e a ficha refletem `inShowcase` atualizado. Conforme o Figma aprovado em 2.0.
- **Caso negativo principal:** exclusão direta de oferta despublicada → 422; despublicar oferta que não está
  publicada → 422 sem mensagens.

## Fora do escopo desta task

O efeito na página e na vitrine públicas (evidência em 9.0 e 10.0, que dependem desta regra); rótulos do ato
na trilha (12.0); compras já feitas (não existem nesta entrega).

## Decisões fechadas

- Despublicar não afeta quem já comprou (RN-O12, PRD); a garantia para o pedido é de `CAP-011`.
- Vitrine e ficha usam o mesmo predicado de elegibilidade; nenhuma fatia o reimplementa
  (`techspec.md#interfaces-entre-fatias-ou-times`).

## Modificar / Referenciar

- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Api/Extensions/EndpointExtensions.cs` (`unpublishOffer`; `deleteOffer` em `unpublished`)
- **modificar:** `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/EndpointExtensions.cs` (rota de despublicar)
- **ref:** `internal-api-contract-commerce.yaml` e `api-contract.yaml` (`unpublishOffer`); `asyncapi-contract.yaml` e `asyncapi-contract-audit.yaml` (payloads); `docs/design/wireframes-catalogo-vitrine.md` e Figma aprovado; skills `dotnet` e `react`

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
| `src/admin-spa` | `npm --prefix src/admin-spa run test -- catalog-` | exit 0 (Catálogo sem regressão) | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run build -- --base=/admin/` | exit 0 | `ci-react-ts.yml` |

`audit` não muda nesta fatia: os três tipos de ato foram aceitos em 6.0. Cobertura agregada ≥ 70%,
`dotnet publish` e imagens ficam para a validação full.

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 18 testes.
- [ ] Duas ofertas publicadas, despublicar uma → a outra continua e o momento de entrada na vitrine permanece; despublicar a última → `inShowcase: false`.
- [ ] Exclusão direta de oferta despublicada → 422; repetição com a mesma chave → um fato e um ato; fato e ato validados contra `AtoPraticadoPayload` 1.3.0 e o payload do fato.
- [ ] SPA: confirmação de despublicar; "Excluir" ausente em oferta despublicada.
- [ ] Smoke no Compose: despublicar em `http://localhost:8081/admin/catalogo/{courseId}` mantém a oferta visível como *despublicada* e grava `oferta-despublicada` na trilha.
