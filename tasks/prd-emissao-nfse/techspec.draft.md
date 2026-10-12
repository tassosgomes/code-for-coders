---
tsg_artifact: techspec
product: code-4-coders
capability: CAP-016
version: 1.0
status: in-review
updated: 2026-10-12
sources: tasks/prd-emissao-nfse/prd.md@1.0, tasks/prd-emissao-nfse/contracts.md@1.0, context/architecture-baseline.md@1.2, domains/identidade-e-acesso/domain.md@1.1
---

# Especificação Técnica — Emissão de NFS-e da compra avulsa

> **Escopo:** Full-stack
> **Modo:** Pipeline + API-First
> **PRD de origem:** `tasks/prd-emissao-nfse/prd.md` (v1.0, OD91)
> **Contratos de integração:** [contracts.md](contracts.md) (aprovado para implementação em 2026-10-12, C-01 a C-13) e os documentos que ele indexa
> **Data:** 2026-10-12
> **Status:** Em Revisão
> **Handoff:** draft — não gerar Tasks

---

## Resumo Executivo

A nota da compra avulsa nasce em `billing`, módulo Fiscal, quando `commerce` publica `vendas.compra-concluida.v1` com `courseTitle`. Venda, concessão e comprovante de compra não esperam a nota. Lista e detalhe de pedido de `CAP-011` não mudam de forma: aluno e financeiro leem a nota numa segunda chamada. Falha dessa chamada não derruba *Meus pedidos* nem a lista do financeiro.

Decisões principais, todas herdadas do contrato aprovado:

- **Fiscal permanece em `billing`** (C-01, BA11). O webhook do emissor entra ao lado do webhook do Stripe, sem se misturar com ele.
- **O aluno chega a `billing` com JWT de audiência `billing`** (C-04, [ADR-0020](../../docs/adr/0020-jwt-de-aluno-em-billing.md), proposta). O ator interno usa a audiência `billing` que a ADR-0005 já prevê.
- **CPF, CNPJ, NIF e e-mail não vão ao broker** (C-05, [ADR-0021](../../docs/adr/0021-billing-le-tomador-em-identity.md), proposta). Fiscal relê Identity na hora de submeter. A nota emitida congela a cópia.
- **O emissor é o recorte da Notaas no fake-nfe** (C-06, OD89). 202 não é nota pronta. `issued` e `documents_ready`, os dois, na submissão corrente, mais os arquivos gravados na plataforma, fecham *Emitida*.

**Trade-off primário:** a nota fica fora do JSON do pedido. Ganha-se que uma queda de `billing` não desfaz a venda nem esconde *Meus pedidos*. Abre-se mão de uma tela que já traria a situação junto do pedido: cada tela faz uma segunda chamada, e o aluno vê `issuing` tanto quando falta prestador quanto quando a nota está na fila do emissor.

---

## Arquitetura da Solução

```text
student-spa ──► bff-student ──► identity (sessão; documento do tomador)
                    │                 ▲
                    │                 │ asserção billing, student-tax-document:read (ADR-0021)
                    └── JWT aud billing ──► billing (Fiscal) ──► emissor (Notaas / fake-nfe)
admin-spa ──► bff-admin ──► commerce (pedido, aud commerce, financeiro.ler)
                └── JWT aud billing ──► billing (leitura financeiro.ler, escrita fiscal.gerir)

commerce ── vendas.compra-concluida.v1 ──► fila de Fiscal em billing
                                      └──► fila de Matrícula (já existe; ignora courseTitle)
identity ── identidade.identificacao-fiscal-informada.v1 ──► fila de Fiscal
emissor ── POST /webhooks/v1/notaas/events ──► billing
billing ── notificacao.envio-solicitado.v1 (nota-fiscal-disponivel) ──► notification ──► identity (contato, ADR-0018)
```

`commerce` não chama `billing` no caminho da nota (BA08, C-02). A única chamada síncrona que já existe, `commerce` → `billing` para abrir o pagamento (ADR-0017), não muda.

### URLs públicas

O link do aviso é a página do pedido, a mesma função que o comprovante já usa (`StudentApp:PublicBaseUrl` + `/pedidos/{orderId}`). `billing` passa a ler a mesma chave. O exemplo `https://app.code4coders.com.br/...` do AsyncAPI é ilustrativo e não é endereço deste repositório.

| Link | Local | Homologação estável | Dev (Komodo) | Quem cria |
|---|---|---|---|---|
| Página do pedido, destino do aviso e dos botões | `http://localhost:8082/student/pedidos/{orderId}` | `https://code4coders.tasso.dev.br/students/pedidos/{orderId}` | `https://dev-code4coders.tasso.dev.br/students/pedidos/{orderId}` | `billing`, no `dados.link` |
| Pedido do financeiro | `http://localhost:8081/admin/financeiro/pedidos/{orderId}` | `https://code4coders.tasso.dev.br/admin/financeiro/pedidos/{orderId}` | o host de admin desse ambiente + `/admin/financeiro/pedidos/{orderId}` | `admin-spa` |
| Prestador | `http://localhost:8081/admin/financeiro/prestador` | `https://code4coders.tasso.dev.br/admin/financeiro/prestador` | o host de admin desse ambiente + `/admin/financeiro/prestador` | `admin-spa` |
| Webhook do emissor | `http://localhost:5110/webhooks/v1/notaas/events` | o host público de `billing` + `/webhooks/v1/notaas/events` | ver risco do túnel, que hoje só deixa passar o Stripe | cadastrado no emissor por ambiente; não é operação de produto |

Não há rota nova no aluno. A nota e o campo de documento ficam em `/pedidos/{orderId}`.

### Bloco Backend

**`billing` — módulo Fiscal, schema `billing_access`, banco `code_for_coders_billing`.** Não é serviço novo. Migrations só pelo EF Core do projeto.

