---
tsg_artifact: prd
product: code-4-coders
capability: CAP-016
version: 1.0
status: approved
updated: 2026-10-11
sources: backlog/capabilities.md@1.4, vision.md@1.2, context/domain-map.md@1.2, context/architecture-baseline.md@1.2, domains/identidade-e-acesso/domain.md@1.1, domains/catalogo-e-oferta/domain.md@1.1
---

# Emissão de NFS-e da compra avulsa

## Visão Geral

A escola já vende um curso à vista (`CAP-011`) e libera o acesso sozinha. O comprovante por e-mail
registra a compra para o aluno, e o próprio comprovante declara que não é documento fiscal. A
obrigação de NFS-e nasce com essa venda (R13), e hoje ninguém emite a nota.

Esta entrega cobre a primeira fatia de `CAP-016`: cada pedido de compra avulsa que fica *Pago*
gera um documento fiscal, o aluno baixa esse documento e o financeiro acompanha a emissão. A falha
da nota, a falta de CPF e a ausência de configuração do prestador não desfazem a venda nem o
acesso (DE06).

Cancelamento da nota e nota de assinatura ficam para as capacidades que criam esses fatos.

---

## Rastreabilidade

### Capacidade e fronteiras

- **Capacidade:** `CAP-016` — Emissão de NFS-e.
- **Escopo desta entrega:** fatia da compra avulsa, confirmada em 2026-10-11. Um documento fiscal
  por pedido que passa a *Pago*; prestador configurado pela escola; tomador identificado por CPF,
  CNPJ ou NIF; emissão assíncrona no provedor; PDF e XML para o aluno autenticado e para o
  financeiro; aviso transacional quando o documento fica disponível.
- **Fora desta entrega:**
  - cancelamento fiscal, inclusive por reembolso → `CAP-015` (Fase 2);
  - NFS-e de assinatura, renovação ou ciclo de cobrança → `CAP-013` (Fase 2);
  - emissão retroativa de pedidos que já estavam *Pago* antes de a emissão estar ligada;
  - NF-e, NFC-e, lote, contabilidade e ERP;
  - código de serviço ou alíquota diferentes por curso;
  - carta de correção e substituição de nota já emitida.
- **Domínios atravessados:**
  - **Fiscal** — dono desta capacidade. Sem domain doc: é a única capacidade do domínio. As regras
    nascem aqui (RN-F) e serão absorvidas pelo domain doc se o domínio ganhar um segundo PRD.
  - **Vendas e Checkout** — o pedido *Pago* é o fato gerador desta fatia. Sem domain doc no
    horizonte (`CAP-012` é Fase 5). Regras do pedido já nascidas em `CAP-011` (RN-V).
  - **Cobrança e Assinatura** — confirma o pagamento que faz o pedido ficar *Pago*. Não emite nota.
    Sem domain doc ainda.
  - **Identidade e Acesso** — [domains/identidade-e-acesso/domain.md](../../domains/identidade-e-acesso/domain.md).
    Conta do aluno (nome, e-mail, documento do tomador) e papel financeiro.
  - **Catálogo e Oferta** — [domains/catalogo-e-oferta/domain.md](../../domains/catalogo-e-oferta/domain.md).
    O preço da oferta é maior que zero (RN-O07); o valor da nota é o preço congelado no pedido.
  - **Notificação** — entrega o aviso de que a nota está disponível. Não monta o PDF.
- **Junta entre os domínios** (Domain Map §6, §7 e §8; tabela de dependências Cobrança → Fiscal):
  - *Cobrança → Vendas.* Cobrança informa o pagamento confirmado. Vendas é quem marca o pedido
    como *Pago* e publica a compra concluída. Fiscal não lê o gateway.
  - *Vendas → Fiscal.* O fato gerador desta fatia é o pedido que passa a *Pago*, com aluno, curso,
    preço congelado e a escola do pedido. Fiscal é dono do documento. Vendas não guarda situação
    de nota nem documento do tomador.
  - *Fiscal → Identidade.* No momento de emitir, Fiscal lê nome, e-mail e documento do tomador na
    conta. O pedido não carrega esses dados. A nota emitida congela a cópia do tomador; a conta
    continua sendo de Identidade.
  - *Fiscal → Notificação.* Quando PDF e XML podem ser baixados, Fiscal pede o envio de um aviso
    transacional pela conta do aluno. Notificação entrega; o arquivo fica na plataforma.
  - *Financeiro.* O papel financeiro, que já consulta pedidos, vê a situação da nota, baixa o
    arquivo e pede nova tentativa. Professor e suporte não veem dado fiscal (RN-16).
