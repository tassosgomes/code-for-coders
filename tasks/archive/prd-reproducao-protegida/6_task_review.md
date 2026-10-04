# Revisão focused — Task 6.0 (V-04: trocar de aula e ajustar a velocidade)

Run: run.Lm6O8Vsr

- Modo: focused · Tentativa: 1/3 · HEAD revisado: 9cc8c611c099141606a569526e10778c0d450632 (árvore fd41491b1a56191f4982a229212e0ea92ae40eb3) · checkpoint anterior: 853c367/9cc8c61
- Escopo: alterações não commitadas em `protected-video-player.tsx`, `student-lesson-screen.tsx`, e os arquivos novos `PublishedLessonRepublicationTests.cs` e `playback-navigation.test.tsx`. `routing.default.json`, `6_task.md` e `flow-state.json` são do fluxo, fora do escopo de código.
- Tudo executado em primeiro plano e em sequência, sem monitor nem background. Nenhum arquivo editado pelo validador.

## Gate declarado (exit code é o veredito)

| # | Comando | Exit | Resultado |
|---|---|---|---|
| 1 | `dotnet test` Learning.IntegrationTests `--filter-class ...PublishedLessonRepublicationTests --minimum-expected-tests 4` | 0 | 4 de 4 |
| 2 | `npm --prefix src/student-spa run test -- playback-navigation` | 0 | 6 de 6 |

`gate_expect` (≥ 10 testes: 4 + 6) atendido.

## Verificações do projeto

| Comando | Exit |
|---|---|
| `dotnet format src/learning/CodeForCoders.Learning.slnx --verify-no-changes` | 0 |
| `dotnet build src/learning/CodeForCoders.Learning.slnx` | 0 (0 warnings, 0 errors) |
| `dotnet test --project ...ArchitectureTests.csproj` | 0 (9 de 9) |
| `npm --prefix src/student-spa run lint` | 0 |
| `npm --prefix src/student-spa run typecheck` | 0 |
| `npm --prefix src/student-spa run test --` (suíte inteira) | 0 (19 arquivos, 115 testes) |
| `npm --prefix src/student-spa run build -- --base=/student/` | 0 |

Cobertura agregada, `dotnet publish` e imagens ficam para a validação full.

## Bloqueante

### B1 — A aula removida não mostra a lista da versão nova (RF-06, V-04, "Pronto quando" nº 3)

`src/student-spa/src/features/student-lessons/components/student-lesson-screen.tsx:40-67` (e o `state={{ course }}` nos `Link`s, linhas ~85).

A spec exige: a aula removida na republicação mostra "Esta aula não está disponível" **com a lista da versão nova à mão** (techspec V-04, task 6.0). O 404 `LESSON_NOT_AVAILABLE` não traz estrutura de curso. A implementação preenche a lista com o `course` que viajou no `location.state` do clique, isto é, a lista que a tela anterior já tinha carregado, antes da republicação.

Cenário que falha:
1. O aluno abre a aula A; a tela guarda a lista da versão 1 (com a aula B).
2. O curso é republicado sem a aula B.
3. O aluno clica em B. A API responde 404 `LESSON_NOT_AVAILABLE`.
4. A tela mostra a lista da **versão 1**, ainda com a aula B. O aluno clica de novo e cai no mesmo 404. Nenhum fetch busca a versão vigente.

Em link direto, favorito ou F5 na aula removida, `location.state` é vazio e a lista não aparece. O ramo cai no alerta simples, sem lista.

O teste `shows unavailable lesson alert with current course list...` (`playback-navigation.test.tsx:~215`) não discrimina isso. O mock mantém a aula 2 na lista inicial, o teste clica nela e depois só confere que a lista (a mesma do estado) está na tela, sem checar que a aula removida saiu dela. Mesmo com a lista velha ele passa, então a regra da versão nova fica sem prova. A integração de `learning` prova o 404 e a identidade estável, mas o requisito da lista é do SPA.

Correção esperada: obter a estrutura vigente depois do 404, por exemplo refazendo a consulta de uma aula conhecida da lista, ou outra fonte já prevista no contrato, sem criar endpoint fora do contrato sem decisão. Cobrir também o caso sem `location.state`. Teste novo: depois da republicação a lista exibida não contém a aula removida e traz a ordem e os nomes novos. Corrigir o teste, não enfraquecê-lo.

## Recomendações (não bloqueantes)

1. `student-lesson-screen.tsx`: o ramo `course` duplica o `<nav>` da lista de aulas do ramo de sucesso. Extrair um componente de lista evita divergência.
2. "Pronto quando" nº 2 e nº 3 pedem verificação no navegador. O validador não a executou nesta chamada (sem app local no escopo desta revisão); a evidência ficou nos testes de componente, e o implementer deve registrá-la ao corrigir B1.
3. Mudanças em `.agents/skills/tsg-flow-orchestrator/scripts/routing.default.json` estão na árvore e fora do escopo da task; não entram no checkpoint da 6.0.

