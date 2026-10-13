---
status: done
task_kind: vertical
blocked_by: ["2.0", "4.0"]
gate: 'dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.LessonTranscriptionTests --minimum-expected-tests 11 && dotnet test --project src/bff-student/tests/CodeForCoders.BffStudent.EndToEndTests/CodeForCoders.BffStudent.EndToEndTests.csproj -- --filter-class CodeForCoders.BffStudent.EndToEndTests.LessonTranscriptionTests --minimum-expected-tests 5 && npm --prefix src/student-spa run test -- lesson-transcription'
gate_expect: "Pelo menos 16 testes .NET passam (11 de integração de media e 5 E2E do bff-student) e os testes do student-spa com o fragmento lesson-transcription passam"
---

# 10.0 O aluno lê, pesquisa e clica na transcrição da aula

**Fatia:** V-06 · **Cobre:** RF-04, RF-05, RF-06 (texto), RN-M21, RN-M22, RN-TL01, RN-TL02, RN-TL03, RN-TL05 · **Spec:** `techspec.md#v-06-aluno-lê-pesquisa-e-clica-no-texto-da-aula` · **ADR:** 0003, 0013, 0020

## Comportamento

**`media` — `getLessonTranscriptionInternal` e `bff-student` — `GET` da transcrição da aula
(`getLessonTranscription`):** na ordem de RN-M10, **404** quando a aula não é Referência de Uso
(antes de consultar Matrícula) → **403** `ACCESS_DENIED` sem direito → **503** sem resposta de Matrícula
(falha fechada, nunca "sem acesso") → estado. Depois da decisão positiva, vídeo sem vigente (não pronto,
em andamento, falhou, desligada) responde **200 `unavailable`**; com vigente, **200 `ready`** com os trechos.
Reutiliza `FindLessonAsync` e `IAccessDecisionClient` como `OpenPlaybackSession` (decisão reaproveitável
por até 30 s). Política de reprodução existente (`PlaybackPolicyName`). `Cache-Control: private,
no-store`; a resposta não contém e-mail nem dado pessoal; o vídeo nunca é informado ao aluno sem direito
(aula do curso B pedida por quem só tem o A → 404/403 da aula). A transcrição servida é a do vídeo
vinculado na versão vigente que o aluno vê (RN-TL03): aula com vídeo trocado na publicação seguinte
mostra o texto do vídeo novo. O `bff-student` repassa corpo, `Content-Type` e `Cache-Control`
(limite de 8 MiB).

**`student-spa`** (página da aula, desenho aprovado em 2.0):

- **Um `GET` por abertura da página**, em paralelo a `getStudentLesson` e à abertura da sessão, sem cache
  entre navegações (`staleTime` e `gcTime` 0) e **sem armazenamento persistente** do texto. `unavailable`
  mostra a mensagem neutra do PRD (RN-TL05); 403 e 503 seguem as mensagens que a aula já usa; 401 segue
  o fluxo de sessão.
- **Painel** com todos os trechos e o tempo de cada um, trecho atual destacado e acompanhamento
  automático que pausa quando o aluno rola e volta com "ir para o trecho atual".
- **Busca no cliente** (a consulta nunca vai ao servidor): normaliza cada trecho uma vez (minúsculas,
  sem acentos, sem pontuação); "introducao" encontra "Introdução"; busca "contém"; destaque da
  ocorrência mapeado de volta ao texto original; contagem de ocorrências; anterior/próxima circular;
  texto só com espaços = busca vazia. Renderização como nós de texto do React (nunca HTML). 3.000
  trechos sem atraso perceptível.
- **Clique no trecho** leva o vídeo ao início dele e toca (RN-TL02): o player expõe ao painel o tempo
  atual e o comando "ir ao tempo e tocar"; sem sessão ativa ou com sessão vencida, o clique define o
  ponto inicial e usa o caminho normal de abertura/retomada, sem reabrir a aula.

**Contratos:** acrescentar `getLessonTranscription` / `getLessonTranscriptionInternal` e seus schemas a
`contracts/bff-student/openapi-aula.yaml` e `contracts/media/openapi-internal.yaml`.

