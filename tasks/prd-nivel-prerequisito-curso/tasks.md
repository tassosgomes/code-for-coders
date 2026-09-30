# Plano de Implementação — Nível e pré-requisito do curso (CAP-005, 2º PRD)

> **TechSpec de origem:** [techspec.md](techspec.md) 1.0, aprovada em 2026-09-30
> **Escopo:** Full-stack (`learning`, `bff-admin`, `admin-spa`; `media` sem mudança, verificada no Compose)
> **ADRs pertinentes:** [0001](../../docs/adr/0001-monorepo-de-codigo.md), [0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md)
> **Contratos:** [contracts.md](contracts.md) 1.0 — OpenAPI BFF 1.1.0, OpenAPI interno de `learning` 1.1.0, AsyncAPI de `learning` 1.1.0
> **Design:** adendo em [wireframes-autoria-curso.md](../../docs/design/wireframes-autoria-curso.md) (a produzir em 1.0 e 2.0), sobre o design aprovado do 1º PRD
> **Status do plano:** Confirmado para implementação (aprovado pelo responsável em 2026-09-30)

## Visão Geral

Ao final, o professor declara no editor o nível do curso e o pré-requisito (texto e até cinco cursos
publicados da escola, em ordem), é avisado em texto de que curso sem nível não entra na vitrine — sem
ser impedido de publicar —, publica ou republica levando os dois para a versão imutável e os vê na
página de cada versão com os títulos da época. O fato `conteudo.versao-publicada.v1` sai no formato
1.1.0, com descrição, nível e pré-requisito sempre presentes, e uma rotina única, desligada por padrão,
reenvia a versão vigente de cada curso publicado para a carga inicial do Catálogo (`CAP-003`).

O design (ASCII → Figma → aprovação) vem antes de qualquer código, para que a seção nova, os avisos e a
página da versão nasçam no desenho aprovado.

## Fases

### Fase 0 — Design aprovado

1.0 adendo ASCII e 2.0 Figma. Checkpoint: cabeçalho de `wireframes-autoria-curso.md` com a linha
`Adendo nível e pré-requisito` registrando `ASCII e Figma aprovados`.

### Fase 1 — Rascunho

3.0 nível no rascunho com aviso e indicador; 4.0 pré-requisito em texto e cursos recomendados, com
busca por título. Checkpoint: em `http://localhost:8081/admin/autoria/{courseId}` o professor declara
nível e pré-requisito, o curso publicado passa a "alterações não publicadas" e a versão vigente não
muda; a lista mostra "Sem nível".

### Fase 2 — Publicação e Catálogo

5.0 publicação leva os dois à versão, à página da versão e ao fato 1.1.0; 6.0 reenvio único da versão
vigente. Checkpoint: publicar com nível gera versão com nível e fato 1.1.0 aceito pela Media sem DLQ;
com a flag ligada, uma mensagem por curso publicado com o `eventId` original, e nada na segunda vez.

## Mapa de Entrega

| Fatia | Task | Comportamento observável | Gate | Bloqueado por |
|---|---|---|---|---|
| EN-01 (ASCII) | 1.0 | Adendo ASCII aprovado | `rg` da linha do adendo em `wireframes-autoria-curso.md` | Nenhum |
| EN-01 (Figma) | 2.0 | Figma do adendo aprovado e registrado | `rg` de `ASCII e Figma aprovados` na linha do adendo | 1.0 |
| V-01 | 3.0 | Professor declara nível; aviso de sem nível no editor, na publicação e na lista; leitor sem controle | Learning `CourseLevelRuleTests` + `CourseLevelTests` + BFF `CourseLevelProxyTests` + SPA `authoring-course-level` | 2.0 |
| V-02 | 4.0 | Professor recomenda texto e até 5 cursos publicados, buscando por título sem acento; recomendado inválido recusado | Learning `CoursePrerequisiteRuleTests` + `CoursePrerequisiteTests` + BFF `CoursePrerequisiteProxyTests` + SPA `authoring-prerequisite` | 3.0 |
| V-03 | 5.0 | Publicação leva nível e pré-requisito à versão, à página da versão e ao fato 1.1.0 | Learning `CourseLevelPublicationTests` + BFF `CourseVersionLevelProxyTests` + SPA `authoring-version-level` | 4.0 |
| V-04 | 6.0 | Reenvio único da versão vigente com `MessageId` = `eventId` original | Learning `CatalogInitialLoadTests` | 5.0 |

EN-01 da TechSpec vira duas tasks (ASCII e Figma) porque cada aprovação é uma decisão separada do
responsável, e o Figma parte do ASCII aprovado. As quatro fatias seguem a TechSpec uma a uma; cada
fatia de tela atravessa SPA → BFF → `learning` na mesma task.

### Habilitadores

