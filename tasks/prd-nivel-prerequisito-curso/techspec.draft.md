---
tsg_artifact: techspec
product: code-4-coders
capability: CAP-005
version: 1.0
status: in_review
updated: 2026-09-30
sources: tasks/prd-nivel-prerequisito-curso/prd.md@1.0, tasks/prd-nivel-prerequisito-curso/contracts.md@1.0, context/architecture-baseline.md@1.2
---

# TechSpec — nível e pré-requisito do curso

> **Escopo:** Full-stack (`learning`, `bff-admin`, `admin-spa`)
> **Modo:** Pipeline, API-First
> **PRD de origem:** [prd.md](prd.md), v1.0, aprovado em 2026-09-30
> **Contratos:** [contracts.md](contracts.md) 1.0; [OpenAPI público](api-contract.yaml) 1.1.0, [OpenAPI interno de learning](internal-api-contract-learning.yaml) 1.1.0 e [AsyncAPI de learning](asyncapi-contract.yaml) 1.1.0, aprovados em 2026-09-30
> **Data:** 2026-09-30
> **Status:** Em Revisão
> **Handoff:** draft — não gerar Tasks

## Resumo Executivo

O rascunho do curso em `learning` ganha nível e pré-requisito (texto e até cinco cursos recomendados), editados por `updateCourse` como qualquer outro campo do rascunho. A publicação copia os dois para o retrato imutável da versão, com o título que cada recomendado tinha na versão vigente dele, e o fato `conteudo.versao-publicada.v1` passa ao formato 1.1.0, com descrição, nível e pré-requisito sempre presentes. `bff-admin` repassa os campos novos e o filtro por título; `admin-spa` acrescenta ao editor a seção "Para quem é este curso", o aviso de curso sem nível e o nível/pré-requisito na página da versão.

A carga inicial do Catálogo (C-01, OD56) é um **reenvio único**, feito por `learning`, do fato da versão vigente de cada curso publicado, com o `eventId` original. Como o `MessageId` enviado ao broker hoje é o `Id` da linha do outbox, e a linha original continua no banco, o outbox de `content` passa a separar a identidade da linha da identidade da mensagem.

O desenho herda o [baseline](../../context/architecture-baseline.md), a [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md) e a [TechSpec de autoria](../prd-autoria-curso/techspec.md) (bloqueio por curso, idempotência, retrato de versão, impressão de conteúdo, outbox de `content`). Não há ADR nova.

**Trade-off primário:** nível e pré-requisito seguem a regra do currículo — só valem depois de publicar. Ganha-se uma regra única (o público vê só o que foi publicado) e histórico por versão; em troca, mudar só o nível exige uma republicação, com versão nova e ato na trilha, e os cursos já publicados só entram na vitrine depois que o professor declarar o nível e republicar.

## Arquitetura da Solução

```text
professor → admin-spa (/admin/autoria/{courseId}) → bff-admin → learning (rascunho, versão, outbox content)
learning outbox content → learning.events: conteudo.versao-publicada.v1 1.1.0 → media (sem mudança) · commerce (CAP-003)
learning (uma vez, por configuração) → reenvio da versão vigente de cada curso → mesmo exchange, mesmo eventId
```

### Bloco Backend

**Rascunho.** `Course` recebe `Level` (um de `beginner`, `intermediate`, `advanced`, ou nulo), `PrerequisiteText` (nulo ou 1–1 000 caracteres) e `RecommendedCourseIds` (lista ordenada, 0–5, sem repetição). A alteração entra pelo caminho existente de `updateCourse` (`CourseEditSession`: bloqueio do curso, recibo de idempotência, `RecordEdit`), com a mesma semântica de campo ausente (não muda), `null` (limpa) e lista inteira substituída. O parser de `learning` passa a aceitar `level`, `prerequisiteText` e `recommendedCourseIds` só no `PATCH` do curso; nível fora da lista, texto acima do limite ou mais de cinco ids → `FIELD_INVALID`/`INVALID_REQUEST`, conforme o contrato.

**Validação dos recomendados.** Dentro da mesma transação e do mesmo tenant (filtro global), cada id precisa existir, ter versão vigente e ser diferente do próprio curso; senão 422 `RECOMMENDED_COURSE_INVALID` e nada muda. Não há revalidação na publicação: no MVP um curso publicado não é excluído nem despublicado (OD45), então um recomendado válido continua válido. Recomendação mútua (A→B e B→A) é aceita.