- **Dependências entre capacidades:** `CAP-011` e `CAP-026` (`done`). `CAP-013` é o outro fato
  gerador possível no card da capacidade e não entra nesta fatia. Provedor: contrato da Notaas no
  recorte do fake-nfe (OD89).
- **Restrições do baseline:**
  - falha fiscal nunca impede venda nem acesso (DE06);
  - provedor de NFS-e atrás de camada anticorrupção; regra municipal não aparece em pedido,
    acesso, aula nem progresso;
  - a confirmação do provedor entra por ponto dedicado, autenticada e idempotente;
  - e-mail do aluno não vai em log, URL, nome de objeto, chave de cache nem routing key (G23).
    O mesmo vale para CPF, CNPJ e NIF.

### Vision Doc

- **Objetivos de negócio atendidos:** Fase 2, capacidade C10 — a venda gera documento fiscal.
  Fecha o risco R13 para a compra avulsa daqui para a frente. Não fecha sozinho o critério inteiro
  da Fase 2 (assinatura que renova e régua de inadimplência são outras capacidades).
- **Restrições globais aplicáveis:** operação brasileira, em português; NFS-e e LGPD; a plataforma
  emite documento fiscal e não fecha contabilidade; dois engenheiros, com o PRD como entrada da
  implementação.
- **Non-Goals globais respeitados:** não é ERP; não migra histórico de outra plataforma; não é
  aplicativo nativo; não define identidade visual.

### Domain Docs

- **Entidades envolvidas:**
  - Fiscal (nascem aqui): Documento Fiscal, Fato Gerador, Tomador do Serviço, Prestador.
  - Vendas: Pedido.
  - Identidade: Conta do aluno, Papel financeiro.
  - Catálogo: Oferta, só como origem do preço que o pedido já congelou.
- **Regras de negócio referenciadas:** RN-V05, RN-V08, RN-V09, RN-V10 e RN-V12 de `CAP-011`;
  RN-O07 de Catálogo; RN-16 de Identidade; DE06.
- **Regras nascidas neste PRD:** RN-F01 a RN-F14.
- **Eventos consumidos:** o pedido que passa a *Pago* (inclui o pagamento tardio de RN-V08).
  Cortesia, pedido aguardando pagamento, expirado ou cancelado sem ter ficado *Pago* não são fato
  gerador.
- **Eventos produzidos:** pedido de envio do aviso "nota disponível", com finalidade transacional,
  pela conta do aluno. A situação da nota não é publicada para Matrícula, aula nem progresso.

## Termos Canônicos

| Termo | Definição de negócio | Escopo/Fonte |
|---|---|---|
| Documento Fiscal | A NFS-e de um pedido *Pago*. Um pedido tem no máximo um | Esta entrega |
| Prestador | A escola que emite, com emissor conectado, código de serviço e alíquota de ISS | Esta entrega |
| Tomador | O aluno do pedido, identificado por CPF, CNPJ ou NIF | Domain Map · esta entrega |
| Nota disponível | PDF e XML já podem ser baixados por quem tem direito | Esta entrega |
| Sem valor fiscal | Marca do próprio documento quando o emissor é a bancada de estudo | OD89 |

Situações do documento, vistas pelo financeiro:

| Situação | Significado |
|---|---|
| Aguardando prestador | O pedido está *Pago* e a escola ainda não tem emissor utilizável |
| Aguardando tomador | Falta CPF, CNPJ ou NIF válido na conta do aluno |
| Na fila | O emissor aceitou e ainda não autorizou |
| Emitida | O emissor autorizou e PDF e XML podem ser baixados |
| Falha | O emissor recusou ou as tentativas automáticas se esgotaram |

O aluno não vê esses cinco nomes. Vê o que a seção de experiência descreve.

---

## Objetivos

