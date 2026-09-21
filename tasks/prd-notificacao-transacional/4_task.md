---
status: done
task_kind: vertical
blocked_by: ["1.0"]
gate: "dotnet test src/notification/tests/CodeForCoders.Notification.IntegrationTests/CodeForCoders.Notification.IntegrationTests.csproj -- --filter-class \"CodeForCoders.Notification.IntegrationTests.DeliverPasswordRecoveryEmailTests\" --minimum-expected-tests 1"
gate_expect: "1 teste passa"
---

# 4.0 Entregar recuperação de senha com o mesmo mecanismo

**Fatia:** V-04 · **Cobre:** RF-03; RN-N15 · **Spec:** `techspec.md#v-04` · **ADR:** —

## Comportamento

Um pedido aceito com `finalidade: recuperacao-de-senha` e `modelo: recuperacao-de-senha` percorre o
mesmo pipeline de aceite, consentimento, entrega e registro da Task 1.0, selecionando o Modelo de
Mensagem de recuperação em vez do de confirmação. A validade exibida vem do parâmetro vigente
**próprio da finalidade `recuperacao-de-senha`** (RN-N15 — prazo independente do de
`confirmacao-de-conta`; o link de recuperação é uma credencial temporária, não deve compartilhar o
mesmo valor por conveniência). A mensagem, como a de confirmação, **não** contém link de descadastro
(RN-N06, mesmo motivo de V-01). Resultado observável: e-mail de recuperação entregue, Registro de
Entrega "entregue" com `finalidade: recuperacao-de-senha`, evento
`notificacao.mensagem-entregue.v1` publicado.

Esta task é a prova retrospectiva do Objetivo 3 do PRD: acrescentar um modelo deve ser acrescentar
`Finalidade` e parâmetro de validade, sem alterar o caso de uso, o consumidor, a verificação de
consentimento ou o registro criados na Task 1.0. Se esta task precisar tocar RF-01, RF-04, RF-05 ou
RF-06, o desenho da fatia anterior estava errado (PRD, §Plano de Rollout).

## Fora do escopo desta task

Recusa por modelo/finalidade desconhecidos (já coberta em V-02, que já rejeita finalidade não
declarada — este PRD não introduz uma terceira finalidade); idempotência (V-03, que já cobre o
mecanismo de forma agnóstica à finalidade); comportamento de "endereço sem conta" — essa garantia é
inteiramente do lado de Identidade e Acesso, que simplesmente não publica o pedido (fora desta
TechSpec).

## Decisões fechadas

- `dados.link` já carrega o token; a validade textual do modelo é sempre renderizada a partir do
  parâmetro vigente, nunca escrita em prosa fixa no texto do modelo (RN-N14) — vale para os dois
  modelos, não só o de confirmação.
- O parâmetro de validade de `recuperacao-de-senha` é independente do de `confirmacao-de-conta`
  (RN-N15) — não reaproveitar o mesmo valor de configuração entre os dois.
- Sincronização entre esse parâmetro e a validade real do Token de Verificação de Identidade e
  Acesso é disciplina operacional de configuração, não algo que este código resolve (`techspec.md`,
  Questões em Aberto) — não implementar lógica de sincronização automática.
- **Decisão pós-revisão (2026-09-21, ver `4.0_task_review.md`, bloqueante resolvido):** a Task 1.0
  implementou RF-01 com a validação de `Modelo` travada em um único valor fixo
  (`AccountConfirmation`), em vez de validar contra o conjunto de modelos conhecidos — exatamente o
  cenário que esta task previa como "desenho da fatia anterior estava errado". Autorizado
  explicitamente: generalizar `NotificationSendRequestRules.cs` para validar `Modelo` contra o
  conjunto de modelos conhecidos (não mais hard-coded a `AccountConfirmation`), em vez de reabrir a
  Task 1.0. A mudança é aceita como correção necessária desta fatia, não como desvio de escopo.

## Modificar / Referenciar

- **modificar:** `src/notification/src/CodeForCoders.Notification.Infra.Data/Configuration/EmailOptions.cs` (parâmetro de validade para `recuperacao-de-senha`, ao lado do de `confirmacao-de-conta` já adicionado na Task 1.0)
- **modificar (autorizado pela decisão pós-revisão acima):** `src/notification/src/CodeForCoders.Notification.Application/Common/NotificationSendRequestRules.cs` (generalizar validação de `Modelo` para o conjunto de modelos conhecidos)
- **ref:** o Modelo de Mensagem de confirmação de conta e o caso de uso da Task 1.0 — o de
  recuperação segue a mesma forma (`nome`, `link`, validade renderizada), sem link de descadastro
- **ref:** `tasks/prd-notificacao-transacional/asyncapi-contract.yaml` (`Finalidade` enum já inclui
  `recuperacao-de-senha`; nenhuma mudança de contrato necessária)

## Pronto quando

- [x] Gate passa (exit 0): `dotnet test src/notification/tests/CodeForCoders.Notification.IntegrationTests/CodeForCoders.Notification.IntegrationTests.csproj -- --filter-class "CodeForCoders.Notification.IntegrationTests.DeliverPasswordRecoveryEmailTests" --minimum-expected-tests 1`
- [x] Pedido com `finalidade: recuperacao-de-senha` termina em Registro de Entrega "entregue" com a
      finalidade correta, e-mail com o link e a validade do parâmetro próprio de recuperação, sem
      link de descadastro
- [x] A suíte completa das Tasks 1.0–3.0 continua passando sem alteração de arquivo fora dos listados
      acima (evidência do Objetivo 3 do PRD) — **nota (2026-09-21):** a suíte completa do projeto já
      é intermitente no checkpoint 3.0 (`a0fdebd`), antes desta task (validator confirmou 1/7 falha
      isolando em worktree limpo nesse commit; outbox compartilhado entre testes da mesma
      `ICollectionFixture` pega linha órfã de teste anterior). Decisão explícita: registrar como
      dívida técnica pré-existente de isolamento de teste, fora do escopo desta task/PRD, e não
      bloquear por ela — critério considerado satisfeito quanto a "sem alteração de arquivo fora dos
      listados acima" (ver decisão pós-revisão acima) e quanto ao comportamento da própria fatia
      (task 1.0 isolada passa; gate declarado desta task passa isolado).
