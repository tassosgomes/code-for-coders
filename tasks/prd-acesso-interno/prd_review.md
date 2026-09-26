# Validação full — PRD Acesso interno por papel e permissão (CAP-002)

## Ciclo 2

Run: run.IYwr3d1R
Modo: full · Tentativa: 2/3 · Data: 2026-09-26

**Resultado: FULL VALIDATION APROVADA.** Os 5 bloqueantes do ciclo 1 foram resolvidos e não há bloqueante
novo. Ficam 10 recomendações: R1–R9 do ciclo 1, reavaliadas, e R10, nova.

| Referência | SHA |
|---|---|
| base_ref (`main`, alvo) | `f7bda4f1dd3d7de85001ec1b77e80b1914e0b24c` |
| validated_commit (HEAD `feature/acesso-interno`) | `0ddfa16c23995a0d0b184ac4610457aaec7874a9` |
| validated_tree | `ad68fcb283f8d82ced18972e4ab8e6ad4e7ecde7` |

- **Estabilidade:** HEAD e árvore foram os mesmos no início e no fim da revisão. `merge-base == base_ref`,
  e `main` não se moveu.
- **Árvore real:** o único arquivo alterado é `flow-state.json`, que é do orquestrador e já estava alterado
  antes da revisão.
- **Isolamento:** toda execução rodou em worktrees temporários no commit validado. Todos foram removidos no
  fim, junto com as imagens `c4c-validate-*`.

### C2.1 Escopo desde a full 1 (`893b8be..0ddfa16`)

O diff contém apenas testes, configuração de teste e artefatos do fluxo. **Nenhum código de produção mudou.**
Isso inclui `docker-compose.yml`, os scripts e a documentação: `git diff --name-only 893b8be..HEAD -- src ':!src/*/tests/**'`
sai vazio.

- **B4:** `StaffPasswordResetTests.ResetStaffPassword_RejectsAnAlreadyUsedLinkWithAnotherIdempotencyKey`
  prova o `RESET_TOKEN_INVALID` e que a senha fica inalterada.
- **B5:** `Identity.EndToEndTests/StaffInvitationIssuingEndpointTests` prova, por HTTP real no Identity, que
  uma sessão de professor recebe 403 `PERMISSION_DENIED` e que não há convite, outbox nem registro de
  idempotência. Traz também o caso positivo (201 com 2 mensagens na outbox).
- **B1, B2 e B3:**
  - `tests/Directory.Build.props` e `tests/testconfig.json` foram adicionados em bff-admin, identity,
    notification, commerce, learning e media.
  - São idênticos ao precedente do `bff-student` (só muda o nome do componente no comentário).
  - Excluem `obj/**` e `*.g.cs`/`*.generated.cs` da coleta de cobertura do MTP. Nenhum teste foi removido
    ou enfraquecido.
  - Nenhum relatório Cobertura gerado contém fonte gerada (`reportsWithGenerated=0` em todos os componentes).
    A decisão foi tomada pelo usuário (context.txt).

### C2.2 Matriz de CI

- **Fonte:** `.github/workflows/<componente>.yml` → `tassosgomes/template-pipeline` `ci-dotnet.yml@v1` e
  `ci-react-ts.yml@v1`, lidos via `gh api` (ref `v1`).
- **Passos .NET:** mesmos comandos e parâmetros do workflow:
  - `dotnet restore`
  - `dotnet format --verify-no-changes --no-restore`
  - `dotnet test --no-restore --configuration Debug --coverage --coverage-output-format cobertura`
  - cobertura por união de linhas de **todos** os relatórios `*.xml` com `<coverage`, usando o script Python
    extraído literalmente do workflow, com limite de 70
  - `dotnet publish --configuration Release --no-restore --output ./publish`
  - `docker build` com o contexto `.` e o Dockerfile do workflow
- **Passos SPA:**
  - `npm ci`
  - `npm run lint`
  - `npx --no-install tsc --noEmit`
  - `npm test`
  - `.total.lines.pct` de `coverage/coverage-summary.json` ≥ 70
  - `npm run build -- --base=/admin/`
  - imagem com o contexto `src/admin-spa`