- Todo pedido que passar a *Pago* com a emissão ligada tem exatamente um documento fiscal.
- O aluno com a nota emitida baixa PDF e XML sem pedir ao suporte.
- Uma nota com falha, sem tomador ou sem prestador deixa o curso assistível.
- O financeiro enxerga a situação de cada pedido pago e consegue pedir nova tentativa numa falha.
- Nenhum pedido pago produz duas notas autorizadas.

Não há meta de conversão nem de ticket. OD2 continua sem métrica de sucesso do produto.

---

## Histórias de Usuário

- Como **aluno**, quero baixar a nota do curso que paguei, para ter o documento fiscal da compra.
- Como **aluno** sem CPF, CNPJ ou NIF na conta, quero informar o documento depois da compra, para
  a nota sair sem eu perder o acesso enquanto isso.
- Como **financeiro**, quero ver, em cada pedido pago, se a nota está disponível, esperando ou
  falhou, para não descobrir a omissão pelo contador.
- Como **financeiro**, quero conectar o emissor da escola e informar código de serviço e alíquota,
  para as próximas vendas emitirem sem planilha.
- Como **financeiro**, quero pedir de novo a emissão de uma nota que falhou, para corrigir o
  prestador ou esperar o aluno completar o documento e seguir.

---

## Regras de negócio desta entrega

| ID | Regra | Origem |
|---|---|---|
| RN-F01 | **O fato gerador desta fatia é o pedido que passa a *Pago*.** Vale para cartão, PIX, boleto e para o pagamento tardio que conclui pedido expirado ou cancelado (RN-V08). Cortesia, clique, pedido não pago e reembolso feito fora da plataforma não geram documento | Domain Map §8 · RN-V05 · RN-V08 |
| RN-F02 | **Há no máximo um documento fiscal por pedido.** Confirmação reentregue, nova tentativa e atraso do emissor não criam outro. Pedido *Pago* não fica sem registro de documento: o registro nasce junto com a passagem a *Pago* | RN-V09 |
| RN-F03 | **Venda e acesso não esperam a nota.** Pedido, concessão, comprovante de compra e aula seguem as regras que já têm. Situação fiscal não é consultada por player, aula nem progresso | DE06 · RN-V10 |
| RN-F04 | **O valor da nota é o preço congelado no pedido, em reais.** Mudança posterior da oferta não altera a nota. A descrição do serviço é "Acesso ao curso {nome do curso congelado no pedido}" | RN-V03 · RN-O07 |
| RN-F05 | **Código de serviço e alíquota de ISS são da escola, não do curso.** Os dois são obrigatórios para o prestador estar utilizável. A plataforma não calcula imposto nem mostra destaque de ISS próprio: envia o total e a alíquota configurados e exibe o documento que o emissor devolve | DP-03 |
| RN-F06 | **A competência é o mês civil de Brasília em que o pagamento foi confirmado** | Esta entrega |
| RN-F07 | **O tomador é o aluno do pedido.** A identificação é um CPF válido, um CNPJ válido ou um NIF não vazio. CPF e CNPJ são recusados quando os dígitos não fecham. Sem identificação válida o documento fica *Aguardando tomador* | DP-02 |
| RN-F08 | **O documento do tomador mora na conta e serve às próximas compras.** O aluno informa e pode substituir o que ainda não foi congelado numa nota emitida. O financeiro não edita o documento do aluno | DP-02 |
| RN-F09 | **A nota emitida congela tomador, descrição, valor e competência.** Troca de nome, e-mail ou documento na conta depois da autorização não reemite e não altera o PDF já disponível | Esta entrega |
| RN-F10 | **O prestador utilizável tem emissor conectado, código de serviço e alíquota.** Sem isso o documento fica *Aguardando prestador*. Quando os três passam a existir, os documentos que esperavam só o prestador entram na fila, sem ação pedido a pedido | DP-03 |
| RN-F11 | **A autorização vem do emissor, não do envio.** *Na fila* cobre o aceite ainda sem autorização. *Emitida* exige autorização e os dois arquivos. Recusa definitiva do emissor leva a *Falha* na hora. "Sem resposta" e "emissor indisponível" têm três tentativas no mesmo documento; a terceira ausência seguida leva a *Falha*. Código de serviço recusado não entra em tentativa automática | OD89 |
| RN-F12 | **Quem baixa o arquivo é o aluno daquele pedido, autenticado, ou o papel financeiro da mesma escola.** O aviso por e-mail não contém o arquivo nem endereço público do emissor: leva o aluno ao próprio pedido. Professor, suporte e aluno de outro pedido não baixam | RN-16 · G23 |
| RN-F13 | **O aviso sai uma vez, quando a nota fica disponível, com finalidade transacional.** Não sai em *Aguardando tomador*, *Aguardando prestador* nem em *Falha*. Falha do aviso não tira a nota de *Emitida* nem esconde o botão de baixar | CAP-026 |
| RN-F14 | **Documento marcado pelo emissor como sem valor fiscal é exibido com essa marca** para o aluno e para o financeiro. A marca não muda a situação *Emitida* nem libera uma segunda emissão "de verdade" do mesmo pedido | OD89 |