**Impressão de conteúdo e revisão.** `CourseContentFingerprint` passa a incluir nível, texto do pré-requisito e a lista ordenada de ids recomendados, tanto a partir do curso quanto da versão; a mudança só de nível já torna `hasUnpublishedChanges` verdadeiro e incrementa `draftRevision` pelo `RecordEdit` existente (C-05 do contrato). Como a fórmula muda, as impressões gravadas em `published_fingerprint` deixam de ser comparáveis: a migration zera a coluna, e o caminho que já existe em `CourseEditSession` (`EnsurePublishedFingerprint` quando a impressão é nula) a recalcula da versão vigente na próxima edição. O valor persistido de `has_unpublished_changes` não é tocado pela migration e continua correto até a próxima edição.

**Publicação.** `Course.Publish` copia nível e pré-requisito para `CourseVersion`. O retrato guarda o texto e, para cada recomendado, `{courseId, title}` com o **título da versão vigente** do recomendado naquele momento (o que o público vê), lido na mesma transação. `DiscardDraft` restaura nível e pré-requisito da versão vigente, como já faz com título e descrição; versões anteriores a esta entrega restauram nível nulo e pré-requisito vazio.

**Fato 1.1.0.** O payload montado em `PublishCourse` acrescenta `description` (texto vazio quando nula), `level` (nulo quando ausente) e `prerequisite` (`text` nulo e `recommendedCourses` vazio quando ausentes). Os nulos precisam ser serializados explicitamente; a opção `JsonSerializerDefaults.Web` usada hoje não omite nulos, e isso passa a ser verificado. `eventId` continua sendo o `Id` da versão. Ato de publicação sem mudança.

**Identidade da mensagem no outbox de `content`.** Hoje o `MessageId` publicado é o `Id` da linha (`RabbitMqPublisher`), e o fato original usa o `Id` da versão como `Id` da linha; linhas processadas não são apagadas. Um reenvio com o mesmo `eventId` colidiria na chave primária. A tabela `content.outbox_messages` ganha a coluna `message_id` (preenchida com `id` nas linhas existentes pela migration e por padrão nas novas); o publicador envia `message_id` como `MessageId`. O outbox de `progress` não muda.

**Reenvio único (C-01).** Uma rotina de `learning`, desligada por padrão e habilitada por configuração (`CatalogInitialLoad:Enabled`), roda uma vez no processo que já hospeda o `OutboxPublisherWorker`. Para cada curso com versão vigente, em transação curta sob o bloqueio do curso, grava no outbox de `content` uma linha nova com `message_id` = `Id` da versão e o fato 1.1.0 montado **a partir do retrato da versão vigente** (nunca do rascunho, nunca da linha antiga). Um registro de execução no schema `content` com a chave `catalogo-carga-inicial` marca a conclusão; se já existir, a rotina não faz nada. Versões anteriores a esta entrega saem com `level: null` e pré-requisito vazio. Não gera ato de auditoria. Uma publicação concorrente é segura: os consumidores aplicam a maior `versionNumber`. **Ordem operacional:** habilitar só depois que a fila do Catálogo em `commerce` (CAP-003) estiver declarada e ligada a `learning.events`; antes disso, o reenvio chegaria só à Media.

**Busca por título.** `listCourses` aceita `title` (2–100 caracteres), comparado sem diferenciar maiúsculas nem acentos contra uma coluna normalizada `title_search`, mantida pelo domínio em criação e alteração do título e preenchida pela migration para os cursos existentes com `lower` + `translate` dos acentos do português. Não depende da extensão `unaccent`, cuja disponibilidade no banco remoto não foi verificada. O termo é escapado para `LIKE`. `currentLevel` na lista vem da versão vigente; para não ler o JSON das versões por item, `courses` guarda `current_level`, atualizado na publicação e no descarte.

**Dados pessoais.** Nenhum dado pessoal novo. Texto do pré-requisito e descrição são texto do autor: persistidos em `learning` e backups, copiados para o outbox, broker e filas de erro do fato (C-03 aprovado), nunca para log, span, métrica ou erro.

### Bloco Frontend