- **Workflows acionados:**
  - `Directory.Packages.props` foi alterado desde a base e aciona todos os workflows .NET.
  - `src/admin-spa` aciona o `admin-spa`.
  - `student-spa` não é acionado.

| Componente | restore | format | test | cobertura (≥70) | publish | imagem | Ciclo 1 |
|---|---|---|---|---|---|---|---|
| identity | 0 | 0 | 0 | 79.25 ✅ | 0 | 0 | 75.92 |
| bff-admin | 0 | 0 | 0 | **78.01 ✅** | 0 | 0 | 67.95 ❌ |
| admin-spa | `npm ci` 0 | lint 0 · tsc 0 | 0 (26/26) | 88.51 ✅ | build 0 | 0 | 88.51 |
| notification | 0 | 0 | 0 (51/51)¹ | 85.45 ✅ | 0 | 0 | 78.21 |
| commerce | 0 | 0 | 0 | **78.41 ✅** | 0 | 0 | 59.87 ❌ |
| bff-student | 0 | 0 | 0 | 83.56 ✅ | 0 | 0 | 83.56 |
| audit | 0 | 0 | 0 | 83.45 ✅ | 0 | 0 | 83.45 |
| learning | 0 | 0 | 0 | **76.09 ✅** | 0 | 0 | 55.69 ❌ |
| media | 0 | 0 | 0 | **77.03 ✅** | 0 | 0 | 56.98 ❌ |

¹ **Notification e a execução descartada por ambiente:**
- A primeira execução, no worktree em `scratchpad/wt-ci`, saiu com exit 134. Os testes de ArchitectureTests e
  IntegrationTests abortaram com `PAL_SEHException` e 0 testes executados nesses dois projetos; os demais
  passaram (21/21).
- **Causa diagnosticada** com `--diagnostic`: o host de teste morre em 0,3 s ao carregar o profiler de
  cobertura. O caminho do profiler tinha 274 caracteres
  (`…/CodeForCoders.Notification.ArchitectureTests/bin/Debug/net10.0/runtimes/linux-x64/native/libInstrumentationEngine.so`),
  e o de `Cov_x64.config`, 261 — acima do limite de 260 do Instrumentation Engine. O controlador então espera
  300 s pela conexão e aborta.
- **Provas de que é ambiente:**
  - O mesmo projeto passa sem `--coverage`.
  - Ele trava igualmente no commit `893b8be`, que passou na full 1.
  - Ele trava igualmente sem o `testconfig.json`.
  - O identity, cujo caminho é 4 caracteres mais curto, passa com cobertura no mesmo worktree.
- **Reexecução válida:** num worktree de caminho curto (`/tmp/claude-1000/c4v2`) no mesmo commit, restore,
  format e test deram 0, com 51/51, 4 relatórios e 85.45%. Publish e imagem já tinham saído 0 no primeiro
  worktree. Os runners do GitHub usam caminhos curtos (`/home/runner/work/...`), então isso não afeta o CI
  (ver R10).

**Passos não reproduzidos:**
- `resolve-version`, upload de artefato e push de imagem são passos de plataforma, sem veredito sobre o
  código.
- SAST, secret scan, dependency scan, container scan e DAST estão com `security-mode: observe` nos
  chamadores: observam e não reprovam.

Esta execução reproduz os passos que determinam o veredito; não é um espelho integral do CI.

### C2.3 Sensor de discriminação

- **Execução:** num worktree temporário no commit validado.
  - Linha de base: HEAD `0ddfa16`, `status` vazio.
  - Cada mutante foi aplicado, a suíte da fatia rodou com `--filter-class` e o arquivo foi restaurado com
    `git checkout --`.
  - Ao final, HEAD `0ddfa16` e `status` vazio, iguais à linha de base. O worktree foi removido.
- **Mutantes refeitos:** três mutantes com `if (false)` não compilaram (CS0162 com warnings-as-errors) e não
  contaram. Eles foram refeitos acrescentando `&& DateTime.UtcNow.Year < 2000` à guarda, o que a neutraliza
  e ainda compila.

