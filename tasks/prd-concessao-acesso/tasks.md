# Plano de Implementação — Concessão de acesso (CAP-008, 1º PRD)

> **TechSpec de origem:** [techspec.md](techspec.md) 1.0, aprovada em 2026-10-01
> **Escopo:** Full-stack (`commerce` módulo Matrícula/`Entitlement`, `identity`, `audit`, `bff-admin`, `admin-spa`; `media` e `learning` sem mudança)
> **ADRs pertinentes:** [0001](../../docs/adr/0001-monorepo-de-codigo.md), [0002](../../docs/adr/0002-plataforma-de-runtime-coolify.md), [0004](../../docs/adr/0004-autenticacao-de-servico-bff-identity.md), [0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md), [0009](../../docs/adr/0009-autenticacao-de-servico-bff-aluno-em-servicos-de-dominio.md), [0010](../../docs/adr/0010-autenticacao-de-servico-commerce-em-identity.md), [0011](../../docs/adr/0011-autenticacao-de-servico-media-learning-em-commerce.md) (**Accepted**)
> **Contratos:** [contracts.md](contracts.md) 1.1 — OpenAPI do backoffice 1.0.0, interno de `commerce` 1.2.0 e interno de Identity 1.4.0; AsyncAPI de `commerce` 1.1.0 e da Auditoria 1.4.0
> **Design:** novo documento `docs/design/wireframes-cortesias.md` (a produzir em 1.0 e 2.0), sobre o design system do backoffice
> **Status do plano:** Confirmado para implementação (aprovado pelo responsável em 2026-10-01)

## Visão Geral

Ao final, o financeiro abre a área **Cortesias**, localiza o aluno pelo e-mail, vê as concessões que ele já tem,
escolhe um curso publicado da escola (com ou sem oferta), define a vigência — por 1 a 60 meses, vendo a data do
término, ou vitalícia, com confirmação reforçada —, informa o motivo e confirma. A concessão nasce com o término
calculado no fuso da escola, junto com o fato `matricula.acesso-concedido` e o ato `cortesia-concedida`, e o
administrador a vê na trilha com o aluno, o curso, a vigência e o motivo legíveis. Qualquer serviço autorizado
pergunta "este aluno pode acessar este curso agora?" e recebe a única resposta, correta no fim da vigência **sem
rotina nem ação humana**; o fato informativo de expiração sai uma vez por concessão.

O design (ASCII → Figma → aprovação) vem antes de qualquer código de tela. Toda fatia atravessa as pontas que o
comportamento exige (serviço, BFF e SPA); nenhuma é dividida por camada.

## Fases

### Fase 0 — Design aprovado

1.0 wireframes ASCII e 2.0 Figma. Checkpoint: cabeçalho de `docs/design/wireframes-cortesias.md` com a linha
`Status` registrando `ASCII e Figma aprovados`.

### Fase 1 — Localizar o aluno e escolher o curso

3.0 permissão e localização do aluno; 4.0 visão do curso e escolha do curso. Checkpoint: em
`http://localhost:8081/admin/cortesias` o financeiro localiza uma conta de aluno e escolhe um curso publicado;
professor, suporte e administrador não veem o item.

### Fase 2 — Conceder

5.0 concessão com término, fato e ato; 6.0 concessões do aluno, aviso e confirmação reforçada. Checkpoint: o
financeiro concede 6 meses e a vitalícia, vê as concessões do aluno, é avisado antes de duplicar, e o ato aparece
como registro conforme na trilha.

### Fase 3 — Decisão, expiração e trilha

7.0 decisão de acesso; 8.0 fato de expiração; 9.0 rótulos legíveis na trilha. Checkpoint: a decisão devolve
`allowed` até o fim do último dia e `denied` depois, sem rotina; o fato de expiração sai uma vez; o administrador
vê a cortesia em `http://localhost:8081/admin/auditoria` com os rótulos.

## Mapa de Entrega

Uma linha por task. A fatia vem da TechSpec; nas fatias com tela, ela cruza SPA → BFF → serviço na mesma task.

