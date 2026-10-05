# ADR-0018: Notificação resolve o destinatário pela conta em Identity, com autenticação de serviço de `notification`

## Status

Accepted

## Identidade e escopo

- Caminho: `docs/adr/0018-notificacao-resolve-destinatario-pela-conta.md`
- Domínios/componentes afetados: `notification` (como chamador e como dono do pedido de envio), `identity` (como destino), remetentes de pedido de envio (`commerce` nesta entrega)
- Origem histórica: `tasks/prd-compra-avulsa`, `CAP-011` (decisão C-06 do conjunto de contratos; QA-01 do PRD)
- Substitui: Nenhuma. Estende a [ADR-0004](0004-autenticacao-de-servico-bff-identity.md) a um emissor novo, no precedente da [ADR-0010](0010-autenticacao-de-servico-commerce-em-identity.md)

## Data

2026-10-05

## Contexto

O e-mail do aluno é dado pessoal mascarado em todo lugar (Identidade RN-21, G10, G23). Há duas exceções declaradas: a marca d'água (ADR-0013) e o pedido de envio a Notificação, cujo destinatário é o e-mail (RN-26). Até aqui, só Identity pedia envio, e Identity tem o e-mail.

Com a compra, Vendas (`commerce`) precisa enviar o comprovante ao aluno. `commerce` não tem o e-mail nem o nome do aluno, e não deve ter: trazê-los para lá faria o e-mail passar pelo banco de `commerce`, pelo outbox e pelo broker — uma terceira exceção a RN-21. Remetentes futuros (`billing`, `learning`) terão o mesmo problema.

## Decisão

1. **O pedido de envio pode ser endereçado à conta**: `destinatarioConta = {tipo: conta-aluno, id}` em vez de `destinatario` (e-mail), exatamente um dos dois (`notification` 1.2.0). Remetentes que não conhecem o e-mail usam a conta; Identity continua usando o e-mail.
2. **Notificação resolve o contato em Identity na hora de entregar**, por `GET /internal/v1/student-accounts/{studentId}/contact`: e-mail, nome e situação **atuais** da conta. Conta inexistente, de outro tenant, interna ou desativada → não entrega e publica a falha com motivo `destinatario-indisponivel`. Identity indisponível → o pedido continua aceito e é tentado de novo, como na indisponibilidade do provedor de e-mail; nunca é descartado por isso.
3. **`notification` é emissor de asserção de serviço em Identity**, no mecanismo da ADR-0004: chave exclusiva, `aud` = o identificador interno de Identity, escopo único `student-contact:read`, `tenantId`, vida de até 60 segundos, `jti` consumido uma vez.
4. **O e-mail resolvido vive só onde a exceção RN-26 já o admitia**: no Registro de Entrega de Notificação (com a retenção em duas camadas já decidida) e no fato de entrega. Nunca em log, span, métrica, chave de cache ou corpo de erro, em nenhum dos dois serviços.

## Alternativas Consideradas

### Alternativa 1: `commerce` busca e-mail e nome em Identity e publica como hoje

- **Descrição:** `commerce`, já emissor em Identity (ADR-0010), ganharia um escopo de leitura de contato.
- **Prós:** Notificação não muda.
- **Contras:** o e-mail passa a existir no outbox de `commerce` e no broker; cada remetente futuro repete a busca e a exposição.
- **Por que rejeitada:** cria uma terceira exceção a RN-21 e espalha o dado; a resolução pela conta concentra a exposição onde ela já é aceita.

### Alternativa 2: Notificação manter uma réplica de contatos por fatos de Identity

- **Descrição:** consumir `identidade.conta-*` e guardar e-mail e nome localmente.
- **Contras:** réplica de dado pessoal fora de Identity, carga inicial e janela em que uma conta desativada ainda recebe.
- **Por que rejeitada:** mais dado pessoal guardado, por mais tempo, para evitar uma leitura por entrega.

## Consequências

### Positivas

- O e-mail nunca entra em `commerce` nem no broker; o comprovante não exige nova exceção.
- O próximo remetente sem e-mail (por exemplo, `billing` na régua de cobrança) não precisa de decisão nova.

### Negativas

- A entrega pela conta passa a depender de Identity estar disponível no momento do envio (atraso, não perda).
- Mais um par de chaves e um escopo para configurar.

### Riscos

- **Pedido parado se Identity ficar fora por muito tempo.** Mitigação: o pedido conta tentativas como na falha do provedor e, esgotadas, falha com motivo e alerta, como a 1.1.0 já faz.
- **E-mail em telemetria do cliente HTTP.** Mitigação: o cliente de Identity em Notificação não registra corpo de resposta; a verificação procura o e-mail de teste em toda saída de telemetria.

## Notas de Implementação

- Chaves geradas por `scripts/generate-local-env.sh` (`NOTIFICATION_IDENTITY_PUBLIC_KEY_B64`/`NOTIFICATION_IDENTITY_PRIVATE_KEY_B64`) e registradas em Identity como `ServiceAssertions:Issuers:notification`.
- Contratos: `tasks/prd-compra-avulsa/asyncapi-contract-notification.yaml` (1.2.0) e `internal-api-contract-identity.yaml` (`getStudentContactInternal`).

## Referências

- [ADR-0004](0004-autenticacao-de-servico-bff-identity.md), [ADR-0010](0010-autenticacao-de-servico-commerce-em-identity.md), [ADR-0013](0013-jwt-de-aluno-validado-por-servicos-de-dominio.md)
- Domain doc de Identidade e Acesso: RN-21, RN-26, RN-27, RN-28
- Origem histórica: `tasks/prd-compra-avulsa/contracts.md` (C-06)