| Fatia | Mutação (arquivo:linha) | Critério | Teste que falhou | Resultado |
|---|---|---|---|---|
| 1.0 | `ServiceAssertionVerifier.cs:71`: ignora `AllowedScopes` do emissor | RN-18 | `ServiceAssertionVerifierTests` (2/6) | morto |
| 2.0 | `ProvisionFirstAdministrator.cs:26`: provisiona com administrador existente | RN-25 | `…IsIdempotentWhenTenantAlreadyHasAdministrator` | morto |
| **2.0 (B4)** | `ResetStaffPassword.cs:51`: remove `ConsumedOn is not null` | RF-11, RN-06/07 | `ResetStaffPassword_RejectsAnAlreadyUsedLinkWithAnotherIdempotencyKey` | **morto** (sobrevivia no ciclo 1) |
| 3.0 | `IdentitySessionStore.cs:126` e `:142`: sessão revogada continua válida | RF-08, RN-13 | `StaffSession_LogoutRevokesOnlyTheRequestedSession…` | morto |
| **4.0 (B5)** | `StaffInvitationEndpoints.cs:136`: emite convite sem `acesso.gerir` | RF-03, RN-14 | `StaffInvitationIssuingEndpointTests.…RejectsASessionWithoutManageAccessWithoutWrites` | **morto** (sobrevivia no ciclo 1) |
| 5.0 | `AcceptStaffInvitation.cs:249`: aceita convite já aceito | RF-05, RN-06 | `…RejectsASecondUseWithTheGenericInvalidInvitationError` | morto |
| 6.0 | `StaffRoleActionExecutor.cs:106`: revogar não encerra sessões | RF-07/08 | `StaffRoleGrantRevoke_RevokesRoleAndAllSessionsInTheAuditedCommit` | morto |
| 7.0 | `StaffRoleActionExecutor.cs:303`: troca sem gravar `papel-concedido` | RF-09, DP-01 | `StaffRoleChangeTests` (3/7) | morto |
| 8.0 | Commerce `ServiceConfigurationExtensions.cs:40`: policy sem `RequireClaim(financeiro.ler)` | RF-13, RN-18 | `FinanceArea_RejectsValidTokenWithoutFinancePermission` | morto |
| 8.0 | BFF `FinanceAreaEndpoints.cs:34`: não confere `financeiro.ler` | RF-13 | `FinanceArea_RefusesProfessorBeforeCallingCommerce` | morto |
| 9.0 | `IdentityPasswordRecoveryStore.cs:50`: recuperação aceita conta que não é interna | RN-03/04 | `StaffPasswordRecoveryRequest_ReturnsNeutralResponsesAndEmailsOnlyInternalAccounts` | morto |
| 3.0 (SPA) | `get-staff-areas.ts:12`: menu não filtra por permissão | RF-10 | `admin-layout-route` (2) e `finance-area-route` (1) | morto |

Resultado: 12 mutantes mortos e nenhum sobrevivente.

### C2.4 Smoke

**Não repetido, por decisão justificada.**
- O smoke da full 1 passou 36/36 sobre `893b8be`.
- Desde então não mudou nenhum código de produção, nem `docker-compose.yml`, `scripts/`, `docs/` ou
  Dockerfiles.
- As imagens foram reconstruídas com sucesso nesta matriz.
- A mudança de B1/B2/B3 só afeta a coleta de cobertura dos projetos de teste, e a de B4/B5 só acrescenta
  testes.

Um novo smoke exercitaria exatamente os mesmos binários de produção.

### C2.5 Bloqueantes do ciclo 1

| Bloqueante | Task | Situação | Evidência |
|---|---|---|---|
| B1: cobertura bff-admin | 3.0 | Resolvido | 78.01% ≥ 70 |
| B2: cobertura commerce | 8.0 | Resolvido | 78.41% ≥ 70 |
| B3: learning e media herdados | 8.0 | Resolvido | 76.09% e 77.03% ≥ 70 |
| B4: mutante de reuso do token | 2.0 | Resolvido | mutante morto |
| B5: mutante de autorização do convite | 4.0 | Resolvido | mutante morto |

