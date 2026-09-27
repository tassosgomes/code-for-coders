# Plano de Implementação — Consulta e complemento da trilha de auditoria (CAP-030, 2º PRD)

> **TechSpec de origem:** [techspec.md](techspec.md) 1.1, aprovada em 2026-09-27 (revisão 1.1: nomes de autor/alvo também na lista, decisão 2 do wireframe aprovada pelo responsável)
> **Escopo:** Full-stack (`audit`, `bff-admin`, `identity`, `admin-spa`)
> **ADRs pertinentes:** [0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md), [0007](../../docs/adr/0007-snapshots-efemeros-da-consulta-de-auditoria.md)
> **Contratos:** [contracts.md](contracts.md) 1.1 — OpenAPI BFF 1.1.0, OpenAPI Audit 1.1.0, OpenAPI Identity 1.0.0, AsyncAPI 1.1.0
> **Design:** [wireframes-auditoria.md](../../docs/design/wireframes-auditoria.md) (fluxo de telas, §7 com node-ids) · [Figma — Screens — Auditoria](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=118-7460)
> **Status do plano:** Confirmado para implementação (aprovado pelo responsável em 2026-09-27)

## Visão Geral

Ao final, o administrador abre **Auditoria** no backoffice, filtra os atos de gestão de acesso por
período, tipo, pessoa e conformidade, percorre páginas estáveis mesmo com atos retroativos chegando,
abre o detalhe que separa o fato recebido (com faltas e referências) do contexto acrescentado depois,
e confirma uma explicação que reaparece como complemento imutável. Nenhum outro papel, nenhum outro
tenant e nenhuma sessão revogada lê ou complementa a trilha, e o original nunca muda.

O design (ASCII → Figma → aprovação) vem antes de qualquer código, para que as telas nasçam no
desenho aprovado.

## Fases

### Fase 0 — Design aprovado

1.0 ASCII e 2.0 Figma, ambos aprovados em 2026-09-27. Checkpoint: cabeçalho do
documento de wireframes com `aprovado (ASCII e Figma)`.

### Fase 1 — Investigação

3.0 lista com filtros, paginação estável, nomes e autorização nas três camadas; 4.0 detalhe com
faltas, referências e volta à lista filtrada pela pessoa. Checkpoint: em
`http://localhost:8081/admin/auditoria`, o administrador localiza e examina uma concessão e um ato não
conforme; um professor cai em B12.

### Fase 2 — Complemento

5.0 aceite durável, idempotente e cifrado da confirmação; 6.0 publicação, consumo e exibição do
complemento. Checkpoint: explicação confirmada vira complemento na linha do tempo, sem recarregar, e
o original é byte a byte igual.

## Mapa de Entrega

| Fatia | Task | Comportamento observável | Gate | Bloqueado por |
|---|---|---|---|---|
| EN-01 | 1.0 | Wireframe ASCII aprovado | `grep` do status em `wireframes-auditoria.md` | Nenhum |
| EN-02 | 2.0 | Figma aprovado e registrado | `grep` de `aprovado (ASCII e Figma)` | 1.0 |
| V-01 | 3.0 | Administrador filtra e percorre a trilha com nomes; outros papéis/tenants barrados | Identity `AuditReferenceAccessTests` + Audit `AuditRecordSearchTests` + BFF `AuditRecordSearchTests` + SPA `audit-trail-list` | 2.0 |
| V-02 | 4.0 | Detalhe com dois momentos, faltas, referências e 404 neutro; lista filtrada pela pessoa | Audit `AuditRecordDetailTests` + BFF `AuditRecordDetailTests` + SPA `audit-record-detail` | 3.0 |
| V-03 | 5.0 | Confirmação aceita uma vez (`202`), cifrada no outbox; UI aguarda sem declarar falha | BFF `AuditComplementConfirmationTests` (integração e ponta a ponta) + SPA `audit-complement-confirmation` | 4.0 |
| V-03 | 6.0 | Fato publicado e consumido vira complemento imutável visível | BFF `AuditComplementPublicationTests` + Audit `AuditComplementRecordingTests` + SPA `audit-complement-recorded` | 5.0 |

