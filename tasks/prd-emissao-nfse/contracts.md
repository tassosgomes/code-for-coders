---
tsg_artifact: contract
product: code-4-coders
capability: CAP-016
version: 1.0
status: approved
updated: 2026-10-12
sources: tasks/prd-emissao-nfse/prd.md@1.0, context/architecture-baseline.md@1.2, context/domain-map.md@1.2, domains/identidade-e-acesso/domain.md@1.1, contracts/README.md
---

# Contratos de integração — emissão de NFS-e da compra avulsa

> - PRD: [prd.md](prd.md), v1.0, aprovado em 2026-10-11 (OD91)
> - Data: 2026-10-11
> - Estado do conjunto: **Aprovado para implementação (1.0)** em 2026-10-12 — C-01 a C-13 aprovadas pelo responsável; todos os documentos validados sem erro. Aprovação significa acordo para implementar, não implantação nem atualização de catálogo.

Este conjunto registra o acordo da fatia de compra avulsa de `CAP-016`. PRDs posteriores podem evoluí-lo. O contrato anterior de cada fronteira é o vigente em `contracts/` (ver `contracts/README.md`). Os arquivos daqui são o recorte do que muda. Catalogação, armazenamento definitivo e promoção para `contracts/` não fazem parte deste acordo.

## Seleção e escopo

| Integração | Modalidade | Justificativa |
|---|---|---|
| `student-spa` → `bff-student` (conta) | OpenAPI | O aluno informa CPF, CNPJ ou NIF na conta (RF-03). |
| `student-spa` → `bff-student` (pedido) | OpenAPI | O aluno vê a nota no pedido e baixa PDF e XML (RF-05, RF-07). |
| `admin-spa` → `bff-admin` | OpenAPI | O financeiro vê a nota, baixa o arquivo, pede nova tentativa e configura o prestador (RF-02, RF-06). |
| `bff-student` / `bff-admin` → `billing` | OpenAPI | Fiscal é módulo de `billing` (BA11). O BFF não decide a nota. |
| `bff-student` → `identity`; `billing` → `identity` | OpenAPI | O documento do tomador mora na conta. Na hora de emitir, Fiscal lê nome, e-mail e documento. |
| Emissor → `billing` | OpenAPI (webhook) | Confirmação do provedor em ponto dedicado, assinada e idempotente (baseline, OD89). |
| `billing` → emissor | OpenAPI (externo) | Recorte da Notaas no fake-nfe: fila, consulta, PDF e XML. Não é API nossa. |
| `commerce` → `billing` | AsyncAPI | O pedido que passa a *Pago* é o fato gerador (RN-F01). |
| `identity` → `billing` | AsyncAPI | Documento do tomador gravado ou trocado, sem o documento no payload. |
| `billing` → `notification` | AsyncAPI | Um aviso quando a nota fica disponível (RN-F13). |
| Nota, pedido ou tomador como produto de dados | Não aplicável | Banco interno de `billing` e de `identity`. Nenhum consumidor recebe dataset com compromisso próprio. A situação da nota não é publicada para Matrícula, aula nem progresso. Nenhum ODCS. |

