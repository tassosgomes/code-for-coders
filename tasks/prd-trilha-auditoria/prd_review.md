# Revisão full — Trilha de auditoria (CAP-030, fatia mínima)

Run: run.nt0gsIJS
Modo: full · Tentativa: 2/3 · Data: 2026-09-24
Tentativa anterior: `run.Wuikipl6` (1/3), **reprovada** por B-01 (E2E quebrada pela migration) e B-02
(cobertura < 70%) sobre `a759d7b`. A correção entrou na rodada 3 da 5.0 (fix `run.3L9XnKW4`, revalidação
`run.qTGaj4gk`).

- **base_ref:** `ffa246fdc53acd1b392aab02a3c890f53ac66d01` (`origin/main`, alvo escolhido pelo usuário)
- **validated_commit:** `bc1f9395463d05eeb2e151d8d2008bd9d3e39027`
- **validated_tree:** `f0ba0547c7b36e84f06d362aa2bed56425f5a20a`
- **Estabilidade:** HEAD e árvore iguais no início e no fim. `git status --porcelain` igual à linha de base
  (`flow-state.json` modificado e `.tsg-flow/` não rastreado, ambos anteriores à revisão). Checks e mutações
  rodaram em worktrees temporários de `HEAD`, removidos ao final.

## Resultado: FULL VALIDATION APROVADA

0 bloqueantes. 5 recomendações (não bloqueiam).

## Matriz de CI — `src/audit`

Fonte: `.github/workflows/audit.yml` → `tassosgomes/template-pipeline/.github/workflows/ci-dotnet.yml@v1`
(lido via `gh api` na ref `v1`, incluindo as actions `secret-scan` e `upload-findings`). Parâmetros:
`working-directory: src/audit`, `test-configuration: Debug`, `coverage-threshold: 70`, `build-container: true`,
`security-mode: observe`. SDK local 10.0.400 (`global.json` 10.0.100, rollForward latestFeature; runner MTP).
Todos os comandos rodaram via `rtk proxy` num worktree limpo de `bc1f939`.

| Passo obrigatório | Comando | Exit | Resultado |
|---|---|---|---|
| Restore | `dotnet restore` | 0 | OK |
| Lint | `dotnet format --verify-no-changes --no-restore` | 0 | OK |
| Testes | `dotnet test --no-restore --configuration Debug --coverage --coverage-output-format cobertura` | 0 | **59/59** (Architecture, Unit, Integration, EndToEnd). B-01 resolvido |
| Cobertura ≥ 70% | Script Python do workflow, copiado literalmente (união de linhas dos 4 `.cobertura.xml`) | — | **83,45%** (1498/1795). B-02 resolvido |
| Publish | `dotnet publish --configuration Release --no-restore` | 0 | OK |
| Container | `docker build -f src/audit/Dockerfile .` (contexto na raiz) | 0 | OK. Build local, sem push. Imagem removida ao final |
| Job `security`: segredos (gitleaks 8.30.1, `enforce`, `severity-fail-on: low`) | `gitleaks dir src/audit --report-format sarif --exit-code 0 --no-banner --config <toml gerado como na action: useDefault + allowlist '^\.platform/'>`, cwd na raiz (lê `.gitleaksignore`) | 0 | **0 achados** no SARIF. O gate de enforce passaria. A varredura incluiu `bin/obj` do worktree, então foi mais ampla que a do CI |
| Job `security`: SAST (Semgrep), dependências (Trivy) | — | não executado | Ferramentas ausentes. `observe`, não bloqueiam |
| Scan do container (Trivy), SBOM, DAST | — | não executado | Observacionais / `observe`. Não bloqueiam |

**Exclusão de cobertura (decisão do usuário):** conferida e restrita a código gerado.
`src/audit/tests/testconfig.json` exclui só `.*[\\/]obj[\\/].*` e `.*\.g(enerated)?\.cs$`. Nenhum arquivo nos
relatórios casa com esses padrões. O total caiu de 2172 (tentativa 1, com E2E corrigida em diagnóstico) para
1795 linhas, uma diferença de exatamente 377, que são as linhas de `OpenApiXmlCommentSupport.generated.cs`.
As linhas cobertas ficaram iguais (1498). Os descobertos mantidos pela equipe continuam visíveis. O
`Directory.Build.props` de `tests/` importa o da raiz, então `TreatWarningsAsErrors` e BannedSymbols
continuam valendo.

