---
tsg_artifact: contract
product: code-4-coders
capability: CAP-005
version: 1.0
status: approved
updated: 2026-09-30
sources: tasks/prd-nivel-prerequisito-curso/prd.md@1.1, domains/conteudo-e-curriculo/domain.md@1.1, domains/catalogo-e-oferta/domain.md@1.1, context/architecture-baseline.md@1.2
---

# Contratos de integração — nível e pré-requisito do curso

> - PRD: [prd.md](prd.md), v1.0, aprovado em 2026-09-30
> - Data da revisão: 2026-09-30
> - Estado do conjunto: **Aprovado para implementação (1.0)** em 2026-09-30 — todas as decisões aprovadas pelo responsável; todos os documentos validados sem erros.

Este conjunto registra o acordo para o segundo PRD de `CAP-005`. É o **provedor** de `CAP-003` ([contracts.md](../prd-vitrine-oferta/contracts.md) daquele PRD), e os dois foram escritos na mesma passada. Os contratos do primeiro PRD de `CAP-005` são preservados em `tasks/prd-autoria-curso/`; este conjunto só os evolui de forma aditiva.

## Seleção e escopo

| Integração identificada | Modalidade | Justificativa |
|---|---|---|
| `admin-spa` → `bff-admin` → `learning` | OpenAPI | O editor lê e grava nível e pré-requisito; o histórico os mostra (RF-01, RF-02, RF-04, RF-05). |
| `learning` → Media e Catálogo | AsyncAPI | O fato de versão passa a levar descrição, nível e pré-requisito (RF-06). |
| Leitura sob demanda de nível e pré-requisito pelo Catálogo | Substituída por C-01 | O Catálogo mantém a própria visão a partir do fato; a carga inicial é o reenvio. |
| Currículo como produto de dados | Não aplicável | Banco interno de `learning`; o estado publicado viaja no fato. Nenhum ODCS. |

| Documento | Padrão e versão | Versão do contrato | Fronteira | Estado |
|---|---|---|---|---|
| [api-contract.yaml](api-contract.yaml) e [.md](api-contract.md) | OpenAPI 3.1.0 | `bff-admin` autoria 1.0.0 → 1.1.0 | Recorte: `listCourses`, `getCourse`, `updateCourse`, `getCourseVersion`; demais operações de autoria sem mudança | **Aprovado para implementação** em 2026-09-30; lint sem erros nem avisos |
| [internal-api-contract-learning.yaml](internal-api-contract-learning.yaml) e [.md](internal-api-contract-learning.md) | OpenAPI 3.1.0 | `learning` 1.0.0 → 1.1.0 | As mesmas quatro operações `*Internal` | **Aprovado para implementação** em 2026-09-30; lint sem erros nem avisos |
| [asyncapi-contract.yaml](asyncapi-contract.yaml) | AsyncAPI 3.0.0 | `learning` 1.0.0 → 1.1.0 | `conteudo.versao-publicada.v1` com os campos novos e o reenvio; ato de publicação e `midia.*` sem mudança | **Aprovado para implementação** em 2026-09-30; parser sem erros |

## Participantes e interfaces

| Identificador | Provedor/produtor | Consumidores | Comportamento | PRD |
|---|---|---|---|---|
| `listCourses` (+`title`, +`currentLevel`) | `bff-admin`, decisão de `learning` | `admin-spa` | Seletor de recomendados e indicação de curso sem nível | RF-02, RF-04 |
| `getCourse`, `updateCourse` | idem | `admin-spa` | Nível e pré-requisito no rascunho; recomendados validados por `learning` | RF-01, RF-02 |
| `getCourseVersion` | idem | `admin-spa` | Nível e pré-requisito como publicados, recomendados com o título da época | RF-05 |
| `*Internal` (4 operações) | `learning` | `bff-admin` | Mesmas regras, JWT de ator, `X-Actor-Name` na escrita | RF-01, RF-02, RF-04, RF-05 |
| `publicarVersaoPublicada` · `conteudo.versao-publicada.v1` 1.1.0 | `learning`, por outbox | Media (sem mudança), Catálogo em `commerce` (novo) | Estado completo com `description`, `level`, `prerequisite`, sempre presentes | RF-03, RF-06 |
| `reenviarVersaoVigente` | `learning` | Catálogo | Uma vez na implantação, mesmo `eventId` | RF-06 (C-01) |

