---
status: in_progress
task_kind: vertical
blocked_by: [2.0]
gate: 'dotnet test src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj -- --filter-class CodeForCoders.Identity.IntegrationTests.StudentSessionTests --minimum-expected-tests 4 && dotnet test src/bff-student/tests/CodeForCoders.BffStudent.EndToEndTests/CodeForCoders.BffStudent.EndToEndTests.csproj -- --filter-class CodeForCoders.BffStudent.EndToEndTests.StudentSessionTests --minimum-expected-tests 4 && npm --prefix src/student-spa run test -- -t StudentSession'
gate_expect: 'Pelo menos 9 testes passam: 4 Identity, 4 BFF e 1 SPA'
---

# 3.0 Entrar, manter sessão e sair

**Fatia:** V-03 · **Cobre:** RF-03/04, US-01/03, RN-01/02/05/08–10/13/13a/13b/21–24 · **Spec:** `techspec.md#v-03` · **ADR:** [0003](../../docs/adr/0003-verificacao-de-sessao-do-aluno.md), [0004](../../docs/adr/0004-autenticacao-de-servico-bff-identity.md)

## Comportamento

`createStudentSession` autentica somente Conta de aluno ativa e confirmada. E-mail inexistente e senha errada têm resposta pública indistinguível; senha correta de Conta não confirmada recebe `EMAIL_NOT_CONFIRMED` e caminho de reenvio, mas senha incorreta não revela esse estado. Conta interna ou desativada não entra como aluno. Identity cria a sessão autoritativa; BFF só emite cookie opaco seguro após gravar o estado operacional em Valkey. Se a gravação falhar, não emite cookie e a sessão órfã expira sem uso. Replay de login retorna a mesma sessão lógica sem reativar sessão já revogada.

`getCurrentStudentSession` e cada ação protegida verificam TTL local e vigência em Identity antes de renovar atomicamente a inatividade em Identity e depois o TTL de Valkey. A resposta traz identidade e prova CSRF vinculada à sessão, sem JWT. JWT interno curto só é emitido para audiência autorizada na validação necessária a outro serviço; o navegador nunca o recebe. Falha ou reinício de Identity/Valkey fecha acesso, sem renovar TTL. Duas sessões simultâneas permanecem independentes. Ausência ou prova CSRF de outra sessão bloqueia escrita autenticada. A SPA protege as rotas, reobtém prova após 403 sem repetir operação sensível, e manda 401 de sessão expirada à entrada. Autenticação não concede direito de acesso a curso.

`endCurrentStudentSession` revoga a sessão corrente em Identity, remove o estado de Valkey e limpa o cookie; outra sessão continua ativa. Logout repetido de sessão encerrada ou expirada limpa o cookie e não a reativa. Se a remoção de Valkey falhar após revogação, a próxima ação continua negada por Identity. Os endpoints públicos e internos seguem os contratos aprovados, inclusive idempotência e ProblemDetails público sem detalhe de infraestrutura. Preserva-se o registro de Conta desativada para auditoria; desativação e exclusão não são criadas nesta fatia.

## Fora do escopo desta task

Redefinição e troca de senha (4.0/5.0). Autorização para assistir a curso pertence a CAP-008.

## Decisões fechadas

- Verificar Identity em cada ação protegida e usar identidade de serviço assimétrica seguem ADR-0003/0004. Não tratar JWT em Valkey como prova de vigência.
- Sessões paralelas são permitidas; logout revoga só a corrente. Prazo de inatividade é configuração validada e fica pendente de valor operacional de Segurança e Produto.
- O contrato interno controla audiência, emissor, tenant, escopo, `kid`, validade e `jti`; a sessão no browser é opaca.

## Modificar / Referenciar

- **modificar:** `src/identity/src/CodeForCoders.Identity.Infra.Data/IdentityDbContext.cs`, `src/identity/src/CodeForCoders.Identity.Api/Extensions/EndpointExtensions.cs` e `src/identity/src/CodeForCoders.Identity.Api/Extensions/ServiceConfigurationExtensions.cs` (sessão autoritativa, endpoints, JWT e autenticação de serviço)
- **modificar:** `src/bff-student/src/CodeForCoders.BffStudent.Application/Common/OpaqueBffSession.cs`, `src/bff-student/src/CodeForCoders.BffStudent.Infra.Data/ValkeyBffSessionStore.cs` e `src/bff-student/src/CodeForCoders.BffStudent.Api/Security/BffSecurityMiddleware.cs` (cookie opaco, TTL e CSRF vinculado)
- **modificar:** `src/bff-student/src/CodeForCoders.BffStudent.Api/Extensions/EndpointExtensions.cs`, `src/bff-student/src/CodeForCoders.BffStudent.Api/Extensions/ServiceConfigurationExtensions.cs` e `src/bff-student/src/CodeForCoders.BffStudent.Api/Endpoints/SessionEndpoints.cs` (login, leitura, logout e alinhamento ao contrato)
- **modificar:** `src/student-spa/src/app/router.tsx`, `src/student-spa/src/config/paths.ts` e `src/student-spa/src/lib/api-client.ts` (entrada, saída, rotas protegidas, cookies e CSRF)
- **ref:** `techspec.md#v-03`, `api-contract.yaml`, `internal-api-contract.yaml`, `context/architecture-baseline.md`, `domains/identidade-e-acesso/domain.md` e skills `dotnet`/`react`

## Pronto quando

- [ ] Gate passa (exit 0), com as três suítes selecionadas pelo comando do frontmatter.
- [ ] Login válido cria sessão opaca; resposta pública nunca inclui JWT; erros genéricos não enumeram conta e conta não confirmada só é distinguida após senha correta.
- [ ] Atividade válida renova inatividade, expiração e falhas de Identity/Valkey fecham acesso, e CSRF de outra sessão bloqueia escrita.
- [ ] Logout revoga só a sessão corrente; outra sessão permanece válida e repetição não reativa a encerrada.
