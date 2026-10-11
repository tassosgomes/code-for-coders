---
tsg_artifact: tasks
product: code-4-coders
capability: CAP-006
version: 1.0
status: approved
updated: 2026-10-11
sources: tasks/prd-tags-video/prd.md@1.0, tasks/prd-tags-video/techspec.md@1.0, tasks/prd-tags-video/contracts.md@1.0
---

# Plano de Implementação — Tags de vídeo (CAP-006, 3ª fatia)

> **TechSpec de origem:** [techspec.md](techspec.md) 1.0, aprovada em 2026-10-11
> **Escopo:** Full-stack — `media`, `bff-admin`, `admin-spa`
> **ADRs pertinentes:** herdadas [0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md), [0006](../../docs/adr/0006-preparacao-de-video-e-custodia-de-chave.md), [0019](../../docs/adr/0019-runtime-e-deploy-atual.md); nenhuma nova
> **Contratos:** [contracts.md](contracts.md) 1.0 — [api-contract.yaml](api-contract.yaml) 1.2.0 e [internal-api-contract.yaml](internal-api-contract.yaml) 1.2.0
> **Design:** adendo em `docs/design/wireframes-videos.md` e `docs/design/wireframes-autoria-curso.md` (1.0 e 2.0)
> **Status do plano:** Aprovado em 2026-10-11 — Confirmado para implementação

## Visão Geral

Ao final, o professor rotula vídeos com tags livres, no envio ou depois na tela Vídeos, com
sugestão das tags que a escola já usa. Ele também filtra a tela Vídeos e o seletor de vídeo da aula
por uma ou mais tags, com E ou OU, combinadas com estado e título. As tags vivem em `media`; o
`bff-admin` repassa; `learning` e os fatos `midia.*` não mudam.

O design (adendo ASCII → Figma → aprovação) vem antes de qualquer código de tela. A primeira fatia
de código (3.0) entrega também a transição de compatibilidade: `admin-spa`, `bff-admin` e `media`
passam a falar `tags` juntos (`contracts.md`, Evolução).

## Fases

### Fase 0 — Design aprovado

1.0 adendo ASCII e 2.0 Figma. **Checkpoint:** os dois documentos de wireframe trazem, no cabeçalho,
a linha `> **Adendo tags de vídeo:** ASCII e Figma aprovados pelo responsável em <data>`.

### Fase 1 — Rotular e sugerir

3.0 edição de tags na tela Vídeos; 4.0 sugestão das tags da escola; 5.0 tags no envio.
**Checkpoint:** com `scripts/remote-infra.sh migrate` e `scripts/apps.sh start --remote`, em
`http://localhost:8081/admin/videos`:

- editar as tags de um vídeo, com sugestão ao digitar;
- recarregar a página e ver as etiquetas;
- enviar um vídeo curto com tags e vê-lo na lista já rotulado.

### Fase 2 — Achar pelo rótulo

6.0 filtro E/OU na tela Vídeos; 7.0 filtro no seletor da aula. **Checkpoint:** em
`http://localhost:8081/admin/videos?tag=docker&tag=m%C3%B3dulo%202&tagMatch=any`, a lista filtrada
sobrevive ao recarregamento. Na tela de aula de um curso, o seletor filtrado por tag oferece só
vídeos prontos com a tag, e o vínculo funciona como antes.

## Mapa de Entrega

| Fatia | Task | Comportamento observável | Gate | Bloqueado por |
|---|---|---|---|---|
| EN-01 (ASCII) | 1.0 | Adendo ASCII de envio, tela Vídeos e seletor aprovado | `rg` da linha do adendo nos dois documentos | Nenhum |
| EN-01 (Figma) | 2.0 | Figma do adendo aprovado e registrado | `rg` de `ASCII e Figma aprovados` nos dois documentos | 1.0 |
| V-01 | 3.0 | O professor edita as tags de um vídeo e as vê na lista | Media `VideoTagNormalizationTests` + `VideoTagReplacementTests` + BFF E2E `VideoTagTests` + SPA `video-tags-edit` | 2.0 |
| V-02 | 4.0 | Ao digitar, o professor vê as tags que a escola já usa | Media `VideoTagSuggestionTests` + BFF E2E `VideoTagSuggestionTests` + SPA `video-tag-suggestions` | 3.0 |
| V-03 | 5.0 | O professor informa tags ao enviar um vídeo | Media `VideoUploadTagTests` + BFF E2E `VideoUploadTagTests` + SPA `video-upload-tags` | 4.0 |
| V-04 | 6.0 | O professor filtra a tela Vídeos por tags, com E ou OU | Media `VideoTagFilterTests` + BFF E2E `VideoTagFilterTests` + SPA `video-library-tag-filter` | 4.0 |
| V-05 | 7.0 | O professor filtra o seletor de vídeo da aula por tags | SPA `authoring-video-picker-tags` | 6.0 |

EN-01 vira duas tasks (ASCII e Figma) porque cada aprovação é uma decisão separada do responsável e
o Figma parte do ASCII aprovado.

