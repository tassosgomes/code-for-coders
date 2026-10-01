---
status: done
task_kind: vertical
blocked_by: ["4.0"]
gate: 'dotnet test --project src/commerce/tests/CodeForCoders.Commerce.UnitTests/CodeForCoders.Commerce.UnitTests.csproj -- --filter-class CodeForCoders.Commerce.UnitTests.OfferRulesTests --minimum-expected-tests 10 && dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.OfferDraftTests --minimum-expected-tests 9 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj -- --filter-class CodeForCoders.BffAdmin.IntegrationTests.OfferDraftProxyTests --minimum-expected-tests 4 && npm --prefix src/admin-spa run test -- catalog-offer-form'
gate_expect: "Pelo menos 29 testes passam: 10 unitários e 9 de integração de commerce, 4 do BFF, 6 do SPA"
---

# 5.0 O financeiro cria, edita e exclui ofertas em rascunho

**Fatia:** V-03 · **Cobre:** RF-03, US-01, US-02, RN-O01, RN-O04, RN-O07, RN-O08, RN-O11 (exclusão de rascunho), RN-D03, RN-D04 (forma da vigência), DP-03, C-05 · **Spec:** `techspec.md#v-03` · **ADR:** [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md)

## Comportamento

- **`commerce` — `createOfferInternal`, `updateOfferInternal` (rascunho), `deleteOfferInternal`:** o agregado
  ficha + ofertas valida nome (1 a 60 caracteres), `priceCents` (1 a 9 999 999) e `accessPeriod` (`months`
  inteiro de 1 a 60, ou `lifetime`) no domínio. A oferta nasce `draft` com `offerRevision` 1; cada edição
  que muda valor incrementa a revisão; valores idênticos ao gravado não mudam nada. No máximo 50 ofertas por
  curso: a 51ª → 422 `FIELD_INVALID` no campo `offers`. Excluir só funciona em `draft`; oferta de outro
  estado → 422 `OFFER_STATE_CONFLICT`. Toda operação trava a linha do curso dono. `Idempotency-Key`
  obrigatória nas escritas, com recibo como em 4.0 (mesma chave e mesmo corpo → a mesma resposta e uma só
  oferta, inclusive sob chamadas concorrentes; corpo diferente → `IDEMPOTENCY_KEY_REUSED`). Campo fora dos
  limites → 422 `FIELD_INVALID` com o campo em `detail`; oferta desconhecida ou de outra escola → 404
  `OFFER_NOT_FOUND`; curso desconhecido → 404 `CATALOG_COURSE_NOT_FOUND`. Nenhuma mensagem é gravada no
  outbox (criar, editar rascunho e excluir não geram fato nem ato).
- **Forma única da promessa (C-05):** preço em centavos inteiros e vigência `{type: months, months}` ou
  `{type: lifetime}` iguais em HTTP e no armazenamento, para o pedido de `CAP-011` congelar sem conversão.
- **`bff-admin`:** `createOffer`, `updateOffer`, `deleteOffer` repassam JWT, `Idempotency-Key` e o erro de
  `commerce` com o mesmo `code`; 502/504 como nas fatias anteriores.
- **`admin-spa`:** a ficha ganha a lista de ofertas (rascunhos primeiro, com estado, preço e vigência) e o
  formulário de oferta: o preço é digitado em reais e convertido para centavos **sem ponto flutuante**,
  recusando mais de duas casas; a vigência alterna entre "Por período" (meses inteiros, 1 a 60) e
  "Vitalícia"; `FIELD_INVALID` vira mensagem no campo nomeado em `detail`; excluir rascunho pede
  confirmação. Conforme o Figma aprovado em 2.0.
- **Caso negativo principal:** preço 0, negativo ou acima de R$ 99.999,99, e vigência 0, fracionada ou acima
  de 60 meses são recusados com o campo indicado; excluir oferta não rascunho → 422.

## Fora do escopo desta task

Publicar, despublicar e qualquer fato ou ato (6.0 a 8.0). Edição de preço e vigência de oferta publicada com
confirmação antes/depois (7.0): aqui `updateOffer` só é exercido sobre rascunho, mas a regra de domínio é a
mesma para qualquer estado.

## Decisões fechadas

- Limites de DP-03; as duas formas do PRD (R$ 497,00 por 12 meses e R$ 897,00 vitalícia) coexistem como
  rascunho (RN-O07).
- Conteúdo validado no domínio; o contrato só repete o formato (`techspec.md#mapeamento-do-contrato-de-api`,
  "Validações além do contrato").
- Moeda implícita BRL nas interfaces HTTP (C-05).

## Modificar / Referenciar

- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Api/Extensions/EndpointExtensions.cs`, `ExceptionHandlers/GlobalExceptionHandler.cs` (códigos de oferta)
- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Infra.Data/CommerceDbContext.cs` (ofertas); migration via `dotnet ef migrations add`
- **modificar:** `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/EndpointExtensions.cs` (rotas de oferta)
- **ref:** `internal-api-contract-commerce.yaml` e `api-contract.yaml` (`createOffer`, `updateOffer`, `deleteOffer`, exemplos e limites); `src/admin-spa/src/features/course-authoring/**` (formulários, confirmação, idempotência por intenção); `docs/design/wireframes-catalogo-vitrine.md` e Figma aprovado; skills `dotnet` e `react`

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
| `src/admin-spa` | `npm --prefix src/admin-spa run test -- catalog-course` | exit 0 (lista e ficha sem regressão) | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run build -- --base=/admin/` | exit 0 | `ci-react-ts.yml` |

Cobertura agregada ≥ 70%, `dotnet publish` e imagens ficam para a validação full.

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 29 testes.
- [ ] Valores-limite: preço 1 e 9 999 999, vigência 1 e 60 meses aceitos; preço 0, 10 000 000, vigência 0 e 61 recusados; o 51º rascunho → `FIELD_INVALID` em `offers`.
- [ ] Criações concorrentes com a mesma chave geram uma só oferta; edição idêntica não muda `offerRevision`; exclusão de oferta não rascunho → 422; nada no outbox em nenhuma operação.
- [ ] SPA: R$ 497,00 → 49700 e "497,005" recusado, sem ponto flutuante; erro no campo nomeado; confirmação de exclusão.
- [ ] Smoke no Compose: as duas ofertas do PRD coexistem como rascunho na ficha `http://localhost:8081/admin/catalogo/{courseId}` e um rascunho excluído deixa de existir.