Bloqueantes novos: nenhum.

### C2.6 Recomendações (não bloqueiam)

- **R1–R9 do ciclo 1:** reavaliadas e mantidas sem mudança.
  - Nenhum código de produção mudou, então o impacto continua o mesmo.
  - Nenhuma quebra jornada do PRD, contrato ou CI.
  - R8 (sensibilidade do fixture do notification) não se manifestou nesta rodada.
- **R10 (nova, ambiente):**
  - Com `--coverage`, o MTP falha de forma opaca quando o caminho do binário de teste deixa o profiler do
    Instrumentation Engine acima de 260 caracteres: exit 134 depois de 300 s de espera.
  - Isso não afeta o CI nem o desenvolvimento em `/home/tsgomes/github-tassosgomes/...`, mas atinge
    validações em diretórios temporários longos.
  - Recomendação para o fluxo: usar worktrees de caminho curto nas execuções com cobertura.

---

# Ciclo 1 (histórico)


Run: run.QeuEMpDO
Modo: full · Tentativa: 1/3 · Data: 2026-09-25

**Resultado: FULL VALIDATION REPROVADA** — 5 bloqueantes, 9 recomendações.

| Referência | SHA |
|---|---|
| base_ref (`main`, alvo) | `f7bda4f1dd3d7de85001ec1b77e80b1914e0b24c` |
| validated_commit (HEAD `feature/acesso-interno`) | `893b8be61fea5a4849be8f8f60e22b82347e8032` |
| validated_tree | `7f6a7a587a3fc022eee466ac232ee500f4c27b75` |

HEAD e árvore iguais no início e no fim da revisão. A branch contém a base (`merge-base == base_ref`).
Único arquivo alterado na árvore real: `flow-state.json` (do orquestrador, já presente antes da revisão).
Toda execução rodou em worktrees temporários no commit validado, removidos no fim.

## 1. Matriz de CI

Fonte: `.github/workflows/<componente>.yml` → `tassosgomes/template-pipeline/.github/workflows/ci-dotnet.yml@v1`
e `ci-react-ts.yml@v1`, lidos via `gh api` (ref `v1`). Passos reproduzidos com os mesmos comandos e
parâmetros:
- `dotnet restore`; `dotnet format --verify-no-changes --no-restore`;
- `dotnet test --no-restore --configuration Debug --coverage --coverage-output-format cobertura` (MTP);
- cobertura por união de linhas de todos os relatórios Cobertura, com o mesmo script Python do workflow;
- `dotnet publish -c Release`; `docker build` com o contexto e o Dockerfile do workflow.

SPA: `npm ci`, `npm run lint`, `tsc --noEmit`, `npm test`, `coverage-summary.json` ≥ 70, `npm run build -- --base=/admin/`, imagem.

Componentes acionados: `src/identity`, `src/bff-admin`, `src/admin-spa`, `src/notification` e `src/commerce`
foram alterados. Além disso, **`Directory.Packages.props`** foi alterado (JwtBearer e TestHost). O filtro
`paths: Directory.*` aciona **todos** os workflows .NET, o que inclui também `bff-student`, `audit`, `learning` e `media`.

| Componente | restore | format | test | cobertura (≥70) | publish | imagem | Base (cobertura) |
|---|---|---|---|---|---|---|---|
| identity | 0 | 0 | 0 (108/108) | 75.92 ✅ | 0 | 0 | — |
| bff-admin | 0 | 0 | 0 (43/43) | **67.95 ❌** | 0 | 0 | 49.15 (já falhava) |
| admin-spa | `npm ci` 0 | lint 0 · tsc 0 | 0 | 88.51 ✅ | build 0 | 0 | — |
| notification | 0 | 0 | 0 (51/51)¹ | 78.21 ✅ | 0 | 0 | — |
| commerce | 0 | 0 | 0 | **59.87 ❌** | 0 | 0 | 55.75 (já falhava) |
| bff-student | 0 | 0 | 0 | 83.56 ✅ | 0 | 0 | — |
| audit | 0 | 0 | 0 | 83.45 ✅ | 0 | 0 | — |
| learning | 0 | 0 | 0 | **55.69 ❌** | 0 | 0 | 55.69 (idêntico) |
| media | 0 | 0 | 0 | **56.98 ❌** | 0 | 0 | 56.98 (idêntico) |

