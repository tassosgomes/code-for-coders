# Plano de Implementação — Vitrine e oferta de curso (CAP-003, 1º PRD)

> **TechSpec de origem:** [techspec.md](techspec.md) 1.0, aprovada em 2026-09-30 (promovida de `techspec.draft.md` na geração deste plano)
> **Escopo:** Full-stack (`commerce` módulo Catálogo, `bff-admin`, `admin-spa`, `bff-student`, `student-spa`; mudanças pontuais em `identity` e `audit`)
> **ADRs pertinentes:** [0001](../../docs/adr/0001-monorepo-de-codigo.md), [0002](../../docs/adr/0002-plataforma-de-runtime-coolify.md), [0004](../../docs/adr/0004-autenticacao-de-servico-bff-identity.md), [0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md), [0009](../../docs/adr/0009-autenticacao-de-servico-bff-aluno-em-servicos-de-dominio.md) (**Accepted**)
> **Contratos:** [contracts.md](contracts.md) 1.0 — OpenAPI do backoffice 1.0.0, da vitrine 1.0.0 e interno de `commerce` 1.1.0; AsyncAPI de `commerce` 1.0.0 e da Auditoria 1.3.0
> **Design:** novo documento `docs/design/wireframes-catalogo-vitrine.md` (a produzir em 1.0 e 2.0), sobre `DESIGN.md` e o design system do backoffice
> **Status do plano:** Concluído

## Visão Geral

Ao final, o financeiro abre a área **Catálogo**, vê os cursos publicados da escola, escreve a chamada
comercial, cria ofertas com preço e vigência (por período ou vitalícia), publica, corrige preço ou vigência
para compras futuras e despublica — e cada ato visível ao público tem autor na trilha, com rótulo legível
para o administrador. O visitante sem conta abre a vitrine, filtra por nível, lê a página do curso com
pré-requisito, estrutura, preço e vigência explícita, clica em *Comprar*, vê o aviso de compra em breve, e a
escola ganha um primeiro sinal de demanda por oferta sem identificar ninguém.

O design (ASCII → Figma → aprovação) vem antes de qualquer código de tela. Toda fatia atravessa as pontas que
o comportamento exige (serviço, BFF e SPA); nenhuma é dividida por camada.

## Fases

### Fase 0 — Design aprovado

1.0 wireframes ASCII e 2.0 Figma. Checkpoint: cabeçalho de `docs/design/wireframes-catalogo-vitrine.md` com a
linha `Status` registrando `ASCII e Figma aprovados`.

### Fase 1 — Backoffice: ficha e ofertas em rascunho

3.0 Catálogo e lista de cursos publicados; 4.0 ficha e chamada comercial; 5.0 ofertas em rascunho.
Checkpoint: em `http://localhost:8081/admin/catalogo` o financeiro vê os cursos da escola, abre a ficha e
mantém duas ofertas em rascunho; professor, suporte e administrador não veem o item.

### Fase 2 — Publicação e trilha

6.0 publicar com fato e ato no outbox do Catálogo; 7.0 alterar preço ou vigência; 8.0 despublicar; 12.0
rótulos na consulta da trilha. Checkpoint: publicar, alterar e despublicar geram fato e ato conformes, e o
administrador os vê em `http://localhost:8081/admin/auditoria` com autor e rótulo.

### Fase 3 — Área pública

9.0 vitrine com filtro por nível e asserção de serviço; 10.0 página do curso; 11.0 clique em *Comprar* com
contagem anônima. Checkpoint: em `http://localhost:8082/student/cursos` o visitante sem sessão filtra,
abre a página, clica em *Comprar* e a ficha do backoffice mostra a contagem.

## Mapa de Entrega

Uma linha por task. A fatia vem da TechSpec; nas fatias com tela, ela cruza SPA → BFF → serviço na mesma task.

