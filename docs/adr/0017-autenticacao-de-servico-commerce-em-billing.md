# ADR-0017: Autenticação de serviço de `commerce` em `billing`, para pedir o pagamento de um pedido

## Status

Accepted

## Identidade e escopo

- Caminho: `docs/adr/0017-autenticacao-de-servico-commerce-em-billing.md`
- Domínios/componentes afetados: `commerce` (módulo Vendas, como chamador), `billing` (Cobrança e Assinatura, como destino)
- Origem histórica: `tasks/archive/prd-compra-avulsa`, `CAP-011` (decisões C-01, C-02 e C-04 do conjunto de contratos)
- Substitui: Nenhuma. Estende a [ADR-0004](0004-autenticacao-de-servico-bff-identity.md) a um destino novo, no precedente da [ADR-0010](0010-autenticacao-de-servico-commerce-em-identity.md)

## Data

2026-10-05

## Contexto

`billing` nasce com `CAP-011` como dono de Cobrança e Assinatura: guarda as credenciais do gateway de pagamento, cria a página de pagamento e recebe a confirmação. Vendas (em `commerce`) pede o recebimento do valor de um pedido e recebe de volta o endereço para onde levar o aluno (Domain Map: "Vendas aciona Cobrança para receber o pagamento"). É uma chamada síncrona de serviço para serviço, um salto (BA08), com o valor e a descrição do pedido.

Quem pode pedir pagamento a `billing` precisa ser autenticado e limitado: um chamador indevido abriria cobrança em nome da escola com qualquer valor. O projeto já tem um mecanismo para isso — asserção de serviço assimétrica e curta, por emissor, com escopos e tenants permitidos (ADR-0004, estendida pela ADR-0010 a um serviço de domínio chamando outro).

## Decisão

1. **`commerce` é emissor de asserção de serviço em `billing`**, no mecanismo da ADR-0004: JWS RS256, `iss`/`sub` = `commerce`, `aud` = `billing`, `scope`, `tenantId` da escola, vida de até 60 segundos e `jti` único, consumido uma vez em `billing` (proteção contra repetição). O par de chaves é **exclusivo** desta relação: não é o de `commerce` → Identity.
2. **Escopo único: `payment:request`**, registrado em `billing` para o emissor `commerce`, com a lista de `tenantId` que ele pode representar. `billing` toma a escola do pagamento do `tenantId` da asserção (G07).
3. **`billing` não consulta `commerce` de volta.** O valor, a moeda e a descrição chegam na chamada; `billing` recusa termos diferentes para um pagamento já aberto do mesmo pedido. A volta do resultado é por fato (`cobranca.pagamento-*`), nunca por chamada.
4. **Nenhum BFF chama `billing`.** O único chamador síncrono nesta entrega é `commerce`; o único ponto público é o webhook do gateway, autenticado pela assinatura do gateway, não por esta asserção.

## Alternativas Consideradas

### Alternativa 1: Comando por mensagem em vez de chamada síncrona

- **Descrição:** Vendas publica "pagamento solicitado" e `billing` responde por fato com o endereço.
- **Prós:** nenhuma credencial síncrona nova.
- **Contras:** o aluno espera o endereço de pagamento na tela; a resposta assíncrona exigiria consulta posterior e levaria o endereço do gateway — uma credencial de pagamento — para o broker e para o banco de `commerce`.
- **Por que rejeitada:** o endereço não pode sair de `billing` a não ser direto para o navegador do aluno (C-02).

### Alternativa 2: Chave compartilhada entre serviços

- **Descrição:** um segredo simétrico comum.
- **Contras:** quem verifica também pode emitir; vazamento em um serviço compromete todos.
- **Por que rejeitada:** a ADR-0004 já decidiu por chaves assimétricas por emissor.

## Consequências

### Positivas

- Só `commerce` abre cobrança, e só para as escolas configuradas.
- O padrão é o mesmo dos demais chamadores internos: nenhuma biblioteca ou conceito novo.

### Negativas

- Mais um par de chaves para gerar e rotacionar em cada ambiente.

### Riscos

- **Repetição de asserção.** Mitigação: `jti` consumido uma vez em `billing`, com armazenamento compartilhado entre instâncias.
- **Termos de pagamento adulterados.** Mitigação: `billing` recusa divergência de valor, moeda ou descrição para o mesmo pedido (`PAYMENT_TERMS_CONFLICT`).

## Notas de Implementação

- Chaves geradas por `scripts/generate-local-env.sh` (`COMMERCE_BILLING_PUBLIC_KEY_B64`/`COMMERCE_BILLING_PRIVATE_KEY_B64`) e registradas em `billing` como `ServiceAssertions:Issuers:commerce`.
- O contrato vigente está em `contracts/billing/openapi-internal.yaml` (`billing` 1.0.0, `ensurePaymentSessionInternal`), promovido em 2026-10-06 a partir do recorte histórico `tasks/archive/prd-compra-avulsa/internal-api-contract-billing.yaml`.

## Referências

- [ADR-0004](0004-autenticacao-de-servico-bff-identity.md), [ADR-0010](0010-autenticacao-de-servico-commerce-em-identity.md)
- Baseline: BA08 (um salto), camada anticorrupção sobre o gateway
- Origem histórica: `tasks/archive/prd-compra-avulsa/contracts.md` (C-01, C-02, C-04)
