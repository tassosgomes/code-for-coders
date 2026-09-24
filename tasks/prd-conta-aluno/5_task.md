---
status: done
task_kind: vertical
blocked_by: [3.0]
gate: 'dotnet test src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj -- --filter-class CodeForCoders.Identity.IntegrationTests.StudentPasswordChangeTests --minimum-expected-tests 4 && dotnet test src/bff-student/tests/CodeForCoders.BffStudent.EndToEndTests/CodeForCoders.BffStudent.EndToEndTests.csproj -- --filter-class CodeForCoders.BffStudent.EndToEndTests.StudentPasswordChangeTests --minimum-expected-tests 2 && npm --prefix src/student-spa run test -- -t StudentPasswordChange'
gate_expect: 'Pelo menos 7 testes passam: 4 Identity, 2 BFF e 1 SPA'
---

# 5.0 Trocar senha com sessão ativa

**Fatia:** V-05 · **Cobre:** RF-06, US-05, RN-06–08/11/21/24 · **Spec:** `techspec.md#v-05` · **ADR:** [0003](../../docs/adr/0003-verificacao-de-sessao-do-aluno.md)

## Comportamento

O aluno autenticado obtém prova CSRF da sessão e envia senha atual e nova senha pela SPA ao BFF. `changeStudentPassword` só alcança Identity depois de validar cookie, vigência e CSRF vinculado à sessão. Identity confere a senha atual contra a Credencial existente, mesmo que ela seja legada e não atenda à política nova. A nova senha deve ter ao menos oito caracteres, maiúscula, minúscula, dígito e símbolo que não seja espaço. Em um commit, Identity troca a Credencial, invalida tokens de recuperação pendentes, revoga todas as outras sessões da Conta e grava `identidade.senha-redefinida`; a sessão corrente permanece válida. A próxima ação das outras sessões recebe 401 imediatamente, inclusive após falha/reinício do BFF. Nova senha autentica e antiga falha.

Senha atual errada, nova senha fora da política, cookie expirado ou CSRF ausente/de outra sessão não mudam Credencial, tokens ou sessões e não publicam fato. Erro 403 de CSRF leva a SPA a reobter a prova, sem repetir automaticamente a troca. Concorrência entre troca e ação de outra sessão respeita a ordem de commit e nunca aceita sessão revogada após a troca concluída. Replay da mesma intenção não duplica fato ou revogação; nenhum segredo aparece em resposta ou telemetria.

## Fora do escopo desta task

Pedir ou consumir link de recuperação (4.0) e alterar permissões de acesso a curso.

## Decisões fechadas

- A sessão atual continua ativa; somente as demais são revogadas. A verificação por ação protegida segue ADR-0003.
- Política nova se aplica à nova senha, não à conferência da senha atual legada. Contratos público/interno fixam códigos e idempotência.

## Modificar / Referenciar

- **modificar:** `src/identity/src/CodeForCoders.Identity.Infra.Data/IdentityDbContext.cs` e `src/identity/src/CodeForCoders.Identity.Api/Extensions/EndpointExtensions.cs` (Credencial, sessões, token e fato no commit)
- **modificar:** `src/identity/src/CodeForCoders.Identity.Infra.Messaging/RabbitMqTopologyInitializer.cs`, `src/identity/src/CodeForCoders.Identity.Infra.Messaging/RabbitMqPublisher.cs`, `src/identity/src/CodeForCoders.Identity.Infra.Messaging/Configuration/RabbitMqOptions.cs`, `src/identity/src/CodeForCoders.Identity.Infra.Data/Outbox/OutboxMessage.cs`, `src/identity/src/CodeForCoders.Identity.Infra.Data/Outbox/OutboxMessageConfiguration.cs` e `src/identity/src/CodeForCoders.Identity.Application/Interfaces/IOutboxMessageWriter.cs` (publicação do fato, se necessário)
- **modificar:** `src/bff-student/src/CodeForCoders.BffStudent.Application/Common/OpaqueBffSession.cs`, `src/bff-student/src/CodeForCoders.BffStudent.Infra.Data/ValkeyBffSessionStore.cs`, `src/bff-student/src/CodeForCoders.BffStudent.Api/Security/BffSecurityMiddleware.cs` e `src/bff-student/src/CodeForCoders.BffStudent.Api/Extensions/EndpointExtensions.cs` (sessão corrente, vigência e CSRF)
- **modificar:** `src/student-spa/src/app/router.tsx`, `src/student-spa/src/config/paths.ts` e `src/student-spa/src/lib/api-client.ts` (formulário protegido, erros e prova CSRF)
- **ref:** `techspec.md#v-05`, `api-contract.yaml`, `internal-api-contract.yaml`, `asyncapi-contract.yaml`, `domains/identidade-e-acesso/domain.md` e skills `dotnet`/`react`

## Pronto quando

- [x] Gate passa (exit 0), com as três suítes selecionadas pelo comando do frontmatter.
- [x] Senha correta troca Credencial e mantém sessão corrente, mas outra sessão é negada na próxima ação; token de recuperação pendente deixa de funcionar.
- [x] Senha atual errada, nova senha inválida e CSRF inválido não alteram Credencial, sessão ou outbox.
