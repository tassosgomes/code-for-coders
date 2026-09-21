# Plano de Implementação — Notificação transacional por e-mail (fatia de fundação)

> **TechSpec de origem:** [techspec.md](techspec.md) (Aprovado, 2026-09-21)
> **Escopo:** Backend
> **ADRs pertinentes:** nenhuma — a TechSpec não introduz convenção nova (`docs/adr/index.md`
> consultado; `ADR-0001` e `ADR-0002` aplicados sem alteração)
> **Status do plano:** Confirmado para implementação

## Visão Geral

Ao final deste plano, o serviço `notification` consome `notificacao.envio-solicitado.v1`, aceita ou
recusa cada pedido por validade, entrega os dois modelos desta fatia (confirmação de conta,
recuperação de senha) por e-mail, repete a entrega ao provedor com backoff até um limite, publica o
desfecho pelo outbox existente e expurga o dado pessoal do Registro de Entrega após a janela de
retenção. É o que destrava `CAP-001` (PRD, Objetivo 1) e prova que o mecanismo aguenta um segundo
modelo sem reescrita (PRD, Objetivo 3).

## Fases

Fases agrupam sequência de comportamento e feedback — não camada arquitetural.

### Fase 1 — Mecanismo de aceite e entrega (caminho feliz e casos negativos)

