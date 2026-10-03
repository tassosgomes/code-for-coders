---
status: done
task_kind: vertical
blocked_by: ["2.0"]
gate: 'dotnet test --project src/identity/tests/CodeForCoders.Identity.UnitTests/CodeForCoders.Identity.UnitTests.csproj -- --filter-class CodeForCoders.Identity.UnitTests.StudentTokenAudienceTests --minimum-expected-tests 5 && dotnet test --project src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj -- --filter-class CodeForCoders.Identity.IntegrationTests.StudentTokenJwksTests --minimum-expected-tests 3 && dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.AccessDecisionIssuersTests --minimum-expected-tests 3 && dotnet test --project src/learning/tests/CodeForCoders.Learning.IntegrationTests/CodeForCoders.Learning.IntegrationTests.csproj -- --filter-class CodeForCoders.Learning.IntegrationTests.StudentLessonTests --minimum-expected-tests 10 && dotnet test --project src/learning/tests/CodeForCoders.Learning.IntegrationTests/CodeForCoders.Learning.IntegrationTests.csproj -- --filter-class CodeForCoders.Learning.IntegrationTests.AccessDecisionClientTests --minimum-expected-tests 4 && dotnet test --project src/bff-student/tests/CodeForCoders.BffStudent.IntegrationTests/CodeForCoders.BffStudent.IntegrationTests.csproj -- --filter-class CodeForCoders.BffStudent.IntegrationTests.StudentLessonProxyTests --minimum-expected-tests 7 && npm --prefix src/student-spa run test -- student-lesson-screen && npm --prefix src/student-spa run test -- student-login-return'
gate_expect: "Pelo menos 43 testes passam: 5 e 3 de identity, 3 de commerce, 14 de learning, 7 do BFF, 11 do SPA"
---

# 3.0 O aluno com direito abre a aula e vê a lista de aulas; sem direito, vê o motivo

**Fatia:** V-01 · **Cobre:** RF-01 (estados de acesso, aula indisponível, volta ao login), RF-06 (lista e versão vigente), RN-R02, RN-R06, RN-R07, RN-R08, RN-D01, RN-D08, US de abrir a aula e de entender a mensagem · **Spec:** `techspec.md#v-01` · **ADR:** [ADR-0003](../../docs/adr/0003-verificacao-de-sessao-do-aluno.md), [ADR-0012](../../docs/adr/0012-autenticacao-de-servico-media-learning-em-commerce-revisada.md), [ADR-0013](../../docs/adr/0013-jwt-de-aluno-validado-por-servicos-de-dominio.md)

## Comportamento

O aluno abre `<origem do student-spa><BASE_PATH>aulas/{lessonId}` (localmente `http://localhost:8082/student/aulas/{lessonId}`) e,
se tem direito vigente ao curso, vê o título da aula e a **lista de módulos e aulas da versão vigente**, na ordem do
currículo, com a aula atual destacada, e a área do player em estado de carregamento (o vídeo é de 4.0). Operação:
`getStudentLesson` → `getStudentLessonInternal` em `api-contract.yaml` e `internal-api-contract-learning.yaml`.

- **`identity`:** a chave de aluno é publicada no JWKS ao lado da de ator (com `PreviousSigningPublicKeys` de aluno); a
  audiência `learning` é configurada com o escopo `lessons:read`. O JWT de aluno para `learning` tem as claims da ADR-0013
  e **não** tem `email` (o e-mail é de 4.0, só na audiência `media`).
- **`commerce`:** o emissor `learning` entra na configuração dos emissores de asserção (escopo `access-decision:read`);
  nenhuma operação, esquema ou versão muda.
- **`learning`:** valida o JWT de aluno **localmente** pelo JWKS, com a política de aluno (escopo `lessons:read` **e** ausência
  da claim `permissions`); o token de ator interno não abre a rota, e o de aluno não abre rota de autoria. Acha a aula na
  **versão vigente** de um curso publicado da escola do JWT (a versão de maior número do curso). Só então consulta a decisão
  de acesso em `commerce` com a asserção de `learning` (timeout de 2 s, sem nova tentativa, cache em memória de até 30 s por
  (escola, aluno, curso), nunca além do término informado, guardando negado e permitido e **nunca** indisponível). Ordem fixa:
  aula (404 `LESSON_NOT_AVAILABLE`) → decisão (403 `ACCESS_DENIED` com `reason` e, vencido, `accessEndedAt`; 503
  `ACCESS_DECISION_UNAVAILABLE`) → corpo. **Nada do curso, nem título, é devolvido antes da decisão positiva.** O `studentId`
  da consulta é sempre a claim `sub`. A busca da aula usa índice sobre `course_versions.modules` (migration do EF).