| Fatia | Task | Comportamento observável | Gate | Bloqueado por |
|---|---|---|---|---|
| EN-01 (ASCII) | 1.0 | Wireframes ASCII aprovados | `rg` do status em `wireframes-catalogo-vitrine.md` | Nenhum |
| EN-01 (Figma) | 2.0 | Figma aprovado e registrado | `rg` de `ASCII e Figma aprovados` no status | 1.0 |
| V-01 | 3.0 | Financeiro abre o Catálogo e vê só os cursos publicados da escola; fato de versão aplicado com regra 1.0.0/1.1.0 | Identity `StaffRoleCatalogOfferPermissionTests` + Commerce `CatalogCourseViewTests` + `CatalogCourseListingTests` + BFF `CatalogCoursesProxyTests` + SPA `catalog-courses` | 2.0 |
| V-02 | 4.0 | Ficha do curso e chamada comercial de 1 a 160 caracteres, com idempotência | Commerce `CatalogTaglineTests` + `CatalogCourseRecordTests` + BFF `CatalogCourseRecordProxyTests` + SPA `catalog-course-record` | 3.0 |
| V-03 | 5.0 | Criar, editar e excluir ofertas em rascunho; limites; teto de 50 | Commerce `OfferRulesTests` + `OfferDraftTests` + BFF `OfferDraftProxyTests` + SPA `catalog-offer-form` | 4.0 |
| V-04 | 6.0 | Publicar oferta com fato e ato na mesma transação; `audit` aceita os atos | Commerce `OfferPublicationRulesTests` + `OfferPublicationTests` + Audit `OfferActPolicyTests` + BFF `OfferPublicationProxyTests` + SPA `catalog-offer-publication` | 5.0 |
| V-05 | 7.0 | Alterar preço ou vigência de oferta publicada, com ato dos pares; só nome em silêncio | Commerce `OfferChangeRulesTests` + `OfferChangeTests` + Audit `OfferChangedActPolicyTests` + SPA `catalog-offer-price-change` | 6.0 |
| V-06 | 8.0 | Despublicar oferta; curso sai da vitrine sem afetar as ofertas | Commerce `OfferUnpublishRulesTests` + `OfferUnpublishTests` + BFF `OfferUnpublishProxyTests` + SPA `catalog-offer-unpublish` | 6.0 |
| V-07 | 9.0 | Vitrine pública anônima com filtro por nível e asserção de serviço para `commerce` | Commerce `ServiceAssertionVerifierTests` + `ShowcaseListingTests` + BFF do aluno `ShowcaseAnonymousRouteTests` + SPA `student-showcase` | 6.0, 2.0 |
| V-08 | 10.0 | Página pública do curso com pré-requisito, estrutura, preço e vigência explícita | Commerce `ShowcaseCourseDetailTests` + BFF do aluno `ShowcaseCourseProxyTests` + SPA `student-course-page` | 9.0 |
| V-09 | 11.0 | Clique em *Comprar* com aviso sempre exibido e contagem diária anônima | Commerce `PurchaseIntentTests` + BFF do aluno `PurchaseIntentRateLimitTests` + SPA `student-purchase-intent` + SPA admin `catalog-purchase-intents` | 10.0 |
| V-10 | 12.0 | Atos de oferta na trilha com rótulo "curso — opção" e formatação dos atributos | Commerce `OfferReferenceResolutionTests` + BFF `OfferAuditReferenceTests` + SPA `audit-trail-offer` | 8.0 |

EN-01 da TechSpec vira duas tasks (ASCII e Figma) porque cada aprovação é uma decisão separada do
responsável e o Figma parte do ASCII aprovado. As dez fatias seguem a TechSpec uma a uma; a fatia V-NN está
na task (NN + 2).0, porque 1.0 e 2.0 são o design.

### Habilitadores

| Enabler | Task | Por que não cabe numa fatia | Desbloqueia |
|---|---|---|---|
| EN-01 (ASCII) | 1.0 | O desenho das telas é decidido pelo responsável antes do código, para o backoffice e para a área pública, e vale para todas as fatias com tela | V-01 a V-10 |
| EN-01 (Figma) | 2.0 | A aprovação do Figma é decisão do responsável e vale para todas as telas de uma vez | V-01 a V-10 |

Não há habilitador técnico: cada migration nasce na fatia que usa a coluna (`dotnet ef migrations add`); o
outbox do Catálogo nasce em V-04, a asserção de serviço em V-07 e o contador em V-09.

## Tasks

