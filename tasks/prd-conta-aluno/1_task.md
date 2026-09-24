---
status: done
task_kind: vertical
blocked_by: []
gate: 'dotnet test src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj -- --filter-class CodeForCoders.Identity.IntegrationTests.StudentRegistrationTests --minimum-expected-tests 4 && dotnet test src/bff-student/tests/CodeForCoders.BffStudent.EndToEndTests/CodeForCoders.BffStudent.EndToEndTests.csproj -- --filter-class CodeForCoders.BffStudent.EndToEndTests.StudentRegistrationTests --minimum-expected-tests 2 && npm --prefix src/student-spa run test -- -t StudentRegistration'
gate_expect: 'Pelo menos 7 testes passam: 4 Identity, 2 BFF e 1 SPA'
---

# 1.0 Cadastrar aluno e solicitar confirmação

**Fatia:** V-01 · **Cobre:** RF-01, US-01, RN-01/02/06/13/13a/13b/21/24/26–28 · **Spec:** `techspec.md#v-01` · **ADR:** [0001](../../docs/adr/0001-monorepo-de-codigo.md), [0002](../../docs/adr/0002-plataforma-de-runtime-coolify.md), [0004](../../docs/adr/0004-autenticacao-de-servico-bff-identity.md)

## Comportamento

O visitante preenche nome, e-mail e senha na SPA; `registerStudent` passa pelo BFF e pela interface interna autenticada por identidade de serviço até Identity. Identity normaliza e-mail, exige senha de oito ou mais caracteres com maiúscula, minúscula, dígito e símbolo que não seja espaço, impede conta não desativada duplicada no tenant inclusive entre aluno e ator interno, e persiste Conta de aluno não confirmada, Credencial derivada e hash do token de confirmação. No mesmo commit grava `identidade.conta-criada` e um pedido `confirmacao-de-conta` com `pedidoId` estável, finalidade, modelo e link; o pedido contém o destinatário somente na exceção contratada e nenhum fato difundido contém e-mail ou segredo. A intenção do outbox guarda o exchange de destino. A publicação confirmada pelo broker chega ao consumidor de Notificação; no ambiente local o transporte SMTP entrega ao smtp4dev e a SPA orienta verificar o e-mail, sem criar sessão ou acesso a curso.

Mesmo e-mail normalizado gera `ACCOUNT_ALREADY_EXISTS` sem credencial ou pedido novo; a interface oferece entrada, recuperação ou reenvio. Senha fora da política não cria Conta. Replay da mesma chave/corpo em até 24 horas mantém status, corpo lógico e um único efeito; mesma chave com corpo diferente gera `IDEMPOTENCY_CONFLICT`. Cadastro simultâneo do mesmo e-mail produz uma só Conta. Falha do broker conserva o outbox para reentrega com o mesmo `pedidoId`; falha após commit e antes da resposta não duplica conta ou pedido. A configuração de validade por finalidade e o texto de Notificação devem ser coerentes e validados antes do tráfego.

## Fora do escopo desta task

Consumir o link e reenviá-lo (2.0), entrar (3.0), recuperar ou trocar senha (4.0/5.0). Entrega a destinatário externo depende do provedor e DNS definidos por Plataforma.

## Decisões fechadas

- Contratos [público](api-contract.yaml), [interno](internal-api-contract.yaml), [mensagens](asyncapi-contract.yaml) e [CAP-026](../prd-notificacao-transacional/asyncapi-contract.yaml) fixam operação, erros e payload; não substituir pedido endereçado por assinatura do fato.
- A autenticação de serviço segue ADR-0004; segredo privado não vai ao navegador. Idempotência é persistida por Identity por 24 horas, com fingerprint protegido.
- O adaptador SMTP local fica atrás de `ITransactionalEmailSender`; o transporte HTTP existente continua selecionável para provedor externo. As convenções de implementação e testes vêm das skills `dotnet` e `react`.

## Modificar / Referenciar

