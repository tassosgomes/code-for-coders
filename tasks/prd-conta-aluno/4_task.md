---
status: in_progress
task_kind: vertical
blocked_by: [3.0]
gate: 'dotnet test src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj -- --filter-class CodeForCoders.Identity.IntegrationTests.StudentPasswordRecoveryTests --minimum-expected-tests 4 && dotnet test src/bff-student/tests/CodeForCoders.BffStudent.EndToEndTests/CodeForCoders.BffStudent.EndToEndTests.csproj -- --filter-class CodeForCoders.BffStudent.EndToEndTests.StudentPasswordRecoveryTests --minimum-expected-tests 2 && npm --prefix src/student-spa run test -- -t StudentPasswordRecovery'
gate_expect: 'Pelo menos 7 testes passam: 4 Identity, 2 BFF e 1 SPA'
---

# 4.0 Recuperar senha sem revelar conta

**Fatia:** V-04 · **Cobre:** RF-05, US-04, RN-03–08/21/24/26–28 · **Spec:** `techspec.md#v-04` · **ADR:** [0003](../../docs/adr/0003-verificacao-de-sessao-do-aluno.md)

## Comportamento

A SPA solicita `requestPasswordReset` e exibe a mesma mensagem para endereço com ou sem Conta elegível. BFF devolve status/corpo públicos indistinguíveis; Identity só cria token de recuperação para Conta de aluno não desativada, grava hash e pedido `recuperacao-de-senha` no outbox com `pedidoId` estável e encaminha a Notificação. Conta inexistente, interna ou inelegível não produz pedido. O link aparece no smtp4dev local e a validade exibida coincide com a validada por Identity. Nenhum e-mail/segredo aparece em log, métrica ou fato difundido.

A SPA captura o token do link, remove-o da URL e chama `resetStudentPassword` com nova senha. Token correto, não vencido, de finalidade e tenant corretos é consumido atomicamente junto da troca de Credencial, invalidação dos demais tokens de recuperação, revogação das sessões da Conta e gravação de `identidade.senha-redefinida`. A próxima ação de qualquer sessão antiga recebe 401, mesmo após falha ou reinício do BFF; senha antiga não autentica, nova senha autentica somente se a Conta já era confirmada. Recuperação de Conta não confirmada preserva seu estado. Token usado, alterado, expirado, de confirmação ou concorrente não muda Credencial; senha fora da política também não a muda e conserva o token válido até expirar. Replay da mesma intenção não duplica efeito; indisponibilidade do broker deixa pedido no outbox para retry.

## Fora do escopo desta task

Troca com senha atual e preservação da sessão corrente (5.0). Entrega a destinatários externos depende da configuração operacional do provedor.

## Decisões fechadas

- Resposta neutra e códigos seguem os OpenAPI; não diferenciar elegibilidade por corpo, status ou mensagem visível.
- Política de senha é a mesma do cadastro; transação de Identity reúne Credencial, tokens, sessões e outbox. A revogação imediata segue ADR-0003.
- Notificação recebe pedido endereçado no modelo `recuperacao-de-senha`; validade é configurada e validada, sem número fixado no plano.

## Modificar / Referenciar

