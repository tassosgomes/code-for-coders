---
status: done
task_kind: vertical
blocked_by: ["8.0"]
gate: 'dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.OfferReferenceResolutionTests --minimum-expected-tests 5 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj -- --filter-class CodeForCoders.BffAdmin.IntegrationTests.OfferAuditReferenceTests --minimum-expected-tests 5 && npm --prefix src/admin-spa run test -- audit-trail-offer'
gate_expect: "Pelo menos 16 testes passam: 5 de integração de commerce, 5 do BFF, 6 do SPA"
---

# 12.0 O administrador vê os atos de oferta na trilha com rótulo legível

**Fatia:** V-10 · **Cobre:** RF-10, US-08, RN-A14, C-09, C-11 · **Spec:** `techspec.md#v-10` · **ADR:** [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md)

## Comportamento

- **`commerce` — `resolveOfferReferencesInternal`:** papel `administrador` no claim `roles` (sem ele, 403
  `PERMISSION_DENIED`), em lotes de até 50 ofertas, da escola do `tenantId`: devolve o rótulo "curso —
  opção" de cada oferta encontrada; oferta desconhecida, excluída ou de outra escola fica **sem rótulo**,
  sem erro e indistinguível entre os casos.
- **`bff-admin` — consulta da trilha (CAP-030):** resolve os alvos `oferta` das páginas da lista e do detalhe
  por esse resolvedor e insere o rótulo, no mesmo desenho do enriquecimento de curso; nenhum schema de
  CAP-030 muda (`type` e `label` já são texto livre e opcional). Falha do resolvedor não derruba a consulta:
  o alvo segue sem rótulo.
- **`admin-spa`:** a consulta da trilha ganha os rótulos *Oferta publicada*, *Oferta alterada* e *Oferta
  despublicada*, as três opções de filtro de tipo, o tipo de referência "Oferta", a formatação dos atributos
  (`precoAnterior`/`precoNovo` em reais; `vigenciaAnterior`/`vigenciaNova` como "12 meses" ou "Vitalícia";
  `curso` como identificador) e o texto "Não se aplica a este tipo" para o motivo dos três tipos. Conforme o
  Figma aprovado em 2.0.
- **Caso negativo principal:** ato de oferta excluída ou desconhecida mostra o alvo sem rótulo e sem erro;
  nenhum ato de oferta aparece como "motivo ausente" nem como tipo desconhecido; chamada ao resolvedor sem o
  papel administrador → 403.

## Fora do escopo desta task

Produzir os atos (6.0 a 8.0, já gravados conformes em `audit`); qualquer mudança de schema de CAP-030.

## Decisões fechadas

- Rótulo da oferta resolvido por `commerce`, não por `audit` (C-11), mesmo padrão do enriquecimento de curso
  de CAP-005.
- Motivo não se aplica aos três tipos (OD50).

## Modificar / Referenciar

- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Api/Extensions/EndpointExtensions.cs` (resolvedor), `ServiceConfigurationExtensions.cs` (política do papel administrador)
- **modificar:** `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/AuditRecordEndpoints.cs` (enriquecer alvo `oferta` na lista e no detalhe)
- **modificar:** `src/admin-spa/src/features/audit-trail/components/audit-trail-screen.tsx` (`typeOptions`), `audit-record-detail-screen.tsx` (`typeLabels`, motivo, `attributeLabel`, tipo de referência)
- **ref:** `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Clients/CourseAuditReferenceEnricher.cs` (padrão de enriquecimento); `internal-api-contract-commerce.yaml` (`resolveOfferReferencesInternal`); `asyncapi-contract-audit.yaml` (complemento dos atos); `tasks/prd-consulta-trilha-auditoria/contracts.md` (contrato de CAP-030); `docs/design/wireframes-catalogo-vitrine.md` e Figma aprovado; skills `dotnet` e `react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/commerce` | `dotnet format src/commerce/CodeForCoders.Commerce.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/commerce` | `dotnet build src/commerce/CodeForCoders.Commerce.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.ArchitectureTests/CodeForCoders.Commerce.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/bff-admin` | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-admin` | `dotnet build src/bff-admin/CodeForCoders.BffAdmin.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/bff-admin` | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj` | exit 0 (consulta da trilha sem regressão) | `ci-dotnet.yml` Testes |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run test -- audit-trail` | exit 0 (trilha sem regressão) | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run build -- --base=/admin/` | exit 0 | `ci-react-ts.yml` |

Cobertura agregada ≥ 70%, `dotnet publish` e imagens ficam para a validação full.

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 16 testes.
- [ ] Composição: o cliente HTTP real do resolvedor em `bff-admin` é exercitado contra a fronteira controlada do teste, e lote de 50 ofertas é aceito.
- [ ] Chamada ao resolvedor sem o papel administrador → 403; com JWT de outra escola → sem rótulos da outra escola.
- [ ] Smoke no Compose: atos reais de 6.0, 7.0 e 8.0 consultados em `http://localhost:8081/admin/auditoria` como administrador mostram rótulo "curso — opção", anterior → novo em *Oferta alterada* e as três opções de filtro; ato de oferta excluída mostra o alvo sem rótulo e sem erro.