| Documento | Padrão | Versão do contrato | Fronteira | Estado |
|---|---|---|---|---|
| [api-contract-conta.yaml](api-contract-conta.yaml) e [.md](api-contract-conta.md) | OpenAPI 3.1.0 | `bff-student` conta 1.0.0 → **1.1.0** | Recorte: leitura e gravação do documento do tomador | **Aprovado para implementação** em 2026-10-12; lint sem erros nem avisos |
| [api-contract-student.yaml](api-contract-student.yaml) e [.md](api-contract-student.md) | OpenAPI 3.1.0 | `bff-student` compra 1.0.0 → **1.1.0** | Recorte: três leituras da nota. Pedido, pagamento e *Meus pedidos* sem mudança de forma | **Aprovado para implementação** em 2026-10-12; lint sem erros nem avisos |
| [api-contract-admin.yaml](api-contract-admin.yaml) e [.md](api-contract-admin.md) | OpenAPI 3.1.0 | `bff-admin` financeiro 1.0.0 → **1.1.0** | Recorte: nota, download, nova tentativa e prestador. Lista e detalhe de pedidos sem mudança de forma | **Aprovado para implementação** em 2026-10-12; lint sem erros nem avisos |
| [internal-api-contract-billing.yaml](internal-api-contract-billing.yaml) e [.md](internal-api-contract-billing.md) | OpenAPI 3.1.0 | `billing` HTTP 1.0.0 → **1.1.0** | Recorte: nota, arquivo, tentativa e prestador. `ensurePaymentSessionInternal` inalterada | **Aprovado para implementação** em 2026-10-12; lint sem erros nem avisos |
| [internal-api-contract-identity-student.yaml](internal-api-contract-identity-student.yaml) e [.md](internal-api-contract-identity-student.md) | OpenAPI 3.1.0 | Identity aluno 1.2.0 → **1.3.0** | Recorte: leitura e gravação pela sessão. Audiência `billing` descrita, operação de sessão não redefinida | **Aprovado para implementação** em 2026-10-12; lint sem erros nem avisos |
| [internal-api-contract-identity.yaml](internal-api-contract-identity.yaml) e [.md](internal-api-contract-identity.md) | OpenAPI 3.1.0 | Identity 1.5.0 → **1.6.0** | Recorte: `getStudentTaxDocumentInternal`. Contato para Notificação inalterado | **Aprovado para implementação** em 2026-10-12; lint sem erros nem avisos |
| [webhook-contract-notaas.yaml](webhook-contract-notaas.yaml) e [.md](webhook-contract-notaas.md) | OpenAPI 3.1.0 | `billing` webhook NFS-e **1.0.0** (novo) | `receiveNfseProviderEvent`. O webhook do Stripe não muda | **Aprovado para implementação** em 2026-10-12; lint sem erros nem avisos |
| [provider-contract-notaas.yaml](provider-contract-notaas.yaml) e [.md](provider-contract-notaas.md) | OpenAPI 3.1.0 | Recorte externo **1.0.0** | Emitir, consultar, PDF e XML. Não é fronteira nossa | **Aprovado para implementação** em 2026-10-12; estrutura OpenAPI sem erros. A norma HTTP interna não se aplica |
| [asyncapi-contract-commerce.yaml](asyncapi-contract-commerce.yaml) | AsyncAPI 3.0.0 | `commerce` 1.2.0 → **1.3.0** | Recorte: `courseTitle` opcional em `vendas.compra-concluida.v1` | **Aprovado para implementação** em 2026-10-12; parser sem erros |
| [asyncapi-contract-identity.yaml](asyncapi-contract-identity.yaml) | AsyncAPI 3.0.0 | `identity` 1.1.0 → **1.2.0** | Fato novo, sem documento no payload | **Aprovado para implementação** em 2026-10-12; parser sem erros |
| [asyncapi-contract-notification.yaml](asyncapi-contract-notification.yaml) | AsyncAPI 3.0.0 | `notification` 1.2.0 → **1.3.0** | Variante `nota-fiscal-disponivel`. Modelos anteriores não repetidos | **Aprovado para implementação** em 2026-10-12; parser sem erros |
| [asyncapi-contract-billing.yaml](asyncapi-contract-billing.yaml) | AsyncAPI 3.0.0 | `billing` 1.0.0 → **1.1.0** | Recebe a compra e a identificação; pede o aviso. Fatos `cobranca.*` inalterados | **Aprovado para implementação** em 2026-10-12; parser sem erros, referências entre os recortes resolvidas |

## Participantes e interfaces

| Identificador | Provedor | Consumidores | Comportamento | PRD |
|---|---|---|---|---|
| `putMyTaxDocument` → `putStudentTaxDocumentInternal` | `bff-student`, decisão de `identity` | `student-spa` | Grava CPF, CNPJ ou NIF na conta da sessão. Dígito inválido não grava | RF-03, RN-F07, RN-F08 |
| `identidade.identificacao-fiscal-informada.v1` | `identity` | `billing` (Fiscal) | Fato da mudança, sem o documento | RF-03 |
| `getStudentTaxDocumentInternal` | `identity` | `billing` | Nome, e-mail e documento na hora de emitir | Junta Fiscal → Identidade |
| `vendas.compra-concluida.v1` 1.3.0 | `commerce` | Matrícula (já) e Fiscal | Abre o único documento se vier `courseTitle` | RF-01, RN-F01, DP-06 |
| `getMyFiscalDocument` → `getMyFiscalDocumentInternal` | `bff-student`, decisão de `billing` | `student-spa` | Visão do aluno. Pedido alheio e sem nota são 404 | RF-05 |
| `lookupFinanceFiscalDocuments` → `lookupFiscalDocumentsInternal` | `bff-admin`, decisão de `billing` | `admin-spa` | Situação canônica da página. Pedido sem nota é omitido | RF-06 |
| `retryFinanceFiscalDocument` → `retryFiscalDocumentInternal` | idem | `admin-spa` | A mesma nota em falha volta para a fila | RF-04, RN-F02 |
| `putServiceProvider` → `putServiceProviderInternal` | idem | `admin-spa` | Chave, código e alíquota. Sem os três, a escola continua vendendo | RF-02, RN-F10 |
| `enqueueNfse` e leituras de arquivo | emissor (Notaas / fake-nfe) | `billing` | 202 na fila; autorização e arquivos vêm depois | OD89, RN-F11 |
| `receiveNfseProviderEvent` | `billing` | emissor | HMAC, idempotente por entrega | baseline, OD89 |
| `notificacao.envio-solicitado.v1` modelo `nota-fiscal-disponivel` | `billing` → `notification` | aluno, por e-mail | Um aviso, sem anexo, link para o pedido | RF-05, RN-F13 |

