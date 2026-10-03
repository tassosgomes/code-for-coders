---
status: done
task_kind: vertical
blocked_by: ["5.0"]
gate: 'dotnet test --project src/commerce/tests/CodeForCoders.Commerce.UnitTests/CodeForCoders.Commerce.UnitTests.csproj -- --filter-class CodeForCoders.Commerce.UnitTests.OfferPublicationRulesTests --minimum-expected-tests 5 && dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.OfferPublicationTests --minimum-expected-tests 9 && dotnet test --project src/audit/tests/CodeForCoders.Audit.UnitTests/CodeForCoders.Audit.UnitTests.csproj -- --filter-class CodeForCoders.Audit.UnitTests.OfferActPolicyTests --minimum-expected-tests 5 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj -- --filter-class CodeForCoders.BffAdmin.IntegrationTests.OfferPublicationProxyTests --minimum-expected-tests 3 && npm --prefix src/admin-spa run test -- catalog-offer-publication'
gate_expect: "Pelo menos 26 testes passam: 5 unitários e 9 de integração de commerce, 5 unitários de audit, 3 do BFF, 4 do SPA"
---

# 6.0 O financeiro publica a oferta e ela nasce com autor na trilha

**Fatia:** V-04 · **Cobre:** RF-04, RN-O03, RN-O15, RN-O18, RN-A05, RN-A06, RN-A08, RN-A14, DP-05 (o que não audita), C-09, C-12 · **Spec:** `techspec.md#v-04` · **ADR:** [ADR-0005](../../../docs/adr/0005-sessao-e-servico-do-backoffice.md)

## Comportamento

- **`commerce` — `publishOfferInternal`:** exige versão vigente com nível lido sob o bloqueio do curso; nível
  nulo → 422 `COURSE_LEVEL_REQUIRED`, a oferta continua em rascunho e **nada** entra no outbox. Estado
  `draft` ou `unpublished` passa a `published` com `publishedAt` e `offerRevision` + 1; oferta já publicada
  → 422 `OFFER_STATE_CONFLICT`. O momento em que o curso passa a estar na vitrine é recalculado pelo predicado
  único de 3.0. `Idempotency-Key` obrigatória, com recibo: repetir a chamada devolve a mesma resposta e **não**
  cria segundo fato nem segundo ato. Republicar uma oferta `unpublished` gera novo fato e novo ato.
- **Fato e ato na mesma transação:** a mudança grava, no outbox do módulo Catálogo (schema `catalog`, próprio
  e distinto do de `sales`), o fato `catalogo.oferta-publicada.v1` (`OfertaPayload`, com nome, preço,
  vigência e `offerRevision` crescente, moeda `BRL`) e o ato `oferta-publicada` para o envelope
  `auditoria.ato-praticado` 1.3.0 (origem `catalogo`, alvo `oferta`, complemento `curso`, sem motivo,
  `fatoId` = `eventId` do fato, autor = `sub` do JWT, escola = `tenantId`, `praticadoEm` = `occurredAt`). O
  ato não leva nome nem preço. Se a transação falhar entre a oferta e o commit, nada nasce.
- **Publicador:** passa a drenar os dois outboxes; envia o ato ao exchange da Auditoria e o fato ao de
  `commerce`, escolhendo o exchange pela chave de roteamento, e propaga o cabeçalho `correlationId`, além do
  `traceparent`. A saúde do outbox cobre os dois schemas. A topologia declara a fila de retenção limitada
  dos fatos `catalogo.oferta-*` ligada às três chaves (quorum, limite de tamanho e de validade, descarte do
  mais antigo); sem ela o publicador esgotaria as tentativas por `mandatory: true`.
- **`audit` 1.3.0:** aceita os tipos `oferta-publicada`, `oferta-alterada` e `oferta-despublicada`, sem
  motivo, com origem `catalogo` e alvo `oferta` obrigatórios, e grava o ato conforme. Ato de origem
  `catalogo` com alvo diferente de `oferta` é gravado como não conforme.
- **`bff-admin` — `publishOffer`:** repassa JWT e `Idempotency-Key`; erros de `commerce` chegam com o mesmo
  `code`.
- **`admin-spa`:** publicar mostra antes o **cartão exato** que o visitante verá (nome, preço, vigência) e só
  publica após a confirmação; depois mostra o estado `published`; sem nível, mostra o motivo no ponto da
  ação e a oferta segue em rascunho. Conforme o Figma aprovado em 2.0.
- **Caso negativo principal:** curso sem nível → 422 sem mensagens; duplo clique com a mesma chave → um fato
  e um ato; ato de `catalogo` com alvo que não seja `oferta` → não conforme em `audit`.

## Fora do escopo desta task

Alteração de preço ou vigência de oferta publicada e seu ato (7.0); despublicação (8.0); rótulos na consulta
da trilha (12.0). A trilha já mostra o ato como registro conforme, ainda com o rótulo padrão do tipo.