¹ A primeira execução do notification saiu com exit 2. As 22 falhas foram todas `Test Collection Cleanup
Failure` em `DisposeAsync` do fixture, por timeout na API do Docker durante a contenção do daemon; em paralelo
rodava a medição da base do commerce. A reexecução isolada, sem mudar nenhum código, passou (exit 0, 51/51).
Classificação: falha de ambiente, não de código.

Passos não reproduzidos: `resolve-version`, upload de artefato e o push de imagem. São passos de
plataforma, sem veredito sobre o código. SAST, secret scan, dependency scan e DAST também ficaram de fora:
com `security-mode: observe` eles apenas observam e não reprovam. Por isso esta execução não é um espelho
integral do CI, e sim o conjunto dos passos que determinam o veredito.

## 2. Sensor de discriminação

Execução em um `git worktree` temporário, com uma mutação de comportamento por fatia e a suíte da fatia
rodando contra cada mutante. Ao fim, o worktree voltou limpo (`git status` vazio) e foi removido. A árvore
real ficou intacta.

| Fatia | Mutação (arquivo) | Suíte | Resultado |
|---|---|---|---|
| 1.0 | Aceitar escopo fora de `AllowedScopes` do emissor (`ServiceAssertionVerifier.cs:71`) | Identity `ServiceAssertionVerifierTests` | morto (2/6) |
| 2.0 | Provisionar de novo com administrador existente (`ProvisionFirstAdministrator.cs:26`) | Identity `FirstAdministratorProvisioningTests` | morto (1/4) |
| **2.0** | **Aceitar token de redefinição já consumido** (`ResetStaffPassword.cs:48-51`, remove `ConsumedOn is not null`) | Identity `StaffPasswordResetTests` e depois a suíte **inteira** do Identity (108) | **SOBREVIVEU** |
| 3.0 | Sessão revogada continua válida (`IdentitySessionStore.cs:126` e `:142`) | Suíte do Identity | morto (1/108)² |
| **4.0** | **Identity emite convite sem a permissão `acesso.gerir`** (`StaffInvitationEndpoints.cs:136`) | Identity `StaffInvitationIssuingTests` e depois a suíte **inteira** (108) | **SOBREVIVEU** |
| 5.0 | Convite já aceito é aceito de novo (`AcceptStaffInvitation.cs:249`) | Identity `StaffInvitationAcceptanceTests` | morto (1/10) |
| 6.0 | Revogar não encerra as sessões do alvo (`StaffRoleActionExecutor.cs:104-107`) | Identity `StaffRoleGrantRevokeTests` | morto (1/10) |
| 7.0 | Troca não grava o ato `papel-concedido` (`StaffRoleActionExecutor.cs:303`) | Identity `StaffRoleChangeTests` | morto (3/7) |
| 8.0 | Commerce exige só autenticação, sem `financeiro.ler` (`ServiceConfigurationExtensions.cs:40`) | Commerce `FinanceAreaAuthorizationTests` | morto (1/7) |
| 8.0 | BFF não confere a permissão financeira (`FinanceAreaEndpoints.cs:34`) | BFF `FinanceAreaTests` | morto (1/3) |
| 9.0 | Recuperação aceita conta de aluno (`IdentityPasswordRecoveryStore.cs:50`) | Identity `StaffPasswordRecoveryRequestTests` | morto (1/3) |
| 3.0 (SPA) | Menu mostra todas as áreas sem filtrar por permissão (`get-staff-areas.ts:12`) | Vitest `admin-layout-route`, `finance-area-route` | morto (3/4) |

² Quando só a guarda da linha 126 era removida, o mutante sobrevivia porque a releitura na linha 142 filtra
`RevokedOn` de novo. Era um mutante equivalente, e por isso não conta como lacuna. Com as duas guardas
removidas, a suíte detecta a falha.

## 3. Smoke da stack (Docker Compose, autorizado)

