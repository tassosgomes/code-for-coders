# Revisão full — Notificação transacional por e-mail

Run: run.118db6
Modo: full · tentativa 1/3 (nova rodada pós-intervenção) · resultado: FULL VALIDATION APROVADA

## Identidade
- base_ref: `83978baf6dc03c47aa5a8a593edc95f9e7585ed4`
- validated_commit: `a3869da6015085b427c5c94d8d7f2f2bcf96b91c` (HEAD)
- validated_tree: `983872c2d365f49321bc039200bd27e70e9d8403` (working tree incl. task 8.0 sem checkpoint: diff `83978baf` + untracked `ConcurrentNamespaceIsolationTests.cs`, `NotificationNamespace.cs`, `NotificationProcessingOptions.cs`, `RabbitMqResourceNames.cs`, `20260922141129_AddProcessingNamespace*`)
- HEAD e árvore estáveis durante toda a revisão (status-hash e HEAD conferidos antes/depois do sensor; relatório anterior preservado em `.tsg-flow/delegate-logs/run.2GpfB0px/report.bak.md`).

## Escopo e evidência
- Revisados PRD, TechSpec, contracts/AsyncAPI, tasks 1.0–8.0, baseline e diff completo desde `83978baf` (committed + working tree), com foco na fronteira de namespace da task 8.0.
- Gate CI `dotnet test --no-restore --configuration Debug --coverage --coverage-output-format cobertura` (src/notification), 6 execuções:
  - Execução 1 (Docker frio, 1ª do dia): **falha de ambiente** — IntegrationTests 19/19 falharam a 0ms (init do collection fixture: readiness de container ~60s) e EndToEndTests 1 falhou; nenhum corpo de teste executado; assinatura distinta do B1 (que era 2 testes específicos com estado cruzado). Não reproduziu.
  - Execuções 2–6: **43/43 exit 0** cada (Unit + Architecture + Integration 19 + E2E). B1 (run.2GpfB0px: `RetryProviderTransientFailureTests`, `DeliverPasswordRecoveryEmailTests`) não recorreu em nenhuma execução.
- `dotnet format --verify-no-changes` → 0; `dotnet ef migrations has-pending-model-changes` (Infra.Data) → "No changes", 0; `dotnet publish` Api → 0; gate da task 8.0 (`dotnet build IntegrationTests.csproj --no-restore -warnaserror`) → 0 erros/0 warnings.
- G14: `asyncapi-contract.yaml` sem diff vs base; routing keys publicadas/consumidas permanecem `notificacao.*.v1` (`RabbitMqPublisher.cs:33`, `DeliverAcceptedNotification.cs:88,132`); apenas exchanges/filas (topologia física, parâmetro de implantação por contracts.md decisão 5) recebem sufixo de namespace (`RabbitMqResourceNames.Compose`).
- Fronteira 8.0 conferida ponta a ponta: coluna `namespace` + PK composta dos counters + chave única `(namespace, tenant_id, request_id)` + índices compostos (migration `20260922141129`, snapshot consistente); claims SQL do `OutboxPublisherWorker.cs:72-83` e `DeliveryRecordRepository.ClaimNextAcceptedAsync:46-65` filtrados por namespace; query filters do `NotificationDbContext.cs:58-66`; `TenantContext`/`NotificationProcessingOptions` com validação no startup (`Application/DependencyInjection.cs:18-25`, charset/length em `NotificationNamespace`); purge worker e `OutboxHealthCheck` delimitados; heartbeat worker usa fila composta; appsettings `Notification:Namespace=default`; testes 1.0–7.0 roda cada classe em namespace próprio; `ConcurrentNamespaceIsolationTests` cobre hosts concorrentes (e-mail/outbox/eventos por namespace) e limpeza seletiva.

## Sensor de discriminação
- Worktree isolado em `/tmp/opencode` (base HEAD + diff da working tree), controle 2/2 pass, depois 5 mutações comportamentais na fatia 8.0, suíte focalizada `ConcurrentNamespaceIsolationTests`:
  - M1 `OutboxPublisherWorker` claim sem filtro de namespace → 1 falha (detectada);
  - M2 `ClaimNextAcceptedAsync` sem filtro de namespace → 2 falhas (detectada);
  - M3 query filters do DbContext sem namespace → 2 falhas (detectada);
  - M4 `RabbitMqResourceNames.Compose` sem sufixo (topologia compartilhada) → 2 falhas (detectada);
  - M5 `OutboxMessageWriter` gravando namespace fixo `default` → 1 falha (detectada).
- 5/5 mutantes mortos; nenhum sobrevivente. Worktree removido; status-hash e HEAD idênticos à linha de base.

## Bloqueantes
Nenhum. O B1 (run.2GpfB0px) está resolvido estruturalmente: workers reivindicam/publicam somente dentro do próprio namespace e os testes discriminam a fronteira (M1–M5).

## Recomendações
1. **Invariante operacional do namespace:** a idempotência RN-N09 agora é `(namespace, tenant_id, pedido_id)`; réplicas de produção devem partilhar um único `Notification:Namespace` (default). Divergência silenciosa divide dedup/outbox — registrar em checklist de deploy e/ou alertar se namespaces múltiplos aparecerem no banco.
2. Robustez a cold-start do Testcontainers (falha da execução 1): considerar waits/timeout explícitos ou política de retry de infra no CI para não consumir tentativa de código.
3. `HttpTransactionalEmailSender.cs:50-51`: HTTP 408 não é classificado como transitório (só 429 e ≥500).
4. `Infra.Data/DependencyInjection.cs:49-52`: validar no startup também `recuperacao-de-senha` (hoje só `confirmacao-de-conta`).
5. Alinhar `tenantId` do AsyncAPI (string/exemplo não-UUID) ao `Guid` dos contratos C#.
6. Menor: em `ConcurrentNamespaceIsolationTests.AssertOutboxOnlyContainsNamespaceAsync`, `processed.All(m => m.Namespace == ns)` é redundante sob o query filter (inofensivo).

## Veredito
**FULL VALIDATION APROVADA** — 0 bloqueantes, 6 recomendações. Gate determinístico em 5/6 execuções (a única falha foi de ambiente/cold-start no init do fixture, sem corpo de teste executado e não reprodutível); evidências de format, EF, publish e sensor 5/5 conforme acima.