O editor (`http://localhost:8081/admin/autoria/{courseId}` no Compose; `https://c4c-admin.lab.tasso.dev.br/admin/autoria/{courseId}` no Coolify de desenvolvimento) ganha a seção **Para quem é este curso**, ao lado de título e descrição: nível como grupo de opções (três níveis e "Sem nível"), texto do pré-requisito e cursos recomendados com seletor. O seletor chama `listCourses` com `status=published` e `title`, exclui o próprio curso na tela, permite reordenar por teclado e limita a cinco. A gravação usa `updateCourse` com a chave de idempotência da intenção, como as demais mutações do editor; a resposta integral substitui o curso exibido, e `422 RECOMMENDED_COURSE_INVALID`/`FIELD_INVALID` viram mensagem no campo.

O aviso de RF-04 lê `currentLevel` e `level` da resposta: sem nível na versão vigente → aviso permanente com atalho para o campo; nível só no rascunho → "o nível só vale depois de publicar". A janela de publicação repete o aviso e mantém o botão habilitado. A lista (`/admin/autoria`) mostra em texto "Sem nível" para `currentLevel` nulo em curso publicado. A página da versão (`/admin/autoria/{courseId}/versoes/{versionNumber}`) mostra nível e pré-requisito com os títulos da época (RF-05); o histórico em lista não muda, porque `listCourseVersions` não mudou no contrato.

Os schemas zod do `admin-spa` são `.strict()`: um SPA antigo recusaria as respostas novas. `bff-admin` e `admin-spa` são implantados juntos (ver Riscos). A linguagem apresenta nível e pré-requisito como recomendação, nunca como condição de compra. Os visuais seguem o ciclo wireframe ASCII → Figma → aprovação sobre [wireframes-autoria-curso.md](../../docs/design/wireframes-autoria-curso.md) antes do código de tela (EN-01).

## Mapa de Fatias Verticais

### V-01: professor declara o nível e vê o aviso de curso sem nível

- **Cobre:** RF-01, RF-04; RN-C05, RN-C10, RN-C18; C-05 do contrato; DP-01.
- **Entrada / gatilho:** escolher nível ou "Sem nível" na seção do editor; abrir editor, lista ou janela de publicação.
- **Processamento:** `learning` valida o valor, grava no rascunho sob o bloqueio do curso, recalcula a impressão incluindo o nível e atualiza `draftRevision`/`hasUnpublishedChanges`; migration zera `published_fingerprint` e cria `level`, `current_level`; `getCourse`/`listCourses` devolvem `level` e `currentLevel`; `bff-admin` repassa; SPA mostra a seção, o aviso e o indicador na lista.
- **Saída observável:** rascunho com nível; curso publicado passa a "alterações não publicadas" sem mudar a versão vigente; aviso e indicador "Sem nível"; leitor sem `autoria.editar` vê o nível sem controle.
- **Evidência / checkpoint:** testes de `learning` (domínio e integração com Testcontainers) para valor válido, inválido, `null`, idempotência e impressão; curso publicado antes da migration editado e revertido volta a `hasUnpublishedChanges: false`; teste do SPA com MSW para aviso, indicador e leitor; `updateCourseInternal` direto com JWT sem `autoria.editar` → 403.
- **Bloqueado por:** EN-01 (só a parte de tela).

### V-02: professor recomenda pré-requisito em texto e cursos da escola

- **Cobre:** RF-02; RN-C17, RN-C18; RN-O05 (sem gate); C-04 do contrato; DP-02.
- **Entrada / gatilho:** escrever o texto, buscar curso no seletor, adicionar, reordenar e remover recomendados; salvar.
- **Processamento:** `listCourses` filtra por `title_search`; `learning` valida limites e cada recomendado (tenant, versão vigente, diferente do próprio); grava texto e lista ordenada; impressão inclui os dois.
- **Saída observável:** rascunho com texto e recomendados na ordem; seletor só mostra publicados da escola, sem o próprio curso; sexto recomendado não é oferecido; erro legível para recomendado inválido.
- **Evidência / checkpoint:** integração de `learning` para recomendado de outro tenant, nunca publicado, inexistente, o próprio curso, 6 ids e recomendação mútua; busca "fundamentos" encontra "Fundamentos de C#" e "fundaméntos" também; teste do SPA para seletor, limite, teclado e erro.
- **Bloqueado por:** V-01.

### V-03: publicação leva nível e pré-requisito à versão, ao histórico e ao fato

