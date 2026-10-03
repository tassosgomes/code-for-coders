# Contratos de integração — Notificação transacional por e-mail (fatia de fundação)

> PRD: `tasks/prd-notificacao-transacional/prd.md` (v1.1, aprovado 2026-09-21)
> Data: 2026-09-21
> Estado do conjunto: **Aprovado para implementação**. Validação estrutural sem erros; a única
> decisão de modelagem levada ao usuário (item 3 de "Origem e decisões") foi confirmada em
> 2026-09-21.

Este conjunto registra o acordo desta implementação. O segundo PRD de `CAP-026` (passo 8, junto de
`CAP-011`) pode evoluí-lo. Não afirma qual contrato está em produção. Catalogação, armazenamento
definitivo e mecanismo de atualização do acervo são responsabilidade do time de plataforma.

## Seleção e escopo

O PRD define uma única junta entre domínios: Identidade e Acesso publica pedidos de envio
endereçados a Notificação; Notificação verifica consentimento, entrega e publica o desfecho. É
mensageria assíncrona ponta a ponta — não há interface HTTP consumida, criada ou alterada nesta
fatia, e não há produto de dados disponibilizado a consumidores fora do fluxo de eventos (a
Inteligência de Negócio consome o desfecho pelo mesmo evento de integração, não por tabela/arquivo
acordado — ODCS não se aplica, conforme `references/odcs.md`: "banco de uso interno não basta").

RF-05 (consulta da operação aos Registros de Entrega) não está descrita no PRD como uma interface
HTTP a ser contratada — pode ser leitura interna (painel, consulta direta) resolvida na TechSpec.
Se a implementação expor essa consulta como endpoint para outro serviço, isso exige um contrato
OpenAPI complementar nesse momento, não neste.

Nenhuma modalidade HTTP ou ODCS gerou documento nesta entrega — apenas o registro de não
aplicabilidade acima.

| Documento | Modalidade/versão do padrão | Versão do contrato | Escopo completo ou recorte | Status |
|---|---|---|---|---|
| [asyncapi-contract.yaml](asyncapi-contract.yaml) | AsyncAPI 3.0.0 | 1.0.0 | Recorte: só os dois modelos desta fatia (`confirmacao-de-conta`, `recuperacao-de-senha`) e os três canais que RF-01–RF-07 exigem. Documento válido e resolvível por si | Aprovado para implementação |

## Participantes e interfaces

| Documento e identificador técnico | Provedor/produtor | Consumidores conhecidos | Comportamento ou compromisso | Requisitos do PRD |
|---|---|---|---|---|
| Canal `notificacao.envio-solicitado.v1` — operação `receberPedidoDeEnvio` (receive) | Qualquer domínio de origem endereça; nesta fatia, só Identidade e Acesso publica pelo seu outbox | Notificação | Aceita/recusa por validade (finalidade declarada, modelo conhecido, dados completos); idempotente por `pedidoId`; indisponibilidade do provedor não recusa o pedido | RF-01, RF-04; RN-N01–RN-N05, RN-N09, RN-N11; RN-26–RN-28 (Identidade e Acesso) |
| Canal `notificacao.mensagem-entregue.v1` — operação `publicarMensagemEntregue` (send) | Notificação, pelo outbox | Inteligência de Negócio (hoje, fora desta fatia); atendimento (futuro) | Publica finalidade e destinatário, nunca o corpo da mensagem | RF-07; RN-N08, RN-N10 |
| Canal `notificacao.entrega-falhou.v1` — operação `publicarEntregaFalhou` (send) | Notificação, pelo outbox | Idem acima | Publica motivo da falha definitiva (imediata ou após esgotar tentativas) | RF-06, RF-07; RN-N09, RN-N11, RN-N12 |

Não há combinação com OpenAPI ou ODCS nesta fatia — a relação operação→mensagem→modelo de dados
não se aplica.

## Origem e decisões