Relação no caminho principal:

| Ação | HTTP | Mensagem |
|---|---|---|
| Pedido fica *Pago* | nenhuma chamada nova no caminho da venda | `vendas.compra-concluida.v1` com `courseTitle` |
| Falta tomador ou prestador | leitura da nota | nenhuma |
| Aluno grava o documento | `putMyTaxDocument` | `identidade.identificacao-fiscal-informada.v1` |
| Fiscal submete | `POST /emitir` no emissor | nenhuma na plataforma |
| Emissor autoriza e libera arquivos | `POST /webhooks/v1/notaas/events` | `notificacao.envio-solicitado.v1` (`nota-fiscal-disponivel`) |
| Aluno ou financeiro baixa | `GET .../pdf` e `.../xml` | nenhuma |

`orderId` é o mesmo do pedido de `CAP-011`, da compra concluída, da `referencia` enviada ao emissor e do link do aviso. `fiscalDocumentId` é da plataforma e não sai no aviso. O id do emissor não aparece no BFF.

## Origem e decisões

| Entrada | Versão e data | O que foi herdado |
|---|---|---|
| [prd.md](prd.md) | v1.0, 2026-10-11, OD91 | RF-01 a RF-07, RN-F01 a RN-F14, DP-01 a DP-06. QA-01, QA-02 e QA-03 não mudam o contrato |
| [Baseline](../../context/architecture-baseline.md) | v1.2 | BA11 (Fiscal em `billing`), BA08 (um salto), camada anticorrupção do provedor de NFS-e, G09, G18, G23 |
| [Domain Map](../../context/domain-map.md) | v1.2 | Cobrança não emite nota; Vendas publica o pedido pago; Fiscal é dono do documento |
| `contracts/commerce/asyncapi.yaml` | 1.2.0 | `CompraConcluida` já tem preço, `paidAt`, `studentId` e `orderId`. Faltava o título |
| `contracts/notification/asyncapi.yaml` | 1.2.0 | Pedido pela conta, idempotência por `pedidoId`. O link do comprovante vigente é a página do pedido |
| `contracts/billing/openapi-webhook.yaml` | 1.0.0 | Webhook dedicado, assinatura, 200 depois de gravar, tipo não tratado ignorado |
| `contracts/bff-student/openapi-compra.yaml`, `openapi-conta.yaml`; `contracts/bff-admin/openapi-financeiro.yaml` | 1.0.0 | Sessão, CSRF, 502/504 de borda, `financeiro.ler` |
| `contracts/identity/openapi-internal.yaml`, `openapi-internal-student.yaml` | 1.5.0, 1.2.0 | Contato sem documento fiscal; sessão por audiência |
| [ADR-0013](../../docs/adr/0013-jwt-de-aluno-validado-por-servicos-de-dominio.md), [ADR-0016](../../docs/adr/0016-jwt-de-aluno-em-commerce.md), [ADR-0017](../../docs/adr/0017-autenticacao-de-servico-commerce-em-billing.md), [ADR-0018](../../docs/adr/0018-notificacao-resolve-destinatario-pela-conta.md), [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md) | aceitas | JWT de aluno por audiência; ator no backoffice; asserção de serviço |
| fake-nfe | `ac5306e68a8c5b10651a8cceaab691a975109c4a`, 2026-10-07 | `POST /api/v1/emitir` 202, status, PDF, XML, webhook HMAC `X-Notaas-Signature`, eventos `nfse.issued`, `nfse.error`, `nfse.cancelled`, `nfse.documents_ready` |
| OD89 | 2026-10-11 | O duplo de dev e homologação é o fake-nfe. Produção é a Notaas no mesmo contrato. Conta de produção não bloqueia esta fatia |