- **Documento fiscal** é o agregado, um por pedido (índice único por tenant e `orderId`). Guarda `orderId`, número do pedido, aluno, curso, `courseTitle`, preço em centavos, descrição, competência, situação canônica, motivo em texto, marca de estudo, número da submissão corrente, contador de ausências dessa submissão, identificador da submissão no emissor e os momentos. PDF e XML, quando existem, são bytes nesse agregado, no mesmo banco. Não vão para o S3 de mídia (BA14 reserva S3 a vídeo) e não guardam URL do emissor.
- **Tomador congelado** (nome, e-mail, tipo e valor do documento) só é escrito na transição para `issued`. Antes disso Fiscal relê Identity a cada submissão. Troca posterior na conta não altera essa cópia nem os arquivos. Um ano depois de `issued`, a cópia e os arquivos saem; a linha do documento fica (QA-01).
- **Prestador** é um por tenant: chave do emissor cifrada, código de serviço, alíquota em texto, flag de utilizável. Utilizável exige os três. A chave usa AES-GCM com chave de 256 bits vinda de configuração (a mesma família de proteção já usada no outbox de Identity e na chave de vídeo), associada ao tenant. A leitura nunca devolve a chave. Gravação sem chave, quando já existe uma, mantém a anterior. A primeira gravação sem chave responde 422 `PROVIDER_KEY_REQUIRED`.
- **Situação.** Sem tomador válido → `awaiting-holder`, mesmo sem prestador. Com tomador e sem prestador → `awaiting-provider`. Com os dois → submete e fica `queued`. `issued` exige, na submissão corrente, `nfse.issued` e `nfse.documents_ready` e os dois arquivos gravados. `failed` é terminal até o financeiro pedir nova tentativa, que volta o **mesmo** documento a `queued` com outro número de submissão.
- **Submissão.** `Idempotency-Key` do emissor é `{orderId}-{n}`, `n` começando em 1. `referencia` é o `orderId`. Total enviado é o preço congelado em reais com duas casas (`49700` → `497.00`), nunca centavos. Alíquota sai do texto configurado, sem passar por binário de ponto flutuante. Descrição é `Acesso ao curso {courseTitle}`. Competência é o mês civil de `America/Sao_Paulo` de `paidAt`, `YYYY-MM`.
- **Ausência e recusa.** Timeout, indisponibilidade, E002 e E003 somam no contador da submissão; a terceira leva a `failed` com o motivo `O emissor não respondeu após as tentativas.` E001, chave recusada e E004 (cota) vão a `failed` na hora, sem contar como ausência. Motivos: `O código de serviço foi recusado pelo emissor.`, `A chave do emissor foi recusada.`, `O emissor recusou por cota.` `awaiting-holder` usa `Falta CPF, CNPJ ou NIF do aluno.` `queued`, `awaiting-provider` e `issued` têm motivo nulo. Nenhum motivo leva código municipal.
- **Arquivo depois da autorização.** Baixar PDF e XML não é submissão nova e não incrementa `n`. Falha desse download não leva a `failed` e não abre outra nota: o documento continua `queued` e o download é tentado de novo. Só a gravação dos dois bytes, na mesma transação do aviso, passa a `issued`.
- **Webhook.** `POST /webhooks/v1/notaas/events`. HMAC-SHA256 do corpo cru, cabeçalho `X-Notaas-Signature: sha256=` mais o hex. O segredo é configuração de ambiente, no mesmo papel de `Stripe:WebhookSigningSecret`, e não é a chave da escola. `X-Notaas-Event` diferente de `evento` no corpo → 400 `SIGNATURE_INVALID`. Idempotência por `X-Notaas-Delivery`: a mesma entrega responde 200 sem segundo efeito. 200 só depois de gravar. Falha antes disso → 503. `nfse.cancelled`, `teste: true`, evento desconhecido e autorização de submissão que já não é a corrente respondem 200 e não mudam a situação. Marca de estudo (`aviso` de sem valor fiscal ou o emissor identificado como estudo) grava `studyDocument` sem tirar `issued` e sem autorizar segunda emissão.
- **Filas próprias**, separadas da de Matrícula (`commerce.entitlement-purchases`):
  - compra concluída, ligada ao exchange em que `commerce` **publica** hoje (`commerce.events` no compose). Não usar `RabbitMqResourceNames.Compose` nesse exchange: ele acrescenta `Billing:Namespace` e a fila passaria a ouvir `commerce.events.default`, onde o fato não é publicado.
  - identificação fiscal, ligada a `identity.events`.
  - O aviso `notificacao.envio-solicitado.v1` sai no exchange que Notificação já consome (`notification.events.default` no mapa de `commerce`). O publicador atual de `billing` manda tudo para `billing.events.{namespace}` (`RabbitMqPublisher`); o aviso fiscal não pode ir para lá. `cobranca.*` continuam nesse exchange.
- **`pedidoId` do aviso** é UUIDv5 (RFC 4122, SHA-1) do `fiscalDocumentId` em minúsculas, formato com hífens, sob o namespace `f46b5092-dcc2-58f2-a330-b5da00d8ac65` (UUIDv5 de `urn:code-4-coders:fiscal-notification` no namespace de URL da RFC). Para o documento de exemplo `8f3e2d1c-0b9a-4c8d-9e7f-6a5b4c3d2e1f`, o `pedidoId` é `8a981d19-6d32-54f4-9f29-1011ffeaf273`. O UUID de exemplo no AsyncAPI não é esse valor. Republicar a transição gera o mesmo `pedidoId`, e Notificação não manda um segundo e-mail.
- **Guarda de um ano (QA-01).** O prazo é o aniversário do instante, em UTC. Para a nota, conta de `issued`: nessa data saem os bytes do PDF e do XML e a cópia congelada do tomador. A linha permanece, com `status` `issued` e `downloadsReady` falso, para o mesmo pedido não ganhar outra nota. O GET do arquivo passa a responder 404 `FISCAL_DOCUMENT_NOT_FOUND`. A leitura da situação continua 200. Excluir a conta não antecipa esse prazo. Para o documento na conta, conta da última gravação válida: Identity apaga o valor, publica o mesmo fato sem documento, e Fiscal relê. Nota já `issued` não muda por isso. Documento ainda não emitido que dependia do tomador volta a `awaiting-holder`. Submissão já aceita pelo emissor não é cancelada. O ciclo é periódico e idempotente, no formato do ciclo de expiração de acesso de Matrícula.
- **Camada anticorrupção.** Porta com enfileirar, consultar, baixar PDF e baixar XML. Tipos, códigos E001–E004 e nomes de evento do emissor não saem dessa porta. Nesta fatia a base é só a bancada (fake-nfe). Conta de produção da Notaas e certificado A1 ficam de fora (QA-03). Cadastro de endpoint de webhook no emissor é configuração de deploy, não caso de uso.

**`commerce`.** `PurchaseCompletedFact` ganha `courseTitle` opcional, sempre preenchido nas publicações novas, a partir de `Order.CourseTitle`, que já está congelado no pedido. Fato sem título não abre documento (DP-06). Matrícula continua no mesmo processo e ignora propriedade desconhecida. Cortesia e pedido que não fica *Pago* não publicam esse fato, então não abrem nota. O valor da nota é `priceCents` do fato, não `paidAmountCents`.