### Outros workflows acionados por `Directory.Packages.props` (filtro `Directory.*`)

A mudança adiciona só `<PackageVersion Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.12" />`.
O único projeto que a referencia é `CodeForCoders.Audit.Application.csproj` (grep em `*.csproj`/`*.props` de `src/`).
Com CPM, a versão central fica inerte nos demais serviços.

| Workflow | `dotnet restore` | `dotnet format --verify-no-changes --no-restore` | `dotnet build --no-restore -c Debug` |
|---|---|---|---|
| identity | 0 | 0 | 0 |
| notification | 0 | 0 | 0 |
| commerce | 0 | 0 | 0 |
| learning | 0 | 0 | 0 |
| media | 0 | 0 | 0 |
| bff-admin | 0 | 0 | 0 |
| bff-student | 0 | 0 | 0 |

Não rodei testes nem cobertura desses serviços. A dispensa se apoia em dois fatos: nenhum projeto deles
referencia o pacote, então o grafo de dependências não muda; e restore e build passaram. Nenhum outro arquivo
desses serviços está no diff. Os arquivos fora de `src/audit` são `Directory.Packages.props`,
`docker-compose.yml` (só a connection string do `audit`) e `scripts/init-local-databases.sql` (só papéis e
`CONNECT` do banco `audit`).

## Sensor de discriminação

Executado depois de os checks passarem, em worktrees temporários, uma mutação de comportamento por fatia,
contra a suíte completa. Todas as mutações foram restauradas por `git checkout`, e os worktrees foram removidos.

| # | Fatia / critério | Arquivo:linha | Mutação | Resultado |
|---|---|---|---|---|
| M1 | V-01: registro fiel do ato (alvo) | `RecordAdministrativeAct.cs:105` | `MapReference(act.Alvo)` → `MapReference(act.Autor)` | **Detectada.** Exit 2, 4 falhas (`RecordsInternalInvitationIssued`, `…AcceptedWithoutReason`, `RecordsRoleGranted`, `RecordsRoleRevoked`) |
| M2 | V-02: reentrega idêntica vs. divergente | `RecordAdministrativeAct.cs:51` | `string.Equals` → `!string.Equals` (troca `identical`/`divergent`) | **Detectada.** Diagnóstico focalizado em `AdministrativeActRedeliveryTests`: exit 2, 3/4 falham (`AcknowledgesIdenticalRedelivery…`, `ConcurrentRedeliveries…`, `AcknowledgesDivergentRedelivery…`). A execução da suíte completa **travou** (ver rec. 1) |
| M3 | V-03: aceite de convite sem motivo é conforme | `AdministrativeActPolicy.cs:20` | `["convite-interno-aceito"] = false` → `true` | **Detectada.** Exit 2, 2 falhas (`InvitationAcceptedWithoutReasonIsConforming`, `RecordsInternalInvitationAcceptedWithoutReason`) |
| M4 | V-04: ilegível vai à DLQ intacto | `AuditEventConsumerWorker.cs:84` | `requeue: false` → `requeue: true` | **Detectada.** Exit 2, 10 falhas (`Sends…ToTheDeadLetterQueueIntact`) |
| M5 | V-05: credencial só `SELECT, INSERT` | `20260924184735_SecureAuditRuntimePermissions.cs:31` | `GRANT SELECT, INSERT` → `GRANT SELECT, INSERT, UPDATE, DELETE` | **Detectada.** Exit 2, 2 falhas (`RuntimeCredentialCannotUpdateAuditRecords`, `…DeleteAuditRecords`) |

Nenhum mutante sobreviveu. Nas revisões focused, a remoção do `REVOKE CONNECT` (P-01) também foi detectada.

## Rastreabilidade

O código de produção não mudou desde a tentativa 1 (`git diff a759d7b..HEAD -- src/` só toca
`AuditApiFactory.cs` e a configuração de cobertura dos testes). A matriz da tentativa 1 continua válida e agora
tem a suíte agregada verde.