Decisões deste contrato:

| ID | Decisão | Alternativa descartada | Estado |
|---|---|---|---|
| C-01 | **Fiscal permanece módulo de `billing`.** O webhook do emissor entra em `billing`, ao lado do webhook do Stripe, sem se misturar com ele | Serviço `fiscal` novo — o baseline já escolheu o módulo e DE06 já está no consumo assíncrono | **Aprovada em 2026-10-12** |
| C-02 | **A nota não entra no JSON do pedido.** Lista e detalhe de `CAP-011` ficam iguais. O BFF faz uma segunda chamada a `billing`. Falha de `billing` não derruba *Meus pedidos* nem a lista do financeiro (DE06, BA08: o BFF não pede a `commerce` para chamar `billing`) | Embutir a situação no pedido — `commerce` passaria a guardar ou buscar nota, e a lista cairia junto com o fiscal | **Aprovada em 2026-10-12** |
| C-03 | **O aluno não vê os cinco nomes internos.** `awaiting-provider` e `queued` são `issuing`. Falha não traz a razão do emissor. O financeiro vê o nome interno e a razão já traduzida, nunca o código municipal | Repassar o enum interno ao aluno — a experiência do PRD usa outras frases | **Aprovada em 2026-10-12** |
| C-04 | **O aluno chega a `billing` pelo JWT de audiência `billing`**, escopo `fiscal-document:own`, sem `email` e sem `permissions`. Ator nessa rota é 403. O financeiro chega pelo JWT de ator audiência `billing` (ADR-0005). **ADR própria na TechSpec**, estendendo ADR-0013 no precedente da ADR-0016 | O BFF informar `studentId` e `billing` confiar — é o desenho que ADR-0016 recusou para o pedido | **Aprovada em 2026-10-12** |
| C-05 | **CPF, CNPJ, NIF e e-mail não entram no broker.** O fato da conta só leva `studentId`. Fiscal lê `getStudentTaxDocumentInternal` com asserção de `billing`, escopo `student-tax-document:read`. Notificação continua no contato, sem documento. **ADR própria na TechSpec**, estendendo ADR-0004 no precedente da ADR-0018 | Publicar o documento no evento — o dado iria para o outbox e a routing key ficaria perto do que G23 afasta | **Aprovada em 2026-10-12** |
| C-06 | **O emissor é o recorte do fake-nfe.** 202 não é nota pronta. `issued` e `documents_ready`, os dois, fecham *Emitida*. `nfse.cancelled` responde 200 e não cancela nada (DP-05). Total enviado em reais, não em centavos. `Idempotency-Key` `{orderId}-{n}` | Tratar 202 como nota emitida; falar o leiaute da prefeitura | **Aprovada em 2026-10-12** |
| C-07 | **Permissão nova `fiscal.gerir` no papel financeiro**, para prestador e nova tentativa. Leitura continua `financeiro.ler`. Professor e suporte não ganham nenhuma das duas. O enum vigente já pede que o cliente ignore valor desconhecido | Usar `financeiro.ler` para gravar a chave do emissor — o nome deixa de dizer o que a rota faz | **Aprovada em 2026-10-12** |
| C-08 | **PDF e XML saem como arquivo**, não como JSON. Exceção em [spectral-fiscal.yaml](spectral-fiscal.yaml), no mesmo espírito da entrega HLS. O nome do arquivo usa o número do pedido | Base64 em JSON só para passar no ruleset — a norma manda não deformar a interface | **Aprovada em 2026-10-12** |
| C-09 | **`courseTitle` entra opcional em `CompraConcluida`.** Publicação nova sempre manda. Fato sem título não abre documento (corte de DP-06). Validador 1.2.0 com `additionalProperties: false` rejeita a mensagem nova; o consumidor atual é Matrícula, no mesmo deploy | Título obrigatório — mensagem já publicada na fila deixaria de validar. Fiscal ir ler o pedido em `commerce` — salto a mais e outro contrato, com o dado já congelado no fato | **Aprovada em 2026-10-12** |
| C-10 | **Uma submissão em aberto por documento.** Ausência (timeout, indisponível, E002, E003) conta três e vai a falha. Código de serviço, chave recusada e cota (E001, E004 na bancada) falham na hora. Nova tentativa do financeiro usa outra chave no emissor. Autorização tardia de submissão abandonada não é adotada | Reusar sempre a mesma chave — o fake-nfe devolve o 202 original e não reprocessa a nota que já falhou | **Aprovada em 2026-10-12** |
| C-11 | **Sem tomador, a situação é `awaiting-holder` mesmo que o prestador também falte.** Gravar o documento nesse caso leva a `awaiting-provider`, como o critério de RF-03. Prestador utilizável solta só quem já tem tomador | Começar por `awaiting-provider` — o aluno veria "nota em emissão" e não o pedido para informar o documento | **Aprovada em 2026-10-12** |
| C-12 | **Um aviso, modelo `nota-fiscal-disponivel`.** `pedidoId` estável por `fiscalDocumentId`. Link da página do pedido. Sem anexo e sem URL do emissor. Falha do aviso não tira a nota de `issued` | Anexar o PDF — DP-04 | **Aprovada em 2026-10-12** |
| C-13 | **Sem ODCS.** Não há produto de dados nem consumidor analítico nesta fatia | Publicar a nota para Inteligência de Negócio — o PRD não pede, e o baseline diz que analytics não é consultado | **Aprovada em 2026-10-12** |

