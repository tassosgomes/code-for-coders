---
status: pending
task_kind: vertical
blocked_by: []
gate: 'dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.PurchaseCompletedCourseTitleTests --minimum-expected-tests 3 && dotnet test --project src/billing/tests/CodeForCoders.Billing.IntegrationTests/CodeForCoders.Billing.IntegrationTests.csproj -- --filter-class CodeForCoders.Billing.IntegrationTests.FiscalDocumentOpeningTests --minimum-expected-tests 8 && dotnet test --project src/billing/tests/CodeForCoders.Billing.IntegrationTests/CodeForCoders.Billing.IntegrationTests.csproj -- --filter-class CodeForCoders.Billing.IntegrationTests.FiscalHolderReadAdapterTests --minimum-expected-tests 2 && dotnet test --project src/billing/tests/CodeForCoders.Billing.IntegrationTests/CodeForCoders.Billing.IntegrationTests.csproj -- --filter-class CodeForCoders.Billing.IntegrationTests.NfseEnqueueAdapterTests --minimum-expected-tests 2'
gate_expect: "Pelo menos 15 testes passam: 3 de commerce e 12 de billing (8 de abertura, 2 do adaptador de tomador e 2 do adaptador de enfileirar)"
---

# 3.0 O pedido pago abre um único documento fiscal e a venda segue

**Fatia:** V-01 · **Cobre:** RF-01; RN-F01, RN-F02, RN-F03, RN-F04, RN-F06, RN-F07, RN-F10; DP-01, DP-06 · **Spec:** `techspec.md#v-01` · **ADR:** 0017, 0021

## Comportamento

Quando `commerce` publica `vendas.compra-concluida.v1` de um pedido que acabou de ficar *Pago*, o fato leva `courseTitle`, o título congelado no pedido. Publicação nova sempre manda o título. Matrícula, no mesmo processo, continua concedendo acesso e ignora o campo. Cortesia e pedido que não fica *Pago* não publicam esse fato, então não abrem nota. O comprovante de compra continua o que já existe; esta task não o substitui.

Fiscal consome o fato numa fila própria, ligada ao exchange em que `commerce` publica hoje (`commerce.events`). A fila não é `commerce.entitlement-purchases` e não escuta `commerce.events.default`.

Sem `courseTitle`, Fiscal confirma a mensagem e não abre documento. Com título, cria o documento se ainda não existe: descrição `Acesso ao curso {courseTitle}`, `priceCents` do fato (não `paidAmountCents`), competência no mês civil de `America/Sao_Paulo` de `paidAt` (`YYYY-MM`) e o número do pedido. Três entregas do mesmo fato deixam um documento, na situação em que já estava. Reentrega não cria outro e não volta atrás.

A situação inicial, depois de reler o tomador em Identity:

- Identity não responde: a mensagem não é confirmada e nada é gravado como *Aguardando tomador* no lugar de uma leitura que falhou.
- Sem tomador válido, mesmo sem prestador: `awaiting-holder`, motivo `Falta CPF, CNPJ ou NIF do aluno.`
- Com tomador válido e sem prestador utilizável: `awaiting-provider`, motivo nulo.
- Com os dois: enfileira no emissor com `n = 1` e fica `queued`, motivo nulo. `Idempotency-Key` `{orderId}-1`, `referencia` igual ao `orderId`. O total enviado é o preço em reais com duas casas (`49700` → `497.00`), nunca centavos. 202 não é nota emitida.

A concessão e o comprovante já saíram na transação de `commerce` e não dependem dessa leitura. Player, aula e progresso não consultam a nota.

O adaptador que lê o tomador é o cliente HTTP real, contra um servidor local que fala `getStudentTaxDocumentInternal`. Documento válido e falha de transporte são distinguíveis: a falha não vira "não tem documento". O adaptador que enfileira é o cliente HTTP real contra um servidor local no recorte do emissor (`provider-contract-notaas.yaml`). Códigos e nomes de evento do emissor não saem dessa porta. A aplicação de teste sobe com esses registros reais e com o consumidor na fila de Fiscal.

Contador de documento aberto, sem e-mail, CPF, CNPJ, NIF ou chave nos atributos. A migration do documento e do prestador sai pela ferramenta do EF, no schema `billing_access`.

`commerce` não passa a chamar `billing` no caminho da nota. A chamada que já abre o pagamento permanece.

## Fora do escopo desta task