## Fora do escopo desta task

- Legenda no player e seu controle: 11.0 (aqui só o painel e o comando de tempo no player).
- Busca em várias aulas: fora do PRD.

## Decisões fechadas

- 403 `ACCESS_DENIED` para quem não tem direito: "quem não tem direito à aula não tem a transcrição" (decisão do responsável, Q-01 do contrato, errata do PRD 1.1).
- Busca e destaque no cliente (D-09). Nenhum texto de aula em cache persistente, telemetria ou URL.
- O player e o hook de reprodução são compartilhados com progresso e renovação: os testes `playback-*.test.tsx` são mantidos e estendidos **antes** de alterar o hook (TechSpec, Riscos).

## Modificar / Referenciar

- **modificar:** `src/media/src/CodeForCoders.Media.Api/Endpoints/PlaybackSessionEndpoints.cs`; `src/media/src/CodeForCoders.Media.Application/DependencyInjection.cs`; `src/bff-student/src/CodeForCoders.BffStudent.Api/Endpoints/PlaybackSessionEndpoints.cs` (ou `StudentLessonEndpoints.cs`, o melhor lugar); `src/bff-student/src/CodeForCoders.BffStudent.Api/Clients`; `src/student-spa/src/features/student-lessons/components/student-lesson-screen.tsx`, `protected-video-player.tsx`; `src/student-spa/src/features/student-lessons/hooks/use-protected-playback.ts`; `src/student-spa/src/testing/handlers.ts`, `student-lesson-data.ts`; `contracts/bff-student/openapi-aula.yaml`; `contracts/media/openapi-internal.yaml`
- **ref:** `docs/design/wireframes-transcricao.md`; `src/media/src/CodeForCoders.Media.Application/UseCases/PlaybackSessions/OpenPlaybackSession/OpenPlaybackSession.cs:14-35` (ordem 404, Matrícula, resposta); `src/bff-student/src/CodeForCoders.BffStudent.Api/Endpoints/PlaybackSessionEndpoints.cs:45-60` (repasse); `src/student-spa/src/features/student-lessons/api/get-student-lesson.ts` (sem cache entre navegações); `src/student-spa/src/app/playback-*.test.tsx`; `api-contract-student.yaml`; skills `dotnet` e `react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/media` | `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.ArchitectureTests/CodeForCoders.Media.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.PlaybackSessionOpenTests --minimum-expected-tests 1` | exit 0 (regressão intacta) | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.PlaybackRenewalTests --minimum-expected-tests 1` | exit 0 (regressão intacta) | `ci-dotnet.yml` Testes |
| `src/bff-student` | `dotnet format src/bff-student/CodeForCoders.BffStudent.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-student` | `dotnet test --project src/bff-student/tests/CodeForCoders.BffStudent.ArchitectureTests/CodeForCoders.BffStudent.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/student-spa` | `npm --prefix src/student-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run test -- playback student-lesson-screen` | exit 0 (telas e fluxos existentes intactos) | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run build` | exit 0 | `ci-react-ts.yml` (build) |
| Contratos | `npx --yes @stoplight/spectral-cli lint contracts/bff-student/openapi-aula.yaml contracts/media/openapi-internal.yaml --ruleset contracts/.spectral-exception.yaml --fail-severity=error` | exit 0 | `contracts/README.md` |

## Pronto quando

- [ ] Gate focalizado passa (exit 0).
- [ ] Visitante → 401; sem direito → 403; sem resposta de Matrícula → 503; aula fora das Referências de Uso → 404 antes de consultar Matrícula; em nenhum desses casos há texto na resposta.
- [ ] Em andamento, falha ou desligada → 200 `unavailable` com a mensagem neutra, e o player continua normal.
- [ ] A busca por "introducao" acha "Introdução"; texto só com espaços não busca; anterior/próxima é circular; 3.000 trechos sem atraso perceptível.
- [ ] O clique num trecho leva o vídeo àquele tempo e toca, inclusive antes de a sessão estar ativa.
- [ ] Os testes `playback-*.test.tsx` seguem passando.