| Entrada consultada | Versão e revisão/data | Decisão herdada ou alterada | Motivo |
|---|---|---|---|
| PRD `prd-notificacao-transacional` | v1.1, 2026-09-21 | Herdada: dois canais de saída (`mensagem-entregue`, `entrega-falhou`), um canal de entrada (`envio-solicitado`); nomes de negócio dos três eventos | RF-01, RF-06, RF-07 |
| `domains/identidade-e-acesso/domain.md` | v1.1, 2026-09-21, §5 (QA-01) | Herdada: payload do pedido carrega destinatário, identificador do Modelo de Mensagem, dados que o preenchem e finalidade — quatro campos distintos, nunca texto pronto | RN-26, RN-27, RN-28; DP-02, DP-03 do PRD |
| `context/architecture-baseline.md` | v1.2 (linhas 253–273) | Herdada: RabbitMQ, exchange topic, fila quorum, DLX/DLQ, `x-delivery-limit`, outbox obrigatório, consumidor idempotente, evolução aditiva, routing key versionada | G06, G09, BA10/G07 |

**Decisões de modelagem tomadas nesta entrega (não estavam explícitas em nenhuma fonte):**

1. **`modelo` e `finalidade` são campos distintos que coincidem em valor nesta fatia.** O domain
   doc de Identidade e Acesso lista os dois como itens separados do payload (destinatário,
   identificador do Modelo de Mensagem, dados, finalidade), e RF-01 recusa por motivos diferentes
   — finalidade não declarada vs. modelo desconhecido. Como esta fatia tem exatamente um modelo
   por finalidade, os dois campos usam o mesmo vocabulário (`Finalidade` no schema), mas
   permanecem campos independentes para que o segundo PRD (passo 8) possa acrescentar um modelo
   sob uma finalidade já existente, ou vice-versa, sem reabrir este schema.

2. **`dados` tem forma única (`nome`, `link`) para as duas finalidades desta fatia.** Ambas
   precisam exatamente disso. Documentado como decisão explícita, não como suposição de que os
   dois modelos são "iguais por acaso" — o segundo PRD pode exigir forma distinta por modelo, e o
   schema já anota que evoluir para `oneOf` por modelo é o caminho que não quebra este consumidor.

3. **Divergência entre PRD e domain doc sobre `validade` — resolvida com o usuário em 2026-09-21.**
   O domain doc de Identidade e Acesso lista "nome, link, validade" como exemplo dos dados que
   preenchem o pedido. O PRD, no entanto, trata a validade do link como **parâmetro de Notificação
   por finalidade** (RN-N13, RN-N14, RN-N15) e resolve QP-02 nesses termos em 2026-09-20:
   "renderizado no modelo a partir do parâmetro vigente" — não como um valor recebido no pedido.
   Confirmado com o usuário: seguir o PRD. **`dados` não carrega `validade`** — só `nome` e
   `link`. Consequência aceita: Notificação depende de um parâmetro próprio, por finalidade, para
   saber por quanto tempo dizer que o link vale, e **esse parâmetro precisa ficar operacionalmente
   sincronizado com a validade real do Token de Verificação, que é decidida e aplicada por
   Identidade e Acesso (RN-03)**. Se os dois valores divergirem, a mensagem mente sobre um prazo
   que Identidade já não aceita (ou aceita por mais tempo do que o texto promete) — exatamente o
   risco que RN-N14 nomeia. Este contrato não resolve essa sincronização; ela é operacional
   (configuração), e fica registrada aqui como pendência de coordenação para a TechSpec/plataforma,
   não como campo de mensagem.

4. **`destinatario` aparece em claro em `notificacao.mensagem-entregue`**, por exigência textual
   explícita do critério de aceite de RF-07 ("com finalidade e destinatário"). Isso não conflita
   com RN-N07/RN-21 (que restringem log, span, métrica e payload de erro — não o fato de
   integração em si) nem com RN-N08 (que restringe o corpo da mensagem, não o endereço). Mantive
   `destinatario` fora de `notificacao.entrega-falhou`, que o PRD não exige explicitamente; o
   campo pode ser adicionado depois de forma aditiva se a implementação precisar dele.

5. **Routing key com sufixo de versão (`.v1`)** nos três canais, aplicando a convenção do baseline
   (`{servico}.{evento}.v{n}`) sobre os nomes de negócio que o PRD e o domain doc já usam
   (`notificacao.envio-solicitado` etc.), que por si não trazem versão. Nome de exchange/fila e
   demais detalhes de topologia física não foram fixados — são parâmetro de implantação da
   plataforma, fora do alcance desta skill.

