---
status: done
task_kind: vertical
blocked_by: ["5.0"]
gate: 'dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.AccessExpirationTests --minimum-expected-tests 7'
gate_expect: "Pelo menos 7 testes passam: 7 de integração de commerce"
---

# 8.0 A concessão que vence gera o fato informativo, uma vez, sem que a decisão dependa dele

**Fatia:** V-06 · **Cobre:** RF-07 (`acesso-expirado`), US-05, RN-D06, DP-10, C-10 · **Spec:** `techspec.md#v-06-a-concessão-que-vence-gera-o-fato-informativo-uma-vez-sem-que-a-decisão-dependa-dele` · **ADR:** —

## Comportamento

- **`commerce` — rotina de expiração:** serviço de fundo com ciclos configuráveis (padrão de poucos minutos, bem
  abaixo da uma hora de DP-10). Em cada ciclo seleciona, em lote e com `FOR UPDATE SKIP LOCKED`, as concessões
  com `expires_at <= agora` e sem marcador de fato publicado; **na mesma transação** grava
  `matricula.acesso-expirado.v1` no outbox do módulo (um `eventId` novo, `occurredAt` = agora, `expiresAt` da
  concessão) e preenche o marcador. Instâncias concorrentes não duplicam; a rotina executada duas vezes não
  republica. Concessão vitalícia nunca entra. Fato é sobre a **concessão**: uma vencida e outra ativa do mesmo
  aluno e curso geram o fato da primeira e a decisão (7.0) continua `allowed`. A decisão **não lê** o marcador.
- **Defasagem:** métrica (agora − `expires_at` do mais antigo pendente) com alerta acima de 30 minutos; sem
  identificador de pessoa em rótulo (G10).
- **Entrega:** o publicador existente (5.0) leva o fato ao exchange de `commerce`, e a fila de retenção o
  absorve.
- **Caso negativo principal:** rotina desligada, fato atrasado ou perdido → a decisão de 7.0 é a mesma; vitalícia
  → nenhum fato, em qualquer data.

## Fora do escopo desta task

Consumidor do fato (Notificação, Inteligência de Negócio, depois). Qualquer mudança na decisão de acesso.

## Decisões fechadas

- O fato é informativo: atraso ou perda não mudam a decisão (RN-D06, DP-10).
- Exatamente um fato por concessão por período, garantido pelo marcador na mesma transação do outbox, e não por rotina "uma vez".
- `TimeProvider` injetado em toda comparação com o tempo; nenhum `DateTime.Now`.

## Modificar / Referenciar

- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Infra.Data/DependencyInjection.cs` (registro da rotina), módulo `Entitlement` (consulta e marcador)
- **ref:** `src/commerce/src/CodeForCoders.Commerce.Infra.Data/Catalog/PurchaseIntentReceiptCleanupWorker.cs` (padrão de serviço de fundo); `Infra.Messaging/OutboxPublisherWorker.cs` (`FOR UPDATE SKIP LOCKED`); `asyncapi-contract.yaml` (`publicarAcessoExpirado`); skill `dotnet`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/commerce` | `dotnet format src/commerce/CodeForCoders.Commerce.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/commerce` | `dotnet build src/commerce/CodeForCoders.Commerce.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj` | exit 0 (sem regressão) | `ci-dotnet.yml` Testes |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.ArchitectureTests/CodeForCoders.Commerce.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |

Cobertura agregada ≥ 70%, `dotnet publish` e imagens ficam para a validação full.

## Pronto quando

- [x] Gate passa (exit 0) com pelo menos 7 testes.
- [x] Com relógio controlado: ao cruzar o término há **um** fato no ciclo seguinte; duas instâncias concorrentes e a rotina executada duas vezes → um fato; vitalícia → nenhum.
- [x] Rotina desligada ou fato perdido → a decisão de 7.0 continua `denied`/`grant-ended` do mesmo jeito.
- [x] Uma vencida e outra ativa → fato da primeira e `allowed` na decisão.
- [x] O alerta de defasagem dispara acima de 30 minutos (métrica sem rótulo de pessoa).
- [x] Smoke no Compose (o de 5.0): uma cortesia de 1 mês cujo término foi ajustado para o passado pelo relógio de teste gera, no ciclo seguinte, o fato na fila de retenção do RabbitMQ.