- **Cobre:** RF-03, RF-05, RF-06; RN-C06, RN-C07, RN-C11, RN-C13, RN-C18; C-02, C-03 do contrato.
- **Entrada / gatilho:** publicar ou republicar (inclusive quando só o nível mudou); descartar alterações; abrir a página da versão.
- **Processamento:** `Course.Publish` copia nível, texto e recomendados com o título da versão vigente de cada um; grava `current_level`; `PublishCourse` monta o fato 1.1.0 com `description`, `level` e `prerequisite` explícitos; descarte restaura os três da versão vigente.
- **Saída observável:** versão nova com nível e pré-requisito; página da versão mostra os dois e o título da época; fato no broker conforme `VersaoPublicadaPayload` 1.1.0; Media aplica referências como antes; publicar sem nível publica com aviso.
- **Evidência / checkpoint:** integração de `learning` capturando o payload do outbox e validando contra o schema do AsyncAPI 1.1.0; republicar só o nível gera versão e ato; recomendado renomeado depois aparece com o título antigo na versão; ambiente Compose com `media` consumindo fato 1.1.0 sem erro nem DLQ.
- **Bloqueado por:** V-02.

### V-04: carga inicial do Catálogo pelo reenvio da versão vigente

- **Cobre:** RF-06 (via C-01, OD56); compromisso `reenviarVersaoVigente` do AsyncAPI.
- **Entrada / gatilho:** `CatalogInitialLoad:Enabled=true` na implantação, depois que a fila do Catálogo existir.
- **Processamento:** migration cria `message_id` no outbox de `content` (backfill = `id`) e o registro de execução; publicador passa a enviar `message_id`; a rotina grava uma linha nova por curso publicado com `message_id` = `Id` da versão e o fato 1.1.0 do retrato vigente; marca a execução.
- **Saída observável:** uma mensagem por curso publicado em `learning.events`, com `MessageId` e `eventId` iguais ao original; Media confirma sem alterar Referências de Uso; segunda execução não gera nada.
- **Evidência / checkpoint:** integração com RabbitMQ (Testcontainers) e uma fila de teste ligada a `conteudo.versao-publicada.v1`: N cursos publicados → N mensagens, `MessageId` = `eventId` = `Id` da versão, payload conforme 1.1.0; flag desligada → nada; segunda execução → nada; Media no Compose confirma sem mudança.
- **Bloqueado por:** V-03.

### Habilitadores inevitáveis

| Habilitador | Por que não cabe numa fatia | Menor escopo | Primeira fatia desbloqueada |
|---|---|---|---|
| EN-01 | Ciclo de design exigido antes de código de tela: wireframe ASCII e depois Figma da seção "Para quem é este curso", do aviso, do indicador na lista e da página da versão, aprovados pelo responsável | Adendo em `docs/design/wireframes-autoria-curso.md` com registro de aprovação no cabeçalho | V-01 (parte de tela) |

## Contratos e Fronteiras

Schemas, parâmetros e respostas vivem nos três documentos aprovados de [contracts.md](contracts.md).

### Mapeamento de HTTP

| operationId público → interno | Responsabilidade | Fatia |
|---|---|---|
| `listCourses` → `listCoursesInternal` | BFF valida `title` (2–100) e repassa codificado; `learning` filtra por `title_search` e devolve `currentLevel` | V-01, V-02 |
| `getCourse` → `getCourseInternal` | `level`, `prerequisite` (recomendados com título atual do rascunho de cada um), `currentLevel`; BFF acrescenta os campos aos tipos que hoje descartam campo desconhecido | V-01, V-02 |
| `updateCourse` → `updateCourseInternal` | parser aceita os três campos novos; validação de recomendados em `learning` | V-01, V-02 |
| `getCourseVersion` → `getCourseVersionInternal` | nível e pré-requisito do retrato | V-03 |

**Validações além do contrato:**

| operationId | Regra | Camada |
|---|---|---|
| `updateCourseInternal` | recomendado da escola, com versão vigente, diferente do próprio curso | application (consulta) + domain (limites) |
| `updateCourseInternal` | lista sem repetição e na ordem enviada | domain |

**Exceção → resposta HTTP:**

| Exceção | HTTP | code do contrato |
|---|---|---|
| `CourseRuleException("RECOMMENDED_COURSE_INVALID")` | 422 | `RECOMMENDED_COURSE_INVALID` |
| `CourseRuleException("FIELD_INVALID")` | 422 | `FIELD_INVALID` |

### Mapeamento de mensagens e dados