5.0 e 6.0 dependem só de 4.0 e não compartilham caso de uso. Rodam em sequência porque o
orquestrador é sequencial e as duas tocam os handlers MSW e `VideoEndpoints`.

### Habilitadores

| Enabler | Task | Por que não cabe numa fatia | Desbloqueia |
|---|---|---|---|
| EN-01 (ASCII) | 1.0 | O desenho é decidido pelo responsável antes do código (regra do projeto). O componente de tags é único para envio, edição e os dois filtros, e precisa ser desenhado de uma vez | 3.0 a 7.0 |
| EN-01 (Figma) | 2.0 | A aprovação visual vale para todas as telas de uma vez | 3.0 a 7.0 |

Não há habilitador técnico. A migration das tabelas de tags nasce em 3.0 com o comportamento que
as usa, e a coluna das tags do envio nasce em 5.0. As migrations são sempre geradas pela ferramenta
do EF.

## Tasks

- [x] 1.0 Adendo ASCII de tags de vídeo (envio, tela Vídeos e seletor da aula) aprovado
- [x] 2.0 Figma do adendo de tags de vídeo aprovado
- [x] 3.0 O professor edita as tags de um vídeo na tela Vídeos e as vê na lista
- [ ] 4.0 Ao digitar uma tag, o professor vê as tags que a escola já usa
- [ ] 5.0 O professor informa tags ao enviar um vídeo
- [ ] 6.0 O professor filtra a tela Vídeos por tags, com E ou OU
- [ ] 7.0 O professor filtra o seletor de vídeo da aula por tags

## Verificação herdada

| Componente | Fonte | Checks e limites | Estado da base | Resolução planejada |
|---|---|---|---|---|
| `src/media` | `.github/workflows/media.yml` → `tassosgomes/template-pipeline/.github/workflows/ci-dotnet.yml@v1` (`test-configuration: Debug`, `coverage-threshold: 70`) | restore; `dotnet format --verify-no-changes`; `dotnet test` (MTP) com cobertura ≥ 70%; `dotnet publish`; imagem. Inclui `src/contract-testing/**` (só AsyncAPI) | Não medido nesta sessão; `main` com `prd-observabilidade-midia` integrado | Nenhuma falha herdada comprovada. Cobertura agregada medida na validação full |
| `src/bff-admin` | `.github/workflows/bff-admin.yml` → `ci-dotnet.yml@v1` | Idem, cobertura ≥ 70% | Não medido | Nenhuma |
| `src/admin-spa` | `.github/workflows/admin-spa.yml` → `ci-react-ts.yml@v1` (`--base=/admin/`, cobertura 70) | lint; typecheck; `vitest run --coverage` ≥ 70%; `vite build`; imagem | Não medido | Nenhuma |
| Migrations de `media` | AGENTS.md; `dotnet-ef` 10.0.12 em `.config/dotnet-tools.json` | Migration gerada pela ferramenta; `has-pending-model-changes` sem pendência | Não medido | 3.0 e 5.0 geram a migration |
| Contratos | `contracts/README.md` (regra 2) | Promoção aditiva por task a `contracts/bff-admin/openapi-midia.yaml` e `contracts/media/openapi-internal.yaml`; Spectral sem erros com `contracts/.spectral-exception.yaml` (exceção HLS C-08 vigente) | Recortes validados em 2026-10-11 (`contracts.md`) | Cada task de 3.0 a 6.0 promove o que implementa; a consolidação (versão, origem, README) fica para o encerramento do PRD |

O workflow reutilizável `ci-dotnet.yml@v1` é de outro repositório e não foi lido nesta sessão. Os
passos acima vêm do plano de `CAP-011` (arquivado), que os registrou. A paridade com o CI não foi
reverificada.

## Cobertura

| Requisito | Task(s) |
|---|---|
| RF-01 | 1.0, 2.0, 5.0 |
| RF-02 | 1.0, 2.0, 3.0 |
| RF-03 | 1.0, 2.0, 4.0 |
| RF-04 | 1.0, 2.0, 6.0 |
| RF-05 | 1.0, 2.0, 7.0 |
| RF-06 | 1.0, 2.0, 6.0, 7.0 |
| RN-T01 | 3.0, 7.0 |
| RN-T02 | 3.0, 4.0, 5.0 |
| RN-T03 | 3.0, 5.0 |
| RN-T04 | 3.0 |
| RN-T05 | 3.0, 4.0, 6.0 |
| RN-T06 | 3.0, 4.0, 6.0 |
| RN-C04 | 7.0 |
| US-01 (tags no envio) | 5.0 |
| US-02 (editar tags depois) | 3.0 |
| US-03 (sugestão das tags da escola) | 4.0 |
| US-04 (filtrar o seletor da aula) | 7.0 |
| US-05 (escolher E ou OU) | 6.0, 7.0 |
| US-06 (combinar tag, título e estado) | 6.0 |
| Métricas do PRD (telemetria) | 3.0 (`media.video_tags.assigned`), 6.0 (`media.videos.listed`) |

> Verificado por `python3 <skill-dir>/scripts/validate_plan.py tasks/prd-tags-video/` antes do handoff.
