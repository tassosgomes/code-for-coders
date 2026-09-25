Run: run.J6dAvetx

# Task 3.0 — implementação

Outcome: `implementation_complete`  
Gate: `passed`  
Branch: `feature/acesso-interno`

## Alterações

- **Identity:** sessões internas separadas das sessões de aluno; autenticação com hash fictício para credenciais ausentes/não internas; papéis e permissões vigentes; validação e renovação por ação; revogação idempotente. Adicionados endpoints internos, configuração de timeout e emissão opcional de JWT com papéis/permissões.
- **Persistência:** novo `staff_session_id` no registro de idempotência, migration e snapshot gerados pelo EF.
- **BFF Admin:** login, consulta e logout; sessão opaca em Valkey; cookie `staff_session`; CSRF vinculado à sessão e checagem de origem. Cada rota protegida consulta Identity; indisponibilidade não renova Valkey. O proxy usa somente o token da validação corrente e recusa a chamada sem token.
- **SPA:** rota pública `/admin/entrar`, loader de sessão para o início, saída, áreas filtradas por permissão e orientação para contas sem áreas.
- **Regressão:** entrada do aluno continua recusando conta interna.

Arquivos e grupos alterados:

- Identity: `src/identity/src/CodeForCoders.Identity.Application/UseCases/Accounts/{AuthenticateStaffSession,ValidateStaffSession,RevokeStaffSession}/`; `Application/Common/{StaffSessionDetails.cs,StaffSessionOptions.cs}`; `Application/Exceptions/StaffSessionException.cs`; `Application/Interfaces/IIdentitySessionStore.cs`; `Domain/Entities/IdempotencyRecord.cs`; `Infra.Data/Accounts/{IdempotencyRecordConfiguration.cs,IdentitySessionStore.cs}`; `Infra.Data/DependencyInjection.cs`; migration `20260925172750_AddStaffSessionIdempotency` e snapshot; `Identity.Api/Endpoints/StaffSessionEndpoints.cs`; configuração e emissão de token em `Identity.Api`; contratos em `Identity.Contracts/StaffSessionMessagesV1.cs`; `Identity.tests/IntegrationTests/{StaffSessionTests.cs,StudentSessionTests.cs}`.
- BFF Admin: endpoints e modelos de sessão em `CodeForCoders.BffAdmin.Api`; cliente Identity, middleware, cookie, contexto, CSRF e transformação do proxy; opções/configuração de segurança; contratos `StaffSessionMessagesV1.cs`; suporte e testes em `tests/CodeForCoders.BffAdmin.EndToEndTests/{BffAdminApiFactory.cs,InMemoryBffSessionStore.cs,StaffSessionIdentityHandler.cs,StaffSessionTests.cs}`.
- SPA: `src/admin-spa/src/app/{router.tsx,routes/{admin-layout-route.tsx,dashboard-route.tsx,staff-login-route.tsx,staff-session-loader.ts}}`; `components/app-shell.tsx`; `config/paths.ts`; `features/admin-dashboard/components/dashboard-screen.tsx`; nova feature `features/staff-session/`; `lib/api-client.ts`; `testing/handlers.ts`; `types/staff-area.ts`; estilos.

## Gates e verificações

- Gate focalizado final: **passou** — Identity StaffSession 5/5; BFF Admin StaffSession 8/8; SPA StaffSession 1/1 (14 testes).
- Identity: format exit 0; build exit 0, 0 warnings; ArchitectureTests 8/8; UnitTests 15/15.
- BFF Admin: format exit 0; build exit 0, 0 warnings; ArchitectureTests 8/8; UnitTests 2/2.
- admin-spa: lint, typecheck e build exit 0.
- BFF Student EndToEndTests: 18/18.
- EF `has-pending-model-changes`: sem mudanças pendentes.
- `git diff --check`: exit 0.

A primeira execução do gate detectou que a remoção do cookie não emitia `Max-Age=0`; a remoção foi ajustada e o gate completo passou na repetição.

## Limitações e preservação

- Smoke pelo Compose/navegador não executado, conforme decisão operacional desta chamada.
- O build SPA passou com avisos do Vite sobre `runtime-env.js` e tamanho do bundle principal (~649 kB).
- As alterações preexistentes em `tasks/prd-acesso-interno/3_task.md` e `flow-state.json` foram preservadas; não fazem parte desta implementação.

