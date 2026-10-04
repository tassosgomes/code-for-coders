---
status: done
task_kind: vertical
blocked_by: ["4.0"]
gate: 'dotnet test --project src/learning/tests/CodeForCoders.Learning.IntegrationTests/CodeForCoders.Learning.IntegrationTests.csproj -- --filter-class CodeForCoders.Learning.IntegrationTests.PublishedLessonRepublicationTests --minimum-expected-tests 4 && npm --prefix src/student-spa run test -- playback-navigation'
gate_expect: "Pelo menos 10 testes passam: 4 de integração de learning e 6 do SPA"
---

# 6.0 O aluno troca de aula e ajusta a velocidade

**Fatia:** V-04 · **Cobre:** RF-05 (velocidade), RF-06 (trocar de aula, versão vigente), RN-R06, RN-R08, RN-C07, RN-C09, RN-D08, DP-04, DP-10, US de trocar de aula · **Spec:** `techspec.md#v-04` · **ADR:** —

## Comportamento

O aluno escolhe outra aula na lista e ela abre do início, com nova sessão e nova decisão; e muda a velocidade no player.

- **`student-spa`:** escolher uma aula navega para `/aulas/{outra}`, que repete a estrutura de 3.0 e a sessão de 4.0; a aula anterior
  para de tocar e a sessão dela simplesmente vence. **Todas as aulas do curso estão abertas** a quem tem direito: a aula 5 abre sem
  ter visto as aulas 2, 3 e 4 (RN-R06, G20). A velocidade tem 0,5x, 1x, 1,25x, 1,5x e 2x, no próprio player, com efeito imediato,
  alcançável por teclado e com nome acessível.
- **`learning`:** a estrutura devolvida é a da **versão vigente**. Depois de uma republicação, o aluno vê a ordem e o nome novos, e o
  endereço de uma aula renomeada ou reordenada continua apontando para **a mesma aula** (identidade estável). A aula removida da
  versão vigente devolve 404 `LESSON_NOT_AVAILABLE`, e a tela mostra "Esta aula não está disponível" com a lista da versão nova à
  mão. Republicar não cria, altera nem encerra direito.

Casos negativos que a task prova: aula removida na republicação (404 indistinto); aluno do curso A abrindo aula do curso B (a decisão
do B é negada); republicação com aula reordenada e renomeada (mesma `lessonId`, nova posição e título).

## Fora do escopo desta task

- Renovação (5.0), avanço (7.0), sinais (8.0), "meus cursos" e retomar de onde parou (`CAP-017`), liberação progressiva (`CAP-018`).

## Decisões fechadas

- A aula sempre começa do início (DP-06); nenhum estado de progresso na lista (DP-04; riscos do PRD).
- O direito é sobre o curso, nunca sobre a versão (RN-D08, RN-R08).

## Modificar / Referenciar

Arquivos a criar não são listados: a estrutura vem das skills `dotnet` e `react`.

- **modificar:** `src/student-spa/src/features/` (lista de aulas navegável e controle de velocidade do player de 4.0), `src/testing/`
- **ref:** `src/learning/src/CodeForCoders.Learning.Application/UseCases/Courses/PublishCourse/` (republicação) e `CourseVersion.cs` (versão vigente, identidade da aula); `docs/design/wireframes-aula.md`; `techspec.md#v-04`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/learning` | `dotnet format src/learning/CodeForCoders.Learning.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/learning` | `dotnet build src/learning/CodeForCoders.Learning.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/learning` | `dotnet test --project src/learning/tests/CodeForCoders.Learning.ArchitectureTests/CodeForCoders.Learning.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/student-spa` | `npm --prefix src/student-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run test --` | exit 0 (suíte inteira, sem regressão) | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run build -- --base=/student/` | exit 0 | `ci-react-ts.yml` (`build-args: --base=/student/`) |

Cobertura agregada ≥ 70%, `dotnet publish` e imagens ficam para a validação full. Testcontainers pesados rodam **em sequência**, nunca em paralelo (AGENTS.md).

## Pronto quando

- [x] Gate focalizado passa (exit 0), com os mínimos do `gate`.
- [ ] No navegador, com duas aulas: trocar de aula toca a outra do início; mudar a velocidade para 1,5x tem efeito imediato; o endereço de uma aula republicada com outro nome continua abrindo a mesma aula.
- [ ] A aula removida da versão vigente mostra "Esta aula não está disponível" e a lista atual.