- **modificar:** `src/identity/src/CodeForCoders.Identity.Infra.Data/IdentityDbContext.cs` e `src/identity/src/CodeForCoders.Identity.Api/Extensions/EndpointExtensions.cs` (token, Credencial, revogação e operações internas)
- **modificar:** `src/identity/src/CodeForCoders.Identity.Infra.Messaging/RabbitMqTopologyInitializer.cs`, `src/identity/src/CodeForCoders.Identity.Infra.Messaging/RabbitMqPublisher.cs`, `src/identity/src/CodeForCoders.Identity.Infra.Messaging/Configuration/RabbitMqOptions.cs`, `src/identity/src/CodeForCoders.Identity.Infra.Data/Outbox/OutboxMessage.cs`, `src/identity/src/CodeForCoders.Identity.Infra.Data/Outbox/OutboxMessageConfiguration.cs` e `src/identity/src/CodeForCoders.Identity.Application/Interfaces/IOutboxMessageWriter.cs` (pedido e fato no destino correto)
- **modificar:** `src/bff-student/src/CodeForCoders.BffStudent.Application/Common/OpaqueBffSession.cs`, `src/bff-student/src/CodeForCoders.BffStudent.Infra.Data/ValkeyBffSessionStore.cs` e `src/bff-student/src/CodeForCoders.BffStudent.Api/Security/BffSecurityMiddleware.cs` (recusa de sessão revogada)
- **modificar:** `src/bff-student/src/CodeForCoders.BffStudent.Api/Extensions/EndpointExtensions.cs` e `src/bff-student/src/CodeForCoders.BffStudent.Api/Extensions/ServiceConfigurationExtensions.cs` (pedido e redefinição)
- **modificar:** `src/student-spa/src/app/router.tsx`, `src/student-spa/src/config/paths.ts` e `src/student-spa/src/lib/api-client.ts` (pedido neutro, link e formulário)
- **ref:** `src/notification/src/CodeForCoders.Notification.Api/Extensions/ServiceConfigurationExtensions.cs`, `src/notification/src/CodeForCoders.Notification.Infra.Data/Configuration/EmailOptions.cs` e `docker-compose.yml` (SMTP local de 1.0)
- **ref:** `techspec.md#v-04`, `api-contract.yaml`, `internal-api-contract.yaml`, `asyncapi-contract.yaml`, `../prd-notificacao-transacional/asyncapi-contract.yaml`, `domains/identidade-e-acesso/domain.md` e skills `dotnet`/`react`

## Pronto quando

- [ ] Gate passa (exit 0), com as três suítes selecionadas pelo comando do frontmatter.
- [ ] Pedido para conta elegível e inelegível tem resposta pública idêntica; só a elegível gera mensagem capturada no smtp4dev.
- [ ] Link válido troca a senha uma vez, mantém estado de confirmação e revoga sessões; token inválido ou senha fora da política preserva Credencial e sessões.

## Decisão registrada (intervenção 2026-09-23)

Bloqueante da revisão `run.05m9FlZW`: o token de verificação chegava em claro ao payload JSONB do outbox pelo link do
pedido `notificacao.envio-solicitado.v1`. Decisão do responsável pelo produto: **cifrar no outbox**. Identity cifra o
payload dos pedidos endereçados (que carregam link/destinatário) ao gravar a intenção, com chave de proteção
fornecida por configuração validada na partida e sem segredo versionado, e decifra somente no publisher antes de
publicar. Entrega e retry mantêm o mesmo `pedidoId`. Fatos difundidos continuam sem segredo e não precisam de cifra.
A proteção vale para todo pedido endereçado gravado pelo writer compartilhado, incluindo os de confirmação das
tasks 1.0/2.0. A evidência deve provar que o payload persistido não contém o token em claro e que a mensagem publicada
ainda carrega o link correto.

## Autorização de ambiente para o smoke (2026-09-23)

O responsável autorizou parar o stack Compose `code-for-coders-*` em execução (de outra checkout) e subir o stack
desta worktree para o smoke no smtp4dev, com chaves efêmeras geradas localmente conforme
`docs/student-registration-local-development.md`, sem versionar nenhuma chave. O smoke deve cobrir cadastro →
e-mail de confirmação, reenvio e pedido de recuperação → e-mail com link de redefinição capturado no smtp4dev.
Registre a evidência no relatório.

Nota do orquestrador (revisão `run.vWr3VWGi`): o stack desta worktree está em execução com `bff-student` caindo na
partida por DI ausente de `ServiceAssertionTokenFactory`. Após corrigir, confirme que `bff-student` sobe saudável no
Compose (a autorização acima vale também para reconstruir esse serviço) e, se possível, capture o smoke no smtp4dev.
