# Plano de Implementação — Autoria e publicação de curso (CAP-005)

> **TechSpec de origem:** [techspec.md](techspec.md) 1.0, aprovada em 2026-09-29
> **Escopo:** Full-stack (`identity`, `learning`, `media`, `audit`, `bff-admin`, `admin-spa`)
> **Contratos:** [contracts.md](contracts.md) 1.0; OpenAPI e AsyncAPI aprovados em 2026-09-28
> **Design:** [wireframes-autoria-curso.md](../../docs/design/wireframes-autoria-curso.md), ASCII e Figma aprovados pelo responsável em 2026-09-29
> **ADRs pertinentes:** [0001](../../docs/adr/0001-monorepo-de-codigo.md), [0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md), [0006](../../docs/adr/0006-preparacao-de-video-e-custodia-de-chave.md), [0007](../../docs/adr/0007-snapshots-efemeros-da-consulta-de-auditoria.md)
> **Status do plano:** Em revisão; task 1.0 concluída e gate visual satisfeito; nenhuma task de implementação iniciada

## Visão Geral

Ao final, um professor cria um curso da escola, organiza módulos e aulas, vincula vídeos prontos,
publica uma versão com ato conforme e Referências de Uso, corrige e republica sem alterar a versão
anterior, consulta o histórico e exclui apenas rascunhos nunca publicados. A UI segue o ASCII
aprovado e o desenho visual aprovado no Figma.

**Gate de continuidade solicitado pelo responsável:** 1.0 entrega o Figma e aguarda sua aprovação
explícita. Enquanto ela não existir, 1.0 permanece `pending`/`validating`, seu gate não passa e
**nenhuma task 2.0–7.0 pode começar**, inclusive trabalho apenas de backend. O registro de aprovação
no documento de wireframes só pode ser escrito após a manifestação do responsável sobre o desenho
real. Aprovação do ASCII ou deste plano não vale como aprovação do Figma. Se houver ajustes, o
desenho volta à revisão; mudança de comportamento exige atualização e nova aprovação do ASCII.

**Checkpoint visual concluído em 2026-09-29:** após receber o link do desenho real, o responsável
aprovou explicitamente nesta conversa: **“Está aprovado”**. A task 1.0 está `done` e seu gate
retornou exit 0. O bloqueio visual das tasks 2.0–7.0 está satisfeito; as demais dependências
do mapa de entrega permanecem válidas.

## Fases

### Fase 0 — Desenho visual aprovado

1.0 desenha no Figma os fluxos, telas, estados, mobile e Dark do ASCII aprovado. Checkpoint:
frames e `node-id` registrados no documento, link entregue ao responsável, aprovação explícita
registrada e gate estático de 1.0 com exit 0. Nenhum código antes desse checkpoint.

### Fase 1 — Rascunho da escola com currículo e vídeo

2.0 abre a área e cria/lista cursos sob autorização e tenant; 3.0 edita e ordena módulos/aulas;
4.0 só vincula vídeo pronto após projeção, replay e reconciliação. Checkpoint: no Compose, dois
professores da escola veem o mesmo rascunho, organizam aulas sem mudar IDs e vinculam um vídeo
pronto da escola; ator sem permissão ou vídeo de outra escola é recusado.

### Fase 2 — Publicação e evolução

5.0 publica versão, fato, ato e referências; 6.0 republica, descarta e consulta versões;
7.0 exclui rascunho nunca publicado. Checkpoint: curso completo chega à versão vigente, ato
“Versão publicada” conforme na Auditoria e Referências de Uso na Mídia; a versão anterior segue
imutável após republicação. Curso publicado não oferece exclusão.

## Mapa de Entrega