- **`bff-student`:** o middleware passa a pedir o JWT com a **audiência da rota** (hoje só `/proxy` tem audiência): rota de
  aula → `learning`. Mapeia os erros de `learning` e de Identity (502 `UPSTREAM_UNAVAILABLE`, 504 `UPSTREAM_TIMEOUT`, 503
  repassado como está) conforme `api-contract.yaml`. GET dispensa CSRF.
- **`student-spa`:** a rota `/aulas/:lessonId` dentro do bloco autenticado, e a tela com os estados do wireframe aprovado:
  carregando, lista de aulas, **sem acesso** ("Você não tem acesso a este curso", sem título), **acesso encerrado** com a data
  do último dia no fuso da escola (`SCHOOL_TIME_ZONE`, padrão `America/Sao_Paulo`), **indisponibilidade** com *Tentar de novo*
  (distinta de "sem acesso") e **aula não disponível** (mesma mensagem para inexistente, de outra escola, não publicada e
  removida). **Sem sessão de login, leva ao login e volta à mesma aula**: o retorno aceita só caminho interno do SPA (começa
  com uma barra, não com duas) e qualquer outro valor é descartado.
- **Provisionamento local:** `scripts/generate-local-env.sh` gera a chave de aluno de Identity e o par de chaves de asserção de
  `learning`; `docker-compose.yml`, `docker-compose.remote.yml` e `docker-compose.coolify.yml` ganham as variáveis
  (`StudentSessionTokens`, `AudienceScopes`, o emissor `learning` em `commerce`, o cliente em `learning`).

Casos negativos que a task prova: aluno sem concessão (403 `no-grant`, nenhum título no corpo); concessão vencida (403
`grant-ended` com `accessEndedAt`); `commerce` sem resposta ou com erro (503, **nada devolvido**, e a mesma consulta logo
depois, com `commerce` de volta, funciona: indisponível não foi guardado); aula inexistente, de outra escola, de curso não
publicado e removida na republicação (**o mesmo** 404); JWT de ator e JWT de aluno sem o escopo (401/403); aula de curso
diferente do que o aluno tem (a decisão do outro curso é negada).

## Fora do escopo desta task

- O player, a sessão de reprodução, a marca d'água e o e-mail na claim → 4.0. A claim `email` não existe nesta task.
- Trocar de aula abrindo outra sessão, velocidade → 6.0. Renovação → 5.0. Avanço → 7.0. Sinais → 8.0.
- A claim `email` e a audiência `media` → 4.0.

## Decisões fechadas

- Cache de decisão em memória por instância, 30 s, **nunca** indisponível (`techspec.md` D-02; ADR-0012, item 5).
- Timeouts: 2 s `learning` → `commerce`; 5 s `bff-student` → `learning` (D-03). Sem nova tentativa no servidor.
- Sem cache de JWT de aluno nem da asserção de serviço (D-12; ADR-0003; ADR-0012, item 6).
- `studentId` só da claim `sub`; nenhuma rota aceita aluno por parâmetro (ADR-0013, item 5).
- Fuso do aluno para a data de término vem de `SCHOOL_TIME_ZONE` (D-13), não do servidor.
- Mensagens para o aluno sem termo técnico e sem "protegido contra cópia" (PRD, RN-M15).
- O retorno depois do login aceita só caminho interno (`techspec.md`, Riscos).

## Modificar / Referenciar

Arquivos a criar não são listados: a estrutura vem das skills `dotnet` e `react`.