## Evolução e compatibilidade

Não existe contrato AsyncAPI anterior para esta junta — é o primeiro. Compatibilidade com versão
anterior não se aplica; não há consumidor a migrar.

Para o segundo PRD de `CAP-026` (passo 8, `CAP-011`): o rollout do PRD já declara o teste
retrospectivo — acrescentar os modelos de confirmação de compra e liberação de acesso deve ser
acrescentar valor a `Finalidade`/`modelo` e, se necessário, uma variante de `dados`, sem tocar
`PedidoDeEnvioSolicitadoPayload` em si nem os canais de desfecho. Enums fechados (`Finalidade`,
`canal`) são o ponto de atenção: adicionar um valor é aditivo; remover ou renomear um valor
existente não é, e exigiria versão nova (`.v2`) com convivência declarada, por convenção do
baseline (G14/BA13).

## Validação e verificação

| Documento | Comando e versão da ferramenta | Schema/ruleset e versão | Resultado e avisos |
|---|---|---|---|
| [asyncapi-contract.yaml](asyncapi-contract.yaml) | `npx -y @asyncapi/cli@6.1.0 validate` | AsyncAPI 3.0.0 (parser da CLI) | **Válido.** Único aviso: informação de que a versão 3.1.0 está disponível — mantida em 3.0.0 por decisão da skill/baseline, não é erro |

Cenários de verificação que a implementação deve cobrir (o parser não comprova nenhum deles):

- Pedido válido (finalidade declarada, modelo conhecido, dados completos) → aceito, entregue,
  `notificacao.mensagem-entregue` publicado (RF-01, RF-02, RF-03, RF-07).
- Pedido sem finalidade, com modelo desconhecido, ou com dado do modelo faltante → recusado com
  motivo registrado, nenhuma mensagem parcial (RF-01).
- Mesmo `pedidoId` reentregue pelo broker → nenhuma segunda mensagem; um novo `pedidoId` com a
  mesma finalidade e destinatário (reenvio) → nova mensagem, dois Registros de Entrega (RF-01,
  RF-02 terceiro critério; RN-N09).
- Falha temporária do provedor → tentativas com espaçamento crescente; falha permanente ou
  esgotamento das tentativas → `notificacao.entrega-falhou` publicado com `esgotouTentativas`
  correto e, no segundo caso, DLQ e alerta à operação (RF-06, RF-07).
- Indisponibilidade do provedor → pedidos novos continuam sendo aceitos e retidos, não recusados
  (RN-N11).
- Nenhum e-mail em claro em log, span, métrica ou payload de erro ao longo de todo o fluxo — só no
  payload do pedido e no evento `mensagem-entregue`, ambos declarados como exceção (RN-N07).

## Pendências e handoff

- **Coordenação operacional entre o parâmetro de validade de Notificação e a validade real do
  Token de Verificação de Identidade e Acesso** (decisão 3 acima). Não bloqueia esta entrega —
  ambos os parâmetros já nascem como configuração, não como valor de código — mas precisa de
  disciplina de operação declarada na TechSpec para não divergir silenciosamente.
- QP-01 (prazo de retenção do Registro de Entrega) e QP-04 (destinatário do alerta de DLQ)
  permanecem abertas no PRD; nenhuma das duas altera este contrato de mensageria — QP-01 é
  retenção interna do Registro de Entrega (não trafega no evento), QP-04 é operacional.
- Topologia física do broker (nome de exchange/fila, DLX, credenciais) fica para a TechSpec e para
  a plataforma; este contrato fixa endereço lógico do canal e garantias observáveis, não infra.

**Handoff para `tsg-flow-techspec-creator`:** referencie este `contracts.md` e
`asyncapi-contract.yaml`. A TechSpec deve mapear as três operações a cenários de envio/recebimento
e falha (lista acima), ao mecanismo de idempotência por `pedidoId` (inbox no consumidor, por
convenção do baseline), ao outbox de Identidade e Acesso para o canal de entrada, e ao outbox de
Notificação para os dois canais de saída — sem duplicar o schema aqui definido.
