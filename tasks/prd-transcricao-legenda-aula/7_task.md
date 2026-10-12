---
status: pending
task_kind: vertical
blocked_by: ["4.0"]
gate: 'dotnet test --project src/media/tests/CodeForCoders.Media.UnitTests/CodeForCoders.Media.UnitTests.csproj -- --filter-class CodeForCoders.Media.UnitTests.TranscriptionWebhookSignatureTests --minimum-expected-tests 7 && dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.TranscriptionEventReceiverTests --minimum-expected-tests 12'
gate_expect: "Pelo menos 19 testes .NET passam (7 unitários de assinatura e 12 de integração do receptor em media)"
---

# 7.0 O aviso do serviço acelera o resultado sem ser necessário

**Fatia:** V-03 · **Cobre:** RF-07 (aviso perdido, deduplicação), RN-M23; `receiveTranscriptionEvent` · **Spec:** `techspec.md#v-03-o-aviso-do-serviço-acelera-o-resultado-sem-ser-necessário` · **ADR:** 0020

## Comportamento

`POST /webhooks/v1/transcription-events` (`receiveTranscriptionEvent`) no papel `api` do `media`,
mapeado **só** se há segredo configurado:

- lê o corpo bruto (limite 16 KiB) e exige `webhook-id`, `webhook-timestamp` (janela de ±300 s) e
  `webhook-signature`;
- calcula HMAC-SHA256 de `id.timestamp.corpo` com **cada** segredo configurado (decodifica o `whsec_`
  em base64; aceita **dois** segredos para rotação) e compara em tempo constante com cada assinatura
  `v1,` do cabeçalho; falha → 401 `SIGNATURE_INVALID` sem eco do corpo;
- insere o evento por `webhook-id` (chave primária; conflito = já tratado, 204); se `clientReference`
  é uma tentativa em andamento e o `jobId` confere (ou a tentativa ainda não gravou o job), marca
  `signaled_at` na mesma transação;
- evento de job desconhecido, de tentativa encerrada ou de outro job → 204 sem efeito;
- falha transitória de gravação → 503 `TEMPORARILY_UNAVAILABLE`; **nunca** 410;
- o **worker**, ao ver `signaled_at`, consulta o job já, sem esperar a cadência; o aviso nunca é aceito
  como resultado (D-05); a manutenção apaga avisos antigos.

Em ambiente sem receptor alcançável a transcrição continua fechando só por consulta.

**Contratos:** criar `contracts/media/openapi-webhook.yaml` com `receiveTranscriptionEvent` a partir de
`webhook-contract-media.yaml` e registrá-lo para validação Spectral. A linha da tabela do README fica
para a consolidação (14.0).

## Fora do escopo desta task

- Validação ponta a ponta com origem e destino privados reais: 13.0 (exige `TRUSTED_PRIVATE_WEBHOOKS`, Q-C).
- Não há tela.

## Decisões fechadas

- O aviso é gatilho; a consulta periódica é o caminho obrigatório (D-05).
- Dois segredos aceitos e receptor ausente sem segredo (D-06). Esquema de assinatura: Standard Webhooks.
- Endpoint anônimo protegido por assinatura; não é exposto por Caddy nem pelos SPAs.

## Modificar / Referenciar

- **modificar:** `src/media/src/CodeForCoders.Media.Api/Endpoints/EndpointExtensions.cs` (mapeamento condicional); `src/media/src/CodeForCoders.Media.Application/DependencyInjection.cs`; `src/media/src/CodeForCoders.Media.Infra.Data/MediaDbContext.cs` (se o evento exigir ajuste de modelo) e `Migrations/` (gerada, se houver); `docker-compose.yml`; `docker-compose.remote.yml`; `docker-compose.komodo.apis.yml`; `docker-compose.stable.yml` (`Transcription__Webhook__Secrets__0/1`); `contracts/media/openapi-webhook.yaml` (novo)
- **ref:** `src/billing/src/CodeForCoders.Billing.Api/Endpoints/PaymentEndpoints.cs:12-31` e `contracts/billing/openapi-webhook.yaml` (molde de endpoint de webhook); `tasks/prd-transcricao-legenda-aula/webhook-contract-media.yaml`; `~/github-tassosgomes/whisper/docs/adr/adr-003.md`; skill `dotnet`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/media` | `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.ArchitectureTests/CodeForCoders.Media.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet ef migrations has-pending-model-changes --project src/media/src/CodeForCoders.Media.Infra.Data --startup-project src/media/src/CodeForCoders.Media.Api` | exit 0, sem mudança pendente | AGENTS.md (migrations pela ferramenta do EF Core) |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.TranscriptionPipelineTests --minimum-expected-tests 1` | exit 0 (regressão intacta) | `ci-dotnet.yml` Testes |
| Contratos | `npx --yes @stoplight/spectral-cli lint contracts/media/openapi-webhook.yaml --ruleset contracts/.spectral-exception.yaml --fail-severity=error` | exit 0 | `contracts/README.md` |

## Pronto quando

- [ ] Gate focalizado passa (exit 0).
- [ ] Assinatura válida (com o segredo atual e com o anterior), inválida, ausente e fora de ±300 s tratadas como no contrato (204 ou 401).
- [ ] Duplicata, job desconhecido e tentativa encerrada → 204 sem efeito; falha de gravação → 503; nenhum 410.
- [ ] O aviso marca a tentativa e o worker consulta antes da próxima cadência; um aviso nunca altera o texto.
- [ ] Sem segredo configurado a rota não existe.