| Fatia | Task | Comportamento observável | Gate | Bloqueado por |
|---|---|---|---|---|
| EN-01 (ASCII) | 1.0 | Wireframes ASCII aprovados | `rg` do status em `wireframes-cortesias.md` | Nenhum |
| EN-01 (Figma) | 2.0 | Figma aprovado e registrado | `rg` de `ASCII e Figma aprovados` no status | 1.0 |
| V-01 | 3.0 | Financeiro abre Cortesias e localiza a conta de aluno pelo e-mail; permissão só do financeiro | Identity `StaffRoleCatalogCourtesyPermissionTests` + `StudentAccountLookupTests` + BFF `CourtesyLookupProxyTests` + SPA `courtesy-student-lookup` | 2.0 |
| V-02 | 4.0 | Financeiro escolhe o curso entre os publicados da escola (com ou sem oferta), pela visão mínima de Entitlement | Commerce `EntitlementCourseViewTests` + `CourtesyTitleSearchTests` + `CourtesyCourseListingTests` + BFF `CourtesyCoursesProxyTests` + SPA `courtesy-course-step` | 3.0 |
| V-03 | 5.0 | Conceder cortesia (período ou vitalícia) com término correto, fato e ato na mesma transação, reconfirmação em Identity, idempotência e prévia do término | Commerce `AccessTermTests` + `CourtesyTermPreviewTests` + `CourtesyGrantTests` + `EntitlementOutboxPublishingTests` + Identity `StudentAccountConfirmationTests` + Audit `CourtesyActPolicyTests` + `CourtesyActRecordingTests` + BFF `CourtesyGrantProxyTests` + SPA `courtesy-confirm` | 4.0 |
| V-04 | 6.0 | Concessões do aluno com situação derivada, aviso de duplicidade e confirmação reforçada da vitalícia | Commerce `StudentAccessGrantsTests` + BFF `CourtesyStudentGrantsProxyTests` + SPA `courtesy-student-grants` | 5.0 |
| V-05 | 7.0 | Decisão "pode acessar agora?" por asserção de serviço, com expiração sem rotina | Commerce `AccessDecisionRulesTests` + `AccessDecisionTests` | 5.0 |
| V-06 | 8.0 | Fato informativo `acesso-expirado`, um por concessão, em até uma hora, sem a decisão depender dele | Commerce `AccessExpirationTests` | 5.0 |
| V-07 | 9.0 | Cortesia na trilha com aluno (nome), curso (título), vigência e motivo | Identity `AuditStudentReferenceTests` + BFF `CourtesyAuditReferenceTests` + SPA `audit-trail-courtesy` | 5.0, 2.0 |

EN-01 da TechSpec vira duas tasks (ASCII e Figma) porque cada aprovação é uma decisão separada do responsável e o
Figma parte do ASCII aprovado. As sete fatias seguem a TechSpec uma a uma; a fatia V-NN está na task (NN + 2).0,
porque 1.0 e 2.0 são o design.

### Habilitadores

| Enabler | Task | Por que não cabe numa fatia | Desbloqueia |
|---|---|---|---|
| EN-01 (ASCII) | 1.0 | O desenho das telas é decidido pelo responsável antes do código e vale para todas as fatias com tela | V-01 a V-04, V-07 |
| EN-01 (Figma) | 2.0 | A aprovação do Figma é decisão do responsável e vale para todas as telas de uma vez | V-01 a V-04, V-07 |

Não há habilitador técnico: cada migration nasce na fatia que usa a coluna (`dotnet ef migrations add`); a visão do
curso nasce em V-02, o modelo, o outbox, a limpeza de recibos e a chamada a Identity em V-03, o verificador da
decisão em V-05 e a rotina de expiração em V-06.

## Tasks

- [x] 1.0 Wireframes ASCII da área Cortesias aprovados
- [x] 2.0 Figma da área Cortesias aprovado
- [x] 3.0 O financeiro abre a área Cortesias e localiza o aluno pelo e-mail
- [x] 4.0 O financeiro escolhe o curso entre os publicados da escola
- [ ] 5.0 O financeiro concede a cortesia e o acesso nasce com término correto, fato e ato
- [ ] 6.0 O financeiro vê as concessões do aluno, é avisado antes de duplicar e confirma a vitalícia com reforço
- [ ] 7.0 Qualquer serviço pergunta se o aluno pode acessar o curso agora e recebe a única resposta
- [ ] 8.0 A concessão que vence gera o fato informativo, uma vez, sem que a decisão dependa dele
- [ ] 9.0 O administrador vê a cortesia na trilha, com aluno, curso, vigência e motivo legíveis

## Caminho crítico e lanes

`1.0 → 2.0 → 3.0 → 4.0 → 5.0`, e depois `6.0`, `7.0`, `8.0` e `9.0`, cada uma dependendo só de `5.0` (9.0 também do
design). A ordem numérica é a de execução: 3.0 a 9.0 tocam `EndpointExtensions.cs` e
`ServiceConfigurationExtensions.cs` de `commerce` e `bff-admin`, o `app-routes.tsx` e `handlers.ts` do SPA, então
não há lane paralela segura; 6.0 a 9.0 só dependem de 5.0 e podem ser reordenadas entre si, desde que serializadas.