---

## Funcionalidades Principais

### RF-01: Nasce um documento quando o pedido fica pago

**Descrição:** A passagem do pedido a *Pago* abre o único documento fiscal daquele pedido e não
atrasa concessão nem comprovante.

**Critérios de Aceitação:**

- **Given** um pedido que acaba de ficar *Pago* e uma escola com prestador utilizável e aluno com
  CPF válido
  **When** a venda se confirma
  **Then** existe um documento *Na fila* para esse pedido, a concessão segue a regra já existente
  e o comprovante de compra não é substituído pela nota

- **Given** o mesmo pagamento confirmado entregue de novo
  **When** o fato se repete
  **Then** continua existindo um único documento, na situação em que já estava

- **Given** uma cortesia ou um pedido que expira sem pagamento
  **When** o tempo passa
  **Then** nenhum documento fiscal é criado

**Prioridade:** Must Have

**Rastreabilidade:** RN-F01, RN-F02, RN-F03

---

### RF-02: Configurar o prestador

**Descrição:** O financeiro conecta o emissor da escola e informa código de serviço e alíquota de
ISS. Sem os três, a escola ainda vende.

**Critérios de Aceitação:**

- **Given** o papel financeiro
  **When** informa emissor, código de serviço e alíquota
  **Then** o prestador fica utilizável e os documentos *Aguardando prestador* que não esperam
  tomador entram na fila

- **Given** um pedido que fica *Pago* sem prestador utilizável
  **When** o aluno abre o curso
  **Then** o acesso existe e o documento permanece *Aguardando prestador*

- **Given** professor ou suporte
  **When** tenta abrir a configuração do prestador ou a nota de um pedido
  **Then** não vê dado fiscal

**Prioridade:** Must Have

**Rastreabilidade:** RN-F05, RN-F10, RN-16

---

### RF-03: Identificar o tomador

**Descrição:** O aluno informa CPF, CNPJ ou NIF na conta. A compra não pede isso para concluir. A
nota espera até a identificação ser válida.

**Critérios de Aceitação:**

- **Given** um aluno sem documento na conta e prestador utilizável
  **When** o pedido fica *Pago*
  **Then** o documento fica *Aguardando tomador*, o curso abre, e o pedido mostra que falta
  informar CPF, CNPJ ou NIF

- **Given** um documento *Aguardando tomador* e a escola ainda sem prestador utilizável
  **When** o aluno salva um documento válido
  **Then** a situação passa a *Aguardando prestador* e o documento não entra na fila

- **Given** esse aluno
  **When** salva um CPF ou CNPJ com dígitos válidos, ou um NIF não vazio
  **Then** o documento que esperava só o tomador entra na fila e a conta guarda o documento para
  a próxima compra

- **Given** um CPF ou CNPJ com dígito errado
  **When** o aluno tenta salvar
  **Then** a conta não grava o valor e o documento continua *Aguardando tomador*

- **Given** uma nota já *Emitida*
  **When** o aluno troca o documento da conta
  **Then** o PDF já disponível permanece com o tomador congelado e a próxima compra usa o novo

**Prioridade:** Must Have

**Rastreabilidade:** RN-F07, RN-F08, RN-F09

---

### RF-04: Emissão e falha

**Descrição:** A nota sai da fila quando o emissor autoriza. Falha fica visível para o financeiro
e invisível para o acesso.

**Critérios de Aceitação:**

