---
status: done
task_kind: vertical
blocked_by: ["5.0"]
gate: 'dotnet test --project src/commerce/tests/CodeForCoders.Commerce.UnitTests/CodeForCoders.Commerce.UnitTests.csproj -- --filter-class CodeForCoders.Commerce.UnitTests.AccessDecisionRulesTests --minimum-expected-tests 6 && dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.AccessDecisionTests --minimum-expected-tests 14'
gate_expect: "Pelo menos 20 testes passam: 6 unitários e 14 de integração de commerce"
---

# 7.0 Qualquer serviço pergunta se o aluno pode acessar o curso agora e recebe a única resposta

**Fatia:** V-05 · **Cobre:** RF-06, US-05, US-06, RN-D01, RN-D03, RN-D06, RN-D07, RN-D08, RN-D15, RN-D17, RN-C08, RN-C09, RN-O09, RN-22, DP-09, C-04 · **Spec:** `techspec.md#v-05-qualquer-serviço-pergunta-este-aluno-pode-acessar-este-curso-agora-e-recebe-a-única-resposta` · **ADR:** [ADR-0011](../../docs/adr/0011-autenticacao-de-servico-media-learning-em-commerce.md)

## Comportamento

- **`commerce` — `decideAccessInternal`:** `GET /internal/v1/access-decision?studentId=&courseId=` autenticado
  **só** por asserção de serviço com o escopo `access-decision:read` (JWS RS256, `iss` do chamador, `aud` =
  `commerce`, `tenantId` permitido ao emissor, vida de até 60 s, `jti` consumido uma vez no Valkey de `commerce`).
  Uma consulta indexada por (escola, aluno, curso) sobre as concessões ativas, comparando `expires_at` com o
  relógio injetado (nunca `DateTime.Now`):
  - alguma concessão sem término → `allowed`, `validity: lifetime`;
  - senão alguma com `expires_at` no futuro → `allowed`, `validity: until` com o **maior** `expires_at`;
  - senão, havendo concessões (todas vencidas) → `denied`, `grant-ended`, `lastExpiredAt` = o maior término;
  - senão → `denied`, `no-grant`.
  Aluno, conta ou curso desconhecidos, conta que não é de aluno e outra escola resultam em `denied`/`no-grant`,
  **sem 404** e sem chamar Identity nem a visão do curso. A resposta sai com `Cache-Control: private,
  max-age=30`. Asserção sem o escopo, de emissor sem o tenant, com `jti` repetido, de `bff-student` ou **JWT de
  ator** na mesma rota → recusada (403 `SCOPE_DENIED` ou 401 `SERVICE_UNAUTHORIZED`/`TOKEN_INVALID`); as rotas de
  cortesia recusam asserção de serviço (um único esquema por rota).
- **Invariância:** a decisão é só sobre o curso: publicar nova versão do curso e alterar ou despublicar oferta
  **não** a mudam; nunca diz se uma aula está liberada.
- **Credenciais:** **nenhuma chave de `media` nem de `learning` é provisionada**; a verificação é exercida com um
  emissor de teste e o escopo novo entra ao lado dos de `showcase:read` e `purchase-intent:write`. Cada serviço
  recebe seu par de chaves na entrega do seu consumidor (`CAP-007`, `CAP-017`).
- **Arquitetura:** só `Entitlement` expõe o caso de uso de decisão (G11), verificado por teste; nenhum
  identificador de pessoa em log, span ou métrica.
- **Caso negativo principal:** a última noite do último dia (23:59:59) → `allowed` e 00:00:00 do dia seguinte →
  `denied`/`grant-ended`, com relógio controlado; uma vencida e uma ativa → `allowed`; 3 meses e vitalícia →
  `lifetime`.

## Fora do escopo desta task

Qualquer consumidor real (`media`, `learning`) e as chaves deles: `CAP-007` e `CAP-017`. Suspensão e revogação (`CAP-009`). Fato de expiração (8.0).

## Decisões fechadas

- A decisão é o único lugar da resposta; nenhum consumidor guarda réplica do direito; cache de até 30 s só no consumidor (RN-D01, BA07, G11).
- Falha fechada é regra do **consumidor**: não existe resposta padrão "pode" (RF-06).
- A asserção prova só o chamador; o `studentId` deve vir de uma sessão de aluno validada pelo chamador ([ADR-0011](../../docs/adr/0011-autenticacao-de-servico-media-learning-em-commerce.md), decisão 3).
- Sem credenciais ociosas: o emissor de produção nasce com o primeiro consumidor (D-07).

## Modificar / Referenciar

- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Api/Extensions/ServiceAssertionExtensions.cs`, `Security/ServiceAssertionScopes.cs` (política e escopo `access-decision:read`); `Extensions/EndpointExtensions.cs` (rota)
- **modificar:** `src/commerce/tests/CodeForCoders.Commerce.ArchitectureTests/ModuleSchemaConventionTest.cs` (só `Entitlement` expõe a decisão)
- **ref:** `src/commerce/src/CodeForCoders.Commerce.Api/Security/ServiceAssertionVerifier.cs`, `ServiceAssertionAuthenticationHandler.cs` (verificador de asserção já usado pelo `bff-student`); `Infra.Data/Outbox/ServiceAssertionReplayStore.cs` (`jti`); `internal-api-contract-commerce.yaml` (`decideAccessInternal`); `domains/matricula-e-direito-de-acesso/domain.md` (RN-D01 a RN-D17); `context/architecture-baseline.md` (BA07, G11); skill `dotnet`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/commerce` | `dotnet format src/commerce/CodeForCoders.Commerce.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/commerce` | `dotnet build src/commerce/CodeForCoders.Commerce.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj` | exit 0 (sem regressão) | `ci-dotnet.yml` Testes |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.ArchitectureTests/CodeForCoders.Commerce.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |

Cobertura agregada ≥ 70%, `dotnet publish` e imagens ficam para a validação full.

## Pronto quando

- [x] Gate passa (exit 0) com pelo menos 20 testes.
- [x] Composição: `commerce` inicia no ambiente de teste com os registros reais (esquema de asserção, política, replay em Valkey) e a rota é exercitada pela fronteira HTTP real.
- [x] Os casos de RF-06: cortesia de 6 meses → `allowed` com o término; sem concessão → `no-grant`; concessão vencida ontem → `grant-ended` **sem rotina rodada**; uma vencida e uma ativa → `allowed`; 3 meses e vitalícia → `lifetime`; outra escola e conta que não é de aluno → `no-grant`.
- [x] Nova versão do curso e alteração ou despublicação de oferta → a mesma decisão de antes; a resposta traz `Cache-Control: private, max-age=30`.
- [x] Asserção sem o escopo, com `jti` repetido, de `bff-student` ou JWT de ator na rota da decisão → recusada; asserção de serviço nas rotas de cortesia → recusada.
- [x] O teste de arquitetura prova que só `Entitlement` expõe a decisão; nenhum identificador de pessoa em log, span ou métrica.
