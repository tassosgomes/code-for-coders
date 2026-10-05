---
status: pending
task_kind: enabling
blocked_by: []
gate: 'dotnet build src/billing/CodeForCoders.Billing.slnx && dotnet test --project src/billing/tests/CodeForCoders.Billing.EndToEndTests/CodeForCoders.Billing.EndToEndTests.csproj -- --filter-class CodeForCoders.Billing.EndToEndTests.PlatformHeartbeatEndpointTests --minimum-expected-tests 1 && docker compose -f docker-compose.yml config -q && docker compose -f docker-compose.yml -f docker-compose.remote.yml config -q && docker compose -f docker-compose.coolify.yml config -q'
gate_expect: "build sem erros; o teste de heartbeat de billing passa (≥ 1) com os registros reais; os três arranjos de compose validam com exit 0"
---

# 3.0 A unidade `billing` compila, sobe saudável e está na esteira e nos ambientes

**Fatia:** EN-01 · **Cobre:** RN-CB01 (segredos do gateway fora do código), D-09 · **Spec:** `techspec.md` § Habilitadores inevitáveis, § Bloco Backend (`billing`) · **ADR:** [ADR-0001](../../docs/adr/0001-monorepo-de-codigo.md), [ADR-0002](../../docs/adr/0002-plataforma-de-runtime-coolify.md), [ADR-0017](../../docs/adr/0017-autenticacao-de-servico-commerce-em-billing.md)

## Comportamento

`billing` passa a ser a 11ª unidade do monorepo, no golden path dos demais serviços .NET (`docs/foundation.md`; skill `dotnet`), **sem nenhum comportamento de pagamento ainda**:

- Solução `src/billing/CodeForCoders.Billing.slnx` nas camadas e projetos de teste padrão (unit, integration, end-to-end, architecture, comum), com os defaults da raiz (`Directory.*`, `BannedSymbols.txt`, analisadores).
- Banco próprio `code_for_coders_billing` com papel próprio; outbox e o heartbeat de plataforma como nos outros serviços; health `/health/live` e `/health/ready`; OpenTelemetry com nome de serviço `CodeForCoders.Billing`.
- Exchange próprio `billing.events` e DLX declarados na inicialização.
- Configuração preparada, sem valor padrão nem segredo no repositório: `Stripe:SecretKey`, `Stripe:WebhookSigningSecret`, `ServiceAssertions` (audiência `billing`; emissor `commerce` com escopo `payment:request`, chave pública e tenants), lista de hosts de retorno permitidos (D-11). A aplicação **não sobe** sem os segredos fora de `Development`/teste.
- Imagem `src/billing/Dockerfile`; workflow `.github/workflows/billing.yml` no formato de `commerce.yml` (mesmo workflow reutilizável, cobertura 70).
- `docker-compose.yml` (porta `5110:8080`, dependências como as dos outros serviços), override em `docker-compose.remote.yml` (sem hostname local, AGENTS.md) e serviço em `docker-compose.coolify.yml` com o domínio público reservado ao webhook (pendência da TechSpec: definir o domínio; a regra de expor só `/webhooks/v1` fica registrada no compose ou na nota de implantação).
- `scripts/init-local-databases.sql` e `scripts/remote-infra.sh` (`provision` cria banco e papel; `migrate` aplica as migrations de `billing`); `scripts/apps.sh` (saúde em `http://localhost:5110/health/ready`); `scripts/generate-local-env.sh` gera o par `commerce`→`billing` (`COMMERCE_BILLING_*`) e o par `notification`→`identity` (`NOTIFICATION_IDENTITY_*`), e documenta `STRIPE_SECRET_KEY` e `STRIPE_WEBHOOK_SECRET` como variáveis que o desenvolvedor preenche com a chave de teste.
- `docs/foundation.md` lista a unidade nova.

## Fora do escopo desta task

- Página de pagamento, webhook, pagamento e fatos → 5.0 a 7.0.
- Chamada de `commerce` a `billing` → 5.0 (a chave é gerada aqui; o uso nasce lá).
- Emissor `notification` em Identity → 8.0 (a chave é gerada aqui).
- Cadastro do webhook no painel do Stripe e domínio público definitivo de homologação.

## Decisões fechadas

- `billing` é serviço próprio, não módulo de `commerce` (OD81, C-01); a Fase 0 dele está neste plano (D-09).
- Porta local 5110; exchange `billing.events` (D-04).
- Segredos do Stripe só por variável de ambiente/segredo do Coolify (ADR-0002); nunca em `appsettings`, imagem ou repositório.

## Modificar / Referenciar

- **modificar:** `docker-compose.yml`, `docker-compose.remote.yml`, `docker-compose.coolify.yml` (serviço `billing`); `scripts/init-local-databases.sql`, `scripts/remote-infra.sh`, `scripts/apps.sh`, `scripts/generate-local-env.sh`; `docs/foundation.md` (tabela de unidades)
- **ref:** `src/commerce/` e `src/notification/` (golden path: camadas, `Program.cs`, outbox, heartbeat, testes); `.github/workflows/commerce.yml`; `src/commerce/Dockerfile`; `docs/foundation.md`; `docs/infra-servidor-desenv.md`; AGENTS.md (infra remota, override remoto)

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/billing` | `dotnet format src/billing/CodeForCoders.Billing.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/billing` | `dotnet test --project src/billing/tests/CodeForCoders.Billing.ArchitectureTests/CodeForCoders.Billing.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/billing` | `dotnet test --project src/billing/tests/CodeForCoders.Billing.IntegrationTests/CodeForCoders.Billing.IntegrationTests.csproj` | exit 0 (infraestrutura: banco, outbox, RabbitMQ) | `ci-dotnet.yml` Testes |
| `src/billing` | `dotnet publish src/billing/src/CodeForCoders.Billing.Api/CodeForCoders.Billing.Api.csproj -c Release` | exit 0 | `ci-dotnet.yml` |
| Imagem | `docker build -f src/billing/Dockerfile .` | exit 0 | `ci-dotnet.yml` build-container |
| Ambiente | `scripts/remote-infra.sh provision && scripts/remote-infra.sh migrate` | exit 0; banco de `billing` existe | AGENTS.md |

## Pronto quando

- [ ] Gate passa (exit 0).
- [ ] Com `scripts/apps.sh start --remote`, `http://localhost:5110/health/ready` responde saudável e o heartbeat de `billing` é publicado e consumido.
- [ ] Sem `STRIPE_SECRET_KEY` fora de desenvolvimento, a aplicação não sobe e diz qual configuração falta, sem expor valor.
- [ ] Nenhum segredo do Stripe aparece em arquivo versionado (busca por `sk_test_`/`whsec_` no repositório vazia).