- [x] 1.0 Wireframes ASCII do Catálogo e da vitrine aprovados
- [x] 2.0 Figma do Catálogo e da vitrine aprovado
- [x] 3.0 O financeiro abre o Catálogo e vê os cursos publicados da escola
- [x] 4.0 O financeiro abre a ficha do curso e escreve a chamada comercial
- [x] 5.0 O financeiro cria, edita e exclui ofertas em rascunho
- [x] 6.0 O financeiro publica a oferta e ela nasce com autor na trilha
- [x] 7.0 O financeiro altera preço ou vigência de oferta publicada, só para compras futuras
- [x] 8.0 O financeiro despublica a oferta: ela sai da vitrine sem afetar quem comprou
- [x] 9.0 O visitante abre a vitrine, filtra por nível e só vê o que está à venda
- [x] 10.0 O visitante lê a página do curso e entende o que compra
- [x] 11.0 O visitante clica em Comprar, vê o aviso e a escola tem um primeiro sinal de demanda
- [ ] 12.0 O administrador vê os atos de oferta na trilha com rótulo legível

## Caminho crítico e lanes

`1.0 → 2.0 → 3.0 → 4.0 → 5.0 → 6.0 → 7.0 / 8.0`, e `6.0 → 9.0 → 10.0 → 11.0`, com `12.0` depois de `8.0`.
A ordem numérica é a de execução: 3.0 a 12.0 tocam o mesmo agregado e `EndpointExtensions.cs` e
`ServiceConfigurationExtensions.cs` de `commerce`, então não há lane paralela segura. 7.0, 8.0 e 9.0
dependem só de 6.0 (9.0 também do design), mas compartilham esses arquivos; o plano as serializa.

**Estado intermediário conhecido:** entre 3.0 e 8.0 nenhum curso aparece na área pública e, enquanto
`learning` não publicar o fato 1.1.0 (CAP-005, outro PRD), nenhum curso tem nível real. A evidência de 6.0 a
12.0 usa fatos conformes a `VersaoPublicadaPayload` 1.1.0 publicados no exchange de `learning` (ou de
`learning` 1.1.0, quando existir) e deve ser reexecutada com `learning` 1.1.0 real. O plano é integrado numa
única entrega; nenhuma implantação intermediária acontece.

## Integridade dos gates

- .NET: `dotnet test --project <csproj> -- --filter-class <classe> --minimum-expected-tests N`
  (Microsoft.Testing.Platform: exit 8 se nada rodar, 9 se rodar menos que N).
- SPA: `npm --prefix src/<spa> run test -- <fragmento de caminho>`. **Não** usar `-t` sozinho: o Vitest sai
  com 0 quando o nome não casa; o filtro de caminho sai com 1 quando nenhum arquivo casa. Os testes de cada
  fatia ficam em arquivos cujo caminho contém o fragmento do gate, e nenhum fragmento é prefixo de arquivo de
  outra fatia ou preexistente: `catalog-courses` (3.0), `catalog-course-record` (4.0), `catalog-offer-form`
  (5.0), `catalog-offer-publication` (6.0), `catalog-offer-price-change` (7.0), `catalog-offer-unpublish`
  (8.0), `catalog-purchase-intents` (11.0), `audit-trail-offer` (12.0); no `student-spa`:
  `student-showcase` (9.0), `student-course-page` (10.0), `student-purchase-intent` (11.0). O fragmento
  `catalog-` usado como verificação de regressão casa todos os arquivos do Catálogo e por isso nunca é gate.
- Design (1.0, 2.0): verificação estática da linha `> **Status:**` do cabeçalho de
  `docs/design/wireframes-catalogo-vitrine.md`, com o texto de aprovação do responsável.
- As classes de teste nomeadas nos gates são criadas ou estendidas na própria task; nenhuma preexiste.

## Artefatos compartilhados

