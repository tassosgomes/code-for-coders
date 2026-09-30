---
status: pending
task_kind: vertical
blocked_by: ["1.0", "2.0"]
gate: "dotnet test --project src/learning/tests/CodeForCoders.Learning.IntegrationTests/CodeForCoders.Learning.IntegrationTests.csproj -- --filter-class CodeForCoders.Learning.IntegrationTests.CourseStructureTests --minimum-expected-tests 8"
gate_expect: "8 testes de edição, identidade e ordenação do currículo passam; menos de 8 reprova"
---

# 3.0 Professor organiza módulos e aulas do rascunho sem mudar identidades

**Fatia:** V-02 · **Cobre:** RF-03; US-01; RN-C01/03/05/07 ·
**Spec:** [V-02](techspec.md#v-02-professor-organiza-o-rascunho-sem-mudar-identidades) ·
**ADR:** [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md)

## Comportamento

No editor aprovado, o professor altera título/descrição do curso e cria, renomeia, reordena,
move ou remove módulos e aulas no rascunho. Cada escrita por item devolve o curso inteiro com
`draftRevision` nova; a UI substitui o estado exibido e mostra quem editou por último. Módulos
e aulas têm posições 1-based, contíguas e únicas. Mover aula para outro módulo, renomear ou
trocar sua ordem preserva `lessonId`; remover e criar outra, ainda que com o mesmo título, gera
ID novo. A remoção de módulo pede confirmação com a contagem de aulas que sairão. Arrastar e
comandos por teclado produzem a mesma ordem e mantêm foco no item movido.

Operação em item de outro curso/tenant devolve 404; posição fora do intervalo, título vazio ou
limite de 100 módulos/200 aulas por módulo devolve erro contratual e não altera a estrutura.
Dois professores da mesma escola enxergam a última gravação confirmada conforme OD44; não se
introduz bloqueio de edição colaborativa. Verificar o BFF com o cliente HTTP real sob fronteira
Learning controlada e o host com DI real. Smoke no Compose de criação, reordenação, movimento,
remoção confirmada e recarga do editor.

## Fora do escopo desta task

Seleção de vídeo (4.0), publicação/versão (5.0/6.0) e exclusão do curso (7.0).

## Decisões fechadas

- Edição por item e resposta integral: C-02; `draftRevision` não é verificada nestas edições
  (C-03, OD44). Somente publicar/descartar a conferem depois.
- IDs estáveis acompanham o item, não a posição; renumerar irmãos acontece no serviço dono.
- Nenhum componente de preço, nível, oferta ou aula sem vídeo entra no editor (PRD, não objetivos).

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
| Learning | `dotnet test --project src/learning/tests/CodeForCoders.Learning.IntegrationTests/CodeForCoders.Learning.IntegrationTests.csproj -- --filter-class CodeForCoders.Learning.IntegrationTests.CourseStructureTests --minimum-expected-tests 8` | exit 0, ≥ 8 testes | mesmo CI, testes; gate focalizado |
| Learning | `dotnet publish src/learning/CodeForCoders.Learning.slnx -c Release` | exit 0 | mesmo CI, publish |
| BFF | `dotnet restore src/bff-admin/CodeForCoders.BffAdmin.slnx` | exit 0 | `.github/workflows/bff-admin.yml` → `ci-dotnet.yml@v1` |
| BFF | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | mesmo CI, lint |
| BFF | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj -- --filter-class CodeForCoders.BffAdmin.IntegrationTests.CourseStructureProxyTests --minimum-expected-tests 4` | exit 0, ≥ 4 testes; cliente/DI reais | mesmo CI, testes |
| BFF | `dotnet publish src/bff-admin/CodeForCoders.BffAdmin.slnx -c Release` | exit 0 | mesmo CI, publish |
| SPA | `npm --prefix src/admin-spa run lint` | exit 0 | `.github/workflows/admin-spa.yml` → `ci-react-ts.yml@v1` |
| SPA | `npm --prefix src/admin-spa run typecheck` | exit 0 | mesmo CI, Typecheck |
| SPA | `npm --prefix src/admin-spa test -- authoring-structure` | exit 0; arquivo selecionado cobre edição e teclado | mesmo CI, Vitest |
| SPA | `npm --prefix src/admin-spa run build -- --base=/admin/` | exit 0 | mesmo CI, Build |

## Pronto quando

- [ ] Gate focalizado passa (exit 0) com ao menos 8 testes de Learning.
- [ ] Módulos/aulas mantêm posição contígua depois de mover e reordenar; aula movida conserva ID,
  aula removida e recriada ganha outro ID.
- [ ] Remover módulo informa quantas aulas saem; cancelamento não altera nada; teclado permite
  ordenar e mover com foco preservado.
- [ ] Limite, posição inválida e item de outro tenant/curso são recusados sem mutação; BFF e host
  usam adaptador e registros reais sob fronteira de teste controlada.