**`identity`.** O documento do tomador entra na conta do aluno e dura um ano desde a última gravação válida (QA-01). CPF e CNPJ são só dígitos (11 e 14) com verificador; NIF é texto aparado, não vazio, até 40 caracteres. Inválido → 422 `TAX_DOCUMENT_INVALID` e nada grava. Papel `financeiro` ganha `fiscal.gerir` ao lado de `financeiro.ler` em `StaffRoleCatalog`. Professor e suporte não ganham nenhuma das duas. Audiência de aluno `billing` / `fiscal-document:own` e audiência de ator `billing` / `fiscal-documents:use`, como na ADR-0020. Asserção de `billing`, como na ADR-0021.

**`notification`.** Aceita finalidade e modelo `nota-fiscal-disponivel` no mesmo canal, como mais um ramo ao lado de `comprovante-de-compra`. Dados: número do pedido, curso e link. Sem anexo e sem URL de emissor. Entrega pela conta, regras já existentes (Identity fora retém; conta indisponível falha com `destinatario-indisponivel`). Uma Notificação 1.2.0 recusa o modelo; por isso ela sobe antes de `billing` publicar o aviso. Falha do aviso não tira a nota de `issued`.

**`bff-student` e `bff-admin`.** Repassam os `operationId`s. Não decidem situação. A exceção de audiência da nota está na ADR-0020. A lista e o detalhe de pedido continuam em `commerce`. A nota é outra chamada, com o próprio prazo; 502/504 nessa chamada não viram erro da lista.

### Bloco Frontend

Estado de nota e de prestador vem do servidor. O cliente não guarda PDF, XML, chave nem documento do tomador em `localStorage` ou cache de consulta. Download pede o arquivo na hora (`Cache-Control: no-store`).

**`student-spa`**, sob o basename já existente (`/student/` local, `/students` na estável):

| Tela | Rota | Estado |
|---|---|---|
| Pedido | `/pedidos/:orderId` | o pedido continua `getMyOrder`. Se o pedido está `paid`, uma segunda consulta `getMyFiscalDocument`. Falha dessa consulta não esconde o pedido |
| Meus pedidos | `/pedidos` | a lista continua `listMyOrders`. Para os pagos da página, a situação da nota é outra consulta. Falha dela não esconde a lista |

Pedido não pago: nenhuma menção a nota. Pedido pago:

- `awaiting-holder`: texto pedindo CPF, CNPJ ou NIF, com o campo da conta na própria página. O curso segue acessível pelo caminho que já existe. Erro de dígito é texto.
- `issuing`: "Nota em emissão". Sem prazo e sem pedir ação sobre o prestador.
- `issued` com `downloadsReady`: baixar PDF e baixar XML. Se `studyDocument`, a marca "sem valor fiscal" aparece junto dos botões, não só por cor. Esta fatia só fala com a bancada, então a marca é o caso esperado.
- `issued` sem `downloadsReady`: a nota existiu e o arquivo não está mais disponível. Sem botões de download e sem tratar isso como falha. O texto diz que a guarda é de um ano.
- `failed`: "A nota não pôde ser emitida. O acesso ao curso não depende disso." Sem botão de tentar de novo.

O campo da conta também aparece no pedido pago cuja nota já está `issued`, para o aluno substituir o documento da próxima compra. O texto diz que o PDF já disponível não muda.

**`admin-spa`**, basename `/admin/`:

| Tela | Rota | Estado |
|---|---|---|
| Pedidos | `/financeiro` | lista atual mais `lookupFinanceFiscalDocuments` (no máximo 50 ids). Pedido sem nota não ganha situação. Falha da consulta não esconde a lista |
| Pedido | `/financeiro/pedidos/:orderId` | detalhe atual mais `getFinanceFiscalDocument`. 404: não há nota. Outra falha: o pedido continua e a seção diz que a nota não carregou |
| Prestador | `/financeiro/prestador` | `getServiceProvider` / `putServiceProvider`. A chave digitada não volta na resposta nem fica em estado depois do envio |

No detalhe, `issued` sem `downloadsReady` mostra a mesma frase de guarda de um ano, sem download e sem nova tentativa. A nova tentativa só aparece em `failed`. Não há ação de cancelar nota nem de marcar emitida à mão. Não há código de serviço nem alíquota padrão: a tela do prestador é a única origem (QA-02). Professor e suporte continuam sem a área (a navegação já exige `financeiro.ler`). Salvar prestador e pedir nova tentativa exigem `fiscal.gerir`; sem ela, a ação responde 403.

As três superfícies novas (bloco no pedido do aluno, bloco no pedido do financeiro, tela do prestador) esperam o wireframe (EN-01).

---

## Mapa de Fatias Verticais

### V-01: o pedido pago abre um único documento e não segura a venda

- **Cobre:** RF-01; RN-F01, RN-F02, RN-F03, RN-F04, RN-F06; DP-01, DP-06; C-01, C-09, C-11.
- **Entrada / gatilho:** `vendas.compra-concluida.v1`.
- **Processamento:**
  - `commerce` inclui `courseTitle` no fato. Publicação nova sempre manda o título congelado no pedido. Matrícula, no mesmo deploy, segue concedendo acesso e ignora o campo.
  - Fiscal consome em fila própria. Sem `courseTitle`, confirma e não abre documento.
  - Com título, cria o documento se ainda não existe: descrição, `priceCents`, competência de Brasília e número do pedido copiados do fato. Reentrega não cria outro e não volta atrás uma situação que já andou.
  - Decide a situação inicial: sem tomador válido em Identity → `awaiting-holder`; com tomador e sem prestador → `awaiting-provider`; com os dois → `enqueueNfse` com `n = 1` e `queued`. Se Identity não responde, a mensagem não é confirmada e a fila tenta de novo: não se grava `awaiting-holder` no lugar de uma leitura que falhou, para não pedir documento a quem já tem. A concessão e o comprovante já saíram na transação de `commerce` e não dependem dessa leitura.
- **Saída observável:** um documento por pedido pago com título; nenhum para cortesia, pedido não pago ou fato sem título; acesso concedido como hoje.
- **Evidência / checkpoint:** teste de integração de `commerce` (fato com `courseTitle`; Matrícula ainda concede); teste de `billing` (três entregas do mesmo fato, um documento; fato sem título, zero documentos; sem tomador, `awaiting-holder` mesmo sem prestador). Conferir que a fila não é `commerce.entitlement-purchases`.
- **Bloqueado por:** Nenhum.