## Evolução e compatibilidade

- **Conta do aluno 1.0.0 → 1.1.0, compra do aluno 1.0.0 → 1.1.0, financeiro 1.0.0 → 1.1.0, `billing` HTTP 1.0.0 → 1.1.0, webhook NFS-e 1.0.0:** só caminhos novos. Respostas já em produção não ganham campo. Cliente antigo continua válido.
- **Identity aluno 1.2.0 → 1.3.0:** operações novas. A audiência `billing` é valor a mais na lista já descrita como texto; o schema de `audience` continua string. Quem rejeitar audiência desconhecida no código, e não no schema, precisa da configuração nova antes de o BFF pedir o JWT.
- **Identity 1.5.0 → 1.6.0:** uma leitura nova. `getStudentContactInternal` igual. A permissão `fiscal.gerir` é valor novo de enum. O contrato vigente já manda ignorar permissão desconhecida. Quem valida o enum como conjunto fechado quebra até atualizar — é a única incompatibilidade de cliente conhecida, e o texto atual já a evita.
- **`commerce` mensagens 1.2.0 → 1.3.0:** campo opcional. Mensagem antiga continua válida no schema novo. Mensagem nova é inválida no schema 1.2.0. Matrícula sobe no mesmo deploy que passa a publicar `courseTitle`. Fiscal só então consome.
- **`identity` mensagens 1.1.0 → 1.2.0:** canal novo. Ninguém precisa dele antes de `billing`.
- **`notification` 1.2.0 → 1.3.0:** finalidade e modelo novos, no mesmo canal. Exemplos 1.2.0 continuam válidos no schema cheio, que este recorte não substitui: na consolidação o variante entra como mais um `if`. Pedido com o modelo novo é recusado por uma Notificação 1.2.0. Por isso Notificação sobe antes de Fiscal publicar o aviso.
- **`billing` mensagens 1.0.0 → 1.1.0:** operações novas. `cobranca.*` iguais.
- **Emissor:** não havia recorte nosso. Compatibilidade com a Notaas de produção não foi verificada além de OD89, que manda usar este contrato e não o marketing (201 síncrono). QA-03 segue aberto.
- **Ordem para a implementação:** Identity (audiência `billing`, as duas leituras/gravação, permissão `fiscal.gerir`, fato novo) → `notification` 1.3.0 → `commerce` 1.3.0 (publica `courseTitle`; Matrícula no mesmo deploy) → `billing` (webhook cadastrado no emissor, filas, asserção em Identity) → `bff-student` e `bff-admin` → SPAs. A venda que já está *Paga* antes de `courseTitle` existir não ganha nota (DP-06).

## Validação e verificação