**Estado intermediário conhecido:** entre 3.0 e 4.0 o formulário só tem os dois primeiros passos, e a concessão só
existe a partir de 5.0. O plano é integrado numa única entrega; nenhuma implantação intermediária acontece. Em
cada ambiente, o consumidor de versão de Entitlement (4.0) precisa existir **antes** do reenvio de `learning`
(`CatalogInitialLoad:Enabled`, de execução única, hoje desligado em todos os Compose versionados), e `audit` 1.4.0
e Identity 1.4.0 antes de `commerce` publicar o primeiro ato.

## Integridade dos gates

- .NET: `dotnet test --project <csproj> -- --filter-class <classe> --minimum-expected-tests N`
  (Microsoft.Testing.Platform: exit 8 se nada rodar, 9 se rodar menos que N).
- SPA: `npm --prefix src/admin-spa run test -- <fragmento de caminho>`. **Não** usar `-t` sozinho: o Vitest sai com
  0 quando o nome não casa; o filtro de caminho sai com 1 quando nenhum arquivo casa. Os testes de cada fatia ficam
  em arquivos cujo caminho contém o fragmento do gate, e nenhum fragmento é prefixo de arquivo de outra fatia ou
  preexistente: `courtesy-student-lookup` (3.0), `courtesy-course-step` (4.0), `courtesy-confirm` (5.0),
  `courtesy-student-grants` (6.0), `audit-trail-courtesy` (9.0). O fragmento `courtesy-` usado como verificação de
  regressão casa todos os arquivos de cortesia e por isso nunca é gate.
- Design (1.0, 2.0): verificação estática da linha `> **Status:**` do cabeçalho de
  `docs/design/wireframes-cortesias.md`, com o texto de aprovação do responsável.
- As classes de teste nomeadas nos gates são criadas na própria task; nenhuma preexiste.

## Artefatos compartilhados

| Artefato | Produzido em | Evolui em |
|---|---|---|
| `docs/design/wireframes-cortesias.md` (ASCII + frames Figma) | 1.0, 2.0 | referência de 3.0 a 6.0 e 9.0 |
| Permissão `cortesia.conceder` no papel financeiro; item, rota e feature `Cortesias` do `admin-spa`; handlers MSW | 3.0 | 4.0 a 6.0 |
| Escopo `student-account:lookup` do `bff-admin` e operação de localização em Identity | 3.0 | — |
| Schema `entitlement`, visão mínima do curso, fila e consumidor do fato de versão | 4.0 | 5.0 (curso elegível), 6.0 (título) |
| Modelo (matrícula, concessão, recibo), outbox do módulo, publicador multi-schema, fila de retenção, limpeza de recibos | 5.0 | 6.0 (leitura), 7.0 (decisão), 8.0 (marcador de expiração) |
| Função de domínio do término (fuso, mês sem o dia) | 5.0 | prévia (5.0), decisão (7.0) |
| Assinador de asserção de `commerce`, emissor `commerce` e operação de confirmação em Identity | 5.0 | — |
| `AdministrativeActPolicy` do `audit` (`cortesia-concedida`) | 5.0 | 9.0 (leitura) |
| Esquema de asserção de serviço de `commerce` com escopo `access-decision:read` | 7.0 | consumidores de `CAP-007` e `CAP-017` |
| Resolução de `conta-aluno` na trilha e atributo derivado `cursoTitulo` | 9.0 | — |

## Verificação herdada