## Decisões fechadas

- Outbox próprio do Catálogo no schema `catalog`, como `learning` fez para `content`; o de `sales` segue
  atendendo o heartbeat (`techspec.md#decisões-técnicas`).
- Fila de retenção limitada para os fatos sem consumidor (C-12); valores iniciais propostos de 7 dias e
  10 000 mensagens, configuráveis, a confirmar pela plataforma (`techspec.md#questões-em-aberto`).
- Ordem de implantação: `audit` 1.3.0 antes de `commerce` publicar o primeiro ato
  (`techspec.md#análise-de-impacto`).
- Fora do ato: nome e texto comercial; o ato carrega só identificadores e valores (`techspec.md#contratos-e-fronteiras`).
- O ato `oferta-publicada` não leva preço; acrescentá-lo é questão aberta da TechSpec e não muda esta task.

## Modificar / Referenciar

- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Infra.Data/Configuration/CommerceSchemas.cs`, `CommerceModules.cs` (outbox no schema `catalog`), `Health/OutboxHealthCheck.cs`; migration via `dotnet ef migrations add`
- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Infra.Messaging/OutboxPublisherWorker.cs`, `RabbitMqPublisher.cs`, `RabbitMqTopologyInitializer.cs`, `Configuration/RabbitMqOptions.cs`
- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Api/Extensions/EndpointExtensions.cs`, `ExceptionHandlers/GlobalExceptionHandler.cs` (`publishOffer`, `COURSE_LEVEL_REQUIRED`)
- **modificar:** `src/audit/src/CodeForCoders.Audit.Domain/Policies/AdministrativeActPolicy.cs`
- **modificar:** `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/EndpointExtensions.cs` (rota de publicar)
- **modificar:** `docker-compose.yml`, `docker-compose.coolify.yml` (somente se o publicador de `commerce` exigir variável nova para o exchange da Auditoria)
- **ref:** `src/learning/src/CodeForCoders.Learning.Infra.Messaging/OutboxPublisherWorker.cs`, `RabbitMqPublisher.cs`, `Infra.Data/Outbox/ContentOutboxMessage*.cs` (precedente de outbox por módulo, exchange da Auditoria e `correlationId`); `asyncapi-contract.yaml` e `asyncapi-contract-audit.yaml` (payloads e exemplos); `internal-api-contract-commerce.yaml` e `api-contract.yaml` (`publishOffer`); `docs/design/wireframes-catalogo-vitrine.md` e Figma aprovado; skills `dotnet` e `react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/commerce` | `dotnet format src/commerce/CodeForCoders.Commerce.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/commerce` | `dotnet build src/commerce/CodeForCoders.Commerce.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj` | exit 0 (heartbeat e área financeira sem regressão) | `ci-dotnet.yml` Testes |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.ArchitectureTests/CodeForCoders.Commerce.ArchitectureTests.csproj` | exit 0 (módulos e schemas) | `ci-dotnet.yml` Testes |
| `src/audit` | `dotnet format src/audit/CodeForCoders.Audit.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/audit` | `dotnet build src/audit/CodeForCoders.Audit.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/audit` | `dotnet test --project src/audit/tests/CodeForCoders.Audit.IntegrationTests/CodeForCoders.Audit.IntegrationTests.csproj` | exit 0 (atos existentes sem regressão) | `ci-dotnet.yml` Testes |
| `src/bff-admin` | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-admin` | `dotnet build src/bff-admin/CodeForCoders.BffAdmin.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/bff-admin` | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run test -- catalog-` | exit 0 (Catálogo sem regressão) | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run build -- --base=/admin/` | exit 0 | `ci-react-ts.yml` |

Cobertura agregada ≥ 70%, `dotnet publish` e imagens ficam para a validação full.

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 26 testes.
- [ ] Publicar gera uma publicação, um fato e um ato, os dois últimos validados contra `OfertaPayload` e `AtoPraticadoPayload` 1.3.0 capturados do outbox e do broker real (Testcontainers); duplo clique com a mesma chave gera um de cada.
- [ ] Falha forçada entre a gravação da oferta e o commit: nada nasce (oferta, fato e ato).
- [ ] O publicador completa o ciclo com a fila de retenção ligada e **esgota** as tentativas quando ela não existe (teste que documenta o motivo da fila); o `correlationId` chega ao exchange da Auditoria.
- [ ] A aplicação `commerce` inicia no ambiente de teste com os registros reais do publicador, do outbox do Catálogo e da topologia.
- [ ] Smoke no Compose com `audit` 1.3.0 subindo antes do Catálogo publicar: o financeiro publica uma oferta em `http://localhost:8081/admin/catalogo/{courseId}` (curso com nível real, por fixture conforme o schema 1.1.0 enquanto `learning` 1.1.0 não existe) e o ato `oferta-publicada` aparece conforme na consulta da trilha; curso sem nível → recusa com o motivo e nada na trilha.
