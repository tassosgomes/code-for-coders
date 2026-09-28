---
status: done
task_kind: vertical
blocked_by: ["4.0"]
gate: 'dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj -- --filter-class CodeForCoders.BffAdmin.IntegrationTests.AuditComplementConfirmationTests --minimum-expected-tests 10 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/CodeForCoders.BffAdmin.EndToEndTests.csproj -- --filter-class CodeForCoders.BffAdmin.EndToEndTests.AuditComplementConfirmationTests --minimum-expected-tests 5 && npm --prefix src/admin-spa run test -- audit-complement-confirmation'
gate_expect: "Pelo menos 21 testes passam: 10 BFF integração, 5 BFF ponta a ponta, 6 SPA"
---

# 5.0 Administrador confirma uma explicação e o BFF a aceita uma única vez, de forma durável e protegida

**Fatia:** V-03 (aceite) · **Cobre:** RF-01, RF-04 (validação, idempotência, recusa), US-03, US-04, RN-A02, RN-A03, RN-A09, RN-A13, DP-04, DP-05 · **Spec:** `techspec.md#v-03-administrador-confirma-uma-explicação-e-a-vê-como-complemento-imutável` · **ADR:** [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md)

## Comportamento

- **BFF — `confirmAuditRecordComplement`:** exige sessão vigente, CSRF e papel administrador
  (401 `SESSION_REQUIRED` / 403 `PERMISSION_DENIED`, sem nenhuma escrita); confirma por
  `getAuditRecordInternal` que o original existe no tenant (senão 404 neutro, sem escrita); valida
  explicação de 1–1000 caracteres com conteúdo não branco (senão 422 `EXPLANATION_REQUIRED`).
  Aceita gravando, **numa única transação do banco do BFF**, a linha de idempotência — unicidade por
  (`tenantId`, `actorId`, operação, `Idempotency-Key`), hash autenticado de (`recordId`, corpo),
  `confirmationId`, ator, tenant, prazo de 24 h — e a mensagem `ComplementoConfirmado` no outbox
  com payload **cifrado** (chave de secret manager e versão de chave), destinada ao exchange
  `audit.events`, com o mesmo `confirmationId` devolvido no `202`. Repetição com mesma chave e
  mesmo corpo devolve o mesmo `202`/`confirmationId`, inclusive em chamadas concorrentes; mesma
  chave com corpo ou original diferente → 422 `IDEMPOTENCY_CONFLICT`. Falha antes do commit não
  aceita nada; nenhuma chamada de rede ocorre dentro da transação.
- **Texto livre protegido:** a explicação não aparece em claro na coluna do outbox, na linha de
  idempotência, em `last_error` (que passa a guardar só código técnico de falha), em logs, spans,
  métricas, headers, URL ou Problem.
- **SPA (A3, A4.a, A4.c):** *+ Acrescentar complemento* abre o formulário no próprio detalhe com
  `ReasonField` 0/1000, aviso fixo de irreversibilidade e *Confirmar complemento*, sem AlertDialog.
  Em branco → "Escreva a explicação do que foi apurado." sem requisição. A `Idempotency-Key` é
  gerada por tentativa do usuário e reutilizada em *Tentar de novo* após falha de rede, com o texto
  mantido (A3.e). Após `202`, o item "Aguardando registro…" entra na linha do tempo com
  `aria-live` e o detalhe é consultado a cada 2 s por até 30 s procurando o `confirmationId`;
  sem ele, Alert info "A confirmação foi aceita e ainda está sendo registrada." + *Atualizar*
  (A4.c), nunca "falhou" e nunca uma nova chave automática. 403 → B12; 401 → B1. Foco volta ao
  botão ao fechar o formulário.

## Fora do escopo desta task

Publicação no broker e consumo em `audit` (6.0): nesta task a mensagem fica pendente no outbox e o
SPA termina em A4.c. O complemento visível (A4.b) é de 6.0.

## Decisões fechadas

- Reusar banco e outbox do BFF com proteção de texto livre e idempotência transacional: TechSpec, Decisões Técnicas 2.
- `202` significa aceite durável, não consumo; confirmação só gera registro depois que `audit` recebe o fato: DP-05, G13.
- Padrão de proteção de payload: `OutboxPayloadProtector` de Identity como referência.
- Migrations do BFF pelo tooling do EF (`dotnet ef migrations add`), nunca à mão.

## Modificar / Referenciar

- **modificar:** `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/EndpointExtensions.cs`, `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/ServiceConfigurationExtensions.cs`, `src/bff-admin/src/CodeForCoders.BffAdmin.Api/appsettings.json`
- **modificar:** `src/bff-admin/src/CodeForCoders.BffAdmin.Infra.Data/BffAdminDbContext.cs`, `src/bff-admin/src/CodeForCoders.BffAdmin.Infra.Data/Outbox/OutboxMessageWriter.cs`, `src/bff-admin/src/CodeForCoders.BffAdmin.Infra.Data/Outbox/OutboxMessage.cs`, `src/bff-admin/src/CodeForCoders.BffAdmin.Infra.Data/Outbox/OutboxMessageConfiguration.cs` (payload cifrado, destino por mensagem, `last_error` sem payload)
- **modificar:** `docker-compose.yml`, `docker-compose.coolify.yml` (chave de proteção do outbox do BFF, valor local gerado fora do repositório)
- **modificar:** `src/admin-spa/src/testing/handlers.ts`
- **ref:** `api-contract.yaml` (`confirmAuditRecordComplement`), `asyncapi-contract.yaml` (`ComplementoConfirmadoPayload`); `techspec.md#backend` (Confirmação) e `techspec.md#mensagem-e-dados` (Percurso sensível); `src/identity/src/CodeForCoders.Identity.Infra.Data/Outbox/OutboxPayloadProtector.cs`; `docs/design/wireframes-auditoria.md` (G18–G21) e Figma aprovado; skills `dotnet` e `react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/bff-admin` | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-admin` | `dotnet build src/bff-admin/CodeForCoders.BffAdmin.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/bff-admin` | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.ArchitectureTests/CodeForCoders.BffAdmin.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/bff-admin` | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.UnitTests/CodeForCoders.BffAdmin.UnitTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run build` | exit 0 | `ci-react-ts.yml` |

Cobertura agregada ≥ 70%, `dotnet publish` e imagens ficam para a validação full.

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 21 testes.
- [ ] Duas confirmações concorrentes com a mesma chave e o mesmo corpo resultam em uma linha de idempotência, uma mensagem no outbox e o mesmo `confirmationId`; corpo divergente → 422 `IDEMPOTENCY_CONFLICT`.
- [ ] Explicação vazia, registro de outro tenant, professor e CSRF inválido não gravam linha alguma.
- [ ] Inspeção das colunas do outbox, da idempotência e de `last_error` e dos logs capturados no teste não encontra o texto da explicação.
