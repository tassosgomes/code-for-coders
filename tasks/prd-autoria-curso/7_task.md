---
status: done
task_kind: vertical
blocked_by: ["1.0", "2.0"]
gate: "dotnet test --project src/learning/tests/CodeForCoders.Learning.IntegrationTests/CodeForCoders.Learning.IntegrationTests.csproj -- --filter-class CodeForCoders.Learning.IntegrationTests.CourseDeletionTests --minimum-expected-tests 4"
gate_expect: "4 testes de exclusão restrita e isolamento de curso passam; menos de 4 reprova"
---

# 7.0 Professor exclui somente curso nunca publicado

**Fatia:** V-06 · **Cobre:** RF-09; US-05; RN-C02/06; DP-03, OD45 ·
**Spec:** [V-06](techspec.md#v-06-professor-exclui-apenas-curso-nunca-publicado) ·
**ADR:** [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md)

## Comportamento

Na lista ou no editor do curso **nunca publicado**, o professor vê “Excluir curso”, abre a
confirmação do desenho aprovado e, ao confirmar, remove curso e itens do rascunho. O curso some
da lista e o link antigo passa a 404. Cancelar não altera nada. Retry da mesma intenção com
`Idempotency-Key` respeita o resultado gravado. Um curso que já teve qualquer versão publicada
não oferece a ação; pedido direto responde 409 `COURSE_ALREADY_PUBLISHED` e preserva versão,
rascunho e referências. Curso de outra escola retorna 404 indistinto. Exclusão de rascunho não
produz fato de publicação nem ato administrativo no outbox.

O BFF usa o adaptador HTTP real sob Learning controlado e o host inicia com DI real. Smoke no
Compose exclui um rascunho, recarrega a lista, tenta abrir o ID e prova que um curso publicado
não pode ser excluído nem pela UI nem por chamada direta.

## Fora do escopo desta task

Despublicar, retirar de circulação ou excluir curso já publicado (OD45); apagar vídeo na Mídia.

## Decisões fechadas

- A exclusão só vale para `draft` sem nenhuma versão histórica (RF-09, OD45). Ausência de ação
  na UI não substitui a regra do serviço dono.
- Não gerar ato de auditoria: rascunho nunca publicado não afetou aluno ou dinheiro (PRD, RF-09).
- Escrita continua exigindo permissão, CSRF, tenant e chave de idempotência do contrato.

## Modificar / Referenciar

- **modificar:** `src/learning/src/CodeForCoders.Learning.Infra.Data/LearningDbContext.cs`,
  `src/learning/src/CodeForCoders.Learning.Api/Extensions/EndpointExtensions.cs`.
- **modificar:** `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/EndpointExtensions.cs`.
- **ref:** [PRD](prd.md), [OpenAPI público](api-contract.yaml),
  [OpenAPI interno](internal-api-contract-learning.yaml),
  [Figma aprovado em 1.0](../../docs/design/wireframes-autoria-curso.md),
  `.agents/skills/dotnet/SKILL.md`, `.agents/skills/react/SKILL.md`.

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| Learning | `dotnet restore src/learning/CodeForCoders.Learning.slnx` | exit 0 | `.github/workflows/learning.yml` → `ci-dotnet.yml@v1` |
| Learning | `dotnet format src/learning/CodeForCoders.Learning.slnx --verify-no-changes` | exit 0 | mesmo CI, lint |
| Learning | `dotnet test --project src/learning/tests/CodeForCoders.Learning.IntegrationTests/CodeForCoders.Learning.IntegrationTests.csproj -- --filter-class CodeForCoders.Learning.IntegrationTests.CourseDeletionTests --minimum-expected-tests 4` | exit 0, ≥ 4 testes | mesmo CI, testes; gate focalizado |
| Learning | `dotnet publish src/learning/CodeForCoders.Learning.slnx -c Release` | exit 0 | mesmo CI, publish |
| BFF | `dotnet restore src/bff-admin/CodeForCoders.BffAdmin.slnx` | exit 0 | `.github/workflows/bff-admin.yml` → `ci-dotnet.yml@v1` |
| BFF | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | mesmo CI, lint |
| BFF | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj -- --filter-class CodeForCoders.BffAdmin.IntegrationTests.CourseDeletionProxyTests --minimum-expected-tests 3` | exit 0, ≥ 3 testes; HTTP/DI reais | mesmo CI, testes |
| BFF | `dotnet publish src/bff-admin/CodeForCoders.BffAdmin.slnx -c Release` | exit 0 | mesmo CI, publish |
| SPA | `npm --prefix src/admin-spa run lint` | exit 0 | `.github/workflows/admin-spa.yml` → `ci-react-ts.yml@v1` |
| SPA | `npm --prefix src/admin-spa run typecheck` | exit 0 | mesmo CI, Typecheck |
| SPA | `npm --prefix src/admin-spa test -- authoring-delete` | exit 0; arquivo selecionado cobre confirmação e publicado sem ação | mesmo CI, Vitest |
| SPA | `npm --prefix src/admin-spa run build -- --base=/admin/` | exit 0 | mesmo CI, Build |

## Pronto quando

- [ ] Gate focalizado passa (exit 0) com ao menos 4 testes de Learning.
- [ ] Rascunho nunca publicado excluído some da lista e passa a 404; cancelar preserva tudo;
  retry idempotente não cria erro inesperado.
- [ ] Curso já publicado responde `COURSE_ALREADY_PUBLISHED` e conserva versões; outro tenant
  recebe 404; não há ato ou fato de exclusão/publicação no outbox.
- [ ] UI só mostra a ação para rascunho nunca publicado; BFF usa cliente e host reais sob
  fronteira de teste controlada.