| Contrato e identificador | Aplicação/produtor e consumidores | Comportamento a implementar | Evidência |
|---|---|---|---|
| `publicarVersaoPublicada` · `conteudo.versao-publicada.v1` 1.1.0 | `learning` (send); Media, Catálogo | payload com `description`, `level`, `prerequisite` sempre presentes; `eventId` = versão | V-03 |
| `reenviarVersaoVigente` | `learning` (send) | uma linha nova por curso publicado, `message_id` = `eventId` original, fato do retrato vigente; execução única | V-04 |

### Mapeamento de jornada

| História | Tela / componente | operationId | Evidência |
|---|---|---|---|
| Declarar nível | Editor, seção "Para quem é este curso" | `updateCourse` | V-01 |
| Recomendar pré-requisito | Editor, texto e seletor | `listCourses`, `updateCourse` | V-02 |
| Saber que sem nível não entra na vitrine | Editor, janela de publicação, lista | `getCourse`, `listCourses` | V-01 |
| Ver nível e pré-requisito de cada versão | Página da versão | `getCourseVersion` | V-03 |

### Entidades do domínio

| Entidade do Domain Doc | Representação técnica | Local |
|---|---|---|
| Curso (nível, pré-requisito declarado) | colunas `level`, `prerequisite_text`, `recommended_course_ids` (jsonb, lista ordenada), `title_search`, `current_level` em `content.courses` | `learning`, schema `content` |
| Versão de Publicação | colunas `level` e `prerequisite` (jsonb com texto e `{courseId, title}`) em `content.course_versions` | idem |

## Arquivos a Modificar e a Referenciar

### A modificar

| Caminho | Fatia | Alteração |
|---|---|---|
| `src/learning/src/CodeForCoders.Learning.Domain/Entities/Course.cs` | V-01–V-03 | campos novos, `Update`, `Publish`, `DiscardDraft`, `title_search`, `current_level` |
| `src/learning/src/CodeForCoders.Learning.Domain/Entities/CourseChanges.cs` | V-01, V-02 | campos com presença explícita (ausente × nulo) |
| `src/learning/src/CodeForCoders.Learning.Domain/Entities/CourseContentFingerprint.cs` | V-01 | incluir nível e pré-requisito |
| `src/learning/src/CodeForCoders.Learning.Domain/Entities/CourseVersion.cs` | V-03 | nível e pré-requisito no retrato |
| `src/learning/src/CodeForCoders.Learning.Api/Endpoints/CourseChangesRequest.cs` | V-01, V-02 | aceitar e validar os três campos no `PATCH` do curso |
| `src/learning/src/CodeForCoders.Learning.Api/Endpoints/CourseEndpoints.cs` | V-02 | parâmetro `title` na lista |
| `src/learning/src/CodeForCoders.Learning.Application/UseCases/Courses/UpdateCourse/UpdateCourse.cs` | V-02 | validação dos recomendados |
| `src/learning/src/CodeForCoders.Learning.Application/UseCases/Courses/PublishCourse/PublishCourse.cs` | V-03 | títulos dos recomendados; payload 1.1.0 |
| `src/learning/src/CodeForCoders.Learning.Application/UseCases/Courses/Common/CourseDetailOutput.cs`, `CourseVersionOutput.cs` | V-01, V-03 | campos novos |
| `src/learning/src/CodeForCoders.Learning.Application/Interfaces/CourseSummary.cs`, `CourseListQuery.cs` | V-01, V-02 | `currentLevel`, `title` |
| `src/learning/src/CodeForCoders.Learning.Infra.Data/Queries/CourseQueries.cs` | V-02 | filtro por `title_search` |
| `src/learning/src/CodeForCoders.Learning.Infra.Data/Configurations/CourseConfiguration.cs`, `CourseVersionConfiguration.cs` | V-01–V-03 | colunas novas |
| `src/learning/src/CodeForCoders.Learning.Infra.Data/Outbox/ContentOutboxMessage.cs`, `ContentOutboxMessageConfiguration.cs` | V-04 | `message_id` |
| `src/learning/src/CodeForCoders.Learning.Infra.Messaging/RabbitMqPublisher.cs` | V-04 | `MessageId` a partir de `message_id` no outbox de `content` |
| `src/learning/src/CodeForCoders.Learning.Infra.Messaging/DependencyInjection.cs` | V-04 | registrar a rotina de reenvio e sua opção |
| Migrations de `learning` (geradas por `dotnet ef migrations add`) | V-01–V-04 | colunas; zerar `published_fingerprint`; backfill de `title_search` e `message_id` |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Application/Interfaces/CourseDetail.cs`, `CourseSummary.cs`, `CourseVersion.cs` | V-01–V-03 | campos novos (hoje descartados na desserialização) |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/CourseAuthoringEndpoints.cs` | V-02 | validar e repassar `title` codificado |
| `src/admin-spa/src/features/course-authoring/types/course.ts`, `types/course-version.ts` | V-01–V-03 | schemas com os campos novos |
| `src/admin-spa/src/features/course-authoring/api/update-course.ts`, `api/get-courses.ts` | V-01, V-02 | entrada e filtro |
| `src/admin-spa/src/features/course-authoring/components/course-curriculum.tsx`, `course-publication.tsx` | V-01, V-02 | seção, aviso e aviso na publicação |
| `src/admin-spa/src/app/routes/authoring-courses-route.tsx`, `authoring-version-route.tsx` | V-01, V-03 | indicador na lista; nível e pré-requisito na versão |
| `src/admin-spa/src/testing/authoring-course-handlers.ts`, `authoring-version-handlers.ts` | V-01–V-03 | handlers MSW com os campos novos |
| `docs/design/wireframes-autoria-curso.md` | EN-01 | adendo e aprovação |

