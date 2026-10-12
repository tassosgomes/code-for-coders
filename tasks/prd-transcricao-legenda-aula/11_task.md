---
status: pending
task_kind: vertical
blocked_by: ["2.0", "10.0"]
gate: 'dotnet test --project src/media/tests/CodeForCoders.Media.UnitTests/CodeForCoders.Media.UnitTests.csproj -- --filter-class CodeForCoders.Media.UnitTests.WebVttBuilderTests --minimum-expected-tests 9 && dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.PlaybackCaptionsTests --minimum-expected-tests 8 && dotnet test --project src/bff-student/tests/CodeForCoders.BffStudent.EndToEndTests/CodeForCoders.BffStudent.EndToEndTests.csproj -- --filter-class CodeForCoders.BffStudent.EndToEndTests.PlaybackCaptionsTests --minimum-expected-tests 4 && npm --prefix src/student-spa run test -- playback-captions'
gate_expect: "Pelo menos 21 testes .NET passam (9 unitários do gerador e 8 de integração em media, 4 E2E do bff-student) e os testes do student-spa com o fragmento playback-captions passam"
---

# 11.0 O aluno liga a legenda no player

**Fatia:** V-07 · **Cobre:** RF-03, RF-06 (legenda), RN-M21, RN-TL04, RN-TL05 · **Spec:** `techspec.md#v-07-aluno-liga-a-legenda-no-player` · **ADR:** 0003, 0013, 0020

## Comportamento

**`media` — `getPlaybackCaptionsInternal` e `bff-student` — `getPlaybackCaptions`:** devolve o WebVTT da
sessão de reprodução válida, `Content-Type: text/vtt; charset=utf-8`, `Cache-Control: private, no-store`.
Não consulta Matrícula de novo. Sessão desconhecida ou de outro aluno/escola → 404; vencida → 410
(como `GetPlaybackResource`); sem vigente → 404 `CAPTIONS_NOT_AVAILABLE`. O `bff-student` preserva
`Content-Type` e `Cache-Control`.

**Gerador de WebVTT** (função pura da camada de aplicação, por requisição, sem cache, D-07): cabeçalho
`WEBVTT`, um cue por trecho; escapa `&`, `<`, `>`; remove `-->` e quebras que formariam linha em branco;
tempos `hh:mm:ss.mmm`; trecho acima de 84 caracteres é dividido em cues de até 2 linhas de ~42
caracteres, com tempos proporcionais aos caracteres; o fim nunca passa do início do cue seguinte (sem
sobreposição); duração mínima de 500 ms; cobre emoji, cue de 3 minutos e texto com quebras de linha.

**`student-spa`** (desenho aprovado em 2.0, incluindo a decisão de faixa nativa ou camada própria):

- O **controle de legenda** só existe se a transcrição está `ready`. Ao ligar, a faixa de texto do
  elemento de vídeo é alimentada pelo WebVTT buscado com a sessão autenticada do aluno (corpo em
  memória, sem endereço persistente).
- Legenda sincronizada com pausa, avanço, retrocesso e velocidade; em tela cheia, sem cobrir
  controles nem a marca d'água.
- A **renovação** da sessão não faz a legenda piscar; **negada na renovação**, a legenda para junto com a
  reprodução (`stop()`), sem texto novo.
- **Preferência** ligada/desligada em `localStorage` do dispositivo (padrão desligada; leitura e
  escrita em `try/catch`; sem dado do aluno): vale nas próximas aulas do mesmo dispositivo e não vai a
  outro (RN-TL04).

**Contratos:** acrescentar `getPlaybackCaptions` / `getPlaybackCaptionsInternal` e a exceção
`text/vtt` em `contracts/.spectral-exception.yaml`; a linha C-08 ficará para a consolidação (14.0).

## Fora do escopo desta task

- Verificação manual em tela cheia nos navegadores da matriz do player entra no checkpoint em dev, não no gate automatizado.
- Painel e busca: 10.0.

## Decisões fechadas

- WebVTT por sessão (decisão do responsável, Q-02); sem texto em URL estável.
- Preferência de legenda é do dispositivo e não é dado do domínio (RN-TL04).
- Sem transcrição pronta não há controle e o player funciona como hoje (RN-TL05).
- A legenda para quando a reprodução para por sessão não renovada.

## Modificar / Referenciar

- **modificar:** `src/media/src/CodeForCoders.Media.Api/Endpoints/PlaybackSessionEndpoints.cs`; `src/media/src/CodeForCoders.Media.Application/DependencyInjection.cs`; `src/bff-student/src/CodeForCoders.BffStudent.Api/Endpoints/PlaybackSessionEndpoints.cs`; `src/student-spa/src/features/student-lessons/components/protected-video-player.tsx`; `src/student-spa/src/features/student-lessons/hooks/use-protected-playback.ts`; `src/student-spa/src/testing/playback-simulator.ts`; `contracts/bff-student/openapi-aula.yaml`; `contracts/media/openapi-internal.yaml`; `contracts/.spectral-exception.yaml`
- **ref:** `docs/design/wireframes-transcricao.md`; `src/media/src/CodeForCoders.Media.Application/UseCases/PlaybackSessions/GetPlaybackResource/GetPlaybackResource.cs:13-17` (sessão desconhecida e vencida); `src/student-spa/src/app/playback-renewal.test.tsx`; `api-contract-student.yaml`; skills `dotnet` e `react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/media` | `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.ArchitectureTests/CodeForCoders.Media.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.PlaybackRenewalTests --minimum-expected-tests 1` | exit 0 (regressão intacta) | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.LessonTranscriptionTests --minimum-expected-tests 1` | exit 0 (regressão intacta) | `ci-dotnet.yml` Testes |
| `src/bff-student` | `dotnet format src/bff-student/CodeForCoders.BffStudent.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-student` | `dotnet test --project src/bff-student/tests/CodeForCoders.BffStudent.ArchitectureTests/CodeForCoders.BffStudent.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/student-spa` | `npm --prefix src/student-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run test -- playback student-lesson-screen` | exit 0 (telas e fluxos existentes intactos) | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run build` | exit 0 | `ci-react-ts.yml` (build) |
| Contratos | `npx --yes @stoplight/spectral-cli lint contracts/bff-student/openapi-aula.yaml contracts/media/openapi-internal.yaml --ruleset contracts/.spectral-exception.yaml --fail-severity=error` | exit 0 | `contracts/README.md` |

## Pronto quando

- [ ] Gate focalizado passa (exit 0).
- [ ] Sessão de outro aluno → 404; vencida → 410; sem transcrição → 404 `CAPTIONS_NOT_AVAILABLE`; `text/vtt` com `Cache-Control: private, no-store`.
- [ ] O gerador escapa `&`, `<`, `>`, remove `-->`, divide cue longo e não sobrepõe cues.
- [ ] Ligar a legenda mostra o texto sincronizado; a renovação não pisca; renovação negada para a legenda.
- [ ] A preferência persiste no mesmo dispositivo e o controle some sem transcrição pronta.
- [ ] Em dev, verificação manual em tela cheia nos navegadores da matriz do player.
