---
status: pending
task_kind: vertical
blocked_by: ["5.0"]
gate: 'dotnet test --project src/learning/tests/CodeForCoders.Learning.IntegrationTests/CodeForCoders.Learning.IntegrationTests.csproj -- --filter-class CodeForCoders.Learning.IntegrationTests.CatalogInitialLoadTests --minimum-expected-tests 9'
gate_expect: "Pelo menos 9 testes de integração de learning passam, com PostgreSQL e RabbitMQ reais (Testcontainers)"
---

# 6.0 Reenvio único da versão vigente de cada curso publicado para a carga inicial do Catálogo

**Fatia:** V-04 · **Cobre:** RF-06 (carga inicial, C-01/OD56), US-05, RN-C08, RN-C12 · **Spec:** `techspec.md#v-04-carga-inicial-do-catálogo-pelo-reenvio-da-versão-vigente` · **ADR:** [ADR-0001](../../docs/adr/0001-monorepo-de-codigo.md)

## Comportamento

- **Identidade da mensagem no outbox de `content`:** `content.outbox_messages` ganha `message_id`,
  preenchido com `id` nas linhas existentes pela migration e igual a `id` por padrão nas novas; o
  publicador passa a enviar `message_id` como `MessageId` ao broker. Publicações normais continuam
  saindo com `MessageId` = `eventId` = `Id` da versão; linhas antigas ainda pendentes saem com o mesmo
  `MessageId` de antes. O outbox de `progress` não muda.
- **Rotina de reenvio (`reenviarVersaoVigente`):** desligada por padrão; com
  `CatalogInitialLoad:Enabled=true`, roda uma vez no processo que já hospeda o publicador do outbox.
  Se o registro de execução `catalogo-carga-inicial` já existe no schema `content`, não faz nada.
  Senão, para cada curso com versão vigente, de **todos os tenants** (cada curso sob o próprio tenant),
  em transação curta sob o bloqueio do curso, grava no outbox de `content` uma linha **nova** com
  `message_id` = `Id` da versão vigente e o fato 1.1.0 montado a partir do **retrato da versão
  vigente** — nunca do rascunho, nunca da linha antiga do outbox. Cursos nunca publicados ficam de
  fora. Versões anteriores a esta entrega saem com `level: null` e pré-requisito vazio. Ao final,
  grava o registro de execução. Não gera ato de auditoria.
- **Entrega:** o `OutboxPublisherWorker` existente publica as linhas novas em `learning.events` com a
  routing key de `conteudo.versao-publicada.v1`: uma mensagem por curso publicado, com `MessageId` e
  `eventId` iguais aos do fato original e payload conforme `VersaoPublicadaPayload` 1.1.0.
- **Observabilidade:** log estruturado com a contagem de cursos reenviados e a conclusão do marcador,
  sem título, descrição, texto do pré-requisito ou nome.
- **Migration EF** (gerada por `dotnet ef migrations add`): coluna `message_id` com backfill e tabela
  do registro de execução no schema `content`.
- **Composição:** a rotina e sua opção são registradas no contêiner real; com a flag ausente, a
  aplicação sobe e nada é reenviado.
- **Media sem mudança:** no Compose, a Media recebe o reenvio de versões já aplicadas e as confirma sem
  alterar Referências de Uso, sem DLQ.

## Fora do escopo desta task

Consumidor do Catálogo em `commerce` e sua fila (`CAP-003`). Repetir o reenvio: exige remover o
marcador, ato operacional registrado fora do código. Habilitar a flag em algum ambiente além do
Compose de verificação: só depois que a fila do Catálogo estiver ligada a `learning.events`.

## Decisões fechadas

- `message_id` separado do `id` da linha no outbox de `content` (`techspec.md#decisões-técnicas`); a
  Media exige `MessageId` = `eventId`.
- Reenvio por flag com marcador de execução única, sem endpoint e sem execução automática na subida
  (`techspec.md#decisões-técnicas`); mesmo `eventId` (C-01).
- Publicação concorrente com o reenvio é segura: consumidores aplicam a maior `versionNumber`.
- Ordem de implantação: migrations → `learning` 1.1.0 → `bff-admin` e `admin-spa` → `commerce` (CAP-003) → flag (`techspec.md#análise-de-impacto`).

## Modificar / Referenciar

- **modificar:** `src/learning/src/CodeForCoders.Learning.Infra.Data/Outbox/ContentOutboxMessage.cs`, `ContentOutboxMessageConfiguration.cs`; migration via `dotnet ef migrations add`
- **modificar:** `src/learning/src/CodeForCoders.Learning.Infra.Messaging/RabbitMqPublisher.cs` (`MessageId` a partir de `message_id` no outbox de `content`), `DependencyInjection.cs` (rotina e opção)
- **modificar:** `src/learning/src/CodeForCoders.Learning.Application/UseCases/Courses/PublishCourse/PublishCourse.cs` (montagem do fato 1.1.0 reutilizada a partir do retrato da versão)
- **ref:** `src/learning/src/CodeForCoders.Learning.Infra.Messaging/OutboxPublisherWorker.cs` (seleção por schema e `FOR UPDATE SKIP LOCKED`); `src/learning/src/CodeForCoders.Learning.Application/UseCases/Courses/Common/CourseEditSession.cs` (bloqueio do curso); `src/media/src/CodeForCoders.Media.Infra.Messaging/PublishedCourseFact.cs`; `asyncapi-contract.yaml` (`reenviarVersaoVigente`, exemplo `reenvioSemNivel`); `src/learning/tests/CodeForCoders.Learning.IntegrationTests/LearningIntegrationFixture.cs` (RabbitMQ real); skill `dotnet`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/learning` | `dotnet format src/learning/CodeForCoders.Learning.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/learning` | `dotnet build src/learning/CodeForCoders.Learning.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/learning` | `dotnet test --project src/learning/tests/CodeForCoders.Learning.IntegrationTests/CodeForCoders.Learning.IntegrationTests.csproj` | exit 0 (sem regressão em publicação e projeção de vídeo) | `ci-dotnet.yml` Testes |
| `src/learning` | `dotnet test --project src/learning/tests/CodeForCoders.Learning.EndToEndTests/CodeForCoders.Learning.EndToEndTests.csproj` | exit 0 (aplicação sobe com os registros reais, flag ausente) | `ci-dotnet.yml` Testes |
| `src/learning` | `dotnet test --project src/learning/tests/CodeForCoders.Learning.ArchitectureTests/CodeForCoders.Learning.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |

Cobertura agregada ≥ 70%, `dotnet publish` e imagem ficam para a validação full.

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 9 testes.
- [ ] Com uma fila de teste ligada a `conteudo.versao-publicada.v1`: N cursos publicados em dois tenants → N mensagens, cada uma com `MessageId` = `eventId` = `Id` da versão vigente e payload conforme 1.1.0; curso nunca publicado não gera mensagem.
- [ ] Flag desligada → nenhuma linha nova; segunda execução com a flag ligada → nenhuma linha nova.
- [ ] Rascunho alterado depois da publicação não vaza para o reenvio; versão anterior à entrega sai com `level: null` e pré-requisito vazio.
- [ ] Linha antiga pendente e publicação nova continuam saindo com `MessageId` = `id` da linha.
- [ ] Smoke no Compose: com a flag ligada, cada curso publicado do seed gera uma mensagem; a Media confirma sem alterar Referências de Uso e sem DLQ; reiniciar `learning` não reenvia.