### V-02: o aluno informa o tomador e a nota que só esperava isso entra na fila

- **Cobre:** RF-03; RN-F07, RN-F08, RN-F09; C-05, C-11; ADR-0021.
- **Entrada / gatilho:** o aluno salva CPF, CNPJ ou NIF no pedido pago; o fato `identidade.identificacao-fiscal-informada.v1`.
- **Processamento:**
  - `putMyTaxDocument` → `putStudentTaxDocumentInternal`, sessão do aluno, CSRF e `Idempotency-Key`. CPF `52998224725` e CNPJ `19131243000197` gravam. Dígito trocado → 422 e a conta não muda. O mesmo valor não publica fato. Valor novo publica o fato sem o documento.
  - Fiscal consome o fato, relê `getStudentTaxDocumentInternal` e, se o documento estava `awaiting-holder` e o tomador agora é válido, passa a `awaiting-provider` ou, se o prestador já é utilizável, submete e fica `queued`.
  - Na página do pedido, `awaiting-holder` mostra o campo e o pedido de documento. Nota `issued` mantém o campo para a próxima compra e não altera o PDF.
- **Saída observável:** documento inválido não grava; documento válido tira a nota de `awaiting-holder`; o curso continua abrindo.
- **Evidência / checkpoint:** teste de Identity (dígito, NIF vazio, fato sem documento no payload); teste de `billing` (holder gravado com prestador ausente → `awaiting-provider`; com prestador → `queued`); teste de rota do `student-spa` no pedido pago. E-mail e documento ausentes do outbox e da routing key.
- **Bloqueado por:** V-01, EN-01.

### V-03: o financeiro conecta o prestador e solta quem já tem tomador

- **Cobre:** RF-02; RN-F05, RN-F10, RN-16; C-07; ADR-0005.
- **Entrada / gatilho:** papel financeiro salva emissor, código de serviço e alíquota.
- **Processamento:**
  - `putServiceProvider` exige `fiscal.gerir`. `getServiceProvider` exige `financeiro.ler` e não devolve a chave. Professor e suporte recebem 403 e não veem a tela.
  - Prestador incompleto não é utilizável e a venda segue. Ao ficar utilizável, todo documento `awaiting-provider` da escola entra na fila, cada um com a própria submissão, sem ação pedido a pedido. Documento `awaiting-holder` não entra.
  - A tela é `/financeiro/prestador`.
- **Saída observável:** `usable` verdadeiro só com os três; documentos que esperavam só o prestador ficam `queued`; chave não aparece na resposta nem em log.
- **Evidência / checkpoint:** teste de `billing` (primeira gravação sem chave → 422; segunda gravação sem chave mantém a anterior; liberação em lote só dos que têm tomador); teste de rota do `admin-spa`; token de professor não chama `billing`.
- **Bloqueado por:** V-01, EN-01.

### V-04: o emissor autoriza, o aluno e o financeiro baixam, e sai um aviso

- **Cobre:** RF-04 (autorização), RF-05, RF-06 (leitura), RF-07; RN-F09, RN-F11, RN-F12, RN-F13, RN-F14; C-03, C-04, C-06, C-08, C-12; ADR-0020.
- **Entrada / gatilho:** `nfse.issued` e `nfse.documents_ready` da submissão corrente; o aluno ou o financeiro abre o pedido pago.
- **Processamento:**
  - Webhook valida assinatura e grava antes do 200. Os dois eventos, em qualquer ordem, na submissão corrente, disparam o download. Com os bytes gravados, a mesma transação congela tomador, descrição, valor e competência, marca `studyDocument` se o emissor sinalizou estudo, passa a `issued` e acrescenta o aviso com `pedidoId` estável. Link é a página do pedido. Sem anexo.
  - Visão do aluno: `awaiting-provider` e `queued` saem como `issuing`; `failed` sai sem motivo. PDF e XML só com `downloadsReady`. Outro aluno recebe 404 `FISCAL_DOCUMENT_NOT_FOUND`.
  - Visão do financeiro: situação canônica, motivo e downloads no detalhe; a lista usa o lookup e omite pedido sem documento. As duas telas de pedido continuam de pé se `billing` falhar.
  - `nfse.issued` sozinho não libera download. Falha de Notificação não esconde o botão.
- **Saída observável:** PDF e XML baixados pelo aluno dono e pelo financeiro; um e-mail, sem anexo, abrindo a página do pedido; marca "sem valor fiscal" na tela quando `studyDocument` é verdadeiro; segunda emissão não nasce por causa da marca.
- **Evidência / checkpoint:** teste de webhook (assinatura ruim não muda nada; a mesma `X-Notaas-Delivery` não gera segundo aviso; `issued` sem `documents_ready` não baixa; a ordem inversa também espera os dois); teste de contrato do aviso (modelo, `pedidoId` do vetor, link sem URL de emissor); teste de rota das duas SPAs, incluindo *Meus pedidos* com o aviso falho e o botão ainda presente. O túnel de webhook, se for o caminho do ambiente, precisa deixar passar `/webhooks/v1/notaas/events` (risco abaixo).
- **Bloqueado por:** V-01. A tela, também por EN-01. O aviso, por Notificação já aceitar `nota-fiscal-disponivel`.

### V-05: a falha fica no mesmo documento e o financeiro pede outra tentativa

- **Cobre:** RF-04 (recusa e ausência), RF-06 (motivo e tentativa); RN-F02, RN-F03, RN-F11; C-10.
- **Entrada / gatilho:** `nfse.error`, timeout ou indisponibilidade na submissão; o financeiro pede nova tentativa num documento `failed`.
- **Processamento:**
  - Recusa definitiva vai a `failed` na hora. Ausência soma até três e então `failed`. O pedido segue *Pago* e a concessão não é consultada.
  - `retryFinanceFiscalDocument` só a partir de `failed`. Outra situação → 422 `FISCAL_DOCUMENT_NOT_RETRYABLE`. A tentativa incrementa `n`, zera o contador de ausência e volta a `queued` se tomador e prestador ainda servem; se o tomador foi removido, vai a `awaiting-holder`; se o prestador deixou de ser utilizável, vai a `awaiting-provider`. Não nasce outro `fiscalDocumentId`.
  - Autorização tardia da submissão abandonada responde 200 e não substitui a submissão nova nem cria segunda nota.
  - O aluno vê só a frase de falha. O financeiro vê o motivo e o botão.
