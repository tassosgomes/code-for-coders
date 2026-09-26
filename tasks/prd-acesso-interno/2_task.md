---
status: done
task_kind: vertical
blocked_by: ["1.0"]
gate: 'dotnet test --project src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj -- --filter-class CodeForCoders.Identity.IntegrationTests.FirstAdministratorProvisioningTests --minimum-expected-tests 4 && dotnet test --project src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj -- --filter-class CodeForCoders.Identity.IntegrationTests.StaffPasswordResetTests --minimum-expected-tests 3 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/CodeForCoders.BffAdmin.EndToEndTests.csproj -- --filter-class CodeForCoders.BffAdmin.EndToEndTests.StaffPasswordResetTests --minimum-expected-tests 2 && npm --prefix src/admin-spa run test -- -t StaffPasswordReset'
gate_expect: 'Pelo menos 10 testes passam: 4 provisionamento e 3 redefinição em Identity, 2 no BFF, 1 no SPA'
---

# 2.0 Primeiro administrador provisionado define a senha pelo link recebido

**Fatia:** V-01 · **Cobre:** RF-02, RF-11 (redefinição), RF-01 (papel administrador), RN-06, RN-07, RN-13a, RN-25, RN-A12 · **Spec:** `techspec.md#v-01-primeiro-administrador-provisionado-define-a-senha-pelo-link-recebido` · **ADR:** [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md)

## Comportamento

**Provisionamento.** O binário de Identity aceita o comando `provision-first-admin --tenant <id>
--email <e-mail> --name <nome>` e, nesse modo, executa e termina sem subir a API.

- Tenant sem conta interna com papel administrador: num commit cria a conta `InternalActor`
  confirmada **sem credencial**, a atribuição do papel `administrador`, um segredo de redefinição
  (só o hash persistido) e o pedido `recuperacao-de-senha` no outbox, protegido, com link
  `{StaffAccount:PasswordResetBaseUrl}?token=…` (local:
  `http://localhost:8081/admin/redefinir-senha?token=…`). Código de saída 0.
- Tenant que já tem administrador: não cria nem altera nada; informa e sai com 0.
- E-mail de conta de aluno (ou de outra conta interna): recusa, sai com código diferente de 0,
  nada gravado.
- **Nenhum ato** é gravado para a Auditoria (RN-25, RN-A12). O comando não imprime e-mail, link nem
  segredo; registra data e tenant.

Nesta task nasce o modelo mínimo que o seed usa: conta interna, atribuição de papel com índice
único por (tenant, conta, papel) e o catálogo de permissões fixo (DP-03). Migration pelo EF.

**Redefinição interna** (`resetStaffPassword` → `resetStaffPasswordInternal`). A página
`/admin/redefinir-senha` lê o token da URL, remove-o do histórico e envia a nova senha no corpo. Identity
consome o segredo, aplica a política de CAP-001, grava a credencial (inclusive quando ainda não havia),
revoga todas as sessões internas da conta e invalida recuperações pendentes, num commit. Segredo
usado, expirado ou de aluno → 422 `RESET_TOKEN_INVALID`; senha fraca → 422
`PASSWORD_POLICY_VIOLATION`. 204 em sucesso. A página mostra o sucesso com link para
`/admin/entrar` (a entrada em si chega em 3.0).

**Borda.** O `bff-admin` passa a ter a fábrica de asserção de serviço (emissor `bff-admin`) e o
cliente HTTP de Identity; o nginx do SPA encaminha `/api/v1/` ao `bff-admin` e registra acesso **sem
query string**. O SPA passa a chamar `/api/v1`.

## Fora do escopo desta task

Entrada, sessão e menu (3.0); pedido de recuperação pela tela "esqueci a senha" (9.0); consumo real do
e-mail além do smoke — Notificação 1.0.0 já entrega `recuperacao-de-senha`.

## Decisões fechadas

- Seed por comando do binário, senha pelo fluxo de redefinição: techspec.md, Decisões Técnicas 3.
- Catálogo de permissões em código: techspec.md, Decisões Técnicas 2; DP-03 do PRD.
- Conta interna criada confirmada: techspec.md, Bloco Backend.

## Modificar / Referenciar

- **modificar:** `src/identity/src/CodeForCoders.Identity.Domain/Entities/Account.cs` (fábrica de conta interna confirmada)
- **modificar:** `src/identity/src/CodeForCoders.Identity.Api/Program.cs` (modo de comando)
- **modificar:** `src/identity/src/CodeForCoders.Identity.Api/Extensions/EndpointExtensions.cs`, `src/identity/src/CodeForCoders.Identity.Api/appsettings.json` (`StaffAccount`)
- **modificar:** `src/identity/src/CodeForCoders.Identity.Infra.Data/IdentityDbContext.cs`, `src/identity/src/CodeForCoders.Identity.Infra.Data/DependencyInjection.cs` (entidades novas; migration pelo EF)
- **modificar:** `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/EndpointExtensions.cs`, `src/bff-admin/src/CodeForCoders.BffAdmin.Api/appsettings.json`
- **modificar:** `src/admin-spa/src/app/router.tsx`, `src/admin-spa/src/config/paths.ts`, `src/admin-spa/src/lib/api-client.ts`, `src/admin-spa/src/testing/handlers.ts`, `src/admin-spa/nginx.conf.template`
- **modificar:** `docker-compose.yml` (URL de redefinição do backoffice, `bff-admin` → Identity), `docs/student-registration-local-development.md` (passo do comando)
- **ref:** `src/identity/src/CodeForCoders.Identity.Application/Services/StudentPasswordRecoveryMessageWriter.cs`, `src/identity/src/CodeForCoders.Identity.Application/Common/StudentPasswordPolicy.cs`, `src/bff-student/src/CodeForCoders.BffStudent.Api/Security/ServiceAssertionTokenFactory.cs`, `src/student-spa/nginx.conf.template`, `internal-api-contract.yaml`, `api-contract.yaml`, skills `dotnet`/`react`

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

Cobertura agregada ≥ 70% por componente, `dotnet publish` e build de imagem ficam para a validação full.

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 10 testes.
- [ ] Comando em tenant vazio → conta, papel e pedido gravados; nenhum ato no outbox; repetição não muda nada; e-mail de aluno recusado.
- [ ] Redefinição com segredo válido → 204 e credencial gravada; segundo uso → `RESET_TOKEN_INVALID`.
- [ ] Smoke local: comando → e-mail no smtp4dev → `http://localhost:8081/admin/redefinir-senha?token=…` abre a página e conclui; log do nginx sem o token.
