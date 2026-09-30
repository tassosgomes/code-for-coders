---
status: done
task_kind: vertical
blocked_by: ["1.0", "4.0"]
gate: "dotnet test --project src/learning/tests/CodeForCoders.Learning.IntegrationTests/CodeForCoders.Learning.IntegrationTests.csproj -- --filter-class CodeForCoders.Learning.IntegrationTests.CoursePublicationTests --minimum-expected-tests 10"
gate_expect: "10 testes de completude, atomicidade e publicação passam; menos de 10 reprova"
---

# 5.0 Professor publica versão com ato conforme e Referências de Uso

**Fatia:** V-04 · **Cobre:** RF-06, RF-10, RF-11, RF-12; US-03, US-07;
RN-C06/08/11/12/13, RN-M02/09/10, RN-A02/03/05/06/08/14 ·
**Spec:** [V-04](techspec.md#v-04-professor-publica-um-curso-completo-com-rastro-e-referências) ·
**ADR:** [ADR-0001](../../docs/adr/0001-monorepo-de-codigo.md),
[ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md),
[ADR-0006](../../docs/adr/0006-preparacao-de-video-e-custodia-de-chave.md),
[ADR-0007](../../docs/adr/0007-snapshots-efemeros-da-consulta-de-auditoria.md)

## Comportamento

Professor abre a conferência de publicação do desenho aprovado. Curso sem módulo, módulo sem
aula ou aula sem vídeo recebe `COURSE_INCOMPLETE` com uma pendência por falta, em ordem
pedagógica, focável no editor; nada é publicado. Curso completo aceita nota opcional de até
1 000 caracteres e `draftRevision` exibida. Learning serializa por curso, verifica revisão e
completude, cria **versão 1 imutável e vigente** e grava na mesma transação duas mensagens
distintas no outbox de `content`: `conteudo.versao-publicada.v1` com estrutura completa e ato
`auditoria.ato-praticado.v1` com `fatoId` igual ao `eventId` do fato. Falha antes do commit não
deixa versão ou só uma mensagem. Duplo clique/retry com a mesma `Idempotency-Key` devolve uma
única versão; chave igual com corpo diferente é recusada. `DRAFT_CHANGED` recarrega e exige
nova confirmação, sem publicar edição que o professor não viu.

O publicador de Learning encaminha cada linha ao exchange certo. Media worker consome o fato,
aplica apenas a maior `versionNumber` por curso/tenant e grava Referências de Uso opacas
`(courseId, lessonId, videoId)` da versão vigente; duplicata ou fato antigo não duplica/não
reverte. Audit 1.2.0 aceita o ato `conteudo/versao-publicada`, alvo `curso`, sem motivo, e o
registra conforme. BFF da trilha resolve o título do curso em Learning na lista e no detalhe,
sem exigir `autoria.ler` do administrador; se Learning falha, mantém ID sem título. SPA da
Auditoria mostra “Versão publicada” e alvo curso, com filtro correto. A nota não vira motivo.

O editor mostra a versão, autor e momento após 201, sem prometer que consumidores assíncronos
já concluíram. Testes cobrem falha entre as duas linhas do outbox, duplicação, ordem de fatos,
autorização/tenant, payload sem nome/e-mail, referências sem título e roteamento real pelo
broker. O cliente HTTP real do BFF e o host com DI real são exercitados com upstream controlado.
Smoke no Compose com curso real, Audit conforme e Media referenciada; antes de expor publicar,
implantar Audit 1.2.0 e receptor de Media, ligar filas, aplicar migrations EF e reconciliar a
visão de vídeos. Medir tamanho do fato no limite C-07 contra a configuração real do broker.

## Fora do escopo desta task

Republicar, descartar e consultar versão antiga (6.0); excluir rascunho (7.0); oferta/aluno.

## Decisões fechadas

- C-01 corrige a frase do PRD sobre “Auditoria não muda”: a política e a tela da trilha mudam
  aditivamente. Audit deve ser implantada antes da primeira publicação.
- `Curso` é a transação; sem rede dentro do commit. Outbox de `content` não reutiliza o de
  `progress`. As duas linhas têm IDs próprios e `fatoId` correlaciona o ato (TechSpec, Publicação).
- O fato carrega títulos do currículo, sem descrição/nota/nome; logs, erros e métricas não
  carregam conteúdo livre ou token (C-09 e TechSpec, Dados pessoais).
- Migrations de Learning/Media/Audit são geradas por EF, nunca editadas manualmente.

## Modificar / Referenciar

- **modificar:** `src/learning/src/CodeForCoders.Learning.Infra.Data/LearningDbContext.cs`,
  `src/learning/src/CodeForCoders.Learning.Api/Extensions/EndpointExtensions.cs`,
  `src/learning/src/CodeForCoders.Learning.Infra.Messaging/RabbitMqPublisher.cs`,
  `src/learning/src/CodeForCoders.Learning.Infra.Messaging/RabbitMqTopologyInitializer.cs`,
  `src/learning/src/CodeForCoders.Learning.Infra.Messaging/Configuration/RabbitMqOptions.cs`.
- **modificar:** `src/media/src/CodeForCoders.Media.Infra.Data/MediaDbContext.cs`,
  `src/media/src/CodeForCoders.Media.Infra.Messaging/RabbitMqTopologyInitializer.cs`,
  `src/media/src/CodeForCoders.Media.Infra.Messaging/DependencyInjection.cs`.
- **modificar:** `src/audit/src/CodeForCoders.Audit.Domain/Policies/AdministrativeActPolicy.cs`.
- **modificar:** `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/EndpointExtensions.cs`,
  `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/AuditRecordEndpoints.cs`.
- **modificar:** `src/admin-spa/src/features/audit-trail/components/audit-trail-screen.tsx`,
  `src/admin-spa/src/features/audit-trail/components/audit-record-detail-screen.tsx`,
  `docker-compose.yml`, `docker-compose.coolify.yml` (ordem de deploy, broker e serviços).
- **ref:** [PRD](prd.md), [OpenAPI público](api-contract.yaml),
  [OpenAPI interno](internal-api-contract-learning.yaml),
  [AsyncAPI](asyncapi-contract.yaml), [Media](asyncapi-contract-media.yaml),
  [Audit](asyncapi-contract-audit.yaml),
  [Figma aprovado em 1.0](../../docs/design/wireframes-autoria-curso.md),
  `src/learning/src/CodeForCoders.Learning.Infra.Data/Outbox/OutboxMessageConfiguration.cs`,
  `src/learning/src/CodeForCoders.Learning.Infra.Messaging/OutboxPublisherWorker.cs`,
  `.agents/skills/dotnet/SKILL.md`, `.agents/skills/react/SKILL.md`.

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| Learning | `dotnet restore src/learning/CodeForCoders.Learning.slnx` | exit 0 | `.github/workflows/learning.yml` → `ci-dotnet.yml@v1` |
| Learning | `dotnet format src/learning/CodeForCoders.Learning.slnx --verify-no-changes` | exit 0 | mesmo CI, lint |
| Learning | `dotnet test --project src/learning/tests/CodeForCoders.Learning.IntegrationTests/CodeForCoders.Learning.IntegrationTests.csproj -- --filter-class CodeForCoders.Learning.IntegrationTests.CoursePublicationTests --minimum-expected-tests 10` | exit 0, ≥ 10 testes | mesmo CI, testes; gate focalizado |
| Learning | `dotnet publish src/learning/CodeForCoders.Learning.slnx -c Release` | exit 0 | mesmo CI, publish |
| Media | `dotnet restore src/media/CodeForCoders.Media.slnx` | exit 0 | `.github/workflows/media.yml` → `ci-dotnet.yml@v1` |
| Media | `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | exit 0 | mesmo CI, lint |
| Media | `dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.CourseReferenceTests --minimum-expected-tests 5` | exit 0, ≥ 5 testes; worker/broker reais | mesmo CI, testes |
| Media | `dotnet publish src/media/CodeForCoders.Media.slnx -c Release` | exit 0 | mesmo CI, publish |
| Audit | `dotnet restore src/audit/CodeForCoders.Audit.slnx` | exit 0 | `.github/workflows/audit.yml` → `ci-dotnet.yml@v1` |
| Audit | `dotnet format src/audit/CodeForCoders.Audit.slnx --verify-no-changes` | exit 0 | mesmo CI, lint |
| Audit | `dotnet test --project src/audit/tests/CodeForCoders.Audit.IntegrationTests/CodeForCoders.Audit.IntegrationTests.csproj -- --filter-class CodeForCoders.Audit.IntegrationTests.ContentActPolicyTests --minimum-expected-tests 4` | exit 0, ≥ 4 testes; ato conforme | mesmo CI, testes |
| Audit | `dotnet publish src/audit/CodeForCoders.Audit.slnx -c Release` | exit 0 | mesmo CI, publish |
| BFF | `dotnet restore src/bff-admin/CodeForCoders.BffAdmin.slnx` | exit 0 | `.github/workflows/bff-admin.yml` → `ci-dotnet.yml@v1` |
| BFF | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | mesmo CI, lint |
| BFF | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj -- --filter-class CodeForCoders.BffAdmin.IntegrationTests.CoursePublicationProxyTests --minimum-expected-tests 4` | exit 0, ≥ 4 testes; HTTP real e DI | mesmo CI, testes |
| BFF | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj -- --filter-class CodeForCoders.BffAdmin.IntegrationTests.CourseAuditReferenceTests --minimum-expected-tests 3` | exit 0, ≥ 3 testes; alvo curso em lista e detalhe | mesmo CI, testes |
| BFF | `dotnet publish src/bff-admin/CodeForCoders.BffAdmin.slnx -c Release` | exit 0 | mesmo CI, publish |
| SPA | `npm --prefix src/admin-spa run lint` | exit 0 | `.github/workflows/admin-spa.yml` → `ci-react-ts.yml@v1` |
| SPA | `npm --prefix src/admin-spa run typecheck` | exit 0 | mesmo CI, Typecheck |
| SPA | `npm --prefix src/admin-spa test -- authoring-publish` | exit 0; arquivo selecionado cobre pendências, nota e revisão | mesmo CI, Vitest |
| SPA | `npm --prefix src/admin-spa test -- audit-course-target` | exit 0; arquivo selecionado cobre rótulo e alvo | mesmo CI, Vitest |
| SPA | `npm --prefix src/admin-spa run build -- --base=/admin/` | exit 0 | mesmo CI, Build |

## Pronto quando

- [ ] Gate focalizado passa (exit 0) com ao menos 10 testes de Learning.
- [ ] Incompleto devolve todas as pendências e nenhuma versão/fato/ato; completo cria versão 1
  imutável, fato e ato no mesmo commit; retry da mesma chave não duplica.
- [ ] Media registra apenas IDs da vigente e ignora fato duplicado/antigo; Audit registra um ato
  conforme; a trilha mostra “Versão publicada”, autor, curso e momento na lista e no detalhe.
- [ ] Smoke no Compose alcança editor e Auditoria por URL pública; métricas/lag/DLQ permitem
  observar propagação, sem título/nome/token em log ou métrica, e tamanho máximo do fato é aceito
  pelo broker real.