- **Saída observável:** E001 não reenvia sozinho; E002 três vezes leva a `failed`; a nova tentativa conserva o id do documento; o curso abre.
- **Evidência / checkpoint:** testes de `billing` desses três caminhos e da autorização tardia; teste de rota do detalhe financeiro com o botão só em `failed`.
- **Bloqueado por:** V-04.

### V-06: um ano depois, o arquivo e o documento do tomador saem

- **Cobre:** QA-01; RN-F02, RN-F09.
- **Entrada / gatilho:** o aniversário de um ano, em UTC, de `issued` ou da última gravação válida do documento na conta.
- **Processamento:**
  - Na nota: apaga PDF, XML e a cópia do tomador. Conserva a linha, `issued`, `downloadsReady` falso. Não chama o emissor e não abre outro documento.
  - Na conta: apaga o documento e publica `identidade.identificacao-fiscal-informada.v1` sem o valor. Fiscal relê. Nota já emitida não é reescrita. Quem ainda esperava o tomador volta a `awaiting-holder`.
  - Exclusão da conta não remove a nota antes do aniversário de `issued`.
- **Saída observável:** a situação da nota continua 200 e `issued`; o download responde 404; a conta não devolve mais o documento; o pedido não ganha segunda nota.
- **Evidência / checkpoint:** teste de `billing` com o relógio no aniversário (arquivo some, linha fica, reentrega da compra não abre outra); teste de Identity (documento some um ano após a gravação e o fato não leva o valor); teste de rota das duas telas de pedido com `downloadsReady` falso.
- **Bloqueado por:** V-02, V-04.

### Habilitadores inevitáveis

| Habilitador | Por que não cabe numa fatia | Menor escopo | Primeira fatia desbloqueada |
|---|---|---|---|
| EN-01 — wireframe ASCII e Figma das três superfícies | O projeto só implementa tela com design aprovado. O bloco do aluno, o bloco do financeiro e a tela do prestador compartilham a linguagem da situação, inclusive a nota emitida cujo arquivo já saiu | Estender `docs/design/wireframes-compra.md` com o bloco no pedido do aluno, o bloco no pedido do financeiro e a rota `/financeiro/prestador` | V-02, V-03 e a parte de tela de V-04 |

---

## Contratos e Fronteiras

O acordo é o de [contracts.md](contracts.md). Esta spec não promove nada para `contracts/`.

### Mapeamento de mensagens e dados

| Contrato e identificador | Aplicação | Comportamento a implementar | Evidência |
|---|---|---|---|
| AsyncAPI `publicarCompraConcluida` / `receberCompraConcluida` | `commerce` envia; Matrícula e Fiscal recebem | Título opcional; Fiscal só abre documento se vier título; fila de Fiscal ≠ fila de Matrícula | Fato com e sem título |
| AsyncAPI `publicarIdentificacaoFiscalInformada` / `receberIdentificacaoFiscalInformada` | `identity` envia; Fiscal recebe | Sem documento no payload; Fiscal relê Identity | Payload sem CPF, CNPJ, NIF e e-mail |
| AsyncAPI `publicarNotaFiscalDisponivel` / `receberNotaFiscalDisponivel` | `billing` envia; `notification` recebe | Um aviso, `pedidoId` UUIDv5, link da página do pedido, sem anexo | Vetor do `pedidoId`; segundo envio não entrega de novo |

Não há ODCS (C-13).

**Percurso do dado sensível.**

| Dado | Onde nasce | Onde é copiado | Onde persiste | Onde é lido | Onde não entra |
|---|---|---|---|---|---|
| CPF, CNPJ ou NIF | Campo no pedido do aluno | Memória do BFF até Identity; memória de `billing` na submissão e no corpo enviado ao emissor | Conta em Identity; cópia congelada no documento `issued`, no banco de `billing` | Identity na gravação; `billing` na submissão | Broker, outbox, log, URL, cache, routing key, resposta do financeiro, e-mail |
| E-mail e nome | Conta já existente | Memória de `billing` na submissão; nome e e-mail congelados junto do tomador em `issued` | Cópia no documento `issued` | `getStudentTaxDocumentInternal` | JWT de audiência `billing`, broker da nota, log |
| Chave do emissor | Formulário do prestador, uma vez | Memória do BFF até `billing` | Cifrada no prestador | Só o adaptador, ao chamar o emissor | Resposta, log, outbox |
| Segredo HMAC do webhook | Configuração de ambiente | Memória na verificação | Configuração, como o segredo do Stripe | Verificação do webhook | API e SPA |
| PDF e XML | Resposta do emissor | Memória até gravar | Bytes no documento, banco de `billing`, por um ano a partir de `issued` | Download autenticado enquanto `downloadsReady` | URL pública, anexo, `localStorage`. Depois do ano, os bytes saem |

A nota `issued` não se apaga junto com a conta. O arquivo, a cópia do tomador e o documento na conta duram um ano, como em V-06.

### Mapeamento do contrato de API

Os schemas ficam nos YAML. O BFF repassa e traduz o erro de rede para 502/504 da borda, como nas rotas de pedido que já existem.

| operationId | Caminho de implementação |
|---|---|
| `putMyTaxDocument` | BFF do aluno → `putStudentTaxDocumentInternal` |
| `getMyTaxDocument` | BFF do aluno → `readStudentTaxDocumentInternal` |
| `putStudentTaxDocumentInternal` / `readStudentTaxDocumentInternal` | Conta do aluno em Identity, pela sessão |
| `getStudentTaxDocumentInternal` | Leitura de Fiscal, asserção de `billing` |
| `getMyFiscalDocument` | BFF do aluno → `getMyFiscalDocumentInternal` |
| `downloadMyFiscalDocumentPdf` / `downloadMyFiscalDocumentXml` | BFF do aluno → `getFiscalDocumentPdfInternal` / `getFiscalDocumentXmlInternal` |
| `lookupFinanceFiscalDocuments` | BFF do financeiro → `lookupFiscalDocumentsInternal` |
| `getFinanceFiscalDocument` | BFF do financeiro → `getFinanceFiscalDocumentInternal` |
| `downloadFinanceFiscalDocumentPdf` / `Xml` | BFF do financeiro → as mesmas leituras de arquivo, com permissão `financeiro.ler` |
| `retryFinanceFiscalDocument` | BFF do financeiro → `retryFiscalDocumentInternal` |
| `getServiceProvider` / `putServiceProvider` | BFF do financeiro → `getServiceProviderInternal` / `putServiceProviderInternal` |
| `receiveNfseProviderEvent` | Webhook de `billing`, fora do BFF |
| `enqueueNfse`, `getNfseStatus`, `getNfsePdf`, `getNfseXml` | Adaptador do emissor. Não é API nossa |

