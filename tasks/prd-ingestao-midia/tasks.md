# Plano de Implementação — Ingestão e preparação de mídia protegida (CAP-006)

> **TechSpec de origem:** [techspec.md](techspec.md), aprovada em 2026-09-26
> **Escopo:** Full-stack (`media` API + worker, `bff-admin`, `admin-spa`, `identity`)
> **ADRs pertinentes:** [0002](../../docs/adr/0002-plataforma-de-runtime-coolify.md), [0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md), [0006](../../docs/adr/0006-preparacao-de-video-e-custodia-de-chave.md)
> **Contratos:** [contracts.md](contracts.md) 1.1 — OpenAPI BFF 1.1.1, OpenAPI Media 1.0.1 (C-16), AsyncAPI Media 1.0.0
> **Status do plano:** Em revisão

## Visão Geral

Ao final, o professor abre a área Vídeos do backoffice, envia um vídeo de até 5 GB direto ao
armazenamento em partes, retoma um envio interrompido, e vê a linha passar sozinha de *recebido* a
*em preparação* e a *pronto* (ou *falhou*, com o motivo). O vídeo pronto existe só como HLS cifrado em
até três qualidades, o original é descartado, nada é público, e Media publica `midia.ativo-pronto` ou
`midia.preparacao-falhou` pelo outbox. Quem não tem `midia.enviar` é barrado no SPA, no BFF e em Media.

O desenho da área (wireframe ASCII → Figma → aprovação) vem **antes** de qualquer código, para que as
telas já nasçam no design aprovado (decisão do usuário em 2026-09-26).

## Fases

### Fase 0 — Design aprovado

1.0 wireframe ASCII da área Vídeos (decide QP-02); 2.0 telas no Figma. Checkpoint: o documento de
wireframes registra a aprovação do ASCII e do Figma, com os node-ids das telas.

### Fase 1 — Área protegida e envio

3.0 abre a área com autorização nas três camadas; 4.0 entrega o envio em partes com MinIO local;
5.0 cria o papel `worker` e a imagem com ffmpeg; 6.0 retoma e expira envios. Checkpoint: professor
envia um MP4 pelo navegador em `http://localhost:8081/admin/videos`, interrompe, retoma e vê *recebido*.

### Fase 2 — Preparação e operação

7.0 prepara o vídeo até *pronto*; 8.0 falha com motivo e sem resíduo; 9.0 título, filtro e busca;
10.0 métricas de volume. Checkpoint: um vídeo de 20 s chega a *pronto · 0:20* sem recarregar, um
arquivo ilegível chega a *falhou* com o motivo, e as três métricas aparecem no coletor OTLP local.

## Mapa de Entrega

| Fatia | Task | Comportamento observável | Gate | Bloqueado por |
|---|---|---|---|---|
| V-01 | [3.0](3_task.md) | Professor vê Vídeos e a lista vazia; suporte e JWT sem permissão são recusados | Identity `MediaAudienceTests`; Media `VideoLibraryAuthorizationTests`; BFF `VideoLibraryTests`; SPA `videos-area` | 2.0 |
| V-02 | [4.0](4_task.md) | Envio em partes direto ao armazenamento; vídeo aparece *recebido* | Media `VideoUploadTests`; BFF `VideoUploadTests`; SPA `video-upload` | 3.0 |
| V-03 | [6.0](6_task.md) | Envio interrompido continua; abandonado some em 24 h | Media `VideoUploadResumeTests`; BFF `VideoUploadResumeTests`; SPA `video-upload-resume` | 4.0, 5.0 |
| V-04 | [7.0](7_task.md) | Vídeo fica pronto em HLS cifrado; lista se atualiza sozinha | Media `VideoPreparationTests`, `MediaHostRoleTests`; SPA `video-status` | 4.0, 5.0 |
| V-05 | [8.0](8_task.md) | Falha com motivo claro, retentativa só para falha passageira, sem resíduo | Media `VideoPreparationFailureTests`; SPA `video-failure` | 7.0 |
| V-06 | [9.0](9_task.md) | Título editável; filtro por estado e busca sem acento | Media `VideoTitleSearchTests`, `VideoTitleNormalizationTests`; BFF `VideoTitleTests`; SPA `video-library-filter` | 4.0 |
| V-07 | [10.0](10_task.md) | Volume guardado, vídeos por estado e vídeos presos na telemetria | Media `MediaVolumeMetricsTests` | 7.0 |

Os gates completos, com `--minimum-expected-tests` (.NET) e filtro de caminho (Vitest), estão no
frontmatter de cada task.

