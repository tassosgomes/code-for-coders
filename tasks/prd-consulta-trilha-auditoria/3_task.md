---
status: in_progress
task_kind: vertical
blocked_by: ["2.0"]
gate: 'dotnet test --project src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj -- --filter-class CodeForCoders.Identity.IntegrationTests.AuditReferenceAccessTests --minimum-expected-tests 7 && dotnet test --project src/audit/tests/CodeForCoders.Audit.IntegrationTests/CodeForCoders.Audit.IntegrationTests.csproj -- --filter-class CodeForCoders.Audit.IntegrationTests.AuditRecordSearchTests --minimum-expected-tests 12 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/CodeForCoders.BffAdmin.EndToEndTests.csproj -- --filter-class CodeForCoders.BffAdmin.EndToEndTests.AuditRecordSearchTests --minimum-expected-tests 8 && npm --prefix src/admin-spa run test -- audit-trail-list'
gate_expect: "Pelo menos 32 testes passam: 7 Identity, 12 Audit, 8 BFF, 5 SPA"
---

# 3.0 Administrador abre a trilha, filtra e percorre páginas estáveis; outros papéis e tenants são barrados

**Fatia:** V-01 · **Cobre:** RF-01, RF-02, RF-05 (indicador na lista), US-01, US-04, RN-A04, RN-A06, RN-A08 a RN-A10, RN-A12 a RN-A14, Identidade RN-13, RN-14, RN-16 a RN-20, RN-23 a RN-25, DP-02 · **Spec:** `techspec.md#v-01-administrador-entra-na-lista-e-percorre-resultados-estáveis` · **ADR:** [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md), [ADR-0007](../../docs/adr/0007-snapshots-efemeros-da-consulta-de-auditoria.md)

## Comportamento

- **Identity — audiência `audit`:** o `bff-admin` passa a obter JWT curto de audiência `audit` com
  escopo `audit-records:read` (local e Coolify), carregando tenant e papel vigentes. Outro emissor
  pedindo audiência `audit` continua recusado como em CAP-002.
- **Identity — `resolveAuditIdentityReferencesInternal`:** aceita somente asserção do `bff-admin`
  com escopo `audit-references:read` e `X-Staff-Session` de administrador vigente; devolve rótulo
  de `conta-interna` (inclusive conta desativada) e `convite-interno` **do tenant da sessão**. ID de
  outro tenant e ID inexistente produzem a mesma ausência. Asserção inválida ou sessão revogada →
  401 `SERVICE_UNAUTHORIZED`; sem escopo ou sem papel administrador → 403 `PERMISSION_DENIED`; mais
  de 50 referências → rejeitado pelo schema. A operação nasce aqui porque a lista mostra nomes
  (TechSpec 1.1); 4.0 a reutiliza no detalhe.
- **Audit — `listAuditRecordsInternal`:** valida localmente JWT (JWKS, issuer, audience `audit`,
  expiração, escopo, papel administrador, tenant) antes de qualquer consulta; usa **só** o tenant do
  token. Sem token, expirado, forjado ou audiência errada → 401; sem papel administrador → 403
  `PERMISSION_DENIED`. A primeira página seleciona numa visão consistente os IDs de **originais**
  elegíveis do tenant, ordenados por `coalesce(praticado_em, recebido_em)` e ID decrescentes, e
  guarda IDs e metadados no Valkey por 30 min absolutos, vinculados a tenant, sessão, filtros
  normalizados e `_size`. Filtros combinam por interseção; período filtra `praticado_em` com as duas
  bordas inclusivas e `null` não satisfaz período informado; autor/alvo casam originais não
  conformes que ainda trazem a referência; conformidade filtra `compliant`. Páginas seguintes leem
  a mesma seleção: um ato retroativo inserido entre páginas não repete nem some da sequência.
  Complementos nunca são linhas; `hasComplements` vem da relação atual. Intervalo invertido ou
  snapshot ausente, expirado ou incompatível → 422 `AUDIT_FILTER_INVALID`; falha do Valkey nunca
  devolve página incompleta. Nenhuma resposta traz dado pessoal de Identity.
- **BFF — `listAuditRecords` (POST):** revalida a sessão em Identity em cada chamada e exige
  CSRF da sessão; sessão ausente/encerrada (inclusive papel revogado com a área aberta) → 401
  `SESSION_REQUIRED` na próxima ação; sem papel administrador ou sem CSRF → 403 `PERMISSION_DENIED`
  **sem chamar `audit`**. Com administrador, pede o JWT `audit`, repassa, e resolve em Identity os
  rótulos das referências distintas da página (lotes de até 50), só na resposta. Falha transitória
  de Identity entrega a página sem `label`; 401/403 de Identity fecha a resposta. O cliente HTTP
  real de `audit` e o de Identity são exercitados contra fronteiras controladas no teste.
- **SPA:** item **Auditoria** abaixo de Acessos e card no Início (A5) somente quando a sessão traz
  papel `administrador` (não permissão). Rota `/admin/auditoria` recusa acesso direto dos demais
  papéis com B12; 401 leva a B1. A lista segue A1 do Figma aprovado: filtros De/Até (data e hora,
  intervalo invertido barrado no cliente), Tipo, abas de conformidade, 20 por página, linha
  "resultado fixado às hh:mm · *Atualizar*", nomes de autor/alvo ou "Nome não disponível" +
  referência curta, `ComplianceBadge` e "Complementado". Mudar filtro ou `_size` descarta o snapshot
  e volta à página 1; 422 de snapshot refaz a busca na página 1 com aviso (A1.e2), sem misturar
  snapshots. Vazio (A1.c/A1.d) mantém os filtros e oferece *Limpar filtros*; carregando (A1.h) e
  erro (A1.i). Filtros não vão para a URL. Logout limpa o cache de consulta. Operável por teclado,
  com `caption`, `Label` e `aria-live` do total.