## O que foi conferido e está correto

- Integração: identidade estável da aula após renomear e reordenar (mesma `lessonId`, nova posição e título, `versionNumber` 2); aula removida devolve 404 `LESSON_NOT_AVAILABLE` sem consultar Matrícula e sem vazar título; aluno do curso A recebe 403 `ACCESS_DENIED` no curso B; republicar não altera o direito.
- Velocidade: 0,5x, 1x, 1,25x, 1,5x e 2x no próprio player, efeito imediato em `playbackRate`, reaplicada em `loadedmetadata`, menu acessível por teclado (Radix) com nome acessível "Velocidade de reprodução", reinício em 1x ao trocar de aula.
- Troca de aula: nova sessão por aula e `hls.destroy` da anterior; aula 5 abre sem ver as 2, 3 e 4.

## Resultado da tentativa 1

VALIDAÇÃO REPROVADA — 1 bloqueante (B1), 3 recomendações.

---

# Revalidação — Tentativa 2/3

Run: run.Lm6O8Vsr

- Modo: revalidation · HEAD: 9cc8c611c099141606a569526e10778c0d450632 · árvore: fd41491b1a56191f4982a229212e0ea92ae40eb3 (HEAD e `git status --porcelain` iguais ao início da revisão; nenhum arquivo editado pelo validador).
- Tudo em primeiro plano e em sequência, sem monitor nem background.
- Mudança nova: `student-lesson-nav.tsx` (lista extraída), `known-course-store.ts` (guarda de aulas conhecidas), `student-lesson-screen.tsx` (consulta de recuperação após 404) e 2 testes novos em `playback-navigation.test.tsx`.

## Gate (exit code é o veredito)

| Comando | Exit | Resultado |
|---|---|---|
| `dotnet test` Learning.IntegrationTests `--filter-class ...PublishedLessonRepublicationTests --minimum-expected-tests 4` | 0 | 4 de 4 |
| `npm --prefix src/student-spa run test -- playback-navigation` | 0 | 8 de 8 |

`gate_expect` (≥ 10: 4 + 8 = 12) atendido.

## Verificações do projeto

| Comando | Exit |
|---|---|
| `dotnet format ... --verify-no-changes` | 0 |
| `dotnet build src/learning/CodeForCoders.Learning.slnx` | 0 (0 warnings, 0 errors) |
| `dotnet test` ArchitectureTests | 0 (9 de 9) |
| `npm run lint` | 0 |
| `npm run typecheck` | 0 |
| `npm run test --` (suíte inteira) | 0 (19 arquivos, 117 testes) |
| `npm run build -- --base=/student/` | 0 |

## Bloqueio anterior

**B1 — resolvido.** Ao receber 404 `LESSON_NOT_AVAILABLE`, a tela refaz a consulta de uma aula irmã conhecida (`location.state` ou `known-course-store`) e exibe a estrutura vigente devolvida por essa consulta, em vez da lista guardada no clique. Usa só o endpoint já contratado. Os testes agora discriminam:
- `playback-navigation.test.tsx:226`: após a republicação, a lista tem a ordem e os nomes novos e **não contém** a aula removida (`queryByRole(... /Persistência/)` ausente); nenhuma aula marcada como atual.
- `playback-navigation.test.tsx:294`: mesmo cenário sem `location.state` (link direto/F5), usando o histórico guardado.
- `playback-navigation.test.tsx:347`: sem histórico, cai no alerta simples com "Ver cursos" e sem lista.

Recomendação 1 (nav duplicado) também foi atendida pela extração de `StudentLessonNav`.

## Recomendações (não bloqueantes)

1. `student-lesson-screen.tsx` (candidato de recuperação): só uma aula irmã é tentada. Se ela também tiver sido removida na republicação, a tela cai no alerta simples sem lista. Tentar a próxima irmã conhecida cobriria republicações que removem várias aulas.
2. `known-course-store.ts`: grava ids de aula em `localStorage` além de `sessionStorage`; em computador compartilhado o histórico sobrevive à sessão. Só há ids, sem dado sensível, mas `sessionStorage` bastaria. Também há `candidateLessonId!` com asserção não nula protegida por `enabled`; um `skipToken` evitaria a asserção.
3. "Pronto quando" nº 2 e nº 3 pedem verificação no navegador; o validador não a executou (sem app local no escopo desta chamada). A evidência está nos testes de componente e de integração; o integrador/QA deve registrar a verificação manual.
4. `routing.default.json` segue modificado na árvore, fora do escopo da task; não entra no checkpoint da 6.0.

## Resultado

VALIDAÇÃO APROVADA — 0 bloqueantes, 4 recomendações.
