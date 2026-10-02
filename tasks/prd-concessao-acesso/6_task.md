---
status: done
task_kind: vertical
blocked_by: ["5.0"]
gate: 'dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.StudentAccessGrantsTests --minimum-expected-tests 8 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj -- --filter-class CodeForCoders.BffAdmin.IntegrationTests.CourtesyStudentGrantsProxyTests --minimum-expected-tests 3 && npm --prefix src/admin-spa run test -- courtesy-student-grants'
gate_expect: "Pelo menos 16 testes passam: 8 de integração de commerce, 3 do BFF, 5 do SPA"
---

# 6.0 O financeiro vê as concessões do aluno, é avisado antes de duplicar e confirma a vitalícia com reforço

**Fatia:** V-04 · **Cobre:** RF-02 (concessões), RF-03 (aviso e reforço), RF-04 (convivência), US-02, RN-D07, RN-D17, DP-06, DP-07 · **Spec:** `techspec.md#v-04-o-financeiro-vê-as-concessões-do-aluno-e-é-avisado-antes-de-duplicar-a-vitalícia-exige-confirmação-reforçada` · **ADR:** —

## Comportamento

- **`commerce` — `listStudentAccessGrantsInternal`:** lista, paginada, as concessões do aluno na escola do claim
  `tenantId`, de qualquer origem, da mais recente à mais antiga, com o título atual do curso (da visão) e
  `status` **calculado na leitura** (`expired` quando o momento atual é igual ou posterior a `expires_at`;
  `active` caso contrário) — nenhuma rotina precisa ter rodado (RN-D06). Aluno sem concessão, inexistente ou de
  outra escola devolve página vazia, não 404. `getCourtesyGrantInternal` já existe desde 5.0; aqui só se prova a
  leitura de origem cortesia da escola.
- **`bff-admin` — `listStudentAccessGrants`:** valida a sessão, exige `cortesia.conceder`, repassa o JWT de
  ator; 502/504 do padrão.
- **`admin-spa`:** a lista das concessões do aluno no passo *Aluno* (curso, origem, vigência em data absoluta ou
  "Acesso vitalício", situação); o **aviso** na revisão — "este aluno já tem acesso até DD/MM/AAAA" — quando o
  aluno tem concessão ativa ao curso escolhido, **sem impedir**; a **confirmação reforçada** da vitalícia, com o
  texto de que **não há como desfazer pela tela** e foco/anúncio acessíveis; duas cortesias ao mesmo curso
  convivem e a segunda não estende nem encerra a primeira. Conforme o Figma aprovado em 2.0.
- **Caso negativo principal:** concessão de outra escola nunca aparece; `getCourtesyGrant` de outra origem,
  inexistente ou de outra escola → 404 `GRANT_NOT_FOUND`; a vitalícia sem passar pelo reforço não é enviada.

## Fora do escopo desta task

Decisão de acesso (7.0), fato de expiração (8.0), revogação e desfazer (CAP-009), diagnóstico para o suporte (CAP-025).

## Decisões fechadas

- Várias concessões convivem e uma não altera a outra; o aviso nunca bloqueia (DP-06, RN-D07).
- Não há desfazer pela tela nesta entrega; o reforço da vitalícia é a mitigação aceita (DP-07, QA-01 do PRD).
- A situação é derivada na leitura; nenhuma coluna de situação "vencida" (`techspec.md#decisões-técnicas`, D-04).

## Modificar / Referenciar

- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Api/Extensions/EndpointExtensions.cs` (rota de listagem), consulta no módulo `Entitlement`
- **modificar:** `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/EndpointExtensions.cs` (rota)
- **modificar:** `src/admin-spa/src/features` (feature de cortesias: lista, aviso e reforço), `src/admin-spa/src/testing/handlers.ts`
- **ref:** `internal-api-contract-commerce.yaml` e `api-contract.yaml` (`listStudentAccessGrants`, `getCourtesyGrant`); `docs/design/wireframes-cortesias.md` e Figma aprovado; skills `dotnet` e `react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/commerce` | `dotnet format src/commerce/CodeForCoders.Commerce.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/commerce` | `dotnet build src/commerce/CodeForCoders.Commerce.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj` | exit 0 (sem regressão) | `ci-dotnet.yml` Testes |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.ArchitectureTests/CodeForCoders.Commerce.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/bff-admin` | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-admin` | `dotnet build src/bff-admin/CodeForCoders.BffAdmin.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/bff-admin` | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj` | exit 0 (sem regressão) | `ci-dotnet.yml` Testes |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run test -- courtesy-confirm` | exit 0 (sem regressão) | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run build -- --base=/admin/` | exit 0 | `ci-react-ts.yml` |

Cobertura agregada ≥ 70%, `dotnet publish` e imagens ficam para a validação full.

## Pronto quando

- [x] Gate passa (exit 0) com pelo menos 16 testes.
- [x] A lista traz uma concessão com término no passado como `expired` **sem** a rotina de 8.0 ter rodado, e uma ativa como `active`.
- [x] Duas cortesias ao mesmo curso convivem na mesma matrícula; o aviso aparece com o término da ativa e não bloqueia a confirmação.
- [x] Aluno de outra escola, inexistente ou sem concessão → página vazia; `getCourtesyGrant` de outra origem, inexistente ou de outra escola → 404 `GRANT_NOT_FOUND`.
- [x] A confirmação reforçada da vitalícia aparece e é necessária para enviar; a de período não a exige.
- [x] Smoke no Compose (o de 5.0): em `http://localhost:8081/admin/cortesias`, localizar o aluno que recebeu a cortesia → a concessão aparece na lista; preparar outra ao mesmo curso → o aviso aparece com a data; vitalícia → o passo reforçado.