Ao final, um pedido de confirmação de conta percorre aceite → consentimento → entrega → registro →
publicação de ponta a ponta (critério do backlog para o passo 1: "um e-mail transacional é entregue
e registrado"), pedidos inválidos são recusados sem envio parcial, e reentrega/reenvio não duplicam
mensagem. Tasks 1.0–3.0.

### Fase 2 — Segundo modelo e resiliência do provedor

Ao final, a recuperação de senha usa o mesmo mecanismo sem tocar RF-01/04/05/06 (prova do Objetivo 3),
e falha do provedor — transitória ou definitiva — termina em estado observável sem perder o pedido
nem recusar tráfego novo durante a indisponibilidade. Tasks 4.0–6.0.

### Fase 3 — Retenção do dado pessoal

Ao final, o Registro de Entrega expurga campo pessoal após a janela paramétrica sem perder o fato
agregado que a operação consulta. Task 7.0.

## Mapa de Entrega

| Fatia | Task | Comportamento observável | Gate | Bloqueado por |
|---|---|---|---|---|
| V-01 | 1.0 | Pedido de confirmação de conta aceito, entregue e registrado; evento publicado sem corpo | `dotnet test src/notification/tests/CodeForCoders.Notification.IntegrationTests/CodeForCoders.Notification.IntegrationTests.csproj -- --filter-class "CodeForCoders.Notification.IntegrationTests.AcceptAndDeliverAccountConfirmationTests" --minimum-expected-tests 1` | Nenhum |
| V-02 | 2.0 | Pedido inválido (sem finalidade, modelo desconhecido, dado faltante) recusado com motivo, sem envio parcial | `dotnet test src/notification/tests/CodeForCoders.Notification.IntegrationTests/CodeForCoders.Notification.IntegrationTests.csproj -- --filter-class "CodeForCoders.Notification.IntegrationTests.RejectInvalidSendRequestTests" --minimum-expected-tests 3` | 1.0 |
| V-03 | 3.0 | Redelivery do mesmo `pedidoId` não duplica entrega; reenvio com `pedidoId` novo gera segundo registro e segundo e-mail | `dotnet test src/notification/tests/CodeForCoders.Notification.IntegrationTests/CodeForCoders.Notification.IntegrationTests.csproj -- --filter-class "CodeForCoders.Notification.IntegrationTests.SendRequestIdempotencyTests" --minimum-expected-tests 2` | 1.0 |
| V-04 | 4.0 | Pedido de recuperação de senha entregue pelo mesmo mecanismo, com validade própria | `dotnet test src/notification/tests/CodeForCoders.Notification.IntegrationTests/CodeForCoders.Notification.IntegrationTests.csproj -- --filter-class "CodeForCoders.Notification.IntegrationTests.DeliverPasswordRecoveryEmailTests" --minimum-expected-tests 1` | 1.0 |
| V-05 | 5.0 | Falha transitória do provedor reentrega com backoff até esgotar; pedidos novos continuam aceitos durante a indisponibilidade | `dotnet test src/notification/tests/CodeForCoders.Notification.IntegrationTests/CodeForCoders.Notification.IntegrationTests.csproj -- --filter-class "CodeForCoders.Notification.IntegrationTests.RetryProviderTransientFailureTests" --minimum-expected-tests 2` | 1.0 |
| V-06 | 6.0 | Falha definitiva imediata do provedor vai a "falhou" sem reentrega | `dotnet test src/notification/tests/CodeForCoders.Notification.IntegrationTests/CodeForCoders.Notification.IntegrationTests.csproj -- --filter-class "CodeForCoders.Notification.IntegrationTests.FailImmediatelyOnPermanentProviderErrorTests" --minimum-expected-tests 1` | 5.0 |
| V-07 | 7.0 | Campo pessoal do Registro de Entrega expurgado após a janela, contador agregado preservado | `dotnet test src/notification/tests/CodeForCoders.Notification.IntegrationTests/CodeForCoders.Notification.IntegrationTests.csproj -- --filter-class "CodeForCoders.Notification.IntegrationTests.PurgeDeliveryRecordPersonalDataTests" --minimum-expected-tests 1` | 1.0 |

Nenhum habilitador horizontal: a TechSpec já resolve a topologia compartilhada (fila, migration,
DbContext) dentro da própria Task 1.0, que as demais fatias estendem por dependência declarada — não
há contrato comum que precise nascer fora de uma fatia (ver `references/vertical-slicing.md`).

## Tasks

- [x] 1.0 Aceitar e entregar confirmação de conta (caminho feliz)
- [ ] 2.0 Recusar pedido de envio inválido
- [ ] 3.0 Garantir idempotência entre reentrega e reenvio
- [ ] 4.0 Entregar recuperação de senha com o mesmo mecanismo
- [ ] 5.0 Reentregar ao provedor em falha transitória até esgotar tentativas
- [ ] 6.0 Falhar imediatamente em erro definitivo do provedor
- [ ] 7.0 Expurgar dado pessoal do Registro de Entrega após a retenção

## Cobertura

Toda linha do PRD aparece aqui. Lacuna é bloqueio de handoff, não observação.

| Requisito | Task(s) |
|---|---|
| RF-01 | 1.0, 2.0, 3.0 |
| RF-02 | 1.0, 3.0 |
| RF-03 | 4.0 |
| RF-04 | 1.0 |
| RF-05 | 1.0, 7.0 |
| RF-06 | 5.0, 6.0 |
| RF-07 | 1.0, 5.0, 6.0 |
| RN-26 | 1.0 |
| RN-27 | 1.0 |
| RN-28 | 1.0 |

**Regras nascidas neste PRD (RN-N01–RN-N16)** — não usam o padrão `RN-<dígitos>` da tabela de
cobertura mecânica acima (o prefixo `N` distingue da numeração do domain doc que Notificação ainda
não tem, PRD §Regras de Negócio); mapeadas aqui para revisão humana:

| Regra | Task(s) | Regra | Task(s) |
|---|---|---|---|
| RN-N01 | 1.0 | RN-N09 | 1.0, 3.0 |
| RN-N02 | 1.0, 2.0 | RN-N10 | 1.0, 5.0, 6.0 |
| RN-N03 | 1.0, 4.0 | RN-N11 | 1.0, 5.0 |
| RN-N04 | 1.0 | RN-N12 | 5.0 |
| RN-N05 | 1.0 | RN-N13 | 1.0 |
| RN-N06 | 1.0, 4.0 | RN-N14 | 1.0 |
| RN-N07 | 1.0 | RN-N15 | 4.0 |
| RN-N08 | 1.0 | RN-N16 | 7.0 |

**Histórias de usuário** (o PRD não as numera; mapeadas por conteúdo, não por ID formal):

| História (resumo) | Task(s) |
|---|---|
| Visitante recém-cadastrado recebe e-mail de confirmação | 1.0 |
| Visitante que não recebeu pede reenvio sem novo cadastro | 3.0 |
| Aluno que esqueceu a senha recebe link de redefinição | 4.0 |
| Pedido de recuperação sem conta não revela nem gera e-mail — **fora desta TechSpec**: garantia do lado Identidade e Acesso (não publica o pedido); Notificação não tem o que fazer além de nunca receber esse pedido | — (PRD de `CAP-001`) |
| Operação vê entregue/falhou com motivo | 1.0, 5.0, 6.0 |
| Encarregado de dados tem um único ponto de verificação de consentimento e descadastro | 1.0 |

> Verificado por `python3 .agents/skills/tsg-flow-task-creator/scripts/validate_plan.py tasks/prd-notificacao-transacional/` antes do handoff.
