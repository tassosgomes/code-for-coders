# Implementer: task 3.0, fix (tentativa 2/3)

Run: run.HlYfAQKO
Modo: fix · Revisão de origem: `3.0_task_review.md` (run.ciaJ631w)

**Resultado: IMPLEMENTATION COMPLETE**

## Correções

### B1: dois `role="status"` na tela inicial do admin-spa
- `src/admin-spa/src/features/admin-dashboard/components/dashboard-screen.tsx`: tirei `role="status"`
  da orientação "Sua conta ainda não tem acesso…". Esse texto é estático e não é uma região viva. O único
  `role="status"` da tela volta a ser o estado do serviço. `dashboard-screen.test.tsx` não mudou e
  continua usando `getByRole('status')` para provar o estado do serviço.
- Criei `src/admin-spa/src/app/routes/admin-layout-route.test.tsx` (`describe('StaffSession areas')`), que
  cobre o R2. Ele usa o loader real (`loadStaffSession`), `AdminLayoutRoute` e `DashboardRoute`, com o
  MSW sobrescrevendo `/api/v1/staff-sessions/current`:
  - `permissions: ['financeiro.ler','suporte.atender']`: a lista mostra só "Financeiro" e "Suporte",
    sem "Acessos" e sem a orientação.
  - `permissions: []`: a orientação aparece, sem nenhuma lista de áreas, e o botão "Sair" está presente.
  - Como a config do vitest não usa `globals`, o RTL não limpa a tela sozinho entre os testes. Por isso
    o arquivo chama `afterEach(cleanup)`.

### Defeito herdado da 2.0: `IdentityInfrastructureTests.HeartbeatFlowsThroughOutboxRabbitMqAndConsumer`
- `src/identity/tests/CodeForCoders.Identity.IntegrationTests/IdentityInfrastructureTests.cs`:
  1. A fixture passou a configurar `StaffAccount:PasswordResetBaseUrl` (URL HTTPS de teste) e
     `StaffAccount:PasswordResetLifetimeHours`. A validação de produção em `DependencyInjection.cs` não mudou.
  2. Depois disso apareceu um segundo problema, antes escondido pela falha de configuração. O
     `OutboxPublisherWorker` pega qualquer mensagem pendente, sem filtro de tenant. As outras classes da
     coleção deixam mensagens pendentes no banco compartilhado, cifradas com a chave de
     `OutboxTestProtection`. O host do heartbeat usa outra chave, então o worker lançava
     `AuthenticationTagMismatchException`, o host parava e o teste terminava em timeout. Para corrigir,
     o teste agora cria um banco próprio no mesmo container PostgreSQL (`identity_heartbeat_<uuidv7>`,
     com `MigrateAsync` do EF) e fica isolado das mensagens dos outros testes. Não mexi em código de
     produção.

## Arquivos alterados nesta chamada
- `src/admin-spa/src/features/admin-dashboard/components/dashboard-screen.tsx`
- `src/admin-spa/src/app/routes/admin-layout-route.test.tsx` (novo)
- `src/identity/tests/CodeForCoders.Identity.IntegrationTests/IdentityInfrastructureTests.cs`

## Gate (comando declarado, sem alteração, via `rtk proxy`)
| Etapa | Resultado |
|---|---|
| Identity `StaffSessionTests` | 5/5 |
| BFF-admin E2E `StaffSessionTests` | 8/8 |
| admin-spa `-t StaffSession` | 3 aprovados, 4 ignorados pelo filtro |
| **Exit** | **0**. São 16 testes, acima do mínimo de 11 do `gate_expect` |

## Verificações do projeto (cada uma em comando separado)
| Comando | Exit |
|---|---|
| `dotnet format` identity `--verify-no-changes` | 0 |
| `dotnet build` identity | 0 (0 warnings) |
| Identity ArchitectureTests | 0 |
| Identity UnitTests | 0 |
| `dotnet format` bff-admin `--verify-no-changes` | 0 |
| `dotnet build` bff-admin | 0 (0 warnings) |
| BFF-admin ArchitectureTests | 0 |
| BFF-admin UnitTests | 0 |
| admin-spa `lint` / `typecheck` / `build` | 0 / 0 / 0 |
| bff-student EndToEndTests (regressão CAP-001) | 0 |

## Suítes completas pedidas pelo orquestrador
| Suíte | Exit | Resultado |
|---|---|---|
| `npm --prefix src/admin-spa run test` | 0 | 7/7 (5 arquivos) |
| Identity IntegrationTests completa | 0 | 38/38 |

## Limitações
- O smoke em `http://localhost:8081/admin/entrar` não foi executado. A stack Compose não foi subida, por
  decisão do orquestrador.
- As recomendações R3–R5 ficaram fora de escopo, como pedido.