### Habilitadores

| Enabler | Task | Por que não cabe numa fatia | Desbloqueia |
|---|---|---|---|
| EN-02 (ASCII) | [1.0](1_task.md) | O fluxo de design do backoffice exige wireframe aprovado antes do código de tela (PRD, Experiência do Usuário); não há comportamento de software a testar | 2.0 |
| EN-02 (Figma) | [2.0](2_task.md) | Idem; o Figma só começa depois do ASCII aprovado, e todas as telas de 3.0–9.0 são construídas sobre ele | V-01 (3.0) e todas as fatias com tela |
| EN-03 | [5.0](5_task.md) | O worker precisa existir como processo implantável, com ffmpeg na imagem e no CI, antes de preparar o primeiro vídeo; embuti-lo em V-04 esconderia o risco do CI (workflow reutilizável sem passo de instalação) atrás da maior fatia do plano | V-03 (6.0, varredura de expiração) e V-04 (7.0) |

**EN-01 (MinIO local, endpoints interno/público, CORS) não virou task própria:** a primeira fatia que
usa o armazenamento é V-02, e seu smoke pelo navegador é exatamente a evidência que EN-01 pedia
(`PUT` de `http://localhost:8081` aceito pelo MinIO). EN-01 entra em 4.0.

**Ajuste de dependência em relação à TechSpec:** V-03 depende também de EN-03 (5.0), porque a
varredura que aborta envios vencidos roda no papel `worker` (TechSpec, Media worker, passo 7a).

## Tasks

- [x] 1.0 Wireframe ASCII da área Vídeos aprovado, com nome e posição da área decididos
- [x] 2.0 Telas da área Vídeos desenhadas no Figma e aprovadas
- [ ] 3.0 Professor abre a área Vídeos, e quem não tem a permissão é barrado nas três camadas
- [ ] 4.0 Professor envia um vídeo e ele aparece na lista como recebido
- [ ] 5.0 Media parte como worker em container próprio, com ffmpeg na imagem e no CI
- [ ] 6.0 Envio interrompido continua de onde parou, e envio abandonado some em 24 h
- [ ] 7.0 Vídeo recebido fica pronto em três qualidades cifradas, e a lista mostra isso sozinha
- [ ] 8.0 Vídeo com problema falha com motivo claro e não fica preso
- [ ] 9.0 Professor corrige o título e encontra vídeos por estado e por nome
- [ ] 10.0 Volume guardado e vídeos por estado visíveis na telemetria

## Caminho crítico e lanes

`1.0 → 2.0 → 3.0 → 4.0 → 5.0 → 7.0 → 8.0`. Depois de 4.0, 9.0 é independente da preparação; depois
de 5.0, 6.0 e 7.0 são independentes entre si; 10.0 só depende de 7.0. A execução do orquestrador é
sequencial na ordem numérica, e as dependências declaradas permitem reordenar 6.0/9.0 se 7.0 travar.

## Integridade dos gates

- .NET: `dotnet test --project <csproj> -- --filter-class <classe> --minimum-expected-tests N`
  (Microsoft.Testing.Platform: exit 8 se nada rodar, 9 se rodar menos que N).
- SPA: `npm --prefix src/admin-spa run test -- <fragmento de caminho>`. **Não** usar `-t` sozinho: o
  Vitest 5 sai com 0 quando o nome não casa com nenhum teste (verificado em 2026-09-26); o filtro de
  caminho sai com 1 quando nenhum arquivo casa. Os testes de cada fatia ficam em arquivos cujo caminho
  contém o fragmento do gate.
- Design (1.0, 2.0): verificação estática do registro de aprovação no documento de wireframes.

## Artefatos compartilhados

| Artefato | Produzido em | Evolui em |
|---|---|---|
| `docs/design/wireframes-videos.md` (ASCII + node-ids Figma) | 1.0, 2.0 | referência de 3.0, 4.0, 6.0–9.0 |
| Tabela de vídeos e de envios de Media (migrations EF) | 3.0 (vídeos), 4.0 (envios) | 7.0 (lease, chave cifrada), 8.0 (tentativas, motivo), 9.0 (título normalizado, se ainda não existir) |
| Porta e adaptador de armazenamento de Media | 4.0 | 6.0 (listar/abortar), 7.0 (baixar, enviar árvore, apagar) |
| Fixture MinIO (Testcontainers) de `Media.IntegrationTests` | 4.0 | 6.0–10.0 |
| Cliente Media no `bff-admin` | 3.0 | 4.0, 6.0, 9.0 |
| Papel `api`/`worker` e serviço `media-worker` | 5.0 | 6.0, 7.0, 10.0 |

