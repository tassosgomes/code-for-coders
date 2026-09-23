---
status: in_progress
task_kind: vertical
blocked_by: [1.0]
gate: 'dotnet test src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj -- --filter-class CodeForCoders.Identity.IntegrationTests.StudentConfirmationTests --minimum-expected-tests 4 && dotnet test src/bff-student/tests/CodeForCoders.BffStudent.EndToEndTests/CodeForCoders.BffStudent.EndToEndTests.csproj -- --filter-class CodeForCoders.BffStudent.EndToEndTests.StudentConfirmationTests --minimum-expected-tests 2 && npm --prefix src/student-spa run test -- -t StudentConfirmation'
gate_expect: 'Pelo menos 7 testes passam: 4 Identity, 2 BFF e 1 SPA'
---

# 2.0 Confirmar conta e reenviar link

**Fatia:** V-02 · **Cobre:** RF-02, US-01/02, RN-02/03/21/24/26–28 · **Spec:** `techspec.md#v-02` · **ADR:** [0004](../../docs/adr/0004-autenticacao-de-servico-bff-identity.md)

## Comportamento

A SPA captura o token do link, retira-o da barra/histórico e chama `confirmStudentAccount` pelo BFF. Identity aceita somente token de confirmação válido, não expirado, do tenant e finalidade corretos; consome-o atomicamente, confirma a Conta e grava `identidade.conta-confirmada` no mesmo commit. Duas tentativas concorrentes confirmam uma vez; token usado, alterado, expirado ou de recuperação não confirma nada. A interface mostra o resultado e oferece reenvio quando o link não serve.

`requestAccountConfirmation` emite token e `pedidoId` novos somente para Conta ainda não confirmada e elegível, sem criar outra Conta. O pedido chega a Notificação pelo destino do outbox de 1.0 e o novo link aparece no smtp4dev local. A configuração da validade exibida no e-mail coincide com a validade aplicada em Identity. Repetição técnica da mesma intenção mantém o efeito; reenvio deliberado é uma nova intenção. O pedido não expõe segredo fora do canal endereçado.

## Fora do escopo desta task

Criar sessão (3.0) e recuperar senha (4.0). Confirmação não autentica automaticamente.

## Decisões fechadas

- Uso único, finalidade e expiração seguem [V-02](techspec.md#v-02) e os dois OpenAPI. Publicar o fato somente após confirmação efetiva; reenvio usa o modelo `confirmacao-de-conta` de CAP-026.
- Autenticação BFF → Identity segue ADR-0004; validade é parâmetro validado, sem número fixado pelo plano.

## Modificar / Referenciar

- **modificar:** `src/identity/src/CodeForCoders.Identity.Infra.Data/IdentityDbContext.cs` e `src/identity/src/CodeForCoders.Identity.Api/Extensions/EndpointExtensions.cs` (consumo atômico e operações internas)
- **modificar:** `src/identity/src/CodeForCoders.Identity.Infra.Messaging/RabbitMqTopologyInitializer.cs`, `src/identity/src/CodeForCoders.Identity.Infra.Messaging/RabbitMqPublisher.cs`, `src/identity/src/CodeForCoders.Identity.Infra.Messaging/Configuration/RabbitMqOptions.cs`, `src/identity/src/CodeForCoders.Identity.Infra.Data/Outbox/OutboxMessage.cs`, `src/identity/src/CodeForCoders.Identity.Infra.Data/Outbox/OutboxMessageConfiguration.cs` e `src/identity/src/CodeForCoders.Identity.Application/Interfaces/IOutboxMessageWriter.cs` (fato e pedido com destino correto, se necessário)
- **modificar:** `src/bff-student/src/CodeForCoders.BffStudent.Api/Extensions/EndpointExtensions.cs` e `src/bff-student/src/CodeForCoders.BffStudent.Api/Extensions/ServiceConfigurationExtensions.cs` (rotas e cliente interno)
- **modificar:** `src/student-spa/src/app/router.tsx`, `src/student-spa/src/config/paths.ts` e `src/student-spa/src/lib/api-client.ts` (link, reenvio e remoção do token da URL)
- **ref:** `src/notification/src/CodeForCoders.Notification.Api/Extensions/ServiceConfigurationExtensions.cs`, `src/notification/src/CodeForCoders.Notification.Infra.Data/Configuration/EmailOptions.cs` e `docker-compose.yml` (SMTP local produzido em 1.0)
- **ref:** `techspec.md#v-02`, `api-contract.yaml`, `internal-api-contract.yaml`, `asyncapi-contract.yaml`, `../prd-notificacao-transacional/asyncapi-contract.yaml`, `domains/identidade-e-acesso/domain.md` e skills `dotnet`/`react`

## Pronto quando

- [ ] Gate passa (exit 0), com as três suítes selecionadas pelo comando do frontmatter.
- [ ] Link válido confirma uma só vez e publica o fato; token inválido, usado, expirado ou de outra finalidade não confirma.
- [ ] Reenvio elegível cria novo pedido sem nova Conta, aceito por Notificação e capturado no smtp4dev; a SPA remove o token da URL.
