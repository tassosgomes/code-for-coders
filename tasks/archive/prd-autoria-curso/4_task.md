---
status: done
task_kind: vertical
blocked_by: ["1.0", "3.0"]
gate: "dotnet test --project src/learning/tests/CodeForCoders.Learning.IntegrationTests/CodeForCoders.Learning.IntegrationTests.csproj -- --filter-class CodeForCoders.Learning.IntegrationTests.VideoReadyProjectionTests --minimum-expected-tests 6"
gate_expect: "6 testes de projeção e vínculo de vídeo pronto passam; menos de 6 reprova"
---

# 4.0 Professor vincula apenas vídeo pronto da escola

**Fatia:** V-03 · **Cobre:** RF-04; US-02; RN-C04, RN-M02/04/05/15; OD35 ·
**Spec:** [V-03](techspec.md#v-03-professor-escolhe-vídeo-pronto-da-escola) ·
**ADR:** [ADR-0006](../../../docs/adr/0006-preparacao-de-video-e-custodia-de-chave.md)

## Comportamento

No editor, o professor abre o seletor do desenho aprovado e encontra **somente vídeos prontos
da escola**, com título atual, duração e autor, inclusive vídeo enviado por colega. Escolher,
trocar ou desvincular atualiza a aula do rascunho sem mudar seu `lessonId`. Vídeo recebido, em
preparação, falho, inexistente ou de outro tenant é recusado por Learning com
`VIDEO_NOT_AVAILABLE` e não altera o vínculo anterior. Um vídeo recém-pronto passa a ser
vinculável quando seu fato chega; se a visão local ainda não o reconhece, a UI explica o atraso
e permite atualizar/tentar novamente sem perder a aula.

Learning mantém visão própria por tenant de `midia.ativo-pronto.v1` e
`midia.preparacao-falhou.v1`, com fila durável, DLQ e deduplicação por `eventId`. Antes de liberar
a área, Media — dona do outbox — reproduz os fatos históricos retidos com ID e payload originais;
Learning reconcilia por tenant os vídeos `ready` de Media com IDs prontos projetados. Nada lê o
banco de outro serviço. Se faltarem fatos históricos e a reconciliação não fechar, esta fatia não
libera autoria nesse ambiente até a solução ser acordada com Mídia; a fila temporária de
auditoria não é fonte de replay.

O BFF usa `listVideos?status=ready` para o seletor e `getVideo` para enriquecer a leitura do
curso, com limite de concorrência e tempo; falha de Media mantém 200 de `getCourse`, com apenas
`videoId` na aula. A seleção mostra erro e retry sem inserir vídeo de estado desconhecido. Testar
consumidor e replay com broker real controlado, e o cliente HTTP real do BFF contra Media
controlada; host com DI real inicia com as filas declaradas. Smoke com vídeos prontos, em
preparação e falhos de dois tenants e com Media indisponível durante a leitura.

## Fora do escopo desta task

Publicação e Referências de Uso (5.0), player/pré-visualização (CAP-007) e envio de vídeo (CAP-006).

## Decisões fechadas

- OD35/C-05: a visão local decide o vínculo; título/duração atuais vêm de Media apenas na
  leitura, sem cópia para Learning. A falha de enriquecimento não invalida um vínculo.
- O seletor usa o contrato de CAP-006 e a permissão `midia.enviar`; nenhuma promessa de proteção
  contra cópia nem player aparece no editor.
- Ordem de rollout: declarar fila antes do replay, aplicar migration EF, reconciliar e só então
  liberar a autoria. Reentrega do mesmo fato é sem efeito.

## Modificar / Referenciar

- **modificar:** `src/learning/src/CodeForCoders.Learning.Infra.Data/LearningDbContext.cs`,
  `src/learning/src/CodeForCoders.Learning.Infra.Messaging/RabbitMqTopologyInitializer.cs`,
  `src/learning/src/CodeForCoders.Learning.Infra.Messaging/Configuration/RabbitMqOptions.cs`,
  `src/learning/src/CodeForCoders.Learning.Api/Extensions/ServiceConfigurationExtensions.cs`.
- **modificar:** `src/media/src/CodeForCoders.Media.Infra.Messaging/OutboxPublisherWorker.cs`
  (replay controlado dos fatos retidos, preservando identidade e payload).
- **modificar:** `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/ServiceConfigurationExtensions.cs`,
  `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/EndpointExtensions.cs`,
  `docker-compose.yml`, `docker-compose.coolify.yml` (filas, credenciais e endereço Media).
- **ref:** [PRD](prd.md), [OpenAPI público](api-contract.yaml),
  [contrato de vídeos CAP-006](../prd-ingestao-midia/api-contract.yaml),
  [AsyncAPI Learning](asyncapi-contract.yaml),
  [Figma aprovado em 1.0](../../../docs/design/wireframes-autoria-curso.md),
  `src/media/src/CodeForCoders.Media.Application/UseCases/Videos/PrepareVideo/PrepareVideo.cs`,
  `src/media/src/CodeForCoders.Media.Api/Endpoints/VideoEndpoints.cs`,
  `.agents/skills/dotnet/SKILL.md`, `.agents/skills/react/SKILL.md`.

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| Learning | `dotnet restore src/learning/CodeForCoders.Learning.slnx` | exit 0 | `.github/workflows/learning.yml` → `ci-dotnet.yml@v1` |
| Learning | `dotnet format src/learning/CodeForCoders.Learning.slnx --verify-no-changes` | exit 0 | mesmo CI, lint |
| Learning | `dotnet test --project src/learning/tests/CodeForCoders.Learning.IntegrationTests/CodeForCoders.Learning.IntegrationTests.csproj -- --filter-class CodeForCoders.Learning.IntegrationTests.VideoReadyProjectionTests --minimum-expected-tests 6` | exit 0, ≥ 6 testes; broker real | mesmo CI, testes; gate focalizado |
| Learning | `dotnet publish src/learning/CodeForCoders.Learning.slnx -c Release` | exit 0 | mesmo CI, publish |
| Media | `dotnet restore src/media/CodeForCoders.Media.slnx` | exit 0 | `.github/workflows/media.yml` → `ci-dotnet.yml@v1` |
| Media | `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | exit 0 | mesmo CI, lint |
| Media | `dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.VideoFactReplayTests --minimum-expected-tests 3` | exit 0, ≥ 3 testes; IDs e payload preservados | mesmo CI, testes |
| Media | `dotnet publish src/media/CodeForCoders.Media.slnx -c Release` | exit 0 | mesmo CI, publish |
| BFF | `dotnet restore src/bff-admin/CodeForCoders.BffAdmin.slnx` | exit 0 | `.github/workflows/bff-admin.yml` → `ci-dotnet.yml@v1` |
| BFF | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | mesmo CI, lint |
| BFF | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj -- --filter-class CodeForCoders.BffAdmin.IntegrationTests.CourseVideoEnrichmentTests --minimum-expected-tests 4` | exit 0, ≥ 4 testes; HTTP real e DI | mesmo CI, testes |
| BFF | `dotnet publish src/bff-admin/CodeForCoders.BffAdmin.slnx -c Release` | exit 0 | mesmo CI, publish |
| SPA | `npm --prefix src/admin-spa run lint` | exit 0 | `.github/workflows/admin-spa.yml` → `ci-react-ts.yml@v1` |
| SPA | `npm --prefix src/admin-spa run typecheck` | exit 0 | mesmo CI, Typecheck |
| SPA | `npm --prefix src/admin-spa test -- authoring-video-picker` | exit 0; arquivo selecionado cobre seleção/erro/retry | mesmo CI, Vitest |
| SPA | `npm --prefix src/admin-spa run build -- --base=/admin/` | exit 0 | mesmo CI, Build |

## Pronto quando

- [ ] Gate focalizado passa (exit 0) com ao menos 6 testes de Learning.
- [ ] Só vídeos `ready` do tenant entram no seletor e no vínculo; inválido, falho, em preparação
  ou outro tenant retorna `VIDEO_NOT_AVAILABLE` sem mutação.
- [ ] Fato novo e replay histórico tornam vídeo pronto vinculável; reentrega não duplica; a
  reconciliação por tenant fecha antes do rollout.
- [ ] Media indisponível não derruba `getCourse`; aula conserva `videoId`, e o seletor oferece
  retry. Cliente HTTP real e host com DI real são exercitados em testes.
