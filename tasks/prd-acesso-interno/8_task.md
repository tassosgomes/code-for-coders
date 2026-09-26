---
status: done
task_kind: vertical
blocked_by: ["3.0"]
gate: 'dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.FinanceAreaAuthorizationTests --minimum-expected-tests 5 && dotnet test --project src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj -- --filter-class CodeForCoders.Identity.IntegrationTests.UserTokenSigningKeysTests --minimum-expected-tests 3 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/CodeForCoders.BffAdmin.EndToEndTests.csproj -- --filter-class CodeForCoders.BffAdmin.EndToEndTests.FinanceAreaTests --minimum-expected-tests 3 && npm --prefix src/admin-spa run test -- -t FinanceArea'
gate_expect: 'Pelo menos 12 testes passam: 5 Commerce, 3 Identity, 3 BFF, 1 SPA'
---

# 8.0 Professor não abre a área financeira por nenhuma via

**Fatia:** V-07 · **Cobre:** RF-13, RN-16, RN-17, RN-18, US-07 · **Spec:** `techspec.md#v-07-professor-não-abre-a-área-financeira-por-nenhuma-via` · **ADR:** [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md)

## Comportamento

- **Identity:** `getUserTokenSigningKeysInternal` (`/internal/v1/jwks`) publica a chave pública atual
  e a anterior por `kid`, nunca material privado. `validateStaffSessionInternal` com `audience:
  commerce` emite JWT RS256 curto com `roles` e `permissions` vigentes; audiência não autorizada para o
  `bff-admin` → 403 `AUDIENCE_NOT_ALLOWED`.
- **Commerce:** `getFinanceAreaInternal` valida o JWT localmente pelo JWKS (emissor `identity`,
  audiência `commerce`, validade, assinatura) e exige `financeiro.ler`. Token ausente, expirado, de
  outra audiência ou assinado por chave desconhecida → 401 `TOKEN_INVALID`; sem a permissão → 403
  `PERMISSION_DENIED`. Resposta `{ "status": "reserved" }`. JWKS indisponível fecha o acesso sem
  derrubar o health do serviço.
- **BFF:** `getFinanceArea` recusa com 403 antes de chamar o Commerce quando a permissão vigente não
  tem `financeiro.ler`; com a permissão, pede o JWT na validação e chama o Commerce.
- **SPA:** item "Financeiro" só aparece com `financeiro.ler`; rota direta de quem não tem → tela "sem
  permissão".
- Revogado o papel financeiro, a próxima chamada é recusada na borda (sessão encerrada, 2.0/6.0).

## Fora do escopo desta task

Qualquer dado financeiro (CAP-011). Rotação real de chaves em produção (plataforma).

## Decisões fechadas

- Commerce dono da área: techspec.md, Decisões Técnicas 5. JWKS sem autenticação: C-09. Documento próprio e códigos: C-10.
- Claims `roles`/`permissions` e validação local: ADR-0005 (4).

## Modificar / Referenciar

- **modificar:** `src/identity/src/CodeForCoders.Identity.Api/Security/StudentSessionTokenIssuer.cs` (claims de ator interno, mesmo `kid`), `src/identity/src/CodeForCoders.Identity.Api/Extensions/EndpointExtensions.cs`
- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Api/Program.cs` e extensões de configuração, `src/commerce/src/CodeForCoders.Commerce.Api/appsettings.json`
- **modificar:** `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/EndpointExtensions.cs`, `src/bff-admin/src/CodeForCoders.BffAdmin.Api/appsettings.json` (cliente Commerce); `src/admin-spa/src/app/router.tsx`, `src/admin-spa/src/config/paths.ts`, `src/admin-spa/src/components/app-shell.tsx`, `src/admin-spa/src/testing/handlers.ts`; `docker-compose.yml` (JWKS e audiência)
- **ref:** `internal-api-contract-commerce.yaml`, `internal-api-contract.yaml` (`getUserTokenSigningKeysInternal`), Context7 para a validação de JWT com JWKS no ASP.NET Core, skills `dotnet`/`react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/commerce` | `dotnet format src/commerce/CodeForCoders.Commerce.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` passo Lint |
| `src/commerce` | `dotnet build src/commerce/CodeForCoders.Commerce.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.ArchitectureTests/CodeForCoders.Commerce.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` passo Testes |
| `src/identity` | `dotnet format src/identity/CodeForCoders.Identity.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` passo Lint |
| `src/identity` | `dotnet build src/identity/CodeForCoders.Identity.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` (build no test) |
| `src/identity` | `dotnet test --project src/identity/tests/CodeForCoders.Identity.ArchitectureTests/CodeForCoders.Identity.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` passo Testes |
| `src/identity` | `dotnet test --project src/identity/tests/CodeForCoders.Identity.UnitTests/CodeForCoders.Identity.UnitTests.csproj` | exit 0 | `ci-dotnet.yml` passo Testes |
| `src/bff-admin` | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` passo Lint |
| `src/bff-admin` | `dotnet build src/bff-admin/CodeForCoders.BffAdmin.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/bff-admin` | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.ArchitectureTests/CodeForCoders.BffAdmin.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` passo Testes |
| `src/bff-admin` | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.UnitTests/CodeForCoders.BffAdmin.UnitTests.csproj` | exit 0 | `ci-dotnet.yml` passo Testes |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run build` | exit 0 | `ci-react-ts.yml` |

Cobertura agregada ≥ 70% por componente, `dotnet publish` e build de imagem ficam para a validação full.

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 12 testes.
- [ ] Token de professor chamado direto no Commerce → 403; token com audiência `identity` → 401; token de chave desconhecida → 401.
- [ ] Financeiro abre a área; professor não vê o item nem abre por link.
- [ ] Smoke: entrar como financeiro e como professor em `http://localhost:8081/admin/financeiro`.