V-03 da TechSpec é entregue em duas tasks por comportamento, não por camada: 5.0 prova o aceite
durável e idempotente ponta a ponta (SPA → BFF → banco), terminando no estado A4.c que o próprio
design prevê; 6.0 prova a entrega do fato e o registro imutável (outbox → broker → `audit` → SPA).
Cada uma compila e tem gate próprio se a seguinte nunca for executada.

### Habilitadores

| Enabler | Task | Por que não cabe numa fatia | Desbloqueia |
|---|---|---|---|
| EN-01 | 1.0 | O desenho das telas é decidido pelo usuário antes do código (fluxo de design do backoffice); já concluído | V-01 a V-03 |
| EN-02 | 2.0 | A aprovação do Figma é decisão do usuário e vale para todas as telas de uma vez | V-01 a V-03 |

A TechSpec declara que não há habilitador técnico: JWT, Valkey e lookup de Identity entram em 3.0;
proteção do outbox em 5.0; mensageria em 6.0; migrations EF nas fatias que as exigem.

## Tasks

- [x] 1.0 Wireframe ASCII da área Auditoria aprovado
- [x] 2.0 Telas da área Auditoria no Figma aprovadas
- [ ] 3.0 Administrador abre a trilha, filtra e percorre páginas estáveis; outros papéis e tenants são barrados
- [ ] 4.0 Administrador examina o fato recebido, suas faltas e referências, e volta à lista filtrada pela pessoa
- [ ] 5.0 Administrador confirma uma explicação e o BFF a aceita uma única vez, de forma durável e protegida
- [ ] 6.0 A confirmação aceita vira complemento imutável e aparece ao lado do original

## Caminho crítico e lanes

`1.0 → 2.0 → 3.0 → 4.0 → 5.0 → 6.0`, sequencial. 4.0 precisa da autorização, do JWT `audit` e do
lookup de Identity de 3.0; 5.0 usa o lookup do original de 4.0; 6.0 consome as mensagens de 5.0.

## Integridade dos gates

- .NET: `dotnet test --project <csproj> -- --filter-class <classe> --minimum-expected-tests N`
  (Microsoft.Testing.Platform: exit 8 se nada rodar, 9 se rodar menos que N).
- SPA: `npm --prefix src/admin-spa run test -- <fragmento de caminho>`. **Não** usar `-t` sozinho: o
  Vitest sai com 0 quando o nome não casa (verificado em 2026-09-26 em CAP-006); o filtro de caminho
  sai com 1 quando nenhum arquivo casa. Os testes de cada fatia ficam em arquivos cujo caminho contém
  o fragmento do gate.
- Design (1.0, 2.0): verificação estática do registro de aprovação no documento de wireframes.

## Artefatos compartilhados

| Artefato | Produzido em | Evolui em |
|---|---|---|
| `docs/design/wireframes-auditoria.md` (ASCII + node-ids Figma) | 1.0, 2.0 | referência de 3.0–6.0 |
| Validação JWT `audit`, audiência e escopo em Identity | 3.0 | 4.0 |
| `resolveAuditIdentityReferencesInternal` e cliente no BFF | 3.0 | 4.0, 6.0 (autor do complemento) |
| Snapshot Valkey de `audit` | 3.0 | — |
| Registro complementar em `audit` (migrations EF) | 4.0 (tipo, FK, tenant) | 6.0 (unicidade `tenantId` + `confirmationId`) |
| Idempotência e outbox cifrado do BFF (migration EF) | 5.0 | 6.0 (lease e destino por mensagem) |
| `src/admin-spa/src/testing/handlers.ts` | 3.0 | 4.0, 5.0, 6.0 |

## Verificação herdada

