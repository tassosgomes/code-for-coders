# API Contract — Webhook do emissor de NFS-e

> **Gerado a partir de:** `tasks/prd-emissao-nfse/prd.md` e do fake-nfe `ac5306e`  
> **Data:** 2026-10-11  
> **Status:** Aprovado para implementação  
> **Versão do contrato:** 1.0.0  
> **Fonte técnica:** [webhook-contract-notaas.yaml](webhook-contract-notaas.yaml)

Ponto público de `billing`, `POST /webhooks/v1/notaas/events`. Não passa pelo BFF.

A assinatura é `X-Notaas-Signature: sha256=` mais o HMAC-SHA256 hexadecimal do corpo cru. A idempotência é `X-Notaas-Delivery`. 200 só depois de gravar. 503 pede reenvio.

`nfse.issued` e `nfse.documents_ready`, juntos, na submissão corrente, tornam a nota disponível. `nfse.cancelled`, teste e evento de submissão abandonada respondem 200 sem mudar a situação. Código de serviço, chave e cota falham na hora. Timeout e indisponibilidade contam até três.

### Aviso de autorização

```json
{
  "evento": "nfse.issued",
  "timestamp": "2026-10-11T18:04:00Z",
  "aviso": "Ambiente de estudo. Nenhum documento foi transmitido à SEFAZ ou a uma prefeitura. Sem valor fiscal.",
  "dados": {
    "id": "inv_01J9STUDYNOTE",
    "numero": "000042",
    "status": "issued",
    "chNFSe": "ESTUDO-000042",
    "valorTotal": 497
  }
}
```
