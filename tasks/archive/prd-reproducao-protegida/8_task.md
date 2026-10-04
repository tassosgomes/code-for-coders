---
status: done
task_kind: vertical
blocked_by: ["4.0"]
gate: 'dotnet test --project src/media/tests/CodeForCoders.Media.UnitTests/CodeForCoders.Media.UnitTests.csproj -- --filter-class CodeForCoders.Media.UnitTests.PlaybackTelemetryTests --minimum-expected-tests 5 && dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.PlaybackSignalsTests --minimum-expected-tests 4 && npm --prefix src/student-spa run test -- playback-telemetry && python3 scripts/kibana/import_observabilidade.py --verify-only'
gate_expect: "Pelo menos 11 testes passam (5 unitários e 4 de integração de media, 2 do SPA) e a verificação do painel sai com exit 0"
---

# 8.0 A equipe vê custo, erros e cache da entrega

**Fatia:** V-06 · **Cobre:** RF-08, RN-M17, US da equipe de plataforma · **Spec:** `techspec.md#v-06` · **ADR:** [ADR-0008](../../docs/adr/0008-observabilidade-kibana-como-codigo.md)

## Comportamento

A equipe abre o painel e vê como a entrega está indo, sem abrir banco e sem nenhum dado pessoal.

- **`media` emite:** sessões abertas e recusadas **por motivo** (negada, indisponível, referência ausente, vídeo não pronto, sem
  e-mail), latência e falha da consulta de decisão, e o **número de alunos ativos nos últimos 30 dias** (alunos distintos com sessão
  aberta), que junto do armazenamento já medido compõe o custo por aluno ativo. Nomes na convenção `{servico}.{agregado}.{evento}`.
- **`student-spa` emite:** um intervalo "tempo até o vídeo começar" (da abertura da aula ao primeiro quadro) pelo OpenTelemetry que já
  usa, observado **sem alvo** (A1/OD2 em aberto).
- **Nenhum atributo, rótulo ou dimensão leva e-mail, nome, título de curso ou de aula, ou identificador de aluno.**
- **Painel e alerta no Kibana como código (ADR-0008):** sessões por desfecho, decisões indisponíveis, tempo até começar (mediana e
  p95), alunos ativos e custo por aluno; e o alerta de taxa de acerto de cache com limiar inicial de **85% em 15 minutos**, para
  recalibrar com dado real. A **fonte** da taxa de acerto é a distribuição, entregue pela plataforma: a task define o alerta como
  código e registra que, sem a fonte, ele não dispara em desenvolvimento.

Casos negativos que a task prova: a busca do e-mail de teste na telemetria exportada de `media` e do SPA não encontra nada; um
desfecho recusado incrementa a contagem com o **motivo** certo e sem dado pessoal; a verificação do NDJSON do painel recusa
instrumento ausente.

## Fora do escopo desta task

- Ingerir métricas ou logs da distribuição no Kibana, e a taxa de acerto em desenvolvimento → plataforma (Questões em Aberto da TechSpec).
- Alvo do SLO de início de reprodução → A1/OD2. Medição de sobreposição de aulas distintas → QA-01 do PRD.

## Decisões fechadas

- Painel e alerta só no Kibana, sem canal externo (ADR-0008). Aplicação garante **emissão**; coleta e roteamento são da plataforma.
- Sem dado pessoal em nenhum sinal (G10, G23). Limiar de cache inicial 85%/15 min (`techspec.md`, Questões em Aberto).

## Modificar / Referenciar

Arquivos a criar não são listados: a estrutura vem das skills `dotnet` e `react`.

- **modificar:** `src/media/src/CodeForCoders.Media.Application/Common/MediaTelemetry.cs` (instrumentos); os casos de uso de sessão de 4.0 e de decisão (emissão)
- **modificar:** `src/student-spa/src/lib/telemetry.ts` e o player de 4.0 (intervalo de início)
- **modificar:** `scripts/kibana/import_observabilidade.py` e `scripts/kibana/observabilidade-midia.ndjson` (painel e alerta de reprodução; instrumentos como contrato)
- **ref:** `tasks/prd-observabilidade-midia/` (padrão do painel e das regras A1 a A5); `src/media/src/CodeForCoders.Media.Infra.Messaging/MediaVolumeMetricsWorker.cs` (armazenamento já medido)

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/media` | `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/media` | `dotnet build src/media/CodeForCoders.Media.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.ArchitectureTests/CodeForCoders.Media.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/student-spa` | `npm --prefix src/student-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run test --` | exit 0 (suíte inteira, sem regressão) | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run build -- --base=/student/` | exit 0 | `ci-react-ts.yml` (`build-args: --base=/student/`) |
| Kibana | `python3 scripts/kibana/import_observabilidade.py --verify-only` | exit 0 (sem rede) | ADR-0008 |

Cobertura agregada ≥ 70%, `dotnet publish` e imagens ficam para a validação full. Testcontainers pesados rodam **em sequência**, nunca em paralelo (AGENTS.md).

## Pronto quando

- [ ] Gate focalizado passa (exit 0), com os mínimos do `gate`.
- [ ] Importado o painel no Kibana de desenvolvimento (`--import`), uma reprodução de teste aparece nas sessões por desfecho e no tempo até começar.
- [ ] A busca do e-mail de teste na telemetria exportada de `media` e do SPA não encontra nada.
