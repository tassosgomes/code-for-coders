# Revisão full — Trilha de auditoria (CAP-030, fatia mínima)

Run: run.Wuikipl6
Modo: full · Tentativa: 1/3 · Data: 2026-09-24

- **base_ref:** `ffa246fdc53acd1b392aab02a3c890f53ac66d01` (`origin/main`)
- **validated_commit:** `a759d7b2a8ec0567f676b520c72802ef10f52d8d`
- **validated_tree:** `ab15fa056821b0774d8b37424b289e9df2fd82d2`
- **Estabilidade:** HEAD e árvore iguais no início e no fim. `git status --porcelain` igual à linha de base:
  `flow-state.json` modificado e `.tsg-flow/` não rastreado, ambos anteriores à revisão. Os checks rodaram
  em worktrees temporários (`HEAD` e base), removidos ao final.

## Resultado: FULL VALIDATION REPROVADA

2 bloqueantes, ambos no job obrigatório `ci / Build & Test` de `audit.yml`. 3 recomendações.

## Matriz de CI — `src/audit`

Fonte: `.github/workflows/audit.yml` → `tassosgomes/template-pipeline/.github/workflows/ci-dotnet.yml@v1`
(lido na ref `v1`). Parâmetros: `working-directory: src/audit`, `test-configuration: Debug`,
`coverage-threshold: 70`, `build-container: true`, `security-mode: observe`. SDK local 10.0.400
(`global.json` 10.0.100, rollForward latestFeature; runner MTP).

| Passo obrigatório | Comando | Exit | Resultado |
|---|---|---|---|
| Restore | `dotnet restore` | 0 | OK |
| Lint | `dotnet format --verify-no-changes --no-restore` | 0 | OK |
| Testes | `dotnet test --no-restore --configuration Debug --coverage --coverage-output-format cobertura` | **2** | **59 testes, 58 passaram e 1 falhou** (`EndToEndTests`). Ver B-01 |
| Cobertura ≥ 70% | Script de agregação do workflow (união de linhas de todos os `.cobertura.xml`) | — | **64,04%** (1391/2172) na execução real. Ver B-02 |
| Publish | `dotnet publish --configuration Release --no-restore --output ./publish` | 0 | OK (aviso NETSDK1194, também presente no CI) |
| Container | `docker build -f src/audit/Dockerfile .` | 0 | OK. Build local, sem push |
| Scan do container (Trivy), SBOM | — | não executado | Ferramentas ausentes. `observe` / observacional |
| Job `security`: SAST (Semgrep), dependências (Trivy) | — | não executado | Ferramentas ausentes. `observe`, não bloqueiam |
| Job `security`: segredos (gitleaks, `enforce`) | — | não executado | `gitleaks` ausente. Passo bloqueante sem evidência; fica pendente para a próxima full |

Outros workflows acionados: a mudança em `Directory.Packages.props` (uma `PackageVersion`
`Microsoft.Extensions.Logging.Abstractions` a mais) casa com o filtro `Directory.*` de `identity`,
`notification`, `commerce`, `learning`, `media`, `bff-admin` e `bff-student`. Esses jobs **não foram executados**.
A veredito já está decidido por B-01 e B-02. Com CPM, uma versão central sem referência é inerte, mas a
próxima full precisa registrar evidência deles ou justificar a dispensa.

Verificação complementar: `dotnet ef migrations has-pending-model-changes` retornou exit 0, sem mudança
pendente no modelo.

## Bloqueantes

### B-01 — `EndToEndTests` quebrado pela migration `SecureAuditRuntimePermissions` (regressão)

- **Local:** `src/audit/src/CodeForCoders.Audit.Infra.Data/Migrations/20260924184735_SecureAuditRuntimePermissions.cs:21-31`
  e `src/audit/tests/CodeForCoders.Audit.EndToEndTests/AuditApiFactory.cs:21-30` (fora do diff).
- **Falha:** `AuditHealthEndpointTests.LiveHealthEndpointConfirmsAuditProcessIsAlive`. A fixture
  `AuditApiFactory.InitializeAsync` lançou `Npgsql.PostgresException 42704: role "code_for_coders_audit_runtime" does not exist`
  durante `MigrateAsync`.
- **Causa:** a migration concede privilégios a `code_for_coders_audit_runtime` e `code_for_coders_audit_writer`,
  o que exige que esses papéis já existam (pré-condição P-02). A factory de E2E sobe um PostgreSQL limpo e
  migra sem criá-los. O `AuditIntegrationFixture` foi ajustado, a factory de E2E não.
- **Regressão confirmada:** na base `ffa246f`, o mesmo projeto passa (exit 0, 1/1).
- **Impacto:** o passo `Testes` do CI sai com código ≠ 0, e o job `Build & Test` reprova.
- **Correção esperada:** alinhar a fixture de E2E à pré-condição de deploy, criando os papéis antes de migrar
  (como o fixture de integração), e rodar o host com a credencial de execução quando fizer sentido. Não
  enfraquecer nem remover o teste.

### B-02 — Cobertura agregada abaixo de 70%, mesmo com B-01 corrigido