- **modificar:** `src/identity/src/CodeForCoders.Identity.Infra.Data/IdentityDbContext.cs` (Conta, Credencial, token, idempotência, tenant e migration evolutiva)
- **modificar:** `src/identity/src/CodeForCoders.Identity.Api/Extensions/EndpointExtensions.cs` e `src/identity/src/CodeForCoders.Identity.Api/Extensions/ServiceConfigurationExtensions.cs` (operação interna e autenticação/configuração)
- **modificar:** `src/identity/src/CodeForCoders.Identity.Infra.Messaging/RabbitMqTopologyInitializer.cs`, `src/identity/src/CodeForCoders.Identity.Infra.Messaging/RabbitMqPublisher.cs` e `src/identity/src/CodeForCoders.Identity.Infra.Messaging/Configuration/RabbitMqOptions.cs` (destino de fatos e pedidos, confirmação e retry)
- **modificar:** `src/identity/src/CodeForCoders.Identity.Infra.Data/Outbox/OutboxMessage.cs`, `src/identity/src/CodeForCoders.Identity.Infra.Data/Outbox/OutboxMessageConfiguration.cs` e `src/identity/src/CodeForCoders.Identity.Application/Interfaces/IOutboxMessageWriter.cs` (destino persistido da intenção)
- **modificar:** `src/notification/src/CodeForCoders.Notification.Api/Extensions/ServiceConfigurationExtensions.cs`, `src/notification/src/CodeForCoders.Notification.Infra.Data/Configuration/EmailOptions.cs` e `docker-compose.yml` (seleção SMTP local, smtp4dev com UI só no host local)
- **modificar:** `src/bff-student/src/CodeForCoders.BffStudent.Api/Extensions/EndpointExtensions.cs` e `src/bff-student/src/CodeForCoders.BffStudent.Api/Extensions/ServiceConfigurationExtensions.cs` (rota pública e cliente Identity)
- **modificar:** `src/student-spa/src/app/router.tsx`, `src/student-spa/src/config/paths.ts` e `src/student-spa/src/lib/api-client.ts` (cadastro, navegação e idempotência)
- **ref:** `context/architecture-baseline.md`, `domains/identidade-e-acesso/domain.md`, `src/notification/src/CodeForCoders.Notification.Api/appsettings.json` e `src/notification/src/CodeForCoders.Notification.Infra.Data/Adapters/HttpTransactionalEmailSender.cs` (fronteiras, exchange efetivo e semântica do transporte)
- **ref:** `techspec.md#v-01`, `contracts.md`, `api-contract.yaml`, `internal-api-contract.yaml`, `asyncapi-contract.yaml`, `../prd-notificacao-transacional/asyncapi-contract.yaml` e skills `dotnet`/`react`

## Pronto quando

- [x] Gate passa (exit 0), com as três suítes selecionadas pelo comando do frontmatter.
- [x] Cadastro válido produz uma Conta não confirmada, um fato e um pedido aceito por Notificação; o link aparece no smtp4dev local e nenhum token/JWT aparece em resposta ou telemetria.
- [x] Duplicidade, senha inválida, replay conflitante e corrida não criam Conta, Credencial ou pedido indevidos; retry do outbox mantém `pedidoId`.

## Reabertura após validação full (run.W4iG4XKc, `prd_review.md`)

Corrigir nesta task:
- **B1:** `src/student-spa/src/features/student-dashboard/components/dashboard-screen.test.tsx` falha (`Link` sem router).
  Renderize o teste com contexto de router (ex.: `MemoryRouter`/`createMemoryRouter` em `renderWithProviders`) sem remover
  nem enfraquecer a asserção.
- **B2 (parte desta task):** `react-hooks/refs` em `student-registration-screen.tsx:37`. Mova a leitura de ref para
  handler/efeito; não desabilite a regra.
Evidência adicional além do gate: `npm --prefix src/student-spa run test` completo com o dashboard passando e
`npx eslint` limpo no arquivo de cadastro.
- **B3 (revalidação run.fsbkn9Sg):** o URL padrão do link de confirmação (`docker-compose.yml:110`,
  `appsettings.json:28`) não inclui o base path `/student/` servido pelo Nginx da SPA. Corrija para
  `/student/confirm-account` (ou equivalente derivado do base path), confira que a rota da SPA corresponde e
  alinhe documentação local. Evidência: URL gerada abre a rota de confirmação servida pela SPA.