| Enabler | Task | Por que não cabe numa fatia | Desbloqueia |
|---|---|---|---|
| EN-01 (ASCII) | 1.0 | O desenho das telas é decidido pelo responsável antes do código (fluxo de design do backoffice) e vale para as três fatias com tela | V-01 a V-03 |
| EN-01 (Figma) | 2.0 | A aprovação do Figma é decisão do responsável e vale para todas as telas de uma vez | V-01 a V-03 |

Não há habilitador técnico: cada migration nasce na fatia que usa a coluna (`dotnet ef migrations add`),
a coluna `message_id` nasce em V-04, que é a única fatia que precisa dela.

## Tasks

- [ ] 1.0 Adendo ASCII de nível e pré-requisito aprovado
- [ ] 2.0 Adendo de nível e pré-requisito no Figma aprovado
- [ ] 3.0 Professor declara o nível do curso e vê o aviso de curso sem nível
- [ ] 4.0 Professor recomenda o pré-requisito em texto e cursos publicados da escola
- [ ] 5.0 Publicação leva nível e pré-requisito à versão, ao histórico e ao fato 1.1.0
- [ ] 6.0 Reenvio único da versão vigente de cada curso publicado para a carga inicial do Catálogo

## Caminho crítico e lanes

`1.0 → 2.0 → 3.0 → 4.0 → 5.0 → 6.0`, sequencial. 4.0 estende os campos e a impressão de conteúdo de
3.0 e usa `currentLevel`/lista de 3.0; 5.0 copia para a versão o que 3.0 e 4.0 gravam no rascunho; 6.0
monta o fato 1.1.0 de 5.0 a partir do retrato da versão. Todas tocam `Course.cs` e as configurações EF
de `learning`, então não há lane paralela segura.

**Estado intermediário conhecido:** entre 3.0 e 5.0, publicar não leva nível nem pré-requisito à
versão (versões seguem sem os dois, como as anteriores a esta entrega). O plano é integrado numa
única entrega; nenhuma implantação intermediária acontece. 3.0 e 4.0 fazem o descarte voltar os
campos novos ao estado da versão vigente (nulo/vazio) para que "descartar" nunca deixe o rascunho
divergente com `hasUnpublishedChanges: false`.

## Integridade dos gates

- .NET: `dotnet test --project <csproj> -- --filter-class <classe> --minimum-expected-tests N`
  (Microsoft.Testing.Platform: exit 8 se nada rodar, 9 se rodar menos que N).
- SPA: `npm --prefix src/admin-spa run test -- <fragmento de caminho>`. **Não** usar `-t` sozinho: o
  Vitest sai com 0 quando o nome não casa (verificado em CAP-006); o filtro de caminho sai com 1 quando
  nenhum arquivo casa. Os testes de cada fatia ficam em arquivos cujo caminho contém o fragmento do
  gate, e nenhum fragmento é prefixo de arquivo existente (`authoring-versions.test.tsx` não casa
  `authoring-version-level`).
- Design (1.0, 2.0): verificação estática da linha `> **Adendo nível e pré-requisito:**` no cabeçalho
  do documento de wireframes. A linha `> **Status:**` já existente é do 1º PRD e não satisfaz o gate.

## Artefatos compartilhados

| Artefato | Produzido em | Evolui em |
|---|---|---|
| `docs/design/wireframes-autoria-curso.md` (adendo ASCII + node-ids Figma) | 1.0, 2.0 | referência de 3.0–5.0 |
| `Course` / `CourseChanges` / `CourseContentFingerprint` (campos e impressão) | 3.0 (nível) | 4.0 (pré-requisito), 5.0 (publicar, descartar a partir do retrato) |
| `content.courses` (migrations EF) | 3.0 (`level`, `current_level`, zera `published_fingerprint`) | 4.0 (`prerequisite_text`, `recommended_course_ids`, `title_search` com backfill, zera `published_fingerprint` de novo) |
| `content.course_versions` (migration EF) | 5.0 (`level`, `prerequisite`) | — |
| `content.outbox_messages.message_id` e registro de execução (migration EF) | 6.0 | — |
| Tipos do BFF `CourseDetail`/`CourseSummary` | 3.0 | 4.0 |
| Tipo do BFF `CourseVersion` | 5.0 | — |
| Schemas zod `types/course.ts` | 3.0 | 4.0 |
| Schema zod `types/course-version.ts` | 5.0 | — |
| Handlers MSW `authoring-course-handlers.ts` | 3.0 | 4.0 |
| Handlers MSW `authoring-version-handlers.ts` | 5.0 | — |

## Verificação herdada