- **Medição do gate:** 64,04% na execução real. Esse valor está deprimido porque a E2E não rodou.
- **Diagnóstico (não é aprovação):** num worktree descartável, a factory de E2E foi ajustada para criar os
  dois papéis. Com isso, 59/59 testes passam, e a cobertura agregada fica em **68,97%** (1498/2172), ainda
  **< 70%**.
- **Base:** 53,46% (633/1184), 14/14 testes. O gate de cobertura **já reprovava na base**. A execução mais
  recente de `audit.yml` no GitHub, em `61a8a1e` (ancestral da base), também terminou em `failure` no job
  `Build & Test`. O log não estava disponível para confirmar o passo. `tasks.md` § Verificação herdada tinha
  registrado a cobertura como “não medida”. A entrega melhora a cobertura, mas pela regra da full o job
  obrigatório continua reprovado.
- **Maior fonte de linhas descobertas:** código gerado pelo `Microsoft.AspNetCore.OpenApi.SourceGenerators`
  (`src/CodeForCoders.Audit.Api/obj/.../OpenApiXmlCommentSupport.generated.cs`), com 377 linhas e 0 hits,
  entra no agregado. Depois vêm `GlobalExceptionHandler.cs` (60 linhas), `ObservabilityExtensions.cs` (34),
  `HealthExtensions.cs` (33) e as migrations novas (52 + 43 + 14).
- **Correção esperada:** atingir ≥ 70% no agregado do workflow. Isso pode vir de testes que exercitem o
  código da Api/erros, ou de uma exclusão legítima e declarada do código gerado na configuração de cobertura
  do projeto. A exclusão precisa ser justificada no relatório da task. Não se muda o limite do workflow.

## Sensor de discriminação

**Não executado.** A skill manda rodá-lo só depois de os checks obrigatórios passarem, e B-01/B-02 já
determinam o veredito. A próxima full precisa executá-lo. As revisões focused já registraram mutações
pontuais: P-01, a remoção do `REVOKE CONNECT`, foi detectada.

## Rastreabilidade (resumo)

| Requisito | Evidência no diff | Situação |
|---|---|---|
| RF-01 / V-01 | `AdministrativeActRecordingTests` (os quatro tipos, aceite sem motivo, dois tenants, campo `email` extra descartado, logs sem `motivo`) | Coberto; testes verdes |
| RF-02 / V-02 | `AdministrativeActRedeliveryTests`; unicidade `ux_audit_records_origem_fato_id` + `AuditRecordAlreadyExistsException` + impressão SHA-256 | Coberto |
| RF-03 / V-03 | `AdministrativeActPolicy` (7 códigos), `AdministrativeActConformityTests`, `NonConformingActRecordingTests`; `tipo`, `motivo`, `autor_tipo` e `alvo_tipo` em `text` (migration `RecordNonConformingAdministrativeActs`) | Coberto |
| RF-04 / V-04 | `TryDeserializeAct` → `nack(requeue:false)`; transitória → `BasicRejectAsync(requeue:true)` (D-01); `AuditDeadLetterTests`, com uuid inválido de A-01 | Coberto. D-01 conferido: comentário no código e teste de DLQ |
| RF-05 / V-05 | `SecureAuditRuntimePermissions`, `AuditImmutabilityTests`, `AuditAppendOnlyGuardrailTests`, compose/init com `code_for_coders_audit_runtime` | Coberto nos testes de integração; **a E2E quebra com a mesma pré-condição (B-01)** |
| Telemetria sem dado pessoal | Tags e logs só com `fatoId`/`origem`/`tipo`/`tenantId`/razões; `EnableSensitiveDataLogging` removido | Conforme a TechSpec |

## Recomendações (não bloqueiam)

1. **A-02 — `audit.acts.recorded` só conta atos conformes.** A TechSpec (V-01) define o atributo
   `conformity=conforming`, e a V-03 define um incremento de `audit.acts.nonconforming` por ato. A
   implementação não viola nenhuma das duas. Mesmo assim, o atributo `conformity`, fixo em `conforming`, dá a
   entender que haveria outro valor. Para a regra de alerta da QT-02, a plataforma precisa saber que o total
   de atos gravados é a soma dos dois contadores. Sugestão: documentar isso no guardrail/notas, ou incrementar
   `recorded{conformity=non_conforming}` também. Qualquer escolha exige atualizar a TechSpec.
2. **D-01 — atualizar a TechSpec.** Trocar `nack(requeue: true)` por `reject(requeue: true)` na § V-04, como
   a nota de implementação prevê.
3. **`EntityValidationException` na criação do registro** hoje só ocorre se o envelope escapar da
   classificação do worker, porque as condições são as mesmas. Se isso acontecer, a mensagem é tratada como
   transitória: recebe 5 reentregas e depois vai à DLQ. É aceitável, mas mandá-la direto à DLQ, como
   ilegível, deixaria o sinal mais claro.

## Notas conhecidas conferidas

- D-01: aceito e conferido (`AuditEventConsumerWorker.cs`, `TryNackAsync`).
- A-01: resolvido. `ReadGuid` usa `TryParse`, e há testes para uuid inválido.
- A-02: ver recomendação 1.
- P-01 e P-02: resolvidos no fixture de integração e no guardrail. **A factory de E2E não recebeu o ajuste
  correspondente (B-01).**
- R3: pré-existente, só no Compose local. Não afeta o CI.
