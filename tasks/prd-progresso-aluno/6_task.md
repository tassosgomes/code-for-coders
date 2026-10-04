---
status: pending
task_kind: vertical
blocked_by: ["5.0"]
gate: 'dotnet test --project src/learning/tests/CodeForCoders.Learning.IntegrationTests/CodeForCoders.Learning.IntegrationTests.csproj -- --filter-class CodeForCoders.Learning.IntegrationTests.StudentCoursesDegradationTests --minimum-expected-tests 5 && npm --prefix src/student-spa run test -- my-courses-degraded'
gate_expect: "Pelo menos 10 testes passam: 5 de integração de learning, 5 do SPA"
---

# 6.0 "Meus cursos" mostra o acesso encerrado e não mente quando algo falha

**Fatia:** V-04 · **Cobre:** RF-04 (encerrados, indisponível, progresso indisponível, nova cortesia), DP-02, C-09, US de acesso terminou · **Spec:** `techspec.md#v-04-meus-cursos-mostra-o-acesso-encerrado-e-não-mente-quando-algo-falha` · **ADR:** —

## Comportamento

- **`learning`:** `ended` traz título da versão vigente, `endedOn`, `endedReason` e o progresso como estava, ordenado por `endedAt` decrescente. Lista de Matrícula sem resposta → 503 `COURSE_ACCESS_UNAVAILABLE`, **nunca** 200 com listas vazias. Falha só na leitura do progresso → 200 com `progressAvailable: false`, `progress: null`, `started: null` e `continueLessonId` na primeira aula de cada curso.
- **`student-spa`, Início:** seção "Acesso encerrado", visualmente secundária, sem ação e sem parecer clicável: título, percentual com contagem e "Seu acesso terminou em dd/mm/aaaa" (data de `endedOn`, sem conversão de fuso). 503 → "Não foi possível carregar seus cursos agora. Tente de novo em instantes." com *Tentar de novo*, nunca o vazio. `progressAvailable: false` → aviso "Seu progresso não pôde ser carregado agora", cursos sem percentual, Começar/Continuar na primeira aula. `endedReason` desconhecido é exibido com a mesma frase. Desenho conforme `docs/design/wireframes-progresso.md`.

Casos que os testes provam: cortesia vencida com 5 de 10 → `ended` com 50% e `endedOn`; nova cortesia sobre curso encerrado → volta a `active` com o progresso anterior; `commerce` sem resposta → 503; falha induzida na leitura do progresso → `progressAvailable: false`; dois encerrados → ordem por término. No SPA: as três telas (encerrado, indisponível, progresso indisponível) e a ação *Tentar de novo*.

## Fora do escopo desta task

- Revogação como ato (`CAP-009`); estado suspenso (Fase 2).

## Decisões fechadas

- Encerrado aparece, sem ação (DP-02); `endedOn` vem de Matrícula (C-09).
- Indisponível nunca vira "Você ainda não tem cursos" (RF-04).

## Modificar / Referenciar

Arquivos a criar não são listados: a estrutura vem das skills `dotnet` e `react`.

- **modificar:** `src/student-spa/src/features/student-dashboard/components/dashboard-screen.tsx`
- **ref:** os casos de uso e o cliente de Matrícula de 5.0 em `src/learning/src/`; `src/learning/tests/CodeForCoders.Learning.IntegrationTests/LessonCommerceBoundaryHandler.cs` (fronteira controlada); `docs/design/wireframes-progresso.md`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/learning` | `dotnet format src/learning/CodeForCoders.Learning.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/learning` | `dotnet build src/learning/CodeForCoders.Learning.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/learning` | `dotnet test --project src/learning/tests/CodeForCoders.Learning.ArchitectureTests/CodeForCoders.Learning.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/student-spa` | `npm --prefix src/student-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run test --` | exit 0 (suíte inteira) | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run build -- --base=/student/` | exit 0 | `ci-react-ts.yml` |

## Pronto quando

- [ ] Gate focalizado passa (exit 0).
- [ ] No ambiente de desenvolvimento: antecipar o `expires_at` de uma cortesia com `scripts/expire-playback-grant.sh` (de `CAP-007`) → o curso migra para "Acesso encerrado" com a data; nova cortesia → volta para "Seus cursos" com o progresso.
- [ ] Com `commerce` parado, o Início mostra a indisponibilidade com *Tentar de novo*, e não o vazio.
