---
id: TASK-5
title: >-
  Limpar os recibos de idempotência vencidos do Catálogo
  (`catalog.edit_receipts`)
status: To Do
assignee: []
created_date: '2026-10-02 00:13'
labels:
  - commerce
  - tech-debt
dependencies: []
references:
  - >-
    src/commerce/src/CodeForCoders.Commerce.Domain/Entities/CatalogEditReceipt.cs
  - >-
    src/commerce/src/CodeForCoders.Commerce.Infra.Data/Catalog/PurchaseIntentReceiptCleanupWorker.cs
documentation:
  - tasks/prd-concessao-acesso/techspec.md
ordinal: 5000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Cada escrita do financeiro no Catálogo (criar, alterar, publicar, despublicar e excluir oferta; chamada comercial) grava um recibo de idempotência em `catalog.edit_receipts`, com `ExpiresAt` de 24 horas, para que o reenvio da mesma `Idempotency-Key` não duplique a escrita. Nenhuma rotina apaga esses recibos depois de vencidos: a única limpeza de recibos de `commerce` é a das intenções de compra (`PurchaseIntentReceiptCleanupWorker`). A tabela cresce sem limite a cada escrita do backoffice. Achado ao especificar a TechSpec de CAP-008 (`tasks/prd-concessao-acesso/techspec.md`, D-10 e Riscos), que traz limpeza própria para os recibos de Entitlement e deixa esta dívida do Catálogo fora do escopo. Fonte: `src/commerce/src/CodeForCoders.Commerce.Domain/Entities/CatalogEditReceipt.cs`.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Recibos de `catalog.edit_receipts` com `ExpiresAt` anterior ao momento atual são removidos por rotina periódica, no padrão da limpeza de intenções de compra
- [ ] #2 Recibo ainda dentro das 24 horas nunca é removido: reenviar a mesma `Idempotency-Key` continua devolvendo a resposta guardada
- [ ] #3 Falha de banco na limpeza não derruba o serviço nem interrompe o ciclo seguinte
- [ ] #4 Teste de integração cobre o recibo vencido removido e o recibo válido preservado
<!-- AC:END -->
