---
status: pending
task_kind: vertical
blocked_by: ["2.0"]
gate: 'dotnet test --project src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj -- --filter-class CodeForCoders.Identity.IntegrationTests.MediaAudienceTests --minimum-expected-tests 3 && dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.VideoLibraryAuthorizationTests --minimum-expected-tests 7 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/CodeForCoders.BffAdmin.EndToEndTests.csproj -- --filter-class CodeForCoders.BffAdmin.EndToEndTests.VideoLibraryTests --minimum-expected-tests 5 && npm --prefix src/admin-spa run test -- videos-area'
gate_expect: 'Pelo menos 18 testes passam: 3 Identity, 7 Media, 5 BFF, 3 SPA'
---

# 3.0 Professor abre a área Vídeos, e quem não tem a permissão é barrado nas três camadas

**Fatia:** V-01 · **Cobre:** RF-01, RF-06 (lista vazia e tenant), RF-08 (`getVideo`), RN-12, RN-16, RN-17, RN-18, RN-M02, RN-M03, RN-M04, US-04, US-07, DP-03, DP-05, C-14 · **Spec:** `techspec.md` § V-01 · **ADR:** [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md)

## Comportamento

- **Identity:** `midia.enviar` entra no catálogo, só no papel professor (administrador, financeiro e
  suporte não a têm — DP-05). A audiência `media` passa a ser emitida para o `bff-admin`, com escopo
  `videos:write`; JWT pedido com `audience: media` carrega `permissions` vigentes. Audiência `media`
  pedida por outro emissor não autorizado continua recusada como em CAP-002.
- **Contratos de CAP-002 (C-14):** `midia.enviar` entra no enum `Permission` de
  `tasks/prd-acesso-interno/api-contract.yaml` e `internal-api-contract.yaml`, versão 1.0.0 → 1.1.0
  (aditivo), com `contracts.md` de CAP-002 registrando a evolução.
- **Media:** valida localmente o JWT do ator interno pelo JWKS de Identity (emissor `identity`,
  audiência `media`, validade, assinatura), no mesmo desenho do Commerce de CAP-002. Toda operação de
  vídeo exige `midia.enviar`: sem ela → 403 `PERMISSION_DENIED`; token ausente, expirado, forjado ou com
  audiência `commerce` → 401 `TOKEN_INVALID`. `listVideosInternal` devolve a página de vídeos **do
  tenant do token** (vazia nesta task), ordenada por envio decrescente, com `_page`/`_size` até 50;
  `getVideoInternal` de id inexistente ou de outro tenant → 404 `VIDEO_NOT_FOUND`. A tabela de vídeos
  nasce com tenant e sem nenhuma noção de curso ou aula (RN-M02). JWKS indisponível fecha o acesso sem
  derrubar o health.
- **BFF:** `listVideos` e `getVideo` recusam com 403 `PERMISSION_DENIED` **antes** de chamar Media
  quando a sessão validada não tem `midia.enviar`; com a permissão, pedem o JWT com `audience: media` e
  repassam. Sessão ausente ou encerrada (inclusive por revogação de papel — RN-17) → 401
  `SESSION_REQUIRED` na próxima ação. Media fora do ar ou 5xx → 502 `MEDIA_UNAVAILABLE`; tempo esgotado
  → 504 `MEDIA_UNAVAILABLE` (C-16), sem detalhe interno.
- **SPA:** área Vídeos no menu, com nome e posição decididos em 1.0, só para quem tem `midia.enviar`;
  rota `/videos` protegida pelo loader de sessão; lista vazia no desenho aprovado em 2.0; link direto sem
  a permissão → tela "sem permissão" de CAP-002.

## Fora do escopo desta task

Envio (4.0), estados além da lista vazia (7.0/8.0), filtro e busca (9.0). Preparação e worker.

## Decisões fechadas

- Validação local por JWKS e claims do ator interno: ADR-0005. Autorização dupla, borda e serviço: PRD, Restrições Técnicas.
- Permissão e audiência: C-14; ordem de implantação Identity antes do BFF expor a área (`contracts.md`, Pendências 3).
- Códigos 502/504 `MEDIA_UNAVAILABLE`: C-16.
- Migrations de Media pelo tooling do EF (`dotnet ef migrations add`), nunca à mão.

