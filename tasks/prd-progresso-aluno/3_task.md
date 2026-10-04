---
status: in_progress
task_kind: vertical
blocked_by: []
gate: 'dotnet test --project src/learning/tests/CodeForCoders.Learning.IntegrationTests/CodeForCoders.Learning.IntegrationTests.csproj -- --filter-class CodeForCoders.Learning.IntegrationTests.PlaybackProgressConsumerTests --minimum-expected-tests 11 && dotnet test --project src/learning/tests/CodeForCoders.Learning.IntegrationTests/CodeForCoders.Learning.IntegrationTests.csproj -- --filter-class CodeForCoders.Learning.IntegrationTests.VideoDurationProjectionTests --minimum-expected-tests 4 && dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.ProgressFactPublicationTests --minimum-expected-tests 3 && python3 scripts/kibana/import_observabilidade.py --verify-only'
gate_expect: "Pelo menos 18 testes passam (11 e 4 de integração de learning, 3 de integração de media) e a verificação do painel sai com exit 0"
---

# 3.0 O avanço da reprodução vira progresso por aula, sem perder o que foi retido

**Fatia:** V-01 · **Cobre:** RF-01, RF-02, RN-P01 a RN-P06, US de dois aparelhos, US da escola preservar o avanço (OD71) · **Spec:** `techspec.md#v-01-o-avanço-da-reprodução-vira-progresso-por-aula-sem-perder-o-que-foi-retido` · **ADR:** [ADR-0008](../../docs/adr/0008-observabilidade-kibana-como-codigo.md)

## Comportamento

`learning` passa a consumir `midia.reproducao-avancou.v1` (operação `receberReproducaoAvancou`, `asyncapi-contract.yaml`) e a guardar a duração dos vídeos (`receberAtivoPronto` revisada). `media` deixa de reter o avanço, e os avanços retidos desde `CAP-007` chegam pelo reenvio que já existe.

- **Fila.** `learning` declara `learning.playback-progress` (quorum, DLQ, limite de entregas), ligada a `media.events` pela rota `midia.reproducao-avancou.v1`. O nome é o mesmo esperado pelo reenvio de `media` (`OutboxOptions.ProgressFactReplayQueue`).
- **Esquema `progress`** (migration pela ferramenta do EF): avanço bruto por `event_id`, progresso por (escola, aluno, aula) e duração por (escola, vídeo), conforme `techspec.md#bloco-backend`.
- **Consumo, por fato, numa transação:** (1) insere o avanço bruto; se o `event_id` já existe, confirma e para (RN-P01). (2) A última posição só muda se (`occurred_at`, `sequence`) for maior que o par guardado (RN-P02); fato atrasado não faz andar para trás. (3) Atualiza a maior posição vista e a última atividade. (4) Conclui (`completed_at`) se `reason` é `ended` ou a maior posição ≥ 90% da duração do vídeo que a aula usa na **versão vigente** (RN-P04); conclusão nunca se desfaz (RN-P05). (5) `reason` desconhecido é guardado e tratado como `heartbeat`. (6) Aula fora da versão vigente, curso ou aula desconhecidos: registrados, nada criado em Content. Payload inválido vai à fila de erro; falha transitória volta.
- **Duração.** A recepção de `midia.ativo-pronto.v1` grava a duração no schema `progress` **fora** do bloco do recibo de Content, por upsert idempotente com (`occurred_at`, `event_id`) maior (RN-P06). Na mesma transação, aulas da versão vigente que usam aquele vídeo, com maior posição ≥ 90% e ainda não concluídas, ficam concluídas. Content continua sem duração.
- **`media`.** `midia.reproducao-avancou.v1` sai de `Outbox:RetainedRoutingKeys`: o fato passa a ser publicado ao vivo. Ordem de virada, que o cenário prova: fila de `learning` existe → retenção desligada → reenvio único (`Outbox__ProgressFactReplayTenantId`).
- **Sinais.** Contadores de fatos de avanço consumidos e mandados à fila de erro, e histograma de atraso (agora − `occurredAt`), sem identificador de aluno nos atributos; logs com `eventId`, nunca o payload. Painel e alerta de atraso p95 > 60 s gerados pelo `scripts/kibana/import_observabilidade.py` (o NDJSON é gerado, não editado à mão).