| Ação | Operação HTTP | Mensagens |
|---|---|---|
| Alterar nível ou pré-requisito | `updateCourse` | nenhuma — fica no rascunho (RN-C05) |
| Publicar ou republicar | `publishCourse` (1.0.0, sem mudança de interface) | `conteudo.versao-publicada.v1` 1.1.0 + ato `versao-publicada` (sem mudança) |
| Implantação desta entrega | — | `conteudo.versao-publicada.v1` reenviado por curso publicado |

## Origem e decisões

| Entrada consultada | Versão e data | Decisão herdada |
|---|---|---|
| [PRD](prd.md) | v1.0, 2026-09-30 | RF-01 a RF-06, DP-01, DP-02; OD54, OD55 |
| [Conteúdo e Currículo](../../../domains/conteudo-e-curriculo/domain.md) | v1.1, 2026-09-30 | RN-C05, RN-C06, RN-C11, RN-C13, RN-C18 |
| [Catálogo e Oferta](../../../domains/catalogo-e-oferta/domain.md) | v1.1, 2026-09-30 | RN-O02, RN-O03 (consumidor) |
| [Contratos de autoria](../prd-autoria-curso/contracts.md) | 1.0, 2026-09-28 | C-02, C-03, C-04, C-07, C-09, C-11; convenções HTTP do backoffice |
| Código de `media` (`PublishedCourseFact.Parse`) | `main` em 2026-09-30 | Lê só os campos que usa; ignora campos novos (base da compatibilidade em "Evolução") |

Decisões deste contrato:

| ID | Decisão | Alternativa descartada | Estado |
|---|---|---|---|
| C-01 | **Carga inicial por reenvio:** na implantação, `learning` reenvia o fato da versão vigente de cada curso publicado, com o **mesmo `eventId`**; Media não muda nada (C-11 de CAP-005), o Catálogo aplica. Substitui a "leitura sob demanda" do RF-06 do PRD, que ganha errata | Leitura síncrona `commerce` → `learning`: exigiria mecanismo novo de autenticação entre serviços de domínio e dependência síncrona | **Aprovada em 2026-09-30** |
| C-02 | **Curso recomendado viaja com o título do momento da publicação.** O consumidor usa o título atual quando conhece o curso, e este quando não conhece. O histórico mostra o da época (RF-05) | Só o id: o Catálogo não teria título para recomendado fora da vitrine | **Aprovada em 2026-09-30** |
| C-03 | **O fato passa a levar a descrição pedagógica** (≤ 5 000). Revisa a parte "sem descrição" de C-09 de CAP-005, para a página pública sair inteira da visão do Catálogo | `bff-student` ler a descrição em `learning`: poria `learning` no caminho da vitrine | **Aprovada em 2026-09-30** |
| C-04 | **Filtro `title` em `listCourses`** para o seletor de recomendados, combinado com `status=published` | Operação própria de busca: duplicaria a lista | **Aprovada em 2026-09-30** |
| C-05 | **Nível e pré-requisito entram em `draftRevision` e `hasUnpublishedChanges`**, para que publicar e descartar protejam também essas mudanças (C-03 de CAP-005) | Tratá-los fora da revisão: publicar poderia levar um nível que o professor não viu | **Aprovada em 2026-09-30** |

## Evolução e compatibilidade