| Componente | Fonte | Checks e limites | Estado da base | Resolução planejada |
|---|---|---|---|---|
| `src/learning` | `.github/workflows/learning.yml` → `tassosgomes/template-pipeline/.github/workflows/ci-dotnet.yml@v1` (Debug, cobertura 70, container, `security-mode: observe`) | `dotnet restore`; `dotnet format --verify-no-changes`; `dotnet test` (MTP) com cobertura ≥ 70%; `dotnet publish -c Release`; imagem `src/learning/Dockerfile` | CI verde em `main` @ `235991c` (2026-09-30); `HEAD` `54e9716` só muda docs | Nenhuma falha herdada |
| `src/bff-admin` | `.github/workflows/bff-admin.yml` → `ci-dotnet.yml@v1` | Idem, cobertura ≥ 70% | CI verde @ `235991c` | Nenhuma |
| `src/admin-spa` | `.github/workflows/admin-spa.yml` → `ci-react-ts.yml@v1` (Node 24) | `npm ci`; `lint`; `tsc --noEmit`; `test` (Vitest, cobertura ≥ 70%); `build --base=/admin/`; imagem | CI verde @ `235991c` | Cobertura medida na full |
| `src/media` | `.github/workflows/media.yml` | Código não muda; nenhum check dispara | CI verde @ `235991c` | Compatibilidade com o fato 1.1.0 e o reenvio verificada no Compose (5.0, 6.0) |
| Contratos | `contracts.md` § Validação | Spectral 6.15.0 com ruleset local; AsyncAPI CLI 6.1.0; `jsonschema` nos exemplos | Os três documentos válidos, 0 erros (2026-09-30) | O schema `VersaoPublicadaPayload` 1.1.0 é o oráculo do teste de payload (5.0, 6.0) |

Os passos do `ci-dotnet.yml@v1` e do `ci-react-ts.yml@v1` foram lidos nesta sessão pela API do GitHub
(restore, format, test com cobertura, publish; install, lint, typecheck, test, build). `IntegrationTests`
de `learning` dependem de Docker (Testcontainers de PostgreSQL e RabbitMQ), disponível no ambiente.

## Cobertura

| Requisito | Task(s) |
|---|---|
| RF-01 | 3.0 |
| RF-02 | 4.0 |
| RF-03 | 5.0 |
| RF-04 | 3.0, 5.0 |
| RF-05 | 5.0 |
| RF-06 | 5.0, 6.0 |
| US-01 | 3.0 |
| US-02 | 4.0 |
| US-03 | 3.0, 5.0 |
| US-04 | 5.0 |
| US-05 | 5.0, 6.0 |
| RN-C03 | 3.0, 4.0 |
| RN-C05 | 3.0, 4.0 |
| RN-C06 | 5.0 |
| RN-C07 | 5.0 |
| RN-C08 | 5.0, 6.0 |
| RN-C10 | 3.0 |
| RN-C11 | 5.0 |
| RN-C12 | 5.0 |
| RN-C13 | 5.0 |
| RN-C17 | 4.0 |
| RN-C18 | 3.0, 4.0, 5.0 |
| RN-O03 | 3.0, 5.0 |
| RN-O05 | 4.0 |
| RN-12 | 3.0 |
| RN-18 | 3.0, 4.0 |
| DP-01 | 3.0, 5.0 |
| DP-02 | 4.0 |

`US-01` a `US-05` seguem a ordem das histórias do PRD. `RN-C*` são de Conteúdo e Currículo v1.1,
`RN-O*` de Catálogo e Oferta v1.1, `RN-12`/`RN-18` de Identidade e Acesso. Decisões de contrato: C-01 em
6.0; C-02 e C-03 em 5.0; C-04 em 4.0; C-05 em 3.0 e 4.0. RF-06 segue a errata do PRD 1.1
(2026-09-30): a carga inicial do Catálogo é o reenvio do fato da versão vigente (C-01, OD56), coberto
por 6.0; o fato 1.1.0 e o isolamento por escola dele, por 5.0.

Experiência do Usuário do PRD (seção "Para quem é este curso", linguagem de recomendação, ajuda que
passa a dizer que preço e vigência são da oferta, acessibilidade) entra no design de 1.0/2.0 e nas telas
de 3.0–5.0. Segurança: autorização por `autoria.editar` e isolamento por tenant em 3.0/4.0; texto do
pré-requisito e descrição fora de log, span, métrica e erro em 4.0/5.0/6.0. Observabilidade específica:
contagem do reenvio e marcador de conclusão em log estruturado em 6.0. Migração de dados: backfill de
`title_search` (4.0) e de `message_id` (6.0) e zeragem de `published_fingerprint` (3.0, 4.0) nas
migrations EF das fatias; nível dos cursos já publicados não é preenchido (Não-Objetivos).

> Verificado por `python3 .claude/skills/tsg-flow-task-creator/scripts/validate_plan.py tasks/prd-nivel-prerequisito-curso/` antes do handoff.
