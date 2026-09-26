# Plano de Implementação — Acesso interno por papel e permissão (CAP-002)

> **TechSpec de origem:** [techspec.md](techspec.md), aprovada em 2026-09-25
> **Escopo:** Full-stack (`identity`, `bff-admin`, `admin-spa`, `notification`, `commerce`)
> **ADRs pertinentes:** [0003](../../docs/adr/0003-verificacao-de-sessao-do-aluno.md), [0004](../../docs/adr/0004-autenticacao-de-servico-bff-identity.md), [0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md)
> **Status do plano:** Em revisão

## Visão Geral

Ao final, o primeiro administrador provisionado define a senha, entra no backoffice, convida atores
internos, que aceitam e entram; o administrador concede, revoga e troca papéis, e a revogação desconecta
na próxima ação; o professor não abre a área financeira nem pela borda nem direto no Commerce; e todo
convite, aceite, concessão e revogação chega à Auditoria como registro conforme. Contratos em
[contracts.md](contracts.md) 1.1.

## Fases

### Fase 1 — Porta do backoffice

1.0 abre a credencial de serviço do `bff-admin`; 2.0 cria o primeiro administrador e a redefinição de
senha; 3.0 entrega entrada, sessão validada por ação, menu por permissão e saída. Checkpoint: o
administrador do seed entra em `http://localhost:8081/admin/` e vê "Acessos".

### Fase 2 — Delegar acesso com trilha

4.0 convite (com Notificação 1.1.0 e o primeiro ato na Auditoria), 5.0 aceite, 6.0 conceder/revogar,
7.0 trocar papel. Checkpoint: convite → aceite → troca de papel, com a trilha mostrando cada ato.

### Fase 3 — Menor privilégio e autonomia

8.0 (área financeira com recusa na borda e no Commerce) e 9.0 (recuperação de senha) partem de 3.0 e
não dependem de 4.0–7.0. Checkpoint: professor recusado nas três vias; ator recupera a senha sozinho.

## Mapa de Entrega

| Fatia | Task | Comportamento observável | Gate | Bloqueado por |
|---|---|---|---|---|
| V-01 | [2.0](2_task.md) | Seed cria o administrador, que define a senha pelo link | Identity `FirstAdministratorProvisioningTests`, `StaffPasswordResetTests`; BFF `StaffPasswordResetTests`; SPA `StaffPasswordReset` | 1.0 |
| V-02 | [3.0](3_task.md) | Entrada indistinguível, sessão validada por ação, menu por permissão, saída | Identity/BFF `StaffSessionTests`; SPA `StaffSession` | 2.0 |
| V-03 | [4.0](4_task.md) | Convite emitido, e-mail entregue, ato conforme na trilha | Identity/BFF `StaffInvitationIssuingTests`; Notificação `StaffInvitationEmailTests`; SPA | 3.0 |
| V-04 | [5.0](5_task.md) | Aceite cria conta e sessão; link não reutilizável | Identity/BFF `StaffInvitationAcceptanceTests`; SPA | 4.0 |
| V-05 | [6.0](6_task.md) | Conceder e revogar com motivo; revogação desconecta | Identity/BFF `StaffRoleGrantRevokeTests`; SPA `StaffMembers` | 5.0 |
| V-06 | [7.0](7_task.md) | Troca atômica com dois atos | Identity/BFF `StaffRoleChangeTests`; SPA | 6.0 |
| V-07 | [8.0](8_task.md) | Professor recusado na borda e no Commerce; financeiro entra | Commerce `FinanceAreaAuthorizationTests`; Identity `UserTokenSigningKeysTests`; BFF `FinanceAreaTests`; SPA | 3.0 |
| V-08 | [9.0](9_task.md) | Recuperação neutra; só conta interna recebe e-mail | Identity/BFF `StaffPasswordRecoveryRequestTests`; SPA | 3.0 |

Os gates completos, com `--minimum-expected-tests`, estão no frontmatter de cada task.

### Habilitadores

| Enabler | Task | Por que não cabe numa fatia | Desbloqueia |
|---|---|---|---|
| EN-01 | [1.0](1_task.md) | Identity aceita um único emissor de asserção; sem a credencial do `bff-admin` nenhuma chamada de backoffice existe, e não há operação ainda para observar. O contrato interno já foi aprovado (`internal-api-contract.yaml`) | V-01 |

## Tasks

- [x] 1.0 Credencial de serviço do bff-admin aceita por Identity sem abrir escopo de aluno
- [x] 2.0 Primeiro administrador provisionado define a senha pelo link recebido
- [x] 3.0 Ator interno entra, vê as áreas das suas permissões e sai
- [ ] 4.0 Administrador convida e o convidado recebe o e-mail
- [x] 5.0 Convidado aceita o convite e entra no backoffice
- [x] 6.0 Administrador concede e revoga papel, e a revogação desconecta na hora
- [x] 7.0 Administrador troca o papel numa ação só
- [x] 8.0 Professor não abre a área financeira por nenhuma via
- [x] 9.0 Ator interno recupera a senha sozinho

## Verificação herdada

