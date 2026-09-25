---
status: pending
task_kind: vertical
blocked_by: ["2.0"]
gate: 'dotnet test --project src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj -- --filter-class CodeForCoders.Identity.IntegrationTests.StaffSessionTests --minimum-expected-tests 5 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/CodeForCoders.BffAdmin.EndToEndTests.csproj -- --filter-class CodeForCoders.BffAdmin.EndToEndTests.StaffSessionTests --minimum-expected-tests 5 && npm --prefix src/admin-spa run test -- -t StaffSession'
gate_expect: 'Pelo menos 11 testes passam: 5 Identity, 5 BFF, 1 SPA'
---

# 3.0 Ator interno entra, vê as áreas das suas permissões e sai

**Fatia:** V-02 · **Cobre:** RF-10, RF-01, RF-08, RN-05, RN-08, RN-10, RN-11, RN-13, RN-18, US-06 · **Spec:** `techspec.md#v-02-ator-interno-entra-vê-as-áreas-das-suas-permissões-e-sai` · **ADR:** [ADR-0003](../../docs/adr/0003-verificacao-de-sessao-do-aluno.md), [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md)

## Comportamento

- `createStaffSession` → `createStaffSessionInternal`: Identity autentica só conta `InternalActor`
  não desativada, verificando contra hash fictício quando não há credencial. Credencial errada, e-mail
  inexistente e conta de aluno → a mesma 401 `INVALID_CREDENTIALS`. Sucesso cria a **sessão interna**
  (entidade própria, inatividade `StaffSession:InactivityTimeoutMinutes`, padrão 60) e o BFF grava o
  identificador em Valkey e emite o cookie `staff_session`. Conta sem papel entra com
  `permissions: []`.
- Toda ação protegida do `bff-admin`: verifica Valkey **e** chama `validateStaffSessionInternal`
  (ADR-0005), que valida, renova e devolve papéis e permissões vigentes. Sessão revogada/expirada →
  401 `SESSION_REQUIRED`, cookie removido. Identity indisponível → 502/504 `IDENTITY_UNAVAILABLE` sem
  renovar TTL (falha fechada).
- CSRF vinculado à sessão e checagem de origem em toda escrita com sessão, como no `bff-student`; o
  CSRF de cookie duplo do esqueleto sai. `getCurrentStaffSession` entrega `csrfToken`.
- `endCurrentStaffSession` revoga em Identity e remove em Valkey; repetição não reativa.
- O proxy `/proxy` existente deixa de usar token guardado na sessão: usa só o JWT da validação da
  requisição corrente (ou recusa, se não houver).
- SPA: rotas `/admin/entrar` (pública) e início (protegida por loader de sessão). O início mostra as
  áreas conforme `permissions`; conta sem papel vê a orientação de conta sem acesso. Botão sair.
- A entrada do aluno continua recusando conta interna com `INVALID_CREDENTIALS` (teste de regressão).

## Fora do escopo desta task

Áreas "Acessos" e "Financeiro" existem só como itens de menu condicionados à permissão; o conteúdo vem
em 4.0/6.0 e 8.0. JWT com audiência de serviço vem em 8.0.

## Decisões fechadas

- Validação por ação em Identity, sessão interna separada: ADR-0005 (1); techspec.md, Decisões Técnicas 1.
- Conta sem papel entra: C-05 em `contracts.md`.
- Cookie `staff_session`: `api-contract.yaml`.

## Modificar / Referenciar

- **modificar:** `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Security/BffSecurityMiddleware.cs`, `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Security/CsrfProtection.cs`, `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Security/BffSessionTransformProvider.cs`
- **modificar:** `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/SessionEndpoints.cs`, `src/bff-admin/src/CodeForCoders.BffAdmin.Application/Common/OpaqueBffSession.cs`, `src/bff-admin/src/CodeForCoders.BffAdmin.Application/Common/BffSecurityOptions.cs`, `src/bff-admin/src/CodeForCoders.BffAdmin.Api/appsettings.json`
- **modificar:** `src/identity/src/CodeForCoders.Identity.Infra.Data/IdentityDbContext.cs` (sessão interna; migration pelo EF), `src/identity/src/CodeForCoders.Identity.Api/Extensions/EndpointExtensions.cs`, `src/identity/src/CodeForCoders.Identity.Api/appsettings.json` (`StaffSession`)
- **modificar:** `src/admin-spa/src/app/router.tsx`, `src/admin-spa/src/config/paths.ts`, `src/admin-spa/src/components/app-shell.tsx`, `src/admin-spa/src/lib/api-client.ts`, `src/admin-spa/src/testing/handlers.ts`
- **ref:** `src/bff-student/src/CodeForCoders.BffStudent.Api/Security/BffSecurityMiddleware.cs`, `src/identity/src/CodeForCoders.Identity.Application/UseCases/Accounts/AuthenticateStudentSession/AuthenticateStudentSession.cs`, `src/student-spa/src/app/router.tsx`, `internal-api-contract.yaml`, skills `dotnet`/`react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
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
| `src/bff-student` | `dotnet test --project src/bff-student/tests/CodeForCoders.BffStudent.EndToEndTests/CodeForCoders.BffStudent.EndToEndTests.csproj` | exit 0 (regressão de CAP-001) | `ci-dotnet.yml` passo Testes |

Cobertura agregada ≥ 70% por componente, `dotnet publish` e build de imagem ficam para a validação full.

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 11 testes.
- [ ] Senha errada, e-mail inexistente e conta de aluno → respostas idênticas.
- [ ] Conta sem papel entra e o SPA mostra a orientação sem nenhuma área.
- [ ] Sessão revogada em Identity → próxima ação 401 mesmo com o registro ainda em Valkey; Identity fora → 502/504 sem renovar TTL.
- [ ] Escrita sem CSRF → 403 `CSRF_INVALID`.
- [ ] Smoke: administrador de 2.0 entra em `http://localhost:8081/admin/entrar`, vê "Acessos", sai.
