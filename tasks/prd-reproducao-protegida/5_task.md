---
status: pending
task_kind: vertical
blocked_by: ["4.0"]
gate: 'dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.PlaybackRenewalTests --minimum-expected-tests 8 && npm --prefix src/student-spa run test -- playback-renewal'
gate_expect: "Pelo menos 15 testes passam: 8 de integração de media e 7 do SPA"
---

# 5.0 A reprodução continua sem interrupção e para quando o direito acaba

**Fatia:** V-03 · **Cobre:** RF-02 (renovação, falha fechada, sessão vencida), RN-R01, RN-R02, RN-M10, RN-M11, DP-01, DP-08, DP-09, US de não ser desconectado e de revogação em minutos · **Spec:** `techspec.md#v-03` · **ADR:** [ADR-0012](../../docs/adr/0012-autenticacao-de-servico-media-learning-em-commerce-revisada.md), [ADR-0014](../../docs/adr/0014-entrega-de-segmentos-por-credencial-opaca-e-porta-de-distribuicao.md)

## Comportamento

O aluno assiste por mais de 4 minutos sem ver nada acontecer, e quando o direito termina a reprodução para em até 5 minutos (mais o
trecho que o player já carregou). Operação: `renewPlaybackSession` → `renewPlaybackSessionInternal`.

- **`media`:** a renovação aceita só sessão do aluno do JWT que **ainda não venceu** (vencida → 410 `PLAYBACK_SESSION_EXPIRED`, sem
  consultar `commerce`; desconhecida ou de outro aluno → 404). **Repete a decisão** (o cache de 30 s não ajuda porque a renovação
  ocorre ~4 min depois). Positiva: a **mesma** `sessionId` ganha validade de agora + 5 min e uma `segmentAccess` **nova**, e a
  resposta tem o mesmo formato da abertura. Negada: 403 `ACCESS_DENIED`. Indisponível: 503, e nada se estende. Sem e-mail na claim:
  422. Duas renovações simultâneas da mesma sessão não estendem além de 5 min.
- **`bff-student`:** rota de renovação com `X-CSRF-Token`, audiência `media`, mapeamento de erros conforme o contrato.
- **`student-spa` (player):** **só enquanto o vídeo toca**, a partir de `renewAfter` chama a renovação, adota a `segmentAccess` e a
  validade novas **sem recarregar a playlist nem pausar**; com 503, repete a cada 5 s até `expiresAt`; com 403, para e mostra o estado
  (acesso encerrado ou sem acesso); vencida sem renovar, para com *Tentar de novo*. Em pausa que passou de `expiresAt`, o próximo
  *play* **abre uma sessão nova** e retoma da posição que o player guarda. Nenhuma renovação acontece pausado.

Casos negativos que a task prova: decisão que vira negada no meio (a reprodução para dentro da janela); Matrícula indisponível por
menos de 90 s (a reprodução não é interrompida) e por mais (para com a mensagem de indisponibilidade e *Tentar de novo*); renovar
sessão vencida (410); renovar sessão de outro aluno (404); `commerce` voltando a responder depois de um 503 antes de `expiresAt`.

## Fora do escopo desta task

- A revogação como ato do produto é de `CAP-009` e não existe aqui; o cenário de fim de direito usa o `expires_at` da concessão.
- Avanço (7.0), troca de aula (6.0), sinais (8.0).

## Decisões fechadas

- Validade de 5 min, `renewAfter` 90 s antes, nova decisão a cada renovação, sem tolerância ao término (DP-01, DP-09; `techspec.md` D-03).
- Indisponível ≠ negado, em todas as camadas (RN-R02, DP-08). Sessão vencida não renova (contrato C-05).
- A renovação só roda com o vídeo tocando; pausa longa reabre sessão (`techspec.md#bloco-frontend`).

## Modificar / Referenciar

Arquivos a criar não são listados: a estrutura vem das skills `dotnet` e `react`.

- **modificar:** `src/media/src/CodeForCoders.Media.Api/Extensions/EndpointExtensions.cs` (endpoint de renovação); `Application/UseCases` de sessão (renovação); a porta de distribuição criada em 4.0 é **reusada**, não alterada
- **modificar:** `src/bff-student/src/CodeForCoders.BffStudent.Api/Extensions/EndpointExtensions.cs` (rota de renovação)
- **modificar:** os componentes do player criados em 4.0 em `src/student-spa/src/features/` e `src/testing/` (renovação e relógio simulado)
- **ref:** `api-contract.yaml` e `internal-api-contract-media.yaml` (`renewPlaybackSession*`); `contracts.md` C-05; relógio controlado (`TimeProvider`) usado nos testes de `media`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/media` | `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/media` | `dotnet build src/media/CodeForCoders.Media.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.ArchitectureTests/CodeForCoders.Media.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/bff-student` | `dotnet format src/bff-student/CodeForCoders.BffStudent.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-student` | `dotnet build src/bff-student/CodeForCoders.BffStudent.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/bff-student` | `dotnet test --project src/bff-student/tests/CodeForCoders.BffStudent.ArchitectureTests/CodeForCoders.BffStudent.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/student-spa` | `npm --prefix src/student-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run test --` | exit 0 (suíte inteira, sem regressão) | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run build -- --base=/student/` | exit 0 | `ci-react-ts.yml` (`build-args: --base=/student/`) |

Cobertura agregada ≥ 70%, `dotnet publish` e imagens ficam para a validação full. Testcontainers pesados rodam **em sequência**, nunca em paralelo (AGENTS.md).

## Pronto quando

- [ ] Gate focalizado passa (exit 0), com os mínimos do `gate`.
- [ ] A integração usa **relógio controlado** em `media` e um `commerce` de teste que responde negado, indisponível e volta a responder; o player é testado com sessão e relógio simulados.
- [ ] No ambiente de desenvolvimento, com um aluno assistindo, o `expires_at` da concessão no banco de `commerce` é antecipado por script de apoio do cenário, e a reprodução para dentro da janela com a mensagem do estado.
- [ ] Assistir 20 minutos seguidos não produz pausa nem recarga da playlist.