| Artefato | Produzido em | Evolui em |
|---|---|---|
| `docs/design/wireframes-catalogo-vitrine.md` (ASCII + frames Figma) | 1.0, 2.0 | referência de 3.0 a 12.0 |
| Visão do curso e consumidor do fato `conteudo.versao-publicada.v1` (`catalog.*`, migrations EF) | 3.0 | lida por 4.0, 6.0, 9.0, 10.0 |
| Predicado único de elegibilidade de vitrine e `in_showcase_since` | 3.0 | recalculado em 6.0, 8.0; lido em 9.0, 10.0 |
| Ficha (`tagline`) e recibos de idempotência do backoffice | 4.0 | reutilizados por 5.0 a 8.0 |
| Ofertas (`catalog.*`, migration EF) | 5.0 | 6.0 a 8.0 (estado), 9.0 a 11.0 (leitura pública) |
| Outbox do Catálogo (`catalog.outbox_messages`), publicador multi-schema, fila de retenção, `correlationId` | 6.0 | 7.0, 8.0 |
| `AdministrativeActPolicy` do `audit` (três tipos de ato) | 6.0 | 7.0 (complemento), 12.0 (leitura) |
| Segundo esquema de autenticação de `commerce`, verificador de asserção, fábrica por destino no `bff-student`, par de chaves | 9.0 | 10.0, 11.0 |
| Contador diário e recibo de 24 h do clique (migration EF) | 11.0 | lido por 4.0 (`getCatalogCourse`) |
| Tipos e schemas zod do Catálogo no `admin-spa`; handlers MSW | 3.0 | 4.0 a 8.0, 11.0, 12.0 |
| Tipos e schemas zod da vitrine no `student-spa`; handlers MSW | 9.0 | 10.0, 11.0 |

## Verificação herdada

| Componente | Fonte | Checks e limites | Estado da base | Resolução planejada |
|---|---|---|---|---|
| `src/commerce` | `.github/workflows/commerce.yml` → `tassosgomes/template-pipeline/.github/workflows/ci-dotnet.yml@v1` (Debug, cobertura 70, container, `security-mode: observe`) | restore; `dotnet format --verify-no-changes`; `dotnet test` (MTP) com cobertura ≥ 70%; `dotnet publish -c Release`; imagem `src/commerce/Dockerfile` | Não medido nesta sessão. CI verde em `main` @ `235991c` (2026-09-30) segundo o plano de CAP-005; os commits posteriores só mudam documentos | Nenhuma falha herdada comprovada. Cobertura de `commerce` (módulo quase sem código de negócio hoje) medida na validação full |
| `src/identity` | `.github/workflows/identity.yml` → `ci-dotnet.yml@v1` | Idem, cobertura ≥ 70% | Não medido; verde @ `235991c` | Nenhuma |
| `src/audit` | `.github/workflows/audit.yml` → `ci-dotnet.yml@v1` | Idem, cobertura ≥ 70% | Não medido; verde @ `235991c` | Nenhuma |
| `src/bff-admin` | `.github/workflows/bff-admin.yml` → `ci-dotnet.yml@v1` | Idem, cobertura ≥ 70% | Não medido; verde @ `235991c` | Nenhuma |
| `src/bff-student` | `.github/workflows/bff-student.yml` → `ci-dotnet.yml@v1` | Idem, cobertura ≥ 70% | Não medido; verde @ `235991c` | Nenhuma |
| `src/admin-spa` | `.github/workflows/admin-spa.yml` → `ci-react-ts.yml@v1` (Node 24, `--base=/admin/`) | `npm ci`; `lint`; `typecheck`; `test` (Vitest, cobertura ≥ 70%); `build`; imagem | Não medido; verde @ `235991c` | Cobertura medida na full |
| `src/student-spa` | `.github/workflows/student-spa.yml` → `ci-react-ts.yml@v1` (Node 24, `--base=/student/`) | Idem | Não medido; verde @ `235991c` | Cobertura medida na full |
| Contratos | `contracts.md` § Validação | Validação sintática dos OpenAPI e AsyncAPI, sem erros (2026-09-30) | Válidos segundo `contracts.md` | A validação sintática **não** comprova comportamento: os payloads capturados nos testes de 6.0 a 8.0 e as respostas de 3.0 a 12.0 são conferidos contra os schemas |

Os passos de `ci-dotnet.yml@v1` e `ci-react-ts.yml@v1` listados acima foram copiados da leitura registrada em
[`prd-nivel-prerequisito-curso/tasks.md`](../prd-nivel-prerequisito-curso/tasks.md); os workflows reutilizáveis
vivem em repositório externo a este e **não foram relidos** nesta sessão, então a paridade com o CI vale para
os comandos listados e deve ser reconfirmada na validação full. `IntegrationTests` de `commerce`, `audit`,
`bff-admin` e `bff-student` dependem de Docker (Testcontainers de PostgreSQL, RabbitMQ e Valkey). O smoke no
Compose usa `./scripts/generate-local-env.sh` e `./scripts/apps.sh start`, migrations aplicadas por
`dotnet ef database update`, os quatro papéis de equipe, dois tenants e um curso de cada nível.