- **Given** um documento *Na fila*
  **When** o emissor autoriza e PDF e XML existem
  **Then** a situação passa a *Emitida*, com valor, descrição, competência e tomador congelados
  (RN-F04, RN-F06, RN-F09)

- **Given** um documento *Na fila*
  **When** o emissor recusa o código de serviço
  **Then** a situação vai a *Falha* sem tentativa automática, o pedido continua *Pago* e o aluno
  continua com acesso

- **Given** "sem resposta" ou "emissor indisponível"
  **When** isso se repete no mesmo documento
  **Then** a terceira ausência seguida leva a *Falha*. Antes disso o aluno vê a nota em emissão,
  não um erro de pagamento

- **Given** um documento em *Falha* e o papel financeiro
  **When** pede nova tentativa
  **Then** o mesmo documento volta para *Na fila* e não nasce um segundo

**Prioridade:** Must Have

**Rastreabilidade:** RN-F02, RN-F11, RN-F03

---

### RF-05: Aluno baixa a nota e recebe o aviso

**Descrição:** A nota disponível aparece no pedido do aluno. O e-mail só avisa.

**Critérios de Aceitação:**

- **Given** um documento *Emitida* do próprio aluno
  **When** ele abre o pedido
  **Then** consegue baixar PDF e XML

- **Given** a transição para *Emitida*
  **When** os arquivos ficam disponíveis
  **Then** sai um único aviso transacional pela conta, levando ao pedido, sem anexo e sem endereço
  do emissor

- **Given** o aviso não é entregue
  **When** o aluno abre *Meus pedidos*
  **Then** o download continua disponível

- **Given** outro aluno autenticado
  **When** tenta baixar a nota de um pedido que não é seu
  **Then** não obtém o arquivo

**Prioridade:** Must Have

**Rastreabilidade:** RN-F12, RN-F13

---

### RF-06: Financeiro acompanha a nota no pedido

**Descrição:** Na consulta de pedidos que o financeiro já usa, cada pedido pago mostra a situação
fiscal e, quando emitida, o download. Pedido não pago não tem nota.

**Critérios de Aceitação:**

- **Given** o papel financeiro da escola do pedido
  **When** abre um pedido *Pago*
  **Then** vê a situação do documento e, se *Emitida*, baixa PDF e XML

- **Given** um pedido *Aguardando pagamento*, *Expirado* ou *Cancelado* que nunca ficou *Pago*
  **When** o financeiro o abre
  **Then** não há documento fiscal

- **Given** um documento *Aguardando tomador* ou *Falha*
  **When** o financeiro olha o pedido
  **Then** vê o motivo (falta o tomador, ou a falha informada pelo emissor) e não consegue marcar
  a nota como emitida à mão

**Prioridade:** Must Have

**Rastreabilidade:** RN-F01, RN-F12

---

### RF-07: Bancada de estudo

**Descrição:** Documento produzido pelo emissor de estudo é utilizável no fluxo e declarado sem
valor fiscal.

**Critérios de Aceitação:**

- **Given** um documento *Emitida* marcado pelo emissor como sem valor fiscal
  **When** o aluno ou o financeiro o abre
  **Then** a tela e o arquivo deixam clara a marca "sem valor fiscal", e o pedido não ganha uma
  segunda emissão por causa da marca

**Prioridade:** Must Have

**Rastreabilidade:** RN-F14, OD89

---

## Experiência do Usuário

O aluno não ganha uma área nova. A nota mora no pedido que ele já consulta em *Meus pedidos*.

- Pedido não pago: nenhuma menção a nota fiscal.
- Pedido pago, falta documento do tomador: texto pedindo CPF, CNPJ ou NIF, com o campo na conta e
  o curso já acessível. O comprovante de compra continua sendo o e-mail que já existe.
- Pedido pago, nota em emissão ou prestador ainda não configurado: "Nota em emissão". Sem prazo
  falso e sem pedir ao aluno que faça algo sobre o prestador.
- Nota disponível: ação de baixar PDF e de baixar XML. Se o emissor marcou sem valor fiscal, a
  marca aparece junto dos botões.
- Falha: "A nota não pôde ser emitida. O acesso ao curso não depende disso." Sem botão de tentar
  de novo para o aluno.

