Run: run.sx6ueL80

# Relatório da task 9.0

## Resultado

Implementação concluída para recuperação autônoma de senha de ator interno. O BFF e Identity respondem `202` sem corpo para conta interna, conta de aluno e e-mail inexistente. Identity só cria token de uso único e mensagem `recuperacao-de-senha` para conta interna ativa. O envio reutiliza o link staff de redefinição e aplica idempotência de 24 horas.

A SPA agora tem `/admin/recuperar-senha`, valida e-mail até 254 caracteres, envia `Idempotency-Key` e apresenta confirmação neutra. O link da tela de entrada usa o texto **“Esqueci a senha”** e aponta para a nova rota.

## Arquivos de implementação e teste

- Identity: `src/identity/src/CodeForCoders.Identity.Application/UseCases/Accounts/RequestStaffPasswordReset/` (caso de uso, contrato de entrada/saída e validator); `src/identity/src/CodeForCoders.Identity.Application/DependencyInjection.cs`; `src/identity/src/CodeForCoders.Identity.Application/Interfaces/IIdentityPasswordRecoveryStore.cs`; `src/identity/src/CodeForCoders.Identity.Infra.Data/Accounts/IdentityPasswordRecoveryStore.cs`; `src/identity/src/CodeForCoders.Identity.Api/Endpoints/StaffPasswordRecoveryEndpoints.cs`; `src/identity/src/CodeForCoders.Identity.Api/Extensions/EndpointExtensions.cs`; `src/identity/src/CodeForCoders.Identity.Contracts/StaffPasswordRecoveryRequestV1.cs`; `src/identity/tests/CodeForCoders.Identity.IntegrationTests/StaffPasswordRecoveryRequestTests.cs`.
- BFF: `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/StaffPasswordRecoveryEndpoints.cs`; `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Clients/IStaffPasswordResetIdentityClient.cs`; `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Clients/StaffPasswordResetIdentityClient.cs`; `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Clients/StaffPasswordRecoveryIdentityResult.cs`; `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/EndpointExtensions.cs`; `src/bff-admin/src/CodeForCoders.BffAdmin.Contracts/StaffPasswordRecoveryRequestV1.cs`; `src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/StaffPasswordResetIdentityHandler.cs`; `src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/StaffPasswordRecoveryRequestTests.cs`.
- SPA: `src/admin-spa/src/app/router.tsx`; `src/admin-spa/src/app/routes/staff-password-recovery-route.tsx`; `src/admin-spa/src/app/routes/staff-password-recovery-route.test.tsx`; `src/admin-spa/src/config/paths.ts`; `src/admin-spa/src/features/staff-session/components/staff-login-screen.tsx`; `src/admin-spa/src/features/staff-password-recovery/api/request-staff-password-reset.ts`; `src/admin-spa/src/features/staff-password-recovery/components/staff-password-recovery-screen.tsx`; `src/admin-spa/src/features/staff-password-recovery/components/staff-password-recovery-screen.test.tsx`; `src/admin-spa/src/testing/handlers.ts`.

Contratos aprovados e TechSpec não foram alterados. O diff de `tasks/prd-acesso-interno/9_task.md` e `flow-state.json` já existia no início desta chamada e foi preservado.

## Gate declarado

Gate executado sem alteração do comando; exit code **0** (`passed`):

`dotnet test --project src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj -- --filter-class CodeForCoders.Identity.IntegrationTests.StaffPasswordRecoveryRequestTests --minimum-expected-tests 3 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/CodeForCoders.BffAdmin.EndToEndTests.csproj -- --filter-class CodeForCoders.BffAdmin.EndToEndTests.StaffPasswordRecoveryRequestTests --minimum-expected-tests 2 && npm --prefix src/admin-spa run test -- -t StaffPasswordRecovery`

Evidência: Identity **3/3**, BFF-admin **2/2**, SPA **2/2**.

## Verificações do projeto e suítes completas

Todos os comandos abaixo terminaram com exit code **0**:

- `dotnet format src/identity/CodeForCoders.Identity.slnx --verify-no-changes`
- `dotnet build src/identity/CodeForCoders.Identity.slnx` — 0 warnings e 0 errors.
- `dotnet test --project src/identity/tests/CodeForCoders.Identity.ArchitectureTests/CodeForCoders.Identity.ArchitectureTests.csproj` — 8/8.
- `dotnet test --project src/identity/tests/CodeForCoders.Identity.UnitTests/CodeForCoders.Identity.UnitTests.csproj` — 15/15.
- `dotnet test --project src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj` — suíte completa: 79/79.
- `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes`
- `dotnet build src/bff-admin/CodeForCoders.BffAdmin.slnx` — 0 warnings e 0 errors.
- `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.ArchitectureTests/CodeForCoders.BffAdmin.ArchitectureTests.csproj` — 8/8.
- `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.UnitTests/CodeForCoders.BffAdmin.UnitTests.csproj` — 2/2.
- `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/CodeForCoders.BffAdmin.EndToEndTests.csproj` — suíte completa: 32/32.
- `dotnet test --project src/notification/tests/CodeForCoders.Notification.IntegrationTests/CodeForCoders.Notification.IntegrationTests.csproj` — suíte completa: 22/22.
- `npm --prefix src/admin-spa run lint`
- `npm --prefix src/admin-spa run typecheck`
- `npm --prefix src/admin-spa run build`
- `npm --prefix src/admin-spa run test` — suíte completa: 26/26; cobertura agregada 89,18%.
- `git diff --check`

## Limitações e suporte

O smoke com smtp4dev não foi executado: o contexto desta chamada proíbe subir o Docker Compose compartilhado. As suítes de integração com Testcontainers foram executadas.

O build do SPA passou, com avisos do Vite sobre `/admin/runtime-env.js` sem `type="module"` e chunk JavaScript maior que 500 kB. Não houve warnings de build .NET.