- **`bff-admin` autoria e `learning` 1.0.0 → 1.1.0:** aditivas. Campos novos em respostas com `additionalProperties: false`: o `admin-spa` é o único cliente e evolui junto; `bff-admin` é o único cliente de `learning` e evolui junto. Parâmetro `title` opcional. Compatibilidade com produção não verificada.
- **`conteudo.versao-publicada.v1` 1.0.0 → 1.1.0:** o schema 1.0.0 declarava `additionalProperties: false`, então um validador estrito recusaria os campos novos. O único consumidor atual, `media`, **não** valida contra o schema: lê só `eventId`, `tenantId`, `courseId`, `versionNumber`, `publishedAt` e `modules[].lessons[].lessonId/videoId` (`PublishedCourseFact.Parse`) e ignora o resto. Por isso a mudança fica no mesmo canal `v1`. Consumidor futuro deve tolerar campo desconhecido. Mensagem 1.0.0 ainda em fila é tratada pelo Catálogo como sem nível e sem pré-requisito.
- **Reenvio (C-01):** Media recebe versões que já aplicou, com o mesmo `eventId` → confirmadas sem efeito. **Ordem de implantação:** consumidor do Catálogo em `commerce` → `learning` 1.1.0 → reenvio.
- **Ato de publicação:** não muda; republicar só para mudar nível gera ato `versao-publicada` como qualquer republicação.

## Validação e verificação

| Documento | Comando e ferramenta | Padrão/ruleset | Resultado |
|---|---|---|---|
| [api-contract.yaml](api-contract.yaml) | `npx --yes @stoplight/spectral-cli@6.15.0 lint tasks/prd-nivel-prerequisito-curso/api-contract.yaml --ruleset .claude/skills/tsg-flow-contract-creator/rulesets/openapi.yaml -F hint` | OpenAPI 3.1.0, ruleset local | Sem erros nem avisos; referências a `tasks/prd-autoria-curso/api-contract.yaml` resolvidas |
| [internal-api-contract-learning.yaml](internal-api-contract-learning.yaml) | mesmo comando | OpenAPI 3.1.0, ruleset local | Sem erros nem avisos |
| [asyncapi-contract.yaml](asyncapi-contract.yaml) | `npx --yes @asyncapi/cli@6.1.0 validate <arquivo>` | AsyncAPI 3.0.0 | Válido; uma informação recomenda 3.1.0, mantida a 3.0.0 da skill |
| Exemplos de mensagem | `jsonschema` 4.26.0 (Draft 7, com formato) | `VersaoPublicadaPayload` 1.1.0 | `versao4` e `reenvioSemNivel` conformes; nível `expert`, nível omitido e 6 recomendados recusados |

A implementação deve verificar:

- `updateCourse` com recomendado de outra escola, nunca publicado, inexistente ou o próprio curso → 422 `RECOMMENDED_COURSE_INVALID`, nada muda; com 6 ids → 400/422; nível fora da lista → 422 `FIELD_INVALID`;
- mudar só o nível num curso publicado → `hasUnpublishedChanges: true`, `draftRevision` cresce, versão vigente intacta;
- publicar sem nível → publica (DP-01), fato com `level: null`;
- fato 1.1.0 sempre com `description`, `level` e `prerequisite`, mesmo vazios;
- Media processa fato 1.1.0 sem mudança de comportamento; reenvio com `eventId` antigo não altera Referências de Uso;
- recomendado renomeado depois da publicação: histórico mostra o título antigo;
- nenhum texto de pré-requisito ou descrição em log, span ou métrica.

## Pendências e handoff

1. **Errata do PRD (RF-06):** "leitura sob demanda pelo Catálogo" passa a ser "carga inicial por reenvio" (C-01). **Aplicada** no [PRD](prd.md) 1.1 em 2026-09-30.
2. **Para a TechSpec:** mecanismo do reenvio (comando de implantação, idempotência, ordem em relação ao consumidor do Catálogo) e do filtro por título sem acento.

Para `tsg-flow-techspec-creator`: usar este índice, [api-contract.yaml](api-contract.yaml), [internal-api-contract-learning.yaml](internal-api-contract-learning.yaml) e [asyncapi-contract.yaml](asyncapi-contract.yaml), sem duplicar schemas.