O financeiro configura o prestador numa tela da área que já é dele e vê a situação no pedido. A
nova tentativa só aparece em *Falha*. Não há ação de cancelar nota.

A situação não é comunicada só por cor. O campo de documento do tomador tem rótulo e o erro de
dígito é texto, não apenas borda vermelha. Identidade visual não é definida aqui.

---

## Decisões de Produto

| ID | Decisão confirmada | Alternativas descartadas e motivo | Impacto no PRD | Registro |
|---|---|---|---|---|
| DP-01 | Um documento por pedido que passa a *Pago*, fora do caminho da concessão | Emitir no retorno do browser — o aluno que fecha a aba ficaria sem nota e a venda pareceria depender da nota | RF-01, RN-F03 | DE06 · fatia de 2026-10-11 |
| DP-02 | CPF, CNPJ ou NIF na conta, depois da compra se ainda não houver | Travar o checkout até ter CPF — atrita a venda que o MVP acabou de fechar. Emitir sem documento — o emissor recusa o tomador. Reaproveitar CPF digitado no boleto do gateway — a plataforma não recebe esse dado | RF-03 | RN-F07 |
| DP-03 | Um código de serviço e uma alíquota por escola | Código por curso — não há essa diferença no catálogo e aumentaria a configuração antes da primeira nota | RF-02, RN-F05 | — |
| DP-04 | Download autenticado no pedido; e-mail só avisa | Anexar PDF ou mandar o endereço do emissor — espalha o arquivo e fura G23 | RF-05 | RN-F12 |
| DP-05 | Cancelamento e assinatura ficam de fora | Incluir cancelamento porque o emissor já tem a operação — o fato de reembolso ainda não existe (`CAP-015`) e a nota cancelada sem revogação de acesso repetiria o buraco do reembolso no gateway | Não-Objetivos | Fatia de 2026-10-11 |
| DP-06 | Não emitir nota para pedido que já estava *Pago* quando a emissão ligou | Varrer o passado automaticamente — mistura venda de homologação com obrigação fiscal e é o tema de Q5, não desta fatia | Não-Objetivos | R13 · Q5 |

---

## Restrições Técnicas de Alto Nível

- O emissor desta fatia fala o contrato da Notaas no recorte já implementado pela bancada
  (OD89): aceite em fila, autorização depois, PDF, XML e aviso de falha. Dev e homologação usam
  essa bancada. Produção usa a Notaas atrás da mesma fronteira. Conta de produção e certificado
  A1 não são requisito para construir a fatia (QA-03).
- Regra municipal, código de retorno do emissor e formato oficial do XML não aparecem em pedido,
  concessão, aula nem progresso. O financeiro vê o motivo da falha em linguagem da situação
  (*Falha* e a razão), não uma tela de prefeitura.
- CPF, CNPJ, NIF e e-mail não aparecem em URL, log, cache nem routing key. O aviso de nota
  disponível referencia a conta e o pedido.
- A nota emitida é registro fiscal: não se apaga junto com um pedido de exclusão de conta. O
  prazo exato de guarda está em QA-01.

---

## Não-Objetivos (Fora de Escopo)

- Cancelar, substituir ou corrigir NFS-e já emitida (`CAP-015` para o cancelamento por reembolso).
- NFS-e de assinatura, renovação, inadimplência ou reembolso (`CAP-013`, `CAP-014`, `CAP-015`).
- Nota de pedido que já estava *Pago* antes de a emissão estar ligada (DP-06).
- NF-e, NFC-e, emissão em lote, múltiplos prestadores na mesma escola, código de serviço por curso.
- Cálculo ou destaque de ISS feito pela plataforma.
- Transformar o comprovante de compra em documento fiscal, ou deixar de enviá-lo.
- Impedir compra, concessão ou reprodução por causa da nota.
- Contabilidade, livro fiscal, ERP e métricas de produto dependentes de OD2.
- Identidade visual nova.

---

## Plano de Rollout Faseado

### Esta entrega

- **Funcionalidades incluídas:** RF-01 a RF-07.
- **Critério para seguir:** um pedido pago na bancada de estudo chega a *Emitida* com PDF e XML
  baixados pelo aluno e vistos pelo financeiro; um pedido pago sem documento do tomador libera o
  curso e fica *Aguardando tomador*; uma recusa do emissor deixa o pedido *Pago* e o documento em
  *Falha*, com uma nova tentativa no mesmo documento.