## Cobertura

| Requisito | Task(s) |
|---|---|
| RF-01 | 3.0 |
| RF-02 | 3.0, 4.0 |
| RF-03 | 5.0 |
| RF-04 | 6.0 |
| RF-05 | 7.0 |
| RF-06 | 8.0 |
| RF-07 | 9.0 |
| RF-08 | 10.0 |
| RF-09 | 11.0 |
| RF-10 | 12.0 |
| US-01 | 3.0, 5.0 |
| US-02 | 5.0 |
| US-03 | 7.0 |
| US-04 | 8.0 |
| US-05 | 9.0 |
| US-06 | 10.0 |
| US-07 | 10.0 |
| US-08 | 12.0 |
| RN-O01 | 3.0, 5.0 |
| RN-O02 | 4.0, 10.0 |
| RN-O03 | 4.0, 6.0, 9.0 |
| RN-O04 | 5.0, 9.0 |
| RN-O05 | 9.0, 10.0, 11.0 |
| RN-O06 | 1.0, 4.0, 10.0 |
| RN-O07 | 5.0 |
| RN-O08 | 5.0, 10.0 |
| RN-O09 | 7.0 |
| RN-O10 | 7.0 |
| RN-O11 | 5.0, 8.0 |
| RN-O12 | 8.0, 9.0 |
| RN-O13 | 9.0, 10.0 |
| RN-O14 | 3.0 |
| RN-O15 | 6.0, 7.0, 8.0 |
| RN-O18 | 3.0, 6.0, 9.0, 10.0 |
| RN-C17 | 3.0 |
| RN-C18 | 9.0, 10.0 |
| RN-D03 | 5.0, 7.0 |
| RN-D04 | 5.0, 10.0 |
| RN-A05 | 6.0 |
| RN-A06 | 6.0 |
| RN-A08 | 6.0 |
| RN-A14 | 6.0, 12.0 |
| RN-12 | 3.0 |
| RN-16 | 3.0 |
| RN-17 | 3.0 |
| RN-18 | 3.0 |
| DP-01 | 3.0 |
| DP-02 | 11.0 |
| DP-03 | 4.0, 5.0 |
| DP-04 | 3.0, 8.0, 9.0 |
| DP-05 | 6.0, 7.0 |

`US-01` a `US-08` seguem a ordem das histórias de usuário do PRD. `RN-O*` são de Catálogo e Oferta v1.1,
`RN-C*` de Conteúdo e Currículo v1.1, `RN-D*` de Matrícula, `RN-A*` de Auditoria e `RN-12`/`RN-16`/`RN-17`/
`RN-18` de Identidade e Acesso. Decisões de contrato: C-01 em 3.0; C-02 em 4.0 e 10.0; C-03 em 10.0; C-04 em
9.0; C-05 em 5.0; C-06 em 4.0; C-07 em 9.0 e 10.0; C-08 em 11.0; C-09 em 6.0, 7.0, 8.0 e 12.0; C-11 em 12.0;
C-12 em 6.0.

Categorias transversais: **segurança** — autorização por `oferta.editar` e isolamento por tenant em 3.0 a
8.0, asserção de serviço e isolamento de escola em 9.0 a 11.0, texto comercial e dado de visitante fora de log,
span e métrica em 3.0, 9.0 e 11.0; **observabilidade** — fato aplicado, ignorado e em DLQ em 3.0, saúde do
outbox em 6.0, leituras públicas em 9.0, cliques em 11.0; **migração de dados** — migrations EF das fatias,
sem backfill: a carga inicial dos cursos já publicados depende do reenvio de `learning` (CAP-005), fora deste
PRD. Mudanças em `docs/adr/index.md` já constam do rascunho da ADR-0009 e não têm task.

> Verificado por `python3 .claude/skills/tsg-flow-task-creator/scripts/validate_plan.py tasks/prd-vitrine-oferta/` antes do handoff.
