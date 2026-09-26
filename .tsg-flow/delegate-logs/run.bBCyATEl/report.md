Run: run.bBCyATEl

# IMPLEMENTATION COMPLETE — task 7.0

Modo `implement`, tentativa `1/3`. O gate final passou (exit 0), acima do mínimo de 8 testes: Identity 7, BFF 2 e SPA 2.

## Implementação

- Identity agora oferece `ChangeStaffRole`: remove o papel de origem, concede o destino quando ainda não está presente, revoga as sessões do alvo e grava os atos de auditoria no mesmo commit. A troca normal gera dois fatos com `fatoId` distintos e mesmo autor, alvo, motivo, momento e `correlationId`; se o destino já existe, grava apenas a revogação. Papel de origem ausente, papéis iguais e ação sobre si retornam 422. Replays idempotentes não duplicam fatos.
- O escritor de auditoria aceita correlação explícita. A troca usa uma correlação comum para os dois fatos; concessões e revogações existentes preservam a correlação por fato que já usavam.
- A rota interna de Identity e a rota pública do BFF encaminham a mudança com sessão, escopo e chave de idempotência. A SPA oferece “Trocar papel” na linha da pessoa, exige motivo e avisa que as sessões serão encerradas.
- Os contratos HTTP documentam `ROLE_CHANGE_INVALID`; os exemplos AsyncAPI existentes para os dois atos foram mantidos.
- As rotas vivem nos módulos atuais `StaffMemberEndpoints`; `EndpointExtensions` já registra esses módulos e não precisou mudar.

## Evidência

- Gate focalizado: Identity 7/7, BFF 2/2, SPA 2/2; exit 0.
- Identity: `dotnet format` e build passaram, sem warnings de build; testes de arquitetura 8/8, unitários 15/15 e integração completos 73/73.
- BFF: `dotnet format` e build passaram, sem warnings de build; arquitetura 8/8, unitários 2/2 e EndToEnd completos 27/27.
- Notification IntegrationTests completos: 22/22.
- SPA: lint, typecheck e build passaram; testes completos 21/21. O build reportou o aviso do Vite sobre o bundle acima de 500 kB e `runtime-env.js` não ser empacotado.
- `git diff --check` passou; os arquivos YAML dos contratos foram lidos pelo `yq` e o exemplo `ROLE_CHANGE_INVALID` foi confirmado.
- Uma execução do gate parou por falha de fixture no BFF (`sessionsEnded`); o fixture foi corrigido. Uma tentativa posterior encontrou uma falha transitória do recurso auxiliar do Testcontainers; o gate final repetido passou integralmente.

## Arquivos alterados

- `src/identity/src/CodeForCoders.Identity.Application/UseCases/Accounts/ChangeStaffRole/ChangeStaffRole.cs`
- `src/identity/src/CodeForCoders.Identity.Application/UseCases/Accounts/ChangeStaffRole/ChangeStaffRoleInput.cs`
- `src/identity/src/CodeForCoders.Identity.Application/UseCases/Accounts/ChangeStaffRole/IChangeStaffRole.cs`
- `src/identity/src/CodeForCoders.Identity.Application/UseCases/Accounts/StaffRoleActions/StaffRoleActionExecutor.cs`
- `src/identity/src/CodeForCoders.Identity.Application/UseCases/Accounts/StaffRoleActions/StaffRoleChangeCommand.cs`
- `src/identity/src/CodeForCoders.Identity.Application/UseCases/Accounts/StaffRoleActions/StaffRoleChangeCommandValidator.cs`
- `src/identity/src/CodeForCoders.Identity.Application/DependencyInjection.cs`
- `src/identity/src/CodeForCoders.Identity.Application/Interfaces/IStaffRoleMessageWriter.cs`
- `src/identity/src/CodeForCoders.Identity.Application/Services/StaffRoleMessageWriter.cs`
- `src/identity/src/CodeForCoders.Identity.Api/ApiModels/StaffMemberMessagesV1.cs`
- `src/identity/src/CodeForCoders.Identity.Api/Endpoints/StaffMemberEndpoints.cs`
- `src/identity/tests/CodeForCoders.Identity.IntegrationTests/StaffRoleChangeTests.cs`
- `src/identity/tests/CodeForCoders.Identity.IntegrationTests/StaffRoleGrantRevokeTests.cs`
- `src/bff-admin/src/CodeForCoders.BffAdmin.Contracts/StaffMemberMessagesV1.cs`
- `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Clients/IStaffMemberIdentityClient.cs`
- `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Clients/StaffMemberIdentityClient.cs`
- `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/StaffMemberEndpoints.cs`
- `src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/StaffMemberIdentityHandler.cs`
- `src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/StaffRoleChangeTests.cs`
- `src/admin-spa/src/features/staff-access/api/staff-members.ts`
- `src/admin-spa/src/features/staff-access/components/staff-access-screen.tsx`
- `src/admin-spa/src/testing/handlers.ts`
- `src/admin-spa/src/app/routes/staff-access-route.test.tsx`
- `tasks/prd-acesso-interno/api-contract.yaml`
- `tasks/prd-acesso-interno/internal-api-contract.yaml`

## Limitações

- O smoke local não foi executado, conforme a decisão do contexto; nenhum Docker Compose foi iniciado. Não foram geradas migrations.
- As alterações pré-existentes em `tasks/prd-acesso-interno/7_task.md` e `flow-state.json` foram preservadas, sem edição nesta chamada.
