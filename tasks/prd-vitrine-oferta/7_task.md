---
status: in_progress
task_kind: vertical
blocked_by: ["6.0"]
gate: 'dotnet test --project src/commerce/tests/CodeForCoders.Commerce.UnitTests/CodeForCoders.Commerce.UnitTests.csproj -- --filter-class CodeForCoders.Commerce.UnitTests.OfferChangeRulesTests --minimum-expected-tests 6 && dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.OfferChangeTests --minimum-expected-tests 6 && dotnet test --project src/audit/tests/CodeForCoders.Audit.UnitTests/CodeForCoders.Audit.UnitTests.csproj -- --filter-class CodeForCoders.Audit.UnitTests.OfferChangedActPolicyTests --minimum-expected-tests 3 && npm --prefix src/admin-spa run test -- catalog-offer-price-change'
gate_expect: "Pelo menos 19 testes passam: 6 unitários e 6 de integração de commerce, 3 unitários de audit, 4 do SPA"
---

# 7.0 O financeiro altera preço ou vigência de oferta publicada, só para compras futuras

**Fatia:** V-05 · **Cobre:** RF-05, US-03, RN-O09 (forma congelável), RN-O10, RN-O15, DP-05, C-09 · **Spec:** `techspec.md#v-05` · **ADR:** [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md)

## Comportamento

- **`commerce` — `updateOfferInternal` sobre oferta `published`:** o agregado compara com o gravado.
  Mudança de preço e/ou vigência incrementa `offerRevision` e grava, na mesma transação, o fato
  `catalogo.oferta-alterada.v1` (`OfertaAlteradaPayload`, com o bloco `previous`) e o ato `oferta-alterada`
  com `precoAnterior`/`precoNovo` (centavos, em texto) e/ou `vigenciaAnterior`/`vigenciaNova` (`12m` ou
  `vitalicia`), **só do que mudou**. Preço e vigência mudando na mesma chamada geram **um** ato com os dois
  pares. Mudança só do nome grava e não emite nada. Valores idênticos não mudam nada (sem revisão, fato ou
  ato). Oferta em rascunho ou despublicada edita em silêncio, sem fato nem ato (DP-05). Idempotência e
  limites como em 5.0.
- **`audit` 1.3.0:** aceita o ato `oferta-alterada` com os pares de complemento e o grava conforme; par
  incompleto ou formato de vigência fora de `12m`/`vitalicia` é não conforme.
- **`admin-spa`:** alterar preço ou vigência de oferta publicada mostra antes e depois e a frase "vale para
  compras futuras e não altera quem já comprou"; alterar só o nome não pede essa confirmação; oferta em
  rascunho ou despublicada edita sem a confirmação. Conforme o Figma aprovado em 2.0.
- **Caso negativo principal:** só nome → nenhuma mensagem no outbox; valores idênticos → nenhuma mudança;
  vigência sem mudança de tipo e duração não gera par.

## Fora do escopo desta task

A vitrine pública mostrando o valor novo (evidência em 9.0 e 10.0, que dependem desta regra); rótulos do ato
na trilha (12.0); qualquer efeito sobre quem já comprou (não existe pedido nesta entrega).

## Decisões fechadas

- Mudança só de nome não é ato auditado; preço e vigência são (DP-05).
- O fato leva nome e preço; o ato não leva nenhum dos dois além dos pares do que mudou
  (`techspec.md#contratos-e-fronteiras`).
- Preço e vigência mudados juntos geram um único ato (`techspec.md#mapeamento-do-contrato-de-api`).

## Modificar / Referenciar

- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Api/Extensions/EndpointExtensions.cs` (`updateOffer` em oferta publicada)
- **modificar:** `src/audit/src/CodeForCoders.Audit.Domain/Policies/AdministrativeActPolicy.cs` (complemento do ato `oferta-alterada`, somente se a validação dos pares não estiver coberta por 6.0)
- **ref:** `asyncapi-contract.yaml` (`OfertaAlteradaPayload`) e `asyncapi-contract-audit.yaml` (exemplos dos pares); `internal-api-contract-commerce.yaml` e `api-contract.yaml` (`updateOffer`); `src/admin-spa/src/features/course-authoring/**` (confirmação por intenção); `docs/design/wireframes-catalogo-vitrine.md` e Figma aprovado; skills `dotnet` e `react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/commerce` | `dotnet format src/commerce/CodeForCoders.Commerce.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/commerce` | `dotnet build src/commerce/CodeForCoders.Commerce.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.ArchitectureTests/CodeForCoders.Commerce.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/audit` | `dotnet format src/audit/CodeForCoders.Audit.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/audit` | `dotnet build src/audit/CodeForCoders.Audit.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/audit` | `dotnet test --project src/audit/tests/CodeForCoders.Audit.IntegrationTests/CodeForCoders.Audit.IntegrationTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run test -- catalog-` | exit 0 (Catálogo sem regressão) | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run build -- --base=/admin/` | exit 0 | `ci-react-ts.yml` |

`bff-admin` não muda nesta fatia: a rota de `updateOffer` já nasceu em 5.0 e é repassada sem distinção de
estado. Cobertura agregada ≥ 70%, `dotnet publish` e imagens ficam para a validação full.

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 19 testes.
- [ ] R$ 497,00 → R$ 397,00: fato `oferta-alterada` com `previous` e ato com os pares de preço; 12 meses → vitalícia: só o par de vigência; preço e vigência juntos: **um** ato com os dois pares; só nome: nenhuma mensagem; valores idênticos: nenhuma mudança.
- [ ] Payloads capturados do outbox e do broker validados contra `OfertaAlteradaPayload` e `AtoPraticadoPayload` 1.3.0.
- [ ] SPA: o texto "compras futuras" aparece na confirmação de preço ou vigência e não na de nome.
- [ ] Smoke no Compose: alterar o preço de uma oferta publicada em `http://localhost:8081/admin/catalogo/{courseId}` faz o ato `oferta-alterada` aparecer na trilha com anterior e novo.