## Verificação herdada

| Componente | Fonte | Checks e limites | Estado da base | Resolução planejada |
|---|---|---|---|---|
| `src/media` | `.github/workflows/media.yml` → `tassosgomes/template-pipeline/.github/workflows/ci-dotnet.yml@v1` (lido em 2026-09-26) | `dotnet restore`; `dotnet format --verify-no-changes`; `dotnet test` (MTP, Debug) com cobertura ≥ 70%; `dotnet publish -c Release`; imagem `src/media/Dockerfile` | Não medido nesta sessão; componente é esqueleto da Fase 0 | O reutilizável não tem entrada para passo de instalação: 5.0 garante ffmpeg nos testes sem editar o template. 4.0 substitui `MediaStorageBoundaryTests`, que testa a porta atual |
| `src/bff-admin` | `.github/workflows/bff-admin.yml` → `ci-dotnet.yml@v1` | Idem, cobertura ≥ 70% | Não medido; CAP-002 integrado com validação full (commit `6cd7e2f`) | Nenhuma falha herdada conhecida |
| `src/identity` | `.github/workflows/identity.yml` → `ci-dotnet.yml@v1` | Idem, cobertura ≥ 70% | Não medido; CAP-002 integrado | Nenhuma |
| `src/admin-spa` | `.github/workflows/admin-spa.yml` → `ci-react-ts.yml@v1` | `lint`, `typecheck`, `test` (Vitest, cobertura ≥ 70%), `build --base=/admin/`, imagem | Não medido; 10 arquivos de teste passam por filtro vazio (execução de 2026-09-26) | Cobertura medida na full |
| Contratos | `contracts.md` § Validação | Spectral 6.15.0 com ruleset local, `--fail-severity=error` | Os dois OpenAPI de CAP-006 sem erros nem avisos após C-16 (2026-09-26) | 3.0 relinta os contratos de CAP-002 após C-14 |

Os `IntegrationTests` e `EndToEndTests` dependem de Docker (Testcontainers), disponível no ambiente.
Paridade exata com o CI é confirmada na validação full.

## Cobertura

| Requisito | Task(s) |
|---|---|
| RF-01 | 3.0 |
| RF-02 | 4.0 |
| RF-03 | 6.0 |
| RF-04 | 7.0, 8.0 |
| RF-05 | 7.0, 8.0 |
| RF-06 | 3.0, 4.0, 7.0, 9.0 |
| RF-07 | 9.0 |
| RF-08 | 3.0, 4.0 |
| RF-09 | 7.0, 8.0 |
| RF-10 | 4.0, 7.0 |
| RF-11 | 10.0 |
| US-01 | 4.0 |
| US-02 | 6.0 |
| US-03 | 7.0, 8.0 |
| US-04 | 3.0, 9.0 |
| US-05 | 7.0 |
| US-06 | 4.0, 7.0 |
| US-07 | 3.0 |
| RN-12 | 3.0 |
| RN-16 | 3.0 |
| RN-17 | 3.0 |
| RN-18 | 3.0 |
| RN-M01 | 4.0, 7.0 |
| RN-M02 | 3.0 |
| RN-M03 | 3.0 |
| RN-M04 | 3.0, 9.0 |
| RN-M05 | 7.0, 8.0 |
| RN-M06 | 8.0 |
| RN-M07 | 7.0 |
| RN-M08 | 7.0 |
| RN-M15 | 1.0, 4.0 |
| RN-M17 | 10.0 |
| DP-01 | 7.0 |
| DP-02 | 4.0, 6.0, 8.0 |
| DP-03 | 3.0, 9.0 |
| DP-04 | 7.0 |
| DP-05 | 3.0 |
| DP-06 | 7.0, 8.0 |
| QP-02 | 1.0 |
| C-16 | 4.0, 6.0 |

Experiência do Usuário do PRD (fluxo, retomada, linguagem, acessibilidade) entra no design de 1.0/2.0
e nas telas de 3.0, 4.0, 6.0–9.0. Observabilidade: spans por etapa da preparação em 7.0; métricas em
10.0. Segurança: autorização dupla em 3.0; nada público e chave fora de log em 4.0 e 7.0. Migração de
dados: não há vídeo real anterior. Auditoria: fora de escopo pelo PRD (Não-Objetivos).

> Verificado por `python3 .claude/skills/tsg-flow-task-creator/scripts/validate_plan.py tasks/prd-ingestao-midia/` antes do handoff.