| Componente | Fonte | Checks e limites | Estado da base | Resolução planejada |
|---|---|---|---|---|
| `src/audit` | `.github/workflows/audit.yml` → `tassosgomes/template-pipeline/.github/workflows/ci-dotnet.yml@v1` (Debug, cobertura 70, container) | `dotnet restore`; `dotnet format --verify-no-changes`; `dotnet test` (MTP) com cobertura ≥ 70%; `dotnet publish -c Release`; imagem `src/audit/Dockerfile` | Não medido nesta sessão; 1º PRD de CAP-030 integrado | Nenhuma falha herdada conhecida. Não há Valkey nos testes de `audit`: 3.0 o traz com Testcontainers |
| `src/bff-admin` | `.github/workflows/bff-admin.yml` → `ci-dotnet.yml@v1` | Idem, cobertura ≥ 70% | Não medido; CAP-006 integrado (PR #68) | Nenhuma |
| `src/identity` | `.github/workflows/identity.yml` → `ci-dotnet.yml@v1` | Idem, cobertura ≥ 70% | Não medido; CAP-006 integrado | Nenhuma |
| `src/admin-spa` | `.github/workflows/admin-spa.yml` → `ci-react-ts.yml@v1` | `lint`, `typecheck`, `test` (Vitest, cobertura ≥ 70%), `build --base=/admin/`, imagem | Não medido | Cobertura medida na full |
| Contratos | `contracts.md` § Validação | Spectral 6.15.0 com ruleset local, `--fail-severity=error`; AsyncAPI CLI 6.1.0 | Os quatro documentos válidos, 0 erros (2026-09-27) | Revisão 1.1 da TechSpec não altera contratos |

O reutilizável `ci-dotnet.yml@v1` foi lido em CAP-006 (2026-09-26), não nesta sessão; paridade exata
com o CI é confirmada na validação full. `IntegrationTests` e `EndToEndTests` dependem de Docker
(Testcontainers), disponível no ambiente.

## Cobertura

| Requisito | Task(s) |
|---|---|
| RF-01 | 3.0, 4.0, 5.0 |
| RF-02 | 3.0 |
| RF-03 | 4.0 |
| RF-04 | 5.0, 6.0 |
| RF-05 | 3.0, 4.0, 6.0 |
| US-01 | 3.0 |
| US-02 | 4.0 |
| US-03 | 5.0, 6.0 |
| US-04 | 3.0, 4.0, 5.0 |
| RN-A01 | 4.0, 6.0 |
| RN-A02 | 5.0, 6.0 |
| RN-A03 | 5.0, 6.0 |
| RN-A04 | 3.0, 4.0 |
| RN-A05 | 4.0 |
| RN-A06 | 3.0, 4.0 |
| RN-A07 | 6.0 |
| RN-A08 | 3.0, 4.0 |
| RN-A09 | 3.0, 4.0, 5.0 |
| RN-A10 | 3.0, 4.0 |
| RN-A11 | 4.0 |
| RN-A12 | 3.0 |
| RN-A13 | 3.0, 4.0, 5.0, 6.0 |
| RN-A14 | 3.0 |
| RN-13 | 3.0 |
| RN-14 | 3.0 |
| RN-16 | 3.0 |
| RN-17 | 3.0 |
| RN-18 | 3.0 |
| RN-19 | 3.0, 4.0 |
| RN-20 | 3.0, 4.0 |
| RN-23 | 3.0, 4.0 |
| RN-24 | 3.0, 4.0 |
| RN-25 | 3.0 |
| DP-01 | 3.0 |
| DP-02 | 3.0 |
| DP-03 | 6.0 |
| DP-04 | 5.0, 6.0 |
| DP-05 | 5.0, 6.0 |

`RN-A*` são do domain doc de Auditoria v1.2; `RN-13` a `RN-25` são de Identidade v1.1. Experiência do
Usuário do PRD (rótulos dos dois momentos, marcação de ausentes, teclado) entra no design de 1.0/2.0 e
nas telas de 3.0–6.0. Segurança: autorização na borda e no serviço em 3.0/4.0, CSRF e texto livre fora
de telemetria em 5.0. Observabilidade específica (idade do outbox, DLQ, snapshots, tempo até
visibilidade) acompanha as fatias que criam cada fluxo e é conferida na validação full. Migração de
dados: não há; atos anteriores à trilha não são reconstruídos (Não-Objetivos).

> Verificado por `python3 .claude/skills/tsg-flow-task-creator/scripts/validate_plan.py tasks/prd-consulta-trilha-auditoria/` antes do handoff.