| Fatia | Task | Comportamento observável | Gate focalizado | Bloqueado por |
|---|---|---|---|---|
| EN-01 | [1.0](1_task.md) | Figma desenhado, revisado e aprovado pelo responsável | Registro de aprovação do Figma no wireframe | Nenhum |
| V-01 | [2.0](2_task.md) | Professor cria e encontra cursos; leitor só lê; outro tenant não existe | Learning `CourseAccessTests` | 1.0 |
| V-02 | [3.0](3_task.md) | Professor organiza currículo por item, com ordem e IDs estáveis | Learning `CourseStructureTests` | 1.0, 2.0 |
| V-03 | [4.0](4_task.md) | Só vídeo pronto da escola pode ser vinculado; leitura degrada sem Mídia | Learning `VideoReadyProjectionTests` | 1.0, 3.0 |
| V-04 | [5.0](5_task.md) | Publicação cria versão, fato, ato conforme e referências coerentes | Learning `CoursePublicationTests` | 1.0, 4.0 |
| V-05 | [6.0](6_task.md) | Republicação conserva histórico e troca referências; descarte restaura vigente | Learning `CourseVersionTests` | 1.0, 5.0 |
| V-06 | [7.0](7_task.md) | Rascunho nunca publicado some; publicado é preservado | Learning `CourseDeletionTests` | 1.0, 2.0 |

O gate no frontmatter de cada task é o veredito focalizado; **Verificações do projeto** nela
declaram, em comandos separados, os demais testes, lint/format e build dos componentes tocados.
Nenhum teste filtrado atual é apresentado como já existente: as tasks produtoras devem criar o
cenário selecionado. `gate_expect` fixa o mínimo de cenários a produzir e observar.

### Habilitadores

| Enabler | Task | Por que não cabe numa fatia vertical | Desbloqueia |
|---|---|---|---|
| EN-01 — Figma e aprovação | [1.0](1_task.md) | O PRD e o fluxo do backoffice exigem aprovação visual antes do código; o artefato externo é comum a todas as telas e não é comportamento executável da aplicação | V-01 a V-06 (2.0–7.0) |

Não há habilitador só para banco, fila ou contrato: as migrations EF e o ambiente mínimo nascem na
fatia que primeiro os usa. O replay de vídeos antigos pertence a V-03 porque sua evidência é o
vínculo de vídeo pronto já existente. Isso evita uma task horizontal sem feedback de negócio.

## Tasks

- [x] 1.0 Desenhar Autoria no Figma e obter aprovação explícita do responsável
- [x] 2.0 Professor cria e encontra cursos da escola com autorização
- [x] 3.0 Professor organiza módulos e aulas do rascunho sem mudar identidades
- [x] 4.0 Professor vincula apenas vídeo pronto da escola
- [ ] 5.0 Professor publica versão com ato conforme e Referências de Uso
- [ ] 6.0 Professor republica, descarta alterações e consulta versões imutáveis
- [ ] 7.0 Professor exclui somente curso nunca publicado

## Caminho crítico e lanes

`1.0 → 2.0 → 3.0 → 4.0 → 5.0 → 6.0`. Depois de 2.0, 7.0 pode avançar em lane
independente; sua dependência explícita de 1.0 preserva o gate de aprovação do Figma mesmo se
as tasks forem reordenadas. O orquestrador executa sequencialmente e não marca 1.0 `done` sem a
aprovação humana e o gate com exit 0.

## Integridade dos gates e smoke

- **Figma:** o `gate` de 1.0 é verificação estática do estado de aprovação no documento. Ele só
  comprova que o registro existe; a evidência humana é a aprovação explícita do responsável após
  receber o link e revisar os frames. `node-id` de A1–A12 e estados é requisito de 1.0.
- **.NET:** `dotnet test --project <csproj> -- --filter-class <classe>
  --minimum-expected-tests N` usa Microsoft.Testing.Platform do `global.json`; menos de N
  testes reprova pelo exit code. Não usar suíte vazia nem `--filter` que só inspeciona nomes.
- **SPA:** `npm --prefix src/admin-spa test -- <fragmento-do-caminho>` seleciona arquivo Vitest
  da fatia; sem arquivo correspondente, o runner reprova. O teste de UI não substitui teste do
  cliente HTTP real do BFF com upstream controlado nem inicialização da aplicação com DI real.
- **Smoke:** cada fatia declara o cenário observável no Compose. Antes de 4.0, ligar filas e
  aplicar migrations; antes da primeira publicação, reconciliar a projeção de vídeos, instalar
  Audit 1.2.0 e Media receptora. URLs `/admin/autoria`, editor, versão e Auditoria devem abrir
  após refresh no host público. Infraestrutura compartilhada exige tenant e recursos isolados,
  conforme a [TechSpec](techspec.md).

## Artefatos compartilhados