## Fora do escopo desta task

Detalhe e navegação lista → detalhe → "ver atos desta pessoa" (4.0). O chip de autor/alvo (A1.b) e
o aviso de busca reiniciada (A1.f) dependem do detalhe e entram em 4.0; a API já aceita `authorId`
e `targetId` aqui. Complemento (5.0, 6.0).

## Decisões fechadas

- Snapshot de IDs em Valkey no `audit`, TTL, vínculo e falha: ADR-0007.
- Sessão vigente na borda e JWT curto validado pelo serviço dono: ADR-0005.
- Busca por POST com filtros no corpo e CSRF: contratos 1.1.0 (`contracts.md`).
- Nomes na lista resolvidos pelo BFF: TechSpec 1.1, decisão 2 do wireframe.
- Namespace/credencial de Valkey próprios de `audit`; recursos de teste com nome por execução.

## Modificar / Referenciar

- **modificar:** `src/identity/src/CodeForCoders.Identity.Api/Extensions/EndpointExtensions.cs`, `src/identity/src/CodeForCoders.Identity.Api/Security/ServiceAssertionVerifier.cs`, `src/identity/src/CodeForCoders.Identity.Api/appsettings.json`
- **modificar:** `src/audit/src/CodeForCoders.Audit.Api/Extensions/EndpointExtensions.cs`, `src/audit/src/CodeForCoders.Audit.Api/Extensions/ServiceConfigurationExtensions.cs`, `src/audit/src/CodeForCoders.Audit.Api/appsettings.json`, `src/audit/src/CodeForCoders.Audit.Infra.Data/Configuration/AuditRecordConfiguration.cs`, `src/audit/src/CodeForCoders.Audit.Infra.Data/AuditDbContext.cs` (índices por tenant/filtros/ordem; migration via `dotnet ef migrations add`)
- **modificar:** `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/EndpointExtensions.cs`, `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/ServiceConfigurationExtensions.cs`, `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/StaffIdentityExtensions.cs`, `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Security/ServiceAssertionTokenFactory.cs`, `src/bff-admin/src/CodeForCoders.BffAdmin.Api/appsettings.json`
- **modificar:** `src/admin-spa/src/config/paths.ts`, `src/admin-spa/src/app/router.tsx`, `src/admin-spa/src/features/staff-session/utils/get-staff-areas.ts`, `src/admin-spa/src/app/routes/admin-layout-route.tsx`, `src/admin-spa/src/components/app-shell.tsx`, `src/admin-spa/src/testing/handlers.ts`
- **modificar:** `docker-compose.yml`, `docker-compose.coolify.yml` (`AudienceScopes__audit`, escopo de lookup, JWKS e Valkey de `audit`, endereço de `audit` no BFF)
- **ref:** `api-contract.yaml`, `internal-api-contract-audit.yaml`, `internal-api-contract-identity.yaml`; `techspec.md#backend` (Autorização, Lista e detalhe, Rótulos); `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Security/BffSecurityMiddleware.cs` (CSRF em POST); `src/audit/src/CodeForCoders.Audit.Infra.Data/Configuration/AuditDatabasePermissions.sql`; `docs/design/wireframes-auditoria.md` e Figma aprovado; skills `dotnet` e `react`; Context7 para JWT Bearer/JWKS e cliente Valkey

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/identity` | `dotnet format src/identity/CodeForCoders.Identity.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/identity` | `dotnet build src/identity/CodeForCoders.Identity.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/identity` | `dotnet test --project src/identity/tests/CodeForCoders.Identity.EndToEndTests/CodeForCoders.Identity.EndToEndTests.csproj` | exit 0 (aplicação parte com os registros reais) | `ci-dotnet.yml` Testes |
| `src/audit` | `dotnet format src/audit/CodeForCoders.Audit.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/audit` | `dotnet build src/audit/CodeForCoders.Audit.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/audit` | `dotnet test --project src/audit/tests/CodeForCoders.Audit.ArchitectureTests/CodeForCoders.Audit.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/audit` | `dotnet test --project src/audit/tests/CodeForCoders.Audit.EndToEndTests/CodeForCoders.Audit.EndToEndTests.csproj` | exit 0 (aplicação parte com JWT e Valkey reais registrados) | `ci-dotnet.yml` Testes |
| `src/bff-admin` | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-admin` | `dotnet build src/bff-admin/CodeForCoders.BffAdmin.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/bff-admin` | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.ArchitectureTests/CodeForCoders.BffAdmin.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run build` | exit 0 | `ci-react-ts.yml` |

Cobertura agregada ≥ 70%, `dotnet publish` e imagens ficam para a validação full.

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 32 testes.
- [ ] `audit` chamado direto: sem papel administrador → 403; audiência errada ou token forjado → 401; registros de outro tenant nunca aparecem nem alteram `total`.
- [ ] Ato retroativo inserido entre a página 1 e a 2 não repete nem perde IDs; as duas bordas do período entram; `praticado_em` nulo fica fora de período informado; snapshot expirado → 422 e a UI volta à página 1 com aviso.
- [ ] BFF: professor recebe 403 sem que `audit` seja chamado; papel revogado com a área aberta → próxima página recusada; Identity indisponível → nomes ausentes, página entregue; Identity 403 → resposta fechada.
- [ ] Smoke no Compose: em `http://localhost:8081/admin/auditoria` o administrador do seed vê a trilha com os atos de convite/concessão já gravados e nomes; um professor não vê o item e, pela URL direta, cai em B12.