### A referenciar (não alterar)

| Caminho | Por que consultar |
|---|---|
| `src/learning/src/CodeForCoders.Learning.Application/UseCases/Courses/Common/CourseEditSession.cs` | bloqueio, recibo de idempotência, recálculo preguiçoso da impressão (linhas 50–54) |
| `src/learning/src/CodeForCoders.Learning.Infra.Messaging/OutboxPublisherWorker.cs` | seleção por schema e `FOR UPDATE SKIP LOCKED`; o reenvio usa o mesmo caminho de entrega |
| `src/media/src/CodeForCoders.Media.Infra.Messaging/PublishedCourseFact.cs` | exige `MessageId` = `eventId` e ignora campos novos |
| `tasks/prd-autoria-curso/techspec.md` | decisões herdadas de rascunho, versão, outbox e URLs |

## Análise de Impacto

| Componente | Tipo | Impacto e risco | Ação requerida |
|---|---|---|---|
| `media` (consumidor do fato) | inalterado | recebe fato 1.1.0 e o reenvio; ignora campos novos; reenvio chega com versão já aplicada | verificar no Compose que não há DLQ |
| `commerce` (CAP-003) | futuro consumidor | depende do fato 1.1.0 e do reenvio | habilitar o reenvio só depois do consumidor dele |
| `content.outbox_messages` | modificado | coluna nova; publicador muda a origem do `MessageId` | migration com backfill antes do publicador novo |
| `content.courses` / `course_versions` | modificado | colunas novas; `published_fingerprint` zerado | migration única, aplicada antes do código novo |
| `bff-admin` + `admin-spa` | modificado | schemas estritos do SPA | implantar juntos |

**Ordem de implantação:** migrations de `learning` → `learning` 1.1.0 → `bff-admin` e `admin-spa` juntos → (CAP-003) `commerce` com o consumidor do fato → habilitar `CatalogInitialLoad:Enabled`.

## Riscos e Preocupações

| Preocupação | Local (`arquivo:linha`) | Impacto | Mitigação |
|---|---|---|---|
| Reenvio com o mesmo `eventId` colide com a linha original do outbox | `src/learning/src/CodeForCoders.Learning.Infra.Messaging/RabbitMqPublisher.cs:23` (`MessageId = message.Id`); `PublishCourse.cs:69` (linha usa `version.Id`) | chave primária duplicada, ou Media recusando `MessageId` diferente do `eventId` | coluna `message_id` separada da identidade da linha (V-04) |
| Fórmula da impressão muda | `src/learning/src/CodeForCoders.Learning.Domain/Entities/Course.cs:134` | todo curso publicado marcaria "alterações não publicadas" na próxima edição | migration zera `published_fingerprint`; recálculo preguiçoso existente em `CourseEditSession.cs:50` |
| BFF descarta campos que não conhece | `src/bff-admin/src/CodeForCoders.BffAdmin.Application/Interfaces/CourseDetail.cs:3` | editor recebe curso sem nível mesmo com `learning` certo | acrescentar campos aos três tipos (V-01, V-03) |
| SPA recusa campos a mais | `src/admin-spa/src/features/course-authoring/types/course.ts:4` (`.strict()`) | SPA antigo com BFF novo quebra o editor | implantar BFF e SPA juntos; teste de contrato com o exemplo do OpenAPI |
| Parser do `PATCH` recusa propriedade desconhecida | `src/learning/src/CodeForCoders.Learning.Api/Endpoints/CourseChangesRequest.cs:12` | campos novos dariam 400 | incluir os três só na lista permitida do curso |
| Busca sem acento depende de extensão não verificada no banco remoto | `src/learning/src/CodeForCoders.Learning.Infra.Data/Queries/CourseQueries.cs:17` | falha de migration no Coolify se usar `unaccent` | coluna normalizada pelo domínio, sem extensão |
| Reenvio antes do consumidor do Catálogo | — (operacional) | Catálogo nasce vazio e o reenvio não se repete (marcador) | flag desligada por padrão; ordem de implantação explícita; se preciso repetir, apagar o marcador é ato operacional registrado |