| Componente | Fonte | Checks e limites | Estado da base | Resolução planejada |
|---|---|---|---|---|
| `src/commerce` | `.github/workflows/commerce.yml` → `tassosgomes/template-pipeline/.github/workflows/ci-dotnet.yml@v1` (Debug, cobertura 70, container, `security-mode: observe`) | restore; `dotnet format --verify-no-changes`; `dotnet test` (MTP) com cobertura ≥ 70%; `dotnet publish -c Release`; imagem `src/commerce/Dockerfile` | Não medido nesta sessão. A base entregou CAP-003 em `main` (PR #128, 2026-10-01) | Nenhuma falha herdada comprovada. Cobertura de `commerce` medida na validação full |
| `src/identity` | `.github/workflows/identity.yml` → `ci-dotnet.yml@v1` | Idem, cobertura ≥ 70% | Não medido | Nenhuma |
| `src/audit` | `.github/workflows/audit.yml` → `ci-dotnet.yml@v1` | Idem, cobertura ≥ 70% | Não medido | Nenhuma |
| `src/bff-admin` | `.github/workflows/bff-admin.yml` → `ci-dotnet.yml@v1` | Idem, cobertura ≥ 70% | Não medido | Nenhuma |
| `src/admin-spa` | `.github/workflows/admin-spa.yml` → `ci-react-ts.yml@v1` (Node 24, `--base=/admin/`) | `npm ci`; `lint`; `typecheck`; `test` (Vitest, cobertura ≥ 70%); `build`; imagem | Não medido | Cobertura medida na full |
| Contratos | `contracts.md` § Validação | Spectral 6.15.0 sem erros nem avisos nos três OpenAPI novos; AsyncAPI CLI 6.1.0 válido nos dois; 108 exemplos conformes aos schemas (2026-10-01) | Válidos segundo `contracts.md` | A validação sintática **não** comprova comportamento: as respostas e os payloads capturados nos testes de 3.0 a 9.0 são conferidos contra os schemas |

Os parâmetros dos workflows locais (`commerce.yml`, `admin-spa.yml` e os demais) foram lidos nesta sessão; os
passos de `ci-dotnet.yml@v1` e `ci-react-ts.yml@v1` listados acima vêm do plano de CAP-003 e **não foram relidos**,
porque os workflows reutilizáveis vivem em repositório externo: a paridade vale para os comandos listados e deve
ser reconfirmada na validação full. `IntegrationTests` de `commerce`, `identity`, `audit` e `bff-admin` dependem de
Docker (Testcontainers de PostgreSQL, RabbitMQ e Valkey). O smoke no Compose usa `./scripts/generate-local-env.sh`
e `./scripts/apps.sh start`, migrations aplicadas por `dotnet ef database update`, os quatro papéis de equipe, uma
conta de aluno ativa, uma não confirmada, uma desativada e uma de ator interno, um curso publicado em `learning`
com oferta e outro sem. Um teste de 5.0 resolve o fuso da escola **dentro da imagem de `commerce`** (a imagem de
runtime não foi verificada quanto a `tzdata`).

## Cobertura

| Requisito | Task(s) |
|---|---|
| RF-01 | 3.0 |
| RF-02 | 3.0, 6.0 |
| RF-03 | 4.0, 5.0, 6.0 |
| RF-04 | 4.0, 5.0, 6.0 |
| RF-05 | 5.0 |
| RF-06 | 7.0 |
| RF-07 | 5.0, 8.0 |
| RF-08 | 5.0, 9.0 |
| US-01 | 3.0, 4.0, 5.0 |
| US-02 | 6.0 |
| US-03 | 5.0 |
| US-04 | 9.0 |
| US-05 | 7.0, 8.0 |
| US-06 | 7.0 |
| RN-D01 | 7.0 |
| RN-D02 | 5.0 |
| RN-D03 | 7.0 |
| RN-D04 | 5.0 |
| RN-D06 | 6.0, 7.0, 8.0 |
| RN-D07 | 5.0, 6.0, 7.0 |
| RN-D08 | 5.0, 7.0 |
| RN-D09 | 3.0, 5.0 |
| RN-D10 | 5.0 |
| RN-D11 | 3.0, 5.0 |
| RN-D15 | 7.0 |
| RN-D17 | 3.0, 4.0, 5.0, 6.0, 7.0 |
| RN-01 | 3.0 |
| RN-12 | 3.0 |
| RN-13 | 3.0 |
| RN-16 | 3.0 |
| RN-17 | 3.0 |
| RN-18 | 3.0 |
| RN-21 | 3.0, 5.0, 9.0 |
| RN-22 | 7.0 |
| RN-23 | 3.0, 9.0 |
| RN-A02 | 5.0 |
| RN-A03 | 5.0 |
| RN-A05 | 5.0 |
| RN-A06 | 5.0 |
| RN-A08 | 5.0, 9.0 |
| RN-A14 | 5.0 |
| RN-C08 | 7.0 |
| RN-C09 | 7.0 |
| RN-C17 | 4.0 |
| RN-O08 | 5.0 |
| RN-O09 | 5.0, 7.0 |
| DP-01 | 3.0 |
| DP-02 | 3.0 |
| DP-03 | 1.0, 5.0 |
| DP-04 | 5.0 |
| DP-05 | 4.0, 5.0 |
| DP-06 | 6.0 |
| DP-07 | 6.0 |
| DP-08 | 5.0 |
| DP-09 | 7.0 |
| DP-10 | 8.0 |

> Verificado por `python3 .claude/skills/tsg-flow-task-creator/scripts/validate_plan.py tasks/prd-concessao-acesso/` antes do handoff.