| Componente | Fonte | Checks e limites | Estado da base | Resolução planejada |
|---|---|---|---|---|
| `src/identity` | `.github/workflows/identity.yml` → `tassosgomes/template-pipeline/.github/workflows/ci-dotnet.yml@v1` | `dotnet restore`; `dotnet format --verify-no-changes`; `dotnet test` (MTP) com cobertura cobertura ≥ 70%; `dotnet publish -c Release`; imagem `src/identity/Dockerfile` | Não medido nesta sessão; CAP-001 e CAP-030 integraram com CI verde (PR #6, #56) | Nenhuma falha herdada conhecida |
| `src/bff-admin` | `.github/workflows/bff-admin.yml` → `ci-dotnet.yml@v1` | Idem, cobertura ≥ 70% | Não medido; componente ainda é esqueleto da Fase 0 | Cobertura medida na full; 3.0 substitui o CSRF e o `/bff/session` do esqueleto e seus testes |
| `src/admin-spa` | `.github/workflows/admin-spa.yml` → `ci-react-ts.yml@v1` | `lint`, `typecheck`, `test` (Vitest com cobertura ≥ 70%), `build`, imagem | Não medido; esqueleto da Fase 0 | Cobertura medida na full |
| `src/notification` | `.github/workflows/notification.yml` → `ci-dotnet.yml@v1` | Idem, cobertura ≥ 70% | Não medido; integrado em CAP-026 | Nenhuma |
| `src/commerce` | `.github/workflows/commerce.yml` → `ci-dotnet.yml@v1` | Idem, cobertura ≥ 70% | Não medido; esqueleto da Fase 0 | Nenhuma |
| `src/bff-student` | `.github/workflows/bff-student.yml` → `ci-dotnet.yml@v1` | Regressão: sem mudança funcional, mas a verificação de asserção de Identity muda (1.0) | Não medido | Regressão nas tasks 1.0 e 3.0 |

O workflow reutilizável `template-pipeline` não está neste repositório; os passos acima vêm dos planos
aprovados de CAP-001/CAP-030, que o consultaram. Paridade exata com o CI é confirmada na validação full.
Os `IntegrationTests` e `EndToEndTests` dependem de Docker (Testcontainers), disponível no ambiente.

## Cobertura

| Requisito | Task(s) |
|---|---|
| RF-01 | 2.0, 3.0 |
| RF-02 | 2.0 |
| RF-03 | 4.0 |
| RF-04 | 4.0 |
| RF-05 | 5.0 |
| RF-06 | 6.0 |
| RF-07 | 6.0 |
| RF-08 | 3.0, 6.0 |
| RF-09 | 7.0 |
| RF-10 | 3.0 |
| RF-11 | 2.0, 9.0 |
| RF-12 | 4.0, 6.0 |
| RF-13 | 8.0 |
| RF-14 | 4.0, 5.0, 6.0, 7.0 |
| US-01 | 4.0 |
| US-02 | 5.0 |
| US-03 | 4.0, 6.0 |
| US-04 | 6.0 |
| US-05 | 7.0 |
| US-06 | 3.0 |
| US-07 | 8.0 |
| US-08 | 6.0 |
| RN-01 | 4.0, 5.0 |
| RN-03 | 9.0 |
| RN-04 | 9.0 |
| RN-05 | 3.0 |
| RN-06 | 2.0, 5.0 |
| RN-07 | 2.0, 9.0 |
| RN-08 | 3.0 |
| RN-10 | 3.0 |
| RN-11 | 3.0 |
| RN-12 | 2.0, 6.0 |
| RN-13 | 3.0, 6.0 |
| RN-13a | 2.0, 4.0 |
| RN-14 | 4.0 |
| RN-15 | 4.0, 5.0 |
| RN-16 | 6.0, 8.0 |
| RN-17 | 6.0, 7.0, 8.0 |
| RN-18 | 1.0, 3.0, 8.0 |
| RN-19 | 4.0, 5.0, 6.0, 7.0 |
| RN-20 | 6.0, 7.0 |
| RN-21 | 4.0 |
| RN-23 | 6.0 (conta sem papel continua existindo; desativar é de CAP-031) |
| RN-24 | 6.0 |
| RN-25 | 2.0 |
| RN-26 | 4.0 |
| RN-27 | 4.0 |
| RN-28 | 4.0 |
| RN-A05 | 4.0, 5.0, 6.0 |
| RN-A06 | — responsabilidade do receptor (CAP-030, integrado); o produtor garante ato conforme em 4.0–7.0 |
| RN-A08 | 4.0 |
| RN-A12 | 2.0 |
| RN-A14 | 4.0 |
| DP-01 | 7.0 |
| DP-02 | 6.0 |
| DP-03 | 2.0, 8.0 |
| DP-04 | 4.0 |
| DP-05 | 4.0 |
| DP-06 | 2.0, 9.0 |

Observabilidade (métrica e span da validação de sessão interna; contagem de atos publicados) entra em
3.0 e 4.0, que emitem o sinal; não há task separada. Migração de dados: não há dado real de backoffice.

> Verificado por `python3 .claude/skills/tsg-flow-task-creator/scripts/validate_plan.py tasks/prd-acesso-interno/` antes do handoff.