### Depois, em outros PRDs

- `CAP-013` — nota do ciclo de assinatura.
- `CAP-015` — cancelamento fiscal quando o reembolso for fato do sistema.

---

## Métricas de Sucesso

| Métrica | Definição | Alvo | Prazo |
|---|---|---|---|
| Pedido pago sem documento | Pedidos que passaram a *Pago* com a emissão ligada e não têm registro de documento | 0 | Contínuo |
| Nota duplicada | Pedidos com mais de um documento fiscal | 0 | Contínuo |
| Acesso bloqueado por nota | Concessões de compra desfeitas ou aulas recusadas por situação fiscal | 0 | Contínuo |
| Tempo até a nota disponível | Do pedido *Pago*, com prestador utilizável e tomador identificado, até PDF e XML baixáveis, quando o emissor autoriza | Medida, sem alvo de produto (OD2). A bancada de estudo responde em segundos por construção | Desde o primeiro dia |
| Falha vista pelo financeiro | Documentos em *Falha* há mais de um dia sem uma tentativa nova nem motivo registrado | Medida | Desde o primeiro dia |

---

## Riscos e Mitigações

- **Venda a público antes desta entrega** deixa um intervalo sem NFS-e (R13, Q5). Mitigação:
  DP-06 não finge que o passado foi emitido; ir a público continua decisão de negócio, fora deste
  PRD.
- **Aluno nunca informa o documento.** A nota permanece *Aguardando tomador* e o financeiro vê o
  motivo. Mitigação: o pedido mostra o pedido de CPF/CNPJ/NIF; o acesso não é moeda de troca.
- **Emissor de produção ainda sem conta ou certificado** (QA-03). Mitigação: a fatia se prova na
  bancada de estudo; a marca "sem valor fiscal" impede tratar esse PDF como nota da escola.
- **Reembolso no gateway não cancela a nota**, pelo mesmo limite já aceito em `CAP-011` até
  `CAP-015`. Mitigação: declarada; o financeiro não ganha um botão que a plataforma não honra no
  acesso.

---

## Alternativas Consideradas

### Abordagem Escolhida: nota assíncrona do pedido pago, tomador na conta

- **Descrição:** o documento nasce do pedido *Pago*, espera prestador e tomador sem segurar a
  venda, e fica disponível no pedido.
- **Por que foi escolhida:** é o recorte que a fatia confirmada pede e o único que respeita DE06
  com o contrato de emissão que OD89 já fechou.

### Alternativa Rejeitada 1: CPF obrigatório no checkout

- **Descrição:** a compra não conclui sem documento do tomador.
- **Trade-offs:** a nota sairia sempre com tomador; a primeira receita da escola passaria a
  depender de um campo que o MVP inteiro evitou.
- **Por que foi rejeitada:** contradiz DE06 e o fechamento recente da compra avulsa.

### Alternativa Rejeitada 2: emitir no leiaute da prefeitura, sem emissor

- **Descrição:** a plataforma monta o XML municipal e fala com a prefeitura.
- **Trade-offs:** cobre o município da escola e só ele; cada mudança de leiaute vaza para o
  pedido.
- **Por que foi rejeitada:** OD89 já escolheu o contrato da Notaas e a bancada como duplo. A
  regra municipal fica no emissor.

---

## Questões em Aberto

- **QA-01 — Prazo de guarda da nota emitida e do documento do tomador.** Dono: encarregado de
  dados. A nota emitida não se apaga com a conta; falta o número de anos. Recomendação a validar:
  o prazo legal do documento fiscal municipal, da ordem de cinco anos, e não o prazo curto de log
  de e-mail (OD11). Se ficar em aberto, a implementação guarda a nota emitida por tempo
  indeterminado e não bloqueia a fatia.
- **QA-02 — Código de serviço e alíquota reais da escola.** Dono: financeiro. São valores de
  configuração, não de comportamento. A fatia funciona na bancada com os valores que o financeiro
  informar.
- **QA-03 — Conta de produção da Notaas e certificado A1.** Dono: negócio. Não bloqueia o PRD nem
  a prova na bancada. Bloqueia tratar a nota como documento fiscal válido fora do estudo.
