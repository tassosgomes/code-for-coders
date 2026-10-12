# ADR-0021: `billing` lê o tomador em Identity, com autenticação de serviço

## Status

Accepted

## Identidade e escopo

- Caminho: `docs/adr/0021-billing-le-tomador-em-identity.md`
- Domínios/componentes afetados: `billing` (módulo Fiscal, como chamador), `identity` (documento do tomador na conta e a leitura interna)
- Origem histórica: `tasks/prd-emissao-nfse`, `CAP-016` (decisão C-05 do conjunto de contratos)
- Substitui: Nenhuma. Estende a [ADR-0004](0004-autenticacao-de-servico-bff-identity.md), no precedente da [ADR-0018](0018-notificacao-resolve-destinatario-pela-conta.md)

## Data

2026-10-12

## Contexto

O documento do tomador (CPF, CNPJ ou NIF) mora na conta do aluno. A nota, quando autorizada, congela uma cópia. Até lá, Fiscal precisa do documento vigente para submeter ao emissor.

CPF, CNPJ, NIF e e-mail não podem ir a URL, log, cache, routing key nem broker (G23, estendido no PRD a esses documentos). O fato de a conta ter mudado, se levasse o documento, gravaria o dado no outbox e no broker. Notificação já resolve o e-mail na entrega (ADR-0018) e não recebe documento fiscal.

`getStudentContactInternal` devolve e-mail e nome só para o emissor `notification`, escopo `student-contact:read`. Reusar essa operação daria a `billing` o contato sem o documento, ou alargaria um escopo que hoje é exclusivo de Notificação.

## Decisão

1. **O documento do tomador fica em Identity**, na conta do aluno. O aluno grava e substitui pela sessão, com a asserção que o `bff-student` já usa. Dígito inválido não grava. O mesmo valor já armazenado não republica fato.
2. **O fato `identidade.identificacao-fiscal-informada.v1` não carrega documento, nome nem e-mail.** Leva identificadores do evento, da escola, do aluno e o momento. Fiscal, ao recebê-lo, relê Identity.
3. **`billing` é emissor de asserção de serviço em Identity**, no mecanismo da ADR-0004: chave exclusiva, `aud` = o identificador interno de Identity, escopo único `student-tax-document:read`, `tenantId`, vida de até 60 segundos, `jti` consumido uma vez.
4. **A leitura é `getStudentTaxDocumentInternal`.** Devolve nome, e-mail, situação da conta e o documento, ou documento nulo. Conta desativada ainda devolve os dados: a nota de uma venda já feita não depende da conta continuar ativa. Conta inexistente ou de outra escola não devolve dado.
5. **Fora dessa leitura e da cópia congelada na nota emitida, `billing` não guarda e-mail nem documento.** Não entram no outbox, no broker, em log, span, métrica, URL, nome de objeto nem chave de cache. A chamada ao emissor usa o documento em memória e não registra o corpo.

## Alternativas Consideradas

### Alternativa 1: Publicar o documento no fato

- **Descrição:** o evento da conta levaria CPF, CNPJ ou NIF, e Fiscal não leria Identity.
- **Prós:** uma leitura a menos na hora de emitir.
- **Contras:** o dado passa pelo outbox de Identity e pelo broker.
- **Por que rejeitada:** é a exposição que G23 afasta. A ADR-0018 já recusou o mesmo para o e-mail.

### Alternativa 2: Alargar `getStudentContactInternal` para `billing`

- **Descrição:** Notificação e Fiscal compartilhariam a leitura de contato, com o documento acrescentado.
- **Prós:** uma operação só.
- **Contras:** Notificação passaria a poder receber o documento, e o escopo `student-contact:read` deixaria de dizer o que a rota faz.
- **Por que rejeitada:** o contato de e-mail e o documento do tomador têm chamadores e finalidades diferentes.

## Consequências

### Positivas

- O broker continua sem documento de tomador e sem e-mail de aluno vindo de Fiscal.
- A nota emitida congela o tomador que Identity devolveu na submissão, e uma troca posterior na conta não reescreve o PDF.

### Negativas

- Emitir depende de Identity estar disponível naquela tentativa. Indisponibilidade conta como ausência do emissor só depois de o prestador e o tomador já existirem; antes disso o documento fica aguardando tomador ou prestador, sem chamar o emissor.
- Mais um par de chaves e um escopo para configurar.

### Riscos

- **Documento em telemetria do cliente HTTP.** Mitigação: o cliente de Identity em `billing` não registra corpo de resposta nem de pedido; a verificação procura CPF, CNPJ, NIF e e-mail de teste na saída de telemetria de `billing`, `commerce` e Identity.
- **Reuso de asserção.** Mitigação: a da ADR-0004 — vida curta e `jti` único.

## Notas de Implementação

- Chave exclusiva de `billing` registrada em Identity como emissor `billing`, escopo permitido `student-tax-document:read`.
- A gravação pela sessão do aluno (`putStudentTaxDocumentInternal`) usa a asserção do `bff-student`, escopo `student-tax-document:write`. Não usa a chave de `billing`.
- `getStudentContactInternal` permanece exclusiva de `notification`.

## Referências

- [ADR-0004](0004-autenticacao-de-servico-bff-identity.md), [ADR-0010](0010-autenticacao-de-servico-commerce-em-identity.md), [ADR-0018](0018-notificacao-resolve-destinatario-pela-conta.md)
- [Baseline arquitetural](../../context/architecture-baseline.md) — G23
- Origem histórica: `tasks/prd-emissao-nfse/contracts.md` (C-05)