## Modificar / Referenciar

- **modificar:** `src/identity/src/CodeForCoders.Identity.Domain/Entities/StaffRoleCatalog.cs`; `tasks/prd-acesso-interno/api-contract.yaml`, `tasks/prd-acesso-interno/internal-api-contract.yaml`, `tasks/prd-acesso-interno/contracts.md`
- **modificar:** `src/media/src/CodeForCoders.Media.Api/Extensions/ServiceConfigurationExtensions.cs`, `src/media/src/CodeForCoders.Media.Api/Program.cs`, `src/media/src/CodeForCoders.Media.Api/appsettings.json`, `src/media/src/CodeForCoders.Media.Infra.Data/DependencyInjection.cs` (desligar `EnableSensitiveDataLogging` — techspec, Riscos)
- **modificar:** `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/ServiceConfigurationExtensions.cs`, `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/EndpointExtensions.cs`, `src/bff-admin/src/CodeForCoders.BffAdmin.Api/appsettings.json`
- **modificar:** `src/admin-spa/src/config/paths.ts`, `src/admin-spa/src/features/staff-session/utils/get-staff-areas.ts`, `src/admin-spa/src/app/router.tsx`, `src/admin-spa/src/testing/handlers.ts`
- **modificar:** `docker-compose.yml`, `docker-compose.coolify.yml` (`StaffSessionTokens__AudienceScopes__media`, configuração de JWT em Media, `Media__BaseAddress` no BFF)
- **ref:** `src/commerce/src/CodeForCoders.Commerce.Api/Security/FinanceAreaJwksConfigurationManager.cs`, `FinanceAreaJwtBearerOptionsSetup.cs`; `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/FinanceAreaEndpoints.cs`; `src/identity/src/CodeForCoders.Identity.Api/Security/StaffSessionTokenIssuer.cs`
- **ref:** `internal-api-contract.yaml` 1.0.1, `api-contract.yaml` 1.1.1; `docs/design/wireframes-videos.md` e Figma aprovado; skills `dotnet` e `react`; Context7 para JWT Bearer com JWKS

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/identity` | `dotnet format src/identity/CodeForCoders.Identity.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/identity` | `dotnet build src/identity/CodeForCoders.Identity.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/identity` | `dotnet test --project src/identity/tests/CodeForCoders.Identity.UnitTests/CodeForCoders.Identity.UnitTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/media` | `dotnet build src/media/CodeForCoders.Media.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.ArchitectureTests/CodeForCoders.Media.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.EndToEndTests/CodeForCoders.Media.EndToEndTests.csproj` | exit 0 (aplicação parte com os registros reais) | `ci-dotnet.yml` Testes |
| `src/bff-admin` | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-admin` | `dotnet build src/bff-admin/CodeForCoders.BffAdmin.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/bff-admin` | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.ArchitectureTests/CodeForCoders.BffAdmin.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run build` | exit 0 | `ci-react-ts.yml` |
| Contratos CAP-002 | `npx --yes @stoplight/spectral-cli@6.15.0 lint tasks/prd-acesso-interno/api-contract.yaml --ruleset .claude/skills/tsg-flow-contract-creator/rulesets/openapi.yaml --fail-severity=error` | exit 0 | `contracts.md` § Validação |
| Contratos CAP-002 | mesmo comando sobre `tasks/prd-acesso-interno/internal-api-contract.yaml` | exit 0 | `contracts.md` § Validação |

Cobertura agregada ≥ 70%, `dotnet publish` e imagem ficam para a validação full.

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 18 testes.
- [ ] Media chamado direto: JWT sem `midia.enviar` → 403; audiência `commerce`, expirado ou forjado → 401; vídeo de outro tenant → 404.
- [ ] BFF: suporte recebe 403 sem que Media seja chamado; Media fora → 502 `MEDIA_UNAVAILABLE`. O cliente HTTP real do BFF é exercitado contra a fronteira de Media controlada no teste.
- [ ] Smoke no Compose: o administrador do seed convida um professor e um suporte (e-mail no smtp4dev), os dois aceitam; em `http://localhost:8081/admin/videos` o professor vê a área e "nenhum vídeo ainda", e o suporte não vê o item nem abre por link.