- **modificar:** `src/identity/src/CodeForCoders.Identity.Api/Security/UserTokenSigningKeySet.cs`, `StudentSessionTokenOptions.cs`, `StudentSessionTokenIssuer.cs` (chave de aluno no JWKS, rotação, escopo por audiência), `Extensions/ServiceConfigurationExtensions.cs` (validar opções)
- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Api/appsettings.json` e os `docker-compose*.yml` (emissor `learning`)
- **modificar:** `src/learning/src/CodeForCoders.Learning.Api/Security/LearningJwtBearerOptionsSetup.cs`, `LearningAuthorization.cs`, `Extensions/ServiceConfigurationExtensions.cs` (política de aluno, cliente da decisão), `Infra.Data/Configurations/CourseVersionConfiguration.cs` e `Queries/CourseQueries.cs` (índice `jsonb`, busca da aula)
- **modificar:** `src/bff-student/src/CodeForCoders.BffStudent.Api/Security/BffSecurityMiddleware.cs`, `Application/Common/BffSecurityOptions.cs`, `Extensions/EndpointExtensions.cs`, `appsettings.json` (audiência por rota, rota e cliente de `learning`)
- **modificar:** `src/student-spa/src/app/router.tsx`, `src/config/paths.ts`, `src/app/routes/dashboard-route.tsx` e `student-login-route.tsx` (retorno ao login), `src/testing/` (handlers MSW da aula)
- **modificar:** `scripts/generate-local-env.sh`, `docker-compose.yml`, `docker-compose.remote.yml`, `docker-compose.coolify.yml`
- **ref:** `tasks/prd-concessao-acesso/internal-api-contract-commerce.yaml` (`decideAccessInternal`); `src/commerce/src/CodeForCoders.Commerce.Api/Security/ServiceAssertionVerifier.cs`; `src/bff-student/src/CodeForCoders.BffStudent.Api/Clients/ShowcaseCommerceClient.cs` e `Security/ServiceAssertionTokenFactory.cs` (padrão de cliente e asserção); `docs/design/wireframes-aula.md`; `api-contract.yaml`, `internal-api-contract-learning.yaml`, `internal-api-contract-identity.yaml`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/identity` | `dotnet format src/identity/CodeForCoders.Identity.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/identity` | `dotnet build src/identity/CodeForCoders.Identity.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/identity` | `dotnet test --project src/identity/tests/CodeForCoders.Identity.ArchitectureTests/CodeForCoders.Identity.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/commerce` | `dotnet format src/commerce/CodeForCoders.Commerce.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/commerce` | `dotnet build src/commerce/CodeForCoders.Commerce.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.ArchitectureTests/CodeForCoders.Commerce.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/learning` | `dotnet format src/learning/CodeForCoders.Learning.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/learning` | `dotnet build src/learning/CodeForCoders.Learning.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/learning` | `dotnet test --project src/learning/tests/CodeForCoders.Learning.ArchitectureTests/CodeForCoders.Learning.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/bff-student` | `dotnet format src/bff-student/CodeForCoders.BffStudent.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-student` | `dotnet build src/bff-student/CodeForCoders.BffStudent.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/bff-student` | `dotnet test --project src/bff-student/tests/CodeForCoders.BffStudent.ArchitectureTests/CodeForCoders.BffStudent.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/student-spa` | `npm --prefix src/student-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run test --` | exit 0 (suíte inteira, sem regressão) | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run build -- --base=/student/` | exit 0 | `ci-react-ts.yml` (`build-args: --base=/student/`) |

Cobertura agregada ≥ 70%, `dotnet publish` e imagens ficam para a validação full. Testcontainers pesados rodam **em sequência**, nunca em paralelo (AGENTS.md).

## Pronto quando

- [x] Gate focalizado passa (exit 0), com os mínimos do `gate`.
- [x] A aplicação de `learning` inicia com os registros reais (JWKS, política de aluno, cliente da decisão) no ambiente de teste, e o cliente da decisão é exercitado pelo **adaptador real** contra um `commerce` de teste controlado, não por dublê da interface.
- [ ] Aluno com cortesia vigente (CAP-008) abre o link no navegador contra o ambiente de desenvolvimento e vê a lista de aulas; aluno sem concessão vê a mensagem; sem sessão de login, o link leva ao login e volta à aula. Pré-requisitos: `scripts/remote-infra.sh provision` e `migrate`, `scripts/apps.sh start --remote`, curso publicado com duas aulas e uma cortesia concedida pelo backoffice.
- [x] Nenhum título de curso, módulo ou aula aparece na resposta de um aluno sem direito.