**Validações além do contrato:**

| operationId | Regra | Camada |
|---|---|---|
| `putStudentTaxDocumentInternal` | Verificador de CPF/CNPJ; NIF aparado e não vazio | domínio da conta |
| `receberCompraConcluida` | Sem título não abre documento; título não reabre um documento que já existe | domínio do documento |
| `retryFiscalDocumentInternal` | Só a partir de `failed`; senão 422 `FISCAL_DOCUMENT_NOT_RETRYABLE` | domínio do documento |
| `putServiceProviderInternal` | Utilizável só com chave, código e alíquota; alíquota no intervalo do contrato | domínio do prestador |
| `receiveNfseProviderEvent` | Efeito só na submissão corrente; arquivo baixado antes de `issued` | domínio do documento |
| `getMyFiscalDocumentInternal` | `sub` é o aluno; ator → 403 `ACTOR_NOT_ALLOWED`; pedido alheio → 404 | aplicação |

**Exceção → resposta HTTP:** o `code` de cada caso já está no YAML (`TAX_DOCUMENT_INVALID`, `FISCAL_DOCUMENT_NOT_FOUND`, `FISCAL_DOCUMENT_NOT_RETRYABLE`, `PROVIDER_KEY_REQUIRED`, `PROVIDER_INVALID`, `SIGNATURE_INVALID`, `ACTOR_NOT_ALLOWED`). Não criar código paralelo.

### Mapeamento de jornada

| User Story | Tela | operationId | Evidência |
|---|---|---|---|
| Aluno baixa a nota | Pedido `/pedidos/:orderId` | `getMyFiscalDocument`, downloads | Rota do `student-spa` |
| Aluno informa o documento | O mesmo pedido | `putMyTaxDocument` | Dígito inválido não grava |
| Financeiro vê a nota | Lista e detalhe do financeiro | `lookupFinanceFiscalDocuments`, `getFinanceFiscalDocument`, downloads | Rota do `admin-spa`; lista sobrevive a 502 |
| Financeiro conecta o emissor | `/financeiro/prestador` | `getServiceProvider`, `putServiceProvider` | Chave não volta |
| Financeiro pede nova tentativa | Detalhe, só em falha | `retryFinanceFiscalDocument` | Mesmo `fiscalDocumentId` |

### Entidades do domínio

| Entidade | Representação | Local |
|---|---|---|
| Documento fiscal | Agregado, um por pedido, com arquivos e tomador congelado só em `issued` | `billing`, schema `billing_access` |
| Prestador | Um por escola, chave cifrada | `billing` |
| Tomador | Documento na conta; cópia congelada na nota emitida | Identity; cópia em `billing` |
| Fato gerador | `vendas.compra-concluida.v1` com título | `commerce`, já existente |

---

## Arquivos a Modificar e a Referenciar

### A modificar