| Documento | Comando e ferramenta | Padrão | Resultado |
|---|---|---|---|
| Os 7 OpenAPI da plataforma | `npx --yes @stoplight/spectral-cli lint <arquivos> --ruleset tasks/prd-emissao-nfse/spectral-fiscal.yaml --fail-severity=warn` | OpenAPI 3.1.0; Spectral **6.17.0**; ruleset da skill mais a exceção C-08 (PDF e XML só na resposta) | Sem erros nem avisos. Exemplos de mídia conferidos pelo ruleset base |
| [provider-contract-notaas.yaml](provider-contract-notaas.yaml) | o mesmo CLI, ruleset [spectral-provider.yaml](spectral-provider.yaml) (`extends: spectral:oas`) | OpenAPI 3.1.0 estrutural. A norma HTTP interna não se aplica: o erro do emissor não é `problem+json` | Sem erros nem avisos |
| Os 4 AsyncAPI, um arquivo por invocação | `npx --yes @asyncapi/cli@6.2.0 validate <arquivo>` | AsyncAPI **3.0.0** (a CLI 6.2.0 informa que existe 3.1.0; os vigentes estão em 3.0.0 e este recorte não migra) | Válidos. `asyncapi-contract-billing.yaml` resolve os `$ref` para os outros três recortes. Cada um tem uma informação `asyncapi-latest-version`, sem erro |

Lint não prova comportamento. A implementação deve verificar:

- **Um documento:** o mesmo pagamento entregue três vezes abre uma nota. Cortesia e pedido que expira sem pagamento não abrem.
- **DE06:** pedido pago sem prestador ou sem tomador concede acesso. `billing` parado devolve 502 na nota e *Meus pedidos* continua abrindo.
- **Prioridade:** sem prestador e sem tomador, a nota fica `awaiting-holder`. O aluno grava um CPF válido e ela passa a `awaiting-provider`. O financeiro grava o prestador e ela entra na fila, sem ação pedido a pedido.
- **Dígito:** CPF `52998224725` grava. Um dígito trocado responde 422 e a nota continua `awaiting-holder`.
- **Congelamento:** nota autorizada mantém tomador, descrição, valor e competência `YYYY-MM` de Brasília do `paidAt`. Trocar o documento da conta depois não muda o PDF.
- **Emissor:** 202 deixa a nota na fila. `nfse.issued` sozinho não libera download. Com `nfse.documents_ready`, baixa PDF e XML. Assinatura ruim responde 400 e não muda nada. A mesma `X-Notaas-Delivery` não emite segundo aviso.
- **Falha:** E001 vai a `failed` sem retry automático. E002 três vezes vai a `failed`. O pedido segue *Pago*. Nova tentativa volta o mesmo `fiscalDocumentId` para `queued`.
- **Marca:** resposta da bancada com o aviso de sem valor fiscal deixa `studyDocument: true` na tela e no arquivo, e não abre segunda nota.
- **Aviso:** um e-mail, sem anexo, link `/student/pedidos/{orderId}`. Falha de entrega não esconde o botão. E-mail, CPF, CNPJ e NIF ausentes de outbox, routing key, URL e log de `billing` e `commerce`.
- **Quem baixa:** outro aluno e um professor recebem 404 ou 403, conforme a borda, e não o arquivo.
- **Passado:** compra concluída sem `courseTitle` não abre documento.

## Pendências e handoff

1. **C-01 a C-13 aprovadas em 2026-10-12.** QA-01 (prazo de guarda), QA-02 (código e alíquota reais) e QA-03 (conta de produção) continuam abertas e não bloqueiam a implementação desta fatia. QA-03 bloqueia tratar o PDF como documento fiscal válido fora da bancada. Se QA-01 seguir aberta, a nota emitida não se apaga com a conta, como o PRD já diz.
2. **ADRs na TechSpec:** JWT de aluno para `billing` (C-04, estende ADR-0013); `billing` como emissor em Identity para ler o tomador (C-05, estende ADR-0004). O JWT de ator com audiência `billing` segue ADR-0005.
3. **Exceção C-08** vive em [spectral-fiscal.yaml](spectral-fiscal.yaml) neste recorte. Incorporá-la ao ruleset de `contracts/` é passo de promoção, fora desta skill.
4. **Para `tsg-flow-techspec-creator`:** usar este índice e os YAML, sem copiar schemas. Mapear o worker que abre o documento, a leitura em Identity, a tradução dos eventos do emissor, a derivação estável do `pedidoId` do aviso, a fila própria de Fiscal (separada da de Matrícula) e os cenários da seção anterior. Tipos do BFF podem sair do OpenAPI. Mensagens se verificam por envio, recebimento e falha acordados. O arquivo se verifica pelo download autenticado e pela marca de estudo.

Não há tarefa, neste acordo, de catalogar, publicar ou substituir os contratos vigentes.