A stack subiu com `docker compose -p c4c-smoke up --build`, a partir de um worktree temporário no commit
validado, com `.env` gerado por `scripts/generate-local-env.sh`. Houve dois passos de ambiente adicionais:
- Migrações EF aplicadas com `dotnet ef database update` em identity, notification, commerce e audit.
- Em volume novo, o Compose não cria os bancos e logins dos serviços além do identity. Eles foram criados
  no volume isolado do smoke (ver R9).

No fim, `docker compose down -v`: os volumes eram exclusivos do smoke. O ambiente principal não foi tocado.

Resultado: 36 verificações, todas OK.

Três verificações do roteiro falharam na primeira passada por defeito do próprio roteiro, não do código:
- o cliente HTTP descartava o cookie `Secure` em `http://localhost`, o que o navegador aceita;
- o roteiro pegou o token do e-mail errado;
- o logout foi chamado sem o `Idempotency-Key` exigido.

Todas foram refeitas e passaram.

- **Fase 1:**
  - O seed cria o administrador; a segunda execução responde "already has an administrator".
  - O e-mail chega no smtp4dev com o link `http://localhost:8081/admin/redefinir-senha`, e a senha é
    definida (204).
  - Reusar o link dá 422 `RESET_TOKEN_INVALID`.
  - E-mail inexistente e senha errada dão respostas idênticas (401 `INVALID_CREDENTIALS`).
  - O login funciona, a sessão traz `acesso.gerir` (o que alimenta o menu "Acessos") e `/admin/` responde 200.
  - A saída explícita dá 204, e a ação seguinte dá 401.
- **Fase 2:**
  - O convite dá 201 e o e-mail chega. O lookup dá 200 e o aceite dá 200, com sessão de professor.
  - Reusar o convite dá 422 `INVITATION_INVALID`.
  - A troca professor → financeiro dá 200. As **duas** sessões do ator recebem 401 na ação seguinte.
  - Revogar com motivo dá 200, e a sessão recebe 401 na ação seguinte. Conceder dá 200.
  - Trilha na Auditoria: `convite-interno-emitido`, `convite-interno-aceito`, 2× `papel-revogado` e
    2× `papel-concedido`, todos `conforming`, com o motivo registrado e **nenhum e-mail** em complemento ou motivo.
- **Fase 3:**
  - O professor recebe 403 `PERMISSION_DENIED` no BFF, tanto em `/api/v1/finance-area` quanto em
    `/api/v1/staff-members`.
  - Chamado direto, o Commerce `/internal/v1/finance-area` recusa sem token (401) e com um JWT forjado
    `alg=none` com `financeiro.ler` (401).
  - O ator financeiro obtém 200 `{"status":"reserved"}` via BFF → Commerce.
  - A recuperação é neutra: 202 tanto para conta interna quanto para inexistente, e só a conta interna
    recebe e-mail. A redefinição pelo link funciona e o login com a nova senha também.
  - Nenhum token aparece no access log do nginx: `GET /admin/redefinir-senha?token=…` foi registrado sem a
    query string. Os logs do identity e do bff-admin não têm e-mail nem `token=`.

Limitações do smoke:
- O menu da SPA não foi aberto num navegador. A evidência vem da sessão e do teste de UI, confirmado pelo
  sensor.
- Não foi possível obter pela borda um JWT válido de professor para chamar o Commerce direto. Esse caso
  está coberto por `FinanceAreaAuthorizationTests`, e o mutante 8.0 foi morto.

## 4. Revisão semântica

- **Rastreabilidade:** RF-01 a RF-14 cobertos pelas tasks 1.0–9.0 (tabela de cobertura do `tasks.md`) e
  exercitados no smoke.
- **Autorização:** dupla checagem da permissão `acesso.gerir`, no BFF e no Identity, com o Identity como
  fonte de verdade. A regra RN-20 (sem ações sobre a própria conta) está em `StaffRoleActionExecutor.cs:46` e `:208`.
- **Tenant:** as consultas filtram por `TenantId`.
- **Atomicidade:**
  - A troca de papel remove e concede numa única transação, com dois atos na outbox e sem campo de
    correlação no payload (DP-01).
  - O `correlationId` vai só no header de transporte.