## Decisões Técnicas

- **`message_id` separado do `id` no outbox de `content`.** Racional: o reenvio precisa do `MessageId` original sem violar a chave da linha, e a Media exige `MessageId` = `eventId`. Trade-off: uma coluna e uma mudança no publicador. Rejeitado: reabrir a linha original trocando o payload (reescreve histórico do outbox) e dar novo `MessageId` ao reenvio (a Media recusaria).
- **Reenvio como rotina única por configuração, com marcador.** Racional: roda na infraestrutura já existente do publicador, sem endpoint novo nem credencial nova, e a ordem em relação ao consumidor do Catálogo fica sob controle da implantação. Trade-off: repetir exige remover o marcador. Rejeitado: endpoint interno (exigiria autorização de operação inexistente) e execução automática na subida (dispararia antes do consumidor).
- **Título do recomendado no retrato = título da versão vigente do recomendado.** Racional: é o título que o público vê. No rascunho, o título mostrado é o do rascunho do recomendado, como diz o contrato. Trade-off: rascunho e versão podem mostrar títulos diferentes para o mesmo recomendado enquanto o professor dele não republicar.
- **Coluna `title_search` normalizada.** Racional: busca sem acento sem depender de `unaccent`. Trade-off: normalização limitada aos acentos do português.
- **`current_level` em `courses`.** Racional: a lista mostra o nível vigente sem ler o JSON das versões por item. Trade-off: valor derivado mantido em publicar e descartar.

## Verificação

- **Cenários críticos não óbvios:** curso publicado antes da migration editado e revertido deve voltar a `hasUnpublishedChanges: false`; republicar só o nível; recomendado renomeado depois da publicação; recomendação mútua; publicação concorrente durante o reenvio (consumidor aplica a maior versão); reenvio com flag ligada duas vezes; `null` explícito no JSON do fato.
- **Dados e ambiente:** Testcontainers de PostgreSQL e RabbitMQ para `learning`; Compose (`./scripts/generate-local-env.sh`, `./scripts/apps.sh start`, migrations por `dotnet ef database update` como na TechSpec de autoria) com `media` consumindo, professor e dois cursos publicados antes da entrega.
- **Verificação dos contratos:** payload capturado do outbox validado contra `VersaoPublicadaPayload` 1.1.0 do [AsyncAPI](asyncapi-contract.yaml); respostas de `getCourse`, `updateCourse`, `listCourses` e `getCourseVersion` conferidas contra os exemplos do [OpenAPI](api-contract.yaml); o lint dos YAMLs, já registrado, não substitui isso.
- **Observabilidade além do padrão:** contagem de mensagens do reenvio e do marcador de conclusão em log estruturado, sem título, texto ou nome.

## Questões em Aberto

- [ ] Errata do PRD, RF-06: "leitura sob demanda" → "carga inicial por reenvio" (OD56) — responsável pelo PRD — não bloqueia; só a letra do PRD fica defasada.
- [ ] Aprovação do adendo de design (EN-01) — responsável pelo produto — bloqueia apenas o código de tela.

## Architecture Decision Records

- [ADR-0001](../../docs/adr/0001-monorepo-de-codigo.md) — serviços continuam com deploy independente; a coordenação é a ordem de implantação acima.
- [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md) — sessão revalidada no BFF, JWT de ator e autorização em `learning`.

Nenhuma ADR nova ou Accepted substituída.