| Requisito | Evidência | Situação |
|---|---|---|
| RF-01 / V-01 | `AdministrativeActRecordingTests` (quatro tipos, aceite sem motivo, dois tenants, campo extra descartado, logs sem `motivo`). M1 foi detectada | Coberto |
| RF-02 / V-02 | `AdministrativeActRedeliveryTests`, `ux_audit_records_origem_fato_id`, `AuditRecordAlreadyExistsException` e impressão SHA-256. M2 foi detectada | Coberto |
| RF-03 / V-03 | `AdministrativeActPolicy` (7 códigos), `AdministrativeActConformityTests`, `NonConformingActRecordingTests`. M3 foi detectada | Coberto |
| RF-04 / V-04 | `TryDeserializeAct` → `nack(requeue:false)`, transitória → `reject(requeue:true)` (D-01), `AuditDeadLetterTests`. M4 foi detectada | Coberto |
| RF-05 / V-05 | `SecureAuditRuntimePermissions`, `AuditImmutabilityTests`, `AuditAppendOnlyGuardrailTests`. A E2E agora sobe com a credencial de execução. M5 foi detectada | Coberto |
| Telemetria sem dado pessoal | Tags e logs só com `fatoId`/`origem`/`tipo`/`tenantId`/razões | Conforme a TechSpec |

**Segurança:** a E2E usa a senha literal de teste `audit-runtime-test-password` num container efêmero. O
gitleaks não a sinaliza. O Compose usa a credencial de execução (`code_for_coders_audit_runtime`), e o init
local revoga `CONNECT` dos outros serviços. **Arquitetura:** sem mudança de camadas desde a tentativa 1. Os
`ArchitectureTests` passaram (8/8).

## Recomendações (não bloqueiam)

1. **Travamento da suíte completa sob falha na reentrega.** Com M2 aplicada, `dotnet test` na solução ficou
   mais de 12 minutos sem progresso, com o log parado depois de Unit, Architecture e EndToEnd, e os containers
   ainda ativos. Encerrei a execução. Rodado sozinho, o mesmo teste falha em cerca de 5 s. A causa provável é o
   teardown depois de uma falha em `ConcurrentRedeliveriesCreateOneRecordAndDoNotReachTheDeadLetterQueue` (dois
   hosts mais uma transação com `LOCK TABLE … IN SHARE MODE`) interagindo com as demais classes na execução
   agregada. Não investiguei a fundo. Uma regressão real nessa área apareceria no CI como timeout do job, não
   como falha clara. Sugestões: `--timeout`/hang-dump do MTP ou cancelamento explícito nos hosts do teste
   concorrente. Não afeta o código correto: a suíte verde conclui em cerca de 30 s.
2. **A-02 — `audit.acts.recorded` só conta atos conformes** (mantida da tentativa 1). A TechSpec (V-01, linha
   126) fixa `conformity=conforming`, e a V-03 (linha 175) usa `audit.acts.nonconforming`. Não há violação,
   mas a regra de alerta QT-02 precisa saber que o total é a soma dos dois contadores. Documente isso ou
   atualize a TechSpec.
3. **D-01 — atualizar a TechSpec.** As linhas 89 e 189 ainda dizem `nack(requeue: true)`. O código usa
   `BasicRejectAsync(requeue: true)`, que é o comportamento correto no RabbitMQ 4.3.
4. **R5 — senha duplicada em `AuditApiFactory`.** O literal aparece no SQL e na constante `RuntimePassword`.
   Se um dos dois mudar sozinho, a E2E falha por autenticação. Não há falso positivo.
5. **R6 — `dotnet publish` da solução publica os projetos de teste** (e agora os `*.testconfig.json`). Isso é
   anterior a esta entrega e inofensivo. Considere `IsPublishable=false` nos testes.

## Notas conhecidas conferidas

- D-01: aceito, conferido no código (`TryNackAsync`) e pela M4. A TechSpec ainda está pendente (rec. 3).
- A-01: resolvido. Os testes de uuid inválido estão na suíte verde.
- A-02: continua aberto como recomendação 2.
- P-01 e P-02: resolvidos. A factory de E2E agora cria `code_for_coders_audit_writer` e
  `code_for_coders_audit_runtime` antes de `MigrateAsync`, como exige a pré-condição de deploy.
- R3: pré-existente, só no Compose local. Não afeta o CI.