- **RN-A08:** o payload de auditoria referencia `conta-interna`/`convite-interno` por id. O e-mail só
  aparece no pedido à Notificação, que é a exceção de RN-26.
- **Commerce:**
  - Valida o JWT pelo JWKS do Identity, restrito a RSA/RS256 e `use=sig`.
  - A policy exige a claim `permissions` com o valor `financeiro.ler`.

## 5. Bloqueantes

**B1 — CI `bff-admin`: cobertura 67.95% < 70% (task 3.0; contribuem 2.0, 4.0, 8.0 e 9.0).**
- O job `bff-admin.yml` é acionado e reprova. Na base a cobertura era 49.15%: a entrega melhora, mas não
  atinge o limite. O `tasks.md` registrou "Cobertura medida na full".
- Maiores lacunas:
  - `Clients/StaffSessionIdentityClient.cs` (57/132 linhas sem cobertura)
  - `ExceptionHandlers/GlobalExceptionHandler.cs` (39/42)
  - `Endpoints/StaffInvitationEndpoints.cs` (35/208)
  - `Clients/StaffPasswordResetIdentityClient.cs` (33/95)
  - `Endpoints/SessionEndpoints.cs` (31/156)
  - `Infra.Data/ValkeyBffSessionStore.cs` (28/28)
  - `Security/BffSecurityMiddleware.cs` (26/165)
  - `Endpoints/StaffMemberEndpoints.cs` (26/188)
  - `Clients/CommerceFinanceAreaClient.cs` (23/35)
- A métrica do workflow também conta `obj/.../OpenApiXmlCommentSupport.generated.cs` (377 linhas, 0%).
  Sem esse arquivo a cobertura seria 78.0%. Excluir código gerado do cálculo é uma alteração de
  configuração que precisa ser decidida e versionada. Não é o validator que a aplica.
- Correção: testes para os ramos de erro e timeout dos clientes, e/ou exclusão do código gerado da coleta,
  sem enfraquecer testes existentes.

**B2 — CI `commerce`: cobertura 59.87% < 70% (task 8.0).**
- O job é acionado pela mudança em `src/commerce`. A base já falhava (55.75%).
- Lacunas:
  - `GlobalExceptionHandler.cs` (57/60)
  - `OutboxPublisherWorker.cs` (23/74)
  - `FinanceAreaJwksConfigurationManager.cs` (19/90: ramos de falha e renovação do JWKS)
  - health checks
  - o mesmo arquivo OpenAPI gerado (377 linhas)
- A regra da validação full se aplica: um job obrigatório acionado que reprova impede a aprovação, mesmo
  quando a falha é herdada.

**B3 — CI `learning` e `media`: cobertura 55.69% e 56.98% < 70% (falha herdada, acionada por `Directory.Packages.props`; tasks 1.0 e 8.0).**
- O código desses serviços não mudou, e a cobertura é idêntica à da base. O job é acionado porque a
  entrega altera `Directory.Packages.props` (filtro `Directory.*`), e por isso reprovaria no PR.
- É preciso uma decisão material do orquestrador ou do usuário: elevar a cobertura de learning e media,
  resolver a métrica do código gerado (a mesma de B1 e B2) na plataforma, ou aceitar formalmente os jobs
  reprovados. O validator não pode aprovar com esses jobs vermelhos.

**B4 — Mutante sobrevivente 2.0: token de redefinição de senha reutilizável (`src/identity/src/CodeForCoders.Identity.Application/UseCases/Accounts/ResetStaffPassword/ResetStaffPassword.cs:48-51`).**
- Mutação: remover `|| token.ConsumedOn is not null`.
- Critério de aceite: RF-11/RF-02, "link de uso único" (RN-06/RN-07).
- Nenhum teste da suíte do Identity (108) falhou. `StaffPasswordResetTests` não tem o caso "reusar o link
  já usado → `RESET_TOKEN_INVALID` e senha inalterada".
- O comportamento real está correto (o smoke deu 422), mas o teste não discrimina.
- Correção: acrescentar esse caso em `src/identity/tests/CodeForCoders.Identity.IntegrationTests/StaffPasswordResetTests.cs`.