| Artefato | Produzido/evoluído em | Consumido depois por |
|---|---|---|
| [Wireframe ASCII + Figma](../../docs/design/wireframes-autoria-curso.md) | 1.0 registra URL, `node-id` e aprovação visual | 2.0–7.0 |
| Permissão `autoria.editar` e audiência `learning` | 2.0 | 3.0–7.0 |
| Schema `content`, rascunho, `draftRevision` e migrations EF | 2.0; evolui em 3.0–7.0 | todas as fatias seguintes |
| Cliente `learning` do BFF e fronteira de testes | 2.0; evolui em 3.0–7.0 | todas as fatias seguintes |
| Projeção de vídeo pronto e replay/reconciliação | 4.0 | 5.0–6.0 |
| Outbox de `content`, tipos de publicação e ato | 5.0 | 6.0 |
| Referências de Uso de Media e versão aplicada | 5.0; prova de substituição em 6.0 | `CAP-007` futuro |
| Componentes visuais de Autoria e teste SPA | 2.0; evoluem em 3.0–7.0 | fatias seguintes |

### Arquivos existentes da TechSpec e task produtora

| Arquivo/área existente a modificar | Task(s) |
|---|---|
| `src/identity/.../StaffRoleCatalog.cs`, `docker-compose.yml`, configuração de deploy | 2.0; filas/ordem de rollout em 4.0–5.0 |
| `src/learning/.../LearningDbContext.cs`, `EndpointExtensions.cs`, `ServiceConfigurationExtensions.cs` | 2.0–7.0 conforme a jornada |
| `src/learning/.../RabbitMqTopologyInitializer.cs`, `RabbitMqOptions.cs` | 4.0; evolução em 5.0 |
| `src/learning/.../RabbitMqPublisher.cs` | 5.0 |
| `src/media/.../MediaDbContext.cs`, `RabbitMqTopologyInitializer.cs`, `DependencyInjection.cs` | 5.0 |
| `src/audit/.../AdministrativeActPolicy.cs` | 5.0 |
| `src/bff-admin/.../EndpointExtensions.cs`, `ServiceConfigurationExtensions.cs` | 2.0–7.0 conforme a jornada |
| `src/bff-admin/.../AuditRecordEndpoints.cs` | 5.0 |
| `src/admin-spa/.../paths.ts`, `router.tsx`, `get-staff-areas.ts` | 2.0; rota de versão em 6.0 |
| `src/admin-spa/.../audit-trail-screen.tsx`, `audit-record-detail-screen.tsx` | 5.0 |

Arquivos novos de domínio, endpoint, teste, fixture e migration não são enumerados; as skills
[dotnet](../../.agents/skills/dotnet/SKILL.md) e [react](../../.agents/skills/react/SKILL.md)
definem estrutura e convenções no momento da implementação. Migrations devem ser geradas por EF.

## Verificação herdada