- O aluno gravar o documento e o fato `identidade.identificacao-fiscal-informada.v1` → 4.0.
- Tornar o prestador utilizável e soltar em lote quem já tem tomador → 5.0.
- Webhook, PDF, XML, aviso e telas → 6.0.
- Recusa, ausência e nova tentativa → 7.0.
- Guarda de um ano → 8.0.
- Pedido que já estava *Pago* antes do título existir não é varrido (DP-06).

## Decisões fechadas

- Um documento por pedido *Pago*, fora do caminho da concessão (DP-01, RN-F03, ADR-0017).
- Fiscal permanece em `billing` (C-01). O valor da nota é `priceCents` (RN-F04).
- Fila no exchange em que `commerce` publica, sem sufixo de namespace de `billing` (`techspec.md` § Riscos).
- Sem título não abre documento (DP-06).
- 202 não é *Emitida* (C-06).

## Modificar / Referenciar

- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Application/Interfaces/PurchaseCompletedFact.cs` (`courseTitle` opcional); `src/commerce/src/CodeForCoders.Commerce.Infra.Data/Sales/SalesPaymentSink.cs` (publicar o título congelado); `src/billing/src/CodeForCoders.Billing.Infra.Messaging/RabbitMqTopologyInitializer.cs` (fila de Fiscal no exchange de quem publica); `src/billing/src/CodeForCoders.Billing.Infra.Data/BillingDbContext.cs` (documento e prestador); `docker-compose.yml`, `docker-compose.remote.yml`, `docker-compose.komodo.apis.yml`, `docker-compose.stable.yml` (audiências, asserção de `billing` para ler o tomador, base do emissor, `StudentApp__PublicBaseUrl` em `billing`); `scripts/generate-local-env.sh` (par de asserção de `billing` em Identity, no padrão dos pares que já existem)
- **ref:** `src/commerce/src/CodeForCoders.Commerce.Infra.Messaging/PurchaseGrantConsumerWorker.cs` (ignora campo desconhecido; não alterar); `src/commerce/src/CodeForCoders.Commerce.Application/Common/StudentAppOptions.cs`; `src/billing/src/CodeForCoders.Billing.Infra.Messaging/RabbitMqResourceNames.cs`; `docs/adr/0017-autenticacao-de-servico-commerce-em-billing.md`; `docs/adr/0021-billing-le-tomador-em-identity.md`; `tasks/prd-emissao-nfse/provider-contract-notaas.yaml`; `tasks/prd-emissao-nfse/asyncapi-contract-commerce.yaml`; `.claude/skills/dotnet/SKILL.md`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/commerce` | `dotnet format src/commerce/CodeForCoders.Commerce.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.ArchitectureTests/CodeForCoders.Commerce.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.PurchaseGrantTests --minimum-expected-tests 1` | exit 0 (a concessão segue) | `ci-dotnet.yml` Testes |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.PurchaseReceiptRequestTests --minimum-expected-tests 1` | exit 0 (o comprovante não é substituído) | `ci-dotnet.yml` Testes |
| `src/billing` | `dotnet format src/billing/CodeForCoders.Billing.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/billing` | `dotnet test --project src/billing/tests/CodeForCoders.Billing.ArchitectureTests/CodeForCoders.Billing.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/billing` | `dotnet ef migrations has-pending-model-changes --project src/billing/src/CodeForCoders.Billing.Infra.Data --startup-project src/billing/src/CodeForCoders.Billing.Api` | exit 0, sem mudança pendente | AGENTS.md |
| Compose | `docker compose -f docker-compose.yml config --quiet` | exit 0 | `scripts/apps.sh` |
| Compose remoto | `docker compose -f docker-compose.yml -f docker-compose.remote.yml config --quiet` | exit 0 com as variáveis que o override já exige | `scripts/apps.sh --remote` |

## Pronto quando

- [ ] Gate focalizado passa (exit 0).
- [ ] Três entregas do mesmo fato deixam um documento; fato sem título deixa zero; cortesia e pedido não pago não publicam o fato.
- [ ] Sem tomador válido, a situação é `awaiting-holder` mesmo sem prestador. Falha de leitura em Identity não grava essa situação.
- [ ] Com tomador e prestador, 202 deixa `queued` com total em reais e não marca a nota emitida.
- [ ] A matrícula concede como hoje, e a fila de Fiscal não é a de Matrícula.
- [ ] A aplicação de `billing` sobe no teste com o consumidor e os dois adaptadores HTTP reais registrados.