**B5 — Mutante sobrevivente 4.0: Identity emite convite sem `acesso.gerir` (`src/identity/src/CodeForCoders.Identity.Api/Endpoints/StaffInvitationEndpoints.cs:136`).**
- Mutação: neutralizar a checagem de `StaffRoleCatalog.ManageAccess` na emissão.
- Critério de aceite: RF-03, "Somente o administrador emite convite" (RN-14), "nada é comunicado à Auditoria".
- Nenhum teste do Identity (108) falhou. A recusa só é provada no BFF, cujo teste E2E usa um fake do
  Identity; é a lacuna 4.0 R3.
- Correção: teste HTTP no Identity com uma sessão sem `acesso.gerir`, esperando 403 `PERMISSION_DENIED`,
  nenhum convite criado e nenhuma mensagem na outbox.

## 6. Recomendações (não bloqueiam)

Reavaliadas as lacunas acumuladas das revisões focused. Nenhuma quebrou jornada, contrato ou CI no smoke.

- **R1 (3.0 R5):** o login anônimo `POST /api/v1/staff-sessions` não confere o `Origin`
  (`BffSecurityMiddleware.cs:188-190` só exige `Origin` no aceite). O risco é login CSRF, mitigado por
  `SameSite=Lax`. Vale estender a checagem de `Origin` aos POSTs anônimos.
- **R2 (4.0 R1):** corrida entre convites simultâneos ao mesmo e-mail com chaves de idempotência distintas.
  O índice parcial resolve, mas vale um teste de concorrência.
- **R3 (4.0 R2 e 6.0 R4):** o 422 `ROLE_NOT_SUPPORTED` fica fora da lista pública e só é alcançável por
  chamada direta. É aceitável como defesa em profundidade.
- **R4 (5.0 R1):** a `Idempotency-Key` do aceite é gerada por envio na SPA.
- **R5 (6.0 R3):** `listStaffMembersInternal` devolve 400 `INVALID_REQUEST`, mas
  `internal-api-contract.yaml` não o declara. Alinhar o contrato.
- **R6 (8.0 R2/R3/R4):**
  - O JWKS pode devolver `keys` vazio, fora da cardinalidade `minItems: 1`.
  - O BFF propaga 401 `TOKEN_INVALID`, que o contrato não prevê.
  - Uma resposta inesperada do Commerce pode virar 500 genérico.
- **R7 (9.0 R1/R2):**
  - Faltam testes HTTP dos `internal/v1/staff-*` de recuperação no Identity.
  - O caminho de conta interna faz mais trabalho (token + outbox) do que o de conta inexistente, o que
    abre um canal de tempo. Registrar como risco aceito ou nivelar.
- **R8:** o fixture de integração do notification é sensível à latência do daemon Docker no `DisposeAsync`.
  Vale considerar um timeout maior ou uma limpeza tolerante.
- **R9:** `docs/student-registration-local-development.md` só descreve as migrações do Identity. Num volume
  novo, o Compose não cria os bancos e logins de notification, commerce, audit (inclusive
  `code_for_coders_audit_runtime` e `code_for_coders_audit_writer`) e bffs. O checkpoint de fase só é
  reproduzível com passos manuais.

Padrões de design: não houve pressão concreta que justificasse recomendar refatoração. `StaffRoleActionExecutor`
concentra conceder, revogar e trocar com replay idempotente em 545 linhas. Extrair uma estratégia por ação
só compensa se surgir um quarto tipo de ato.

## 7. Atribuição

| Bloqueante | Task | Tipo |
|---|---|---|
| B1 | 3.0 (com 2.0, 4.0, 8.0, 9.0) | CI: cobertura do bff-admin |
| B2 | 8.0 | CI: cobertura do commerce (herdada) |
| B3 | 1.0 / 8.0 (gatilho `Directory.Packages.props`) | CI: learning e media herdados; exige decisão |
| B4 | 2.0 | Teste não discrimina uso único do link |
| B5 | 4.0 | Teste não discrimina a autorização de convite no Identity |