Casos que os testes provam: duplicado → um registro e progresso igual; fora de ordem (14:10/480 chegando antes de 14:02/252) → 480; dois aparelhos (10:00/300 e 11:00/120) → 120; aula de 600 s → 540 conclui, 530 não; `ended` conclui em qualquer posição; avanço antes da duração e depois o fato de vídeo → conclui sem novo avanço; rever e parar no minuto 2 → continua concluída, última posição 120; `reason` novo → não conclui; aula fora da versão vigente → registrada; o avanço bruto guarda só os campos do fato; payload recebido conforme `contracts/media/asyncapi.yaml` (helper de `src/contract-testing/`). Em `media`: com a retenção desligada e a fila de teste ligada o fato é publicado; o reenvio publica as linhas retidas com o mesmo `eventId`; com a retenção ainda ligada nada sai.

## Fora do escopo desta task

- Leituras do aluno (percentual, retomada, meus cursos) → 4.0 a 6.0.
- Prazo de guarda do avanço bruto → QA-01 do PRD.
- Preencher a duração de vídeos antigos: o reenvio de fatos de vídeo (`Outbox__VideoFactReplayTenantId`) é operação opcional do ambiente, não critério desta task.

## Decisões fechadas

- Avanço bruto é o recibo do consumidor e o insumo de `CAP-029` (`techspec.md`, Decisões Técnicas; OD71).
- Duração vem de `midia.ativo-pronto.v1`, nunca estimada (C-07); conclusão 90% ou fim (DP-01).
- `media` não ganha código: só configuração e ordem (`techspec.md#bloco-backend`).
- Progress lê a versão vigente pela interface de leitura do módulo Content, nunca pelas tabelas de `content`.

## Modificar / Referenciar

Arquivos a criar não são listados: a estrutura vem da skill `dotnet`.

- **modificar:** `src/learning/src/CodeForCoders.Learning.Infra.Messaging/RabbitMqTopologyInitializer.cs`, `Configuration/RabbitMqOptions.cs`, `VideoProjectionStore.cs`, `DependencyInjection.cs`; `src/learning/src/CodeForCoders.Learning.Infra.Data/LearningDbContext.cs` (entidades no schema `progress`, migration do EF)
- **modificar:** `src/media/src/CodeForCoders.Media.Api/appsettings.json` (`Outbox:RetainedRoutingKeys`)
- **modificar:** `scripts/kibana/import_observabilidade.py` e o NDJSON que ele gera (painel e alerta do progresso)
- **ref:** `src/learning/src/CodeForCoders.Learning.Infra.Messaging/VideoProjectionConsumerWorker.cs` (padrão de consumidor); `src/media/src/CodeForCoders.Media.Infra.Messaging/OutboxPublisherWorker.cs:134` e `src/media/src/CodeForCoders.Media.Api/Extensions/VideoReplayOperationsExtensions.cs` (reenvio); `src/media/tests/CodeForCoders.Media.IntegrationTests/RetainedOutboxFactTests.cs` (padrão de teste do reenvio); `src/media/src/CodeForCoders.Media.Infra.Messaging/RabbitMqPublisher.cs:43` (publicação `mandatory`); `src/contract-testing/AsyncApiContract.cs`; `asyncapi-contract.yaml`, `asyncapi-contract-media.yaml`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/learning` | `dotnet format src/learning/CodeForCoders.Learning.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/learning` | `dotnet build src/learning/CodeForCoders.Learning.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/learning` | `dotnet test --project src/learning/tests/CodeForCoders.Learning.ArchitectureTests/CodeForCoders.Learning.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/media` | `dotnet build src/media/CodeForCoders.Media.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.RetainedOutboxFactTests` | exit 0 (o comportamento de retenção de `CAP-007` continua testado com a configuração explícita) | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.PlaybackProgressTests` | exit 0 (sem a retenção no `appsettings`, os testes de avanço de `CAP-007` não podem depender de não haver publicação; ajustar a configuração do teste, nunca o comportamento) | `ci-dotnet.yml` Testes |
| Kibana | `python3 scripts/kibana/import_observabilidade.py --verify-only` | exit 0 | ADR-0008 |

Cobertura agregada ≥ 70%, `dotnet publish` e imagens ficam para a validação full. Testcontainers pesados rodam **em sequência** (AGENTS.md).

## Pronto quando

- [x] Gate focalizado passa (exit 0).
- [x] A aplicação `learning` inicia com os registros reais (fila, consumidor, DbContext) no ambiente de teste.
- [x] No ambiente de desenvolvimento (`scripts/remote-infra.sh migrate`, `scripts/apps.sh start --remote`): com avanços retidos de `CAP-007`, implantar `learning`, desligar a retenção e rodar o reenvio → a contagem de avanços brutos é igual à de linhas do outbox de `media` daquela rota; repetir o reenvio não muda nada.
- [x] Um avanço com `event_id` repetido não altera o progresso; um avanço fora de ordem não faz a posição voltar.