Os workflows chamadores foram lidos junto dos reutilizáveis
[`ci-dotnet.yml@v1`](https://raw.githubusercontent.com/tassosgomes/template-pipeline/v1/.github/workflows/ci-dotnet.yml)
e [`ci-react-ts.yml@v1`](https://raw.githubusercontent.com/tassosgomes/template-pipeline/v1/.github/workflows/ci-react-ts.yml).
Cada check local deve registrar seu próprio exit code; cobertura agregada é aferida na validação
full e no CI. Estado da base: **não medido nesta criação de plano**; não há falha herdada
comprovada atribuída a qualquer fatia.

| Componente | Fonte e parâmetros do chamador | Checks e limites do CI | Estado da base | Resolução planejada |
|---|---|---|---|---|
| `src/identity` | `.github/workflows/identity.yml` → `ci-dotnet.yml@v1`; .NET 10, Debug | restore, `dotnet format --verify-no-changes`, testes MTP com cobertura ≥ 70%, publish Release, imagem | não medido | 2.0; falha herdada real bloqueia integração e recebe diagnóstico antes de atribuição |
| `src/learning` | `.github/workflows/learning.yml` → `ci-dotnet.yml@v1`; .NET 10, Debug | mesmos checks; cobertura ≥ 70% | não medido | 2.0–7.0; full mede cobertura agregada |
| `src/media` | `.github/workflows/media.yml` → `ci-dotnet.yml@v1`; .NET 10, Debug, `ubuntu-latest` | mesmos checks; cobertura ≥ 70%; fixture ffmpeg no runner | não medido | 4.0/5.0; replay e receptor têm teste com broker |
| `src/audit` | `.github/workflows/audit.yml` → `ci-dotnet.yml@v1`; .NET 10, Debug | mesmos checks; cobertura ≥ 70% | não medido | 5.0 antes da primeira publicação |
| `src/bff-admin` | `.github/workflows/bff-admin.yml` → `ci-dotnet.yml@v1`; .NET 10, Debug | mesmos checks; cobertura ≥ 70% | não medido | 2.0–7.0; adaptador real e DI testados |
| `src/admin-spa` | `.github/workflows/admin-spa.yml` → `ci-react-ts.yml@v1`; Node 24, `--base=/admin/` | `npm ci`, lint, `tsc --noEmit`, Vitest com cobertura de linhas ≥ 70%, build, imagem | não medido | 2.0–7.0; full mede cobertura agregada |
| Contratos | [contracts.md](contracts.md) | Spectral 6.15.0 e AsyncAPI 6.1.0 já validados no acordo; revalidar se alterados | aprovados; comportamento não medido | tarefas seguem schemas aprovados e provam erros no runtime |
| Design | [wireframe](../../docs/design/wireframes-autoria-curso.md) §5–6 | Sem CI de Figma; aprovação visual do responsável é obrigatória | ASCII e Figma aprovados em 2026-09-29; gate exit 0 | 1.0 concluída; bloqueio visual satisfeito |

Testes de integração/EndToEnd com Testcontainers precisam de Docker. CI de segurança roda em
paralelo; o chamador usa `security-mode: observe`, enquanto vazamento de segredo continua bloqueante
no workflow reutilizável. A validação full comprova paridade de todos os checks antes da integração.

## Cobertura

| Requisito | Task(s) |
|---|---|
| RF-01 | 2.0 |
| RF-02 | 2.0 |
| RF-03 | 3.0 |
| RF-04 | 4.0 |
| RF-05 | 2.0 |
| RF-06 | 5.0 |
| RF-07 | 6.0 |
| RF-08 | 6.0 |
| RF-09 | 7.0 |
| RF-10 | 5.0 |
| RF-11 | 5.0 |
| RF-12 | 5.0, 6.0 |
| US-01 | 2.0, 3.0 |
| US-02 | 4.0 |
| US-03 | 5.0 |
| US-04 | 6.0 |
| US-05 | 7.0 |
| US-06 | 6.0 |
| US-07 | 5.0 |
| US-08 | 2.0 |
| RN-C01 | 3.0 |
| RN-C02 | 2.0, 7.0 |
| RN-C03 | 2.0, 3.0 |
| RN-C04 | 4.0 |
| RN-C05 | 3.0, 6.0 |
| RN-C06 | 5.0, 6.0, 7.0 |
| RN-C07 | 3.0, 6.0 |
| RN-C08 | 5.0, 6.0 |
| RN-C09 | 6.0 |
| RN-C10 | 2.0 |
| RN-C11 | 5.0 |
| RN-C12 | 5.0 |
| RN-C13 | 5.0 |
| RN-C14 | 2.0 |
| RN-C17 | 6.0 |
| RN-M02 | 4.0, 5.0 |
| RN-M04 | 4.0 |
| RN-M05 | 4.0 |
| RN-M09 | 5.0, 6.0 |
| RN-M10 | 5.0 |
| RN-M15 | 1.0, 4.0 |
| RN-A02 | 5.0 |
| RN-A03 | 5.0 |
| RN-A05 | 5.0 |
| RN-A06 | 5.0 |
| RN-A08 | 5.0 |
| RN-A14 | 5.0 |
| RN-12 | 2.0 |
| RN-16 | 2.0 |
| RN-17 | 2.0 |
| RN-18 | 2.0 |
| DP-01 | 6.0 |
| DP-02 | 2.0 |
| DP-03 | 7.0 |
| DP-04 | 5.0, 6.0 |
| Experiência do Usuário — ASCII → Figma → aprovação | 1.0 |

`RN-C16` aparece no PRD apenas como encaixe futuro de avaliação no currículo; está
explicitamente fora desta entrega e será tratada com `CAP-020`.

> Gate estrutural: `python3 .agents/skills/tsg-flow-task-creator/scripts/validate_plan.py tasks/prd-autoria-curso/`.