| Caminho | Fatia | Alteração |
|---|---|---|
| `src/commerce/src/CodeForCoders.Commerce.Application/Interfaces/PurchaseCompletedFact.cs` | V-01 | `courseTitle` opcional |
| `src/commerce/src/CodeForCoders.Commerce.Infra.Data/Sales/SalesPaymentSink.cs` | V-01 | Publicar o título congelado do pedido |
| `src/billing/src/CodeForCoders.Billing.Infra.Messaging/RabbitMqTopologyInitializer.cs` | V-01 | Filas de Fiscal na compra e na identificação, no exchange de quem publica |
| `src/billing/src/CodeForCoders.Billing.Infra.Messaging/RabbitMqPublisher.cs` | V-04 | Aviso no exchange de Notificação; `cobranca.*` onde já estão |
| `src/billing/src/CodeForCoders.Billing.Infra.Data/BillingDbContext.cs` | V-01 | Documento e prestador no modelo |
| `src/billing/src/CodeForCoders.Billing.Api/Endpoints/PaymentEndpoints.cs` | V-04 | Não misturar o webhook do emissor com o do Stripe; o novo ponto segue o mesmo cuidado de não registrar o corpo |
| `src/identity/src/CodeForCoders.Identity.Domain/Entities/Account.cs` | V-02 | Documento do tomador |
| `src/identity/src/CodeForCoders.Identity.Domain/Entities/StaffRoleCatalog.cs` | V-03 | `fiscal.gerir` no papel financeiro |
| `src/identity/src/CodeForCoders.Identity.Api/Endpoints/StudentContactEndpoints.cs` | V-02 | Não alargar esta rota; a leitura do tomador é outra |
| `src/identity/src/CodeForCoders.Identity.Api/Security/StaffSessionTokenIssuer.cs` | V-03 | Audiência `billing` configurada, senão o emissor recusa |
| `src/bff-student/src/CodeForCoders.BffStudent.Application/Common/BffSecurityOptions.cs` | V-04 | Prefixo `/api/v1/orders` continua `commerce` |
| `src/bff-student/src/CodeForCoders.BffStudent.Api/Security/BffSecurityMiddleware.cs` | V-04 | Exceção de audiência `billing` na nota |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/FinanceOrderEndpoints.cs` | V-04 | Pedido segue audiência `commerce`; a nota pede `billing` à parte |
| `src/notification/src/CodeForCoders.Notification.Application/Common/NotificationPurposes.cs` | V-04 | Modelo `nota-fiscal-disponivel` |
| `src/student-spa/src/features/student-purchase/components/student-order-screen.tsx` | V-02, V-04 | Bloco da nota e campo do tomador |
| `src/student-spa/src/features/student-purchase/components/my-orders-screen.tsx` | V-04 | Lista sobrevive sem a nota; situação quando a segunda chamada responde |
| `src/admin-spa/src/features/finance-area/components/finance-order-detail-screen.tsx` | V-04, V-05 | Seção da nota, download e nova tentativa |
| `src/admin-spa/src/features/finance-area/components/finance-orders-screen.tsx` | V-04 | Situação por lookup, sem derrubar a lista |
| `src/admin-spa/src/config/paths.ts` | V-03 | Rota `/financeiro/prestador` |
| `docker-compose.yml`, `docker-compose.remote.yml`, `docker-compose.komodo.apis.yml` | V-01 | Audiências, asserção de `billing`, fila, base do emissor, `StudentApp__PublicBaseUrl` em `billing` |
| `infra/cloudflared/billing-webhook.yml` | V-04 | Liberar `/webhooks/v1/notaas/events` no túnel, se esse for o caminho público |

### A referenciar (não alterar)

| Caminho | Por que consultar |
|---|---|
| `src/commerce/src/CodeForCoders.Commerce.Infra.Messaging/PurchaseGrantConsumerWorker.cs` | Desserializa o fato e ignora campo desconhecido; a concessão não lê nota |
| `src/commerce/src/CodeForCoders.Commerce.Application/Common/StudentAppOptions.cs` | Forma do link do pedido |
| `src/identity/src/CodeForCoders.Identity.Infra.Data/Outbox/OutboxPayloadProtector.cs` | Família AES-GCM para a chave do emissor; o outbox de `billing` não cifra sozinho |
| `src/billing/src/CodeForCoders.Billing.Infra.Data/Payments/GatewayInboxWorker.cs` | Inbox: gravar, responder, processar depois |
| `src/commerce/src/CodeForCoders.Commerce.Infra.Data/Entitlement/AccessExpirationCycle.cs` | Formato do ciclo periódico e idempotente usado na guarda de um ano |
| `docs/adr/0017-autenticacao-de-servico-commerce-em-billing.md` | A asserção de pagamento não muda |
| `tasks/prd-emissao-nfse/provider-contract-notaas.yaml` | Recorte externo; não é fronteira nossa |

---

## Análise de Impacto

| Componente | Tipo | Impacto e risco | Ação requerida |
|---|---|---|---|
| `commerce` mensagens 1.2.0 → 1.3.0 | modificado | Campo opcional. O consumidor em código ignora campo novo. Quem validar o schema 1.2.0 com `additionalProperties: false` recusa | Matrícula sobe no mesmo deploy que passa a publicar o título |
| `notification` 1.2.0 → 1.3.0 | modificado | Modelo novo no canal existente. A 1.2.0 recusa | Notificação sobe antes de Fiscal publicar o aviso |
| Identity | modificado | Audiência, permissão, documento na conta, fato novo | Sobe primeiro |
| `billing` | modificado | Módulo novo no serviço que já cobra. Webhook novo | Sobe depois de Identity, Notificação e `commerce` |
| BFFs e SPAs | modificado | Segunda chamada | Por último |
| Pedido de `CAP-011` | inalterado | JSON igual | Não embutir a nota |
| Stripe | inalterado | Webhook e `cobranca.*` | Não compartilhar inbox com o emissor de nota |
| Túnel `billing-webhook.tasso.dev.br` | modificado | Hoje só `/webhooks/v1/stripe/events` | Incluir o caminho da Notaas antes de homologar o webhook por esse host |

Ordem de deploy: Identity → Notificação → `commerce` (com Matrícula) → `billing` → BFFs → SPAs. Pedido que já estava *Pago* antes do título existir não ganha nota.

---

## Riscos e Preocupações

| Preocupação | Local (`arquivo:linha`) | Impacto | Mitigação |
|---|---|---|---|
| O prefixo `/api/v1/orders` pede audiência `commerce` pelo caminho mais longo | `src/bff-student/src/CodeForCoders.BffStudent.Api/Security/BffSecurityMiddleware.cs:90` e `src/bff-student/src/CodeForCoders.BffStudent.Application/Common/BffSecurityOptions.cs:24` | A nota receberia JWT de pedido e `billing` recusaria, ou aceitaria o token errado | Exceção explícita, no estilo das linhas 92–93 do middleware, para os três caminhos da nota. Teste dos dois tokens |
| `RabbitMqResourceNames.Compose` acrescenta o namespace ao exchange de `commerce` | `src/billing/src/CodeForCoders.Billing.Infra.Messaging/RabbitMqResourceNames.cs:20` | A fila de Fiscal ouviria `commerce.events.default` e não veria a compra, publicada em `commerce.events` | Ligar a fila ao exchange em que `commerce` publica, sem sufixo de namespace de `billing` |
| O publicador de `billing` manda toda routing key para o exchange próprio | `src/billing/src/CodeForCoders.Billing.Infra.Messaging/RabbitMqPublisher.cs:31` | O aviso não chegaria a Notificação, que consome `notification.events.default` | Mapa de exchange por routing key, como `commerce` já faz; `cobranca.*` permanecem |
| O outbox de `billing` grava o payload em claro | `src/billing/src/CodeForCoders.Billing.Infra.Data/Outbox/OutboxMessageWriter.cs:15` | Um payload com documento ou e-mail ficaria no banco e iria ao broker | O aviso e os fatos de Fiscal não levam esses dados. A cópia do tomador fica só na linha do documento `issued` |
| O túnel público de `billing` responde 404 fora do Stripe | `infra/cloudflared/billing-webhook.yml:7` | O fake-nfe ou a Notaas não entregam o webhook nesse host | Acrescentar o caminho da nota no ingress antes de usar esse host. Localmente o webhook é `localhost:5110` |
| Emissor de token de ator recusa audiência ausente | `src/identity/src/CodeForCoders.Identity.Api/Security/StaffSessionTokenIssuer.cs:14` | O financeiro toma 502 na nota se `billing` não estiver em `AudienceScopes` | Configurar o escopo `fiscal-documents:use` no mesmo passo da audiência de aluno |
| A leitura de contato exige o emissor `notification` | `src/identity/src/CodeForCoders.Identity.Api/Endpoints/StudentContactEndpoints.cs:21` | Reusar a rota daria 403 a `billing` ou abriria o e-mail para o escopo errado | Operação nova, escopo `student-tax-document:read` |
| O detalhe financeiro é uma chamada só e já mostra e-mail | `src/admin-spa/src/features/finance-area/components/finance-order-detail-screen.tsx:13` | Tratar a nota como parte dessa resposta derruba o pedido quando `billing` cai, e convida a colocar o CPF na mesma tela | Segunda consulta. O contrato do financeiro não tem o documento do tomador; a seção não o exibe |
| O webhook do Stripe lê o corpo inteiro em memória | `src/billing/src/CodeForCoders.Billing.Api/Endpoints/PaymentEndpoints.cs:29` | O mesmo padrão no webhook da nota registraria tomador se alguém logar o corpo | Não registrar corpo nem cabeçalho de assinatura. O handler já loga só o path (`GlobalExceptionHandler.cs:49`) |

---

## Decisões Técnicas

- **Decisão:** PDF e XML ficam em bytes no documento, no PostgreSQL de `billing`.
- **Racional:** o arquivo tem de permanecer na plataforma, sem URL do emissor, e `billing` não tem cliente de objeto. S3 está decidido para vídeo.
- **Trade-offs:** o banco guarda arquivo por um ano. Uma nota é pequena. Depois disso os bytes saem e a linha fica.
- **Alternativas rejeitadas:** buscar o arquivo no emissor a cada download (a nota some se o emissor cair depois de autorizar); gravar no bucket de mídia (mistura domínio e torna o arquivo vizinho de vídeo).

- **Decisão:** falha ao baixar PDF ou XML depois de `documents_ready` não é a ausência que leva a `failed` e não incrementa `n`.
- **Racional:** a nota já pode ter sido autorizada. `failed` seguido de nova tentativa com outra chave criaria uma segunda nota, contra RN-F02.
- **Trade-offs:** o documento pode permanecer `queued` enquanto o arquivo não grava, e o aluno continua vendo "Nota em emissão".
- **Alternativas rejeitadas:** contar o download no limite de três e falhar.

- **Decisão:** `pedidoId` do aviso é o UUIDv5 descrito no bloco backend, com vetor fixo.
- **Racional:** a republicação da mesma transição precisa do mesmo id para Notificação deduplicar, mesmo se a linha de outbox for reescrita.
- **Trade-offs:** o exemplo do AsyncAPI não é o valor calculado.
- **Alternativas rejeitadas:** `Guid` aleatório gravado à parte, que divergiria se a transação recomeçasse antes de persistir o id.

- **Decisão:** a guarda é de um ano, no aniversário UTC de `issued` para o arquivo e a cópia do tomador, e no aniversário da última gravação válida para o documento na conta (QA-01, 2026-10-12).
- **Racional:** o responsável fechou o prazo que o PRD tinha deixado aberto. Um ano, não o prazo legal de cerca de cinco anos que o PRD só recomendava.
- **Trade-offs:** depois de um ano o aluno e o financeiro não baixam mais o arquivo. A linha do documento continua, senão o mesmo pedido poderia emitir de novo.
- **Alternativas rejeitadas:** guardar por tempo indeterminado; apagar a linha inteira no fim do prazo.

- **Decisão:** código de serviço e alíquota não têm valor de fábrica. O financeiro informa os dois na tela (QA-02, 2026-10-12).
- **Racional:** são configuração da escola, não regra desta fatia.
- **Trade-offs:** sem os dois, o prestador não fica utilizável e a venda segue.
- **Alternativas rejeitadas:** gravar um código ou uma alíquota fixos no produto.

- **Decisão:** o emissor desta fatia é só a bancada (fake-nfe). Sem conta de produção da Notaas e sem certificado A1 (QA-03, 2026-10-12).
- **Racional:** a prova é na bancada. O PDF da bancada não é documento fiscal válido.
- **Trade-offs:** a marca de sem valor fiscal é o caso esperado. A mesma porta continua capaz de apontar para outro host numa fatia futura, que esta não configura.
- **Alternativas rejeitadas:** exigir a conta de produção para construir a fatia.

Aplicar ADR-0005 ao JWT de ator em `billing` não é decisão nova. C-01 a C-13 não se reabrem.

---

## Verificação

- **Cenários críticos não óbvios:** três entregas do mesmo pagamento abrem uma nota; cortesia e expiração sem pagamento não abrem; sem prestador e sem tomador a nota fica `awaiting-holder`; CPF válido passa a `awaiting-provider` e, com prestador, a `queued`; dígito inválido responde 422 e a nota não muda; nota autorizada conserva tomador, descrição, valor e competência depois da troca do documento; 202 deixa `queued`; `nfse.issued` sozinho não baixa; com `documents_ready` e os arquivos, baixa; assinatura ruim não muda nada; a mesma entrega não manda segundo aviso; E001 falha sem retry automático; E002 três vezes falha; nova tentativa conserva o id; autorização da submissão antiga não cria outra nota; marca de estudo não cria outra nota; compra sem `courseTitle` não abre documento; `billing` parado devolve 502 na nota e *Meus pedidos* abre; outro aluno e um professor não baixam o arquivo; no aniversário de um ano o arquivo e a cópia do tomador somem, a linha `issued` fica e a compra reentregue não abre outra nota; o documento na conta some um ano após a última gravação.
- **Dados ou ambiente especiais:** só a bancada fake-nfe. Sem Notaas de produção e sem certificado A1. Chaves de asserção e o segredo HMAC por `scripts/generate-local-env.sh`, no padrão das chaves que já existem. Fuso `America/Sao_Paulo` na competência. A guarda usa o aniversário em UTC. O webhook local é `http://localhost:5110/webhooks/v1/notaas/events`.
- **Observabilidade além do padrão:** contadores de documento aberto, de `issued`, de `failed` e de autorização ignorada por submissão antiga. Nenhum log com e-mail, CPF, CNPJ, NIF, chave ou corpo de webhook. A verificação procura esses valores na telemetria de `billing`, `commerce` e Identity.
- **Verificação dos contratos:** os cenários acima exercem os `operationId`s e as três mensagens. Lint dos YAML já foi feito na aprovação do contrato e não substitui estes testes. O download se prova pelo arquivo autenticado e pela marca de estudo, não por JSON.

---

## Architecture Decision Records

- [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md) — o financeiro chega a `billing` com JWT de ator. Sem ADR nova.
- [ADR-0013](../../docs/adr/0013-jwt-de-aluno-validado-por-servicos-de-dominio.md) — JWT de aluno por audiência, sem e-mail fora de `media`.
- [ADR-0016](../../docs/adr/0016-jwt-de-aluno-em-commerce.md) — precedente da audiência por serviço. O pedido continua nela.
- [ADR-0017](../../docs/adr/0017-autenticacao-de-servico-commerce-em-billing.md) — pagamento inalterado. `commerce` não ganha chamada de nota.
- [ADR-0018](../../docs/adr/0018-notificacao-resolve-destinatario-pela-conta.md) — o aviso vai pela conta; Notificação resolve o e-mail.
- [ADR-0020](../../docs/adr/0020-jwt-de-aluno-em-billing.md) — proposta. Audiência `billing` para o aluno baixar a própria nota.
- [ADR-0021](../../docs/adr/0021-billing-le-tomador-em-identity.md) — proposta. `billing` lê o tomador por asserção, sem o documento no broker.
