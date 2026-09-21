---
tsg_artifact: prd
product: code-4-coders
capability: CAP-026
version: 1.1
status: approved
updated: 2026-09-21
sources: backlog/capabilities.md@1.4, vision.md@1.2, context/domain-map.md@1.2, context/architecture-baseline.md@1.2, domains/identidade-e-acesso/domain.md@1.1
---

# Notificação transacional por e-mail — fatia de fundação

## Visão Geral

`CAP-001` não fecha o próprio ciclo sem e-mail: uma conta que não se confirma não autentica, e uma
senha esquecida vira chamado de atendimento no dia do lançamento. Esta entrega constrói o **carteiro
da plataforma** — o único componente que fala com o aluno fora do produto — e o constrói já com o
dono único do consentimento, porque essa obrigação não admite um segundo lugar onde seja verificada.

O recorte é deliberadamente estreito: **dois e-mails**, um canal, nenhuma preferência, nenhuma
campanha. Não é a capacidade `CAP-026` inteira, é o primeiro PRD dela — dimensionado pelo que a
capacidade consumidora precisa e nada além.

- **Problema que resolve:** hoje não existe caminho pelo qual a plataforma alcance o aluno fora
  dela. Sem isso, confirmação de conta e recuperação de senha são impossíveis, e `CAP-001` entrega
  cadastro sem prova de posse do e-mail.
- **Para quem:** o aluno que se cadastra e o aluno que esqueceu a senha. Secundariamente, a operação,
  que deixa de recuperar senha por atendimento manual.
- **Por que é valioso:** é pré-condição do primeiro passo do MVP. A antecipação de `CAP-026` ao MVP
  foi decisão registrada (R5 · Q1 · OD1) precisamente porque sem ela o ciclo de fundação não fecha.

---

## Rastreabilidade

### Capacidade e fronteiras

- **Capacidade:** `CAP-026` — Notificação transacional por e-mail
- **Escopo desta entrega:** primeira fatia de `CAP-026`, dimensionada por `CAP-001`. Entrega o
  mecanismo de envio transacional por e-mail (um canal) e exatamente **dois** modelos de mensagem:
  **confirmação de conta** e **recuperação de senha**. Inclui consentimento, registro de entrega e
  tratamento de falha, porque nenhum dos três é postergável sem reescrever o mecanismo depois.
- **Fora desta entrega:**
  - **Confirmação de compra** e **liberação de acesso** — os outros dois e-mails que o backlog lista
    no escopo mínimo de `CAP-026` no MVP. Voltam num **segundo PRD da mesma capacidade**, no passo 8
    da ordem de ataque, junto de `CAP-011`. Motivo: o consumidor deles não existe antes disso, e
    `CAP-011` ainda depende de contratação externa não fechada (OD5 · R3).
  - **Push e WhatsApp**, **preferência de contato por tipo de mensagem** e **modelo editável pelo
    negócio** — tudo `CAP-027`, Fase 4.
- **Domínios atravessados:**
  - **Notificação** — domínio principal. **Não tem domain doc**: `CAP-027` está na Fase 4, então o
    domínio rende um único PRD no horizonte visível. As regras do ciclo transacional nascem aqui
    (RN-N01…RN-N12) e serão absorvidas pelo domain doc quando ele existir, em D15.
  - **Identidade e Acesso** — `domains/identidade-e-acesso/domain.md` (v1.1). É a origem dos dois
    pedidos de envio desta fatia.
- **Junta entre os domínios:** decidida em OD9 e registrada em §5 do domain doc de Identidade e
  Acesso. **Identidade dita o quê e o quando; Notificação dita como aquilo se parece e entrega.**
  Concretamente: Identidade publica `notificacao.envio-solicitado` pelo seu outbox, com
  destinatário, identificador do Modelo de Mensagem, os dados que o preenchem e a finalidade.
  Notificação verifica consentimento, renderiza, entrega, repete em falha e registra. **Notificação
  nunca aprende o que é "confirmação de conta"**; Identidade nunca monta mensagem de canal (RN-28).
- **Dependências entre capacidades:** `CAP-001` **depende desta entrega**. É a razão do recorte e a
  razão de `CAP-026` sair primeiro na rodada. Esta entrega não depende de nenhuma capacidade.
- **Restrições do baseline que delimitam escopo:**
  - `notification` é serviço próprio do grupo inicial (BA02), não módulo de `identity` — a
    alternativa foi avaliada e descartada porque `CAP-011` também precisará de e-mail e forçaria
    `commerce → identity`, junta que o Domain Map não permite.
  - Publicação no broker somente por outbox (G06); caso de uso não publica direto.
  - Toda entidade carrega `tenant_id` desde a Fase 1 (BA10 · G07).
  - Nenhum dado pessoal em log, span, métrica ou payload de erro (G10 · G23).
  - Contrato evolui de forma aditiva (G14 · BA13).

### Vision Doc

- **Objetivos de negócio atendidos:** habilitar o ciclo de conta do aluno (`C01`), que a visão põe
  na Fase 1. Esta entrega deriva de `C14`, que a visão põe na Fase 2 — **divergência registrada e
  decidida** (R5 · Q1 · OD1), não omissão.
- **Restrições globais aplicáveis:** provedor de e-mail transacional é dependência externa declarada
  na visão, de risco **baixo**; autenticação é própria da plataforma, sem SSO.
- **Non-Goals globais respeitados:** nada de campanha, automação de funil ou marketing; a plataforma
  não é marketplace; nenhuma comunicação promete exclusividade de conteúdo (BA15).

### Domain Docs

- **Entidades envolvidas:**
  - De **Notificação** (Domain Map, detalhadas aqui): Notificação, Canal, Modelo de Mensagem,
    Consentimento, Registro de Entrega. **Preferência de Contato** é entidade do domínio mas fica
    **fora desta fatia** — é `CAP-027`.
  - De **Identidade e Acesso**: Conta, Token de Verificação (só como origem do pedido; esta entrega
    não os manipula).
- **Regras de negócio referenciadas** (Identidade e Acesso): RN-21 (mascaramento do e-mail),
  RN-26 (o e-mail no payload do pedido é exceção declarada), RN-27 (finalidade sempre declarada),
  RN-28 (a origem não monta mensagem de canal).
- **Regras nascidas neste PRD:** RN-N01…RN-N12, abaixo. Numeradas com prefixo `N` para não colidirem
  com a numeração que o domain doc de Notificação terá quando nascer em D15.
- **Eventos consumidos:** `notificacao.envio-solicitado` (de qualquer domínio de origem; nesta fatia,
  só de Identidade e Acesso).
- **Eventos produzidos:** `notificacao.mensagem-entregue`, `notificacao.entrega-falhou`.

## Termos Canônicos

| Termo | Definição de negócio | Escopo/Fonte |
|---|---|---|
| Pedido de envio | Solicitação endereçada a Notificação para entregar uma mensagem a um destinatário, indicando modelo, dados e finalidade — nunca texto pronto | Decisão desta entrega (OD9) |
| Finalidade | A razão declarada do envio, que permite a Notificação aplicar consentimento sem conhecer o conteúdo. Nesta fatia: `confirmacao-de-conta` e `recuperacao-de-senha` | Decisão desta entrega |
| Mensagem transacional | Mensagem consequência direta de um ato do próprio aluno, necessária para ele concluir o que começou | Decisão desta entrega |
| Mensagem promocional | Mensagem que o aluno não pediu, enviada por iniciativa do negócio. **Não existe nesta fatia** e é o que o descadastro protege | Decisão desta entrega |
| Registro de Entrega | O que aconteceu com um pedido de envio: aceito, entregue, falhou, descartado — com motivo | Domain Map |

---

## Objetivos

1. **Destravar `CAP-001`.** Ao fim desta entrega, um aluno consegue confirmar a conta e recuperar a
   senha sozinho, sem atendimento humano. É o critério que a ordem de ataque do backlog usa para
   declarar o passo 1 pronto: *"um e-mail transacional é entregue e registrado"*.
2. **Fundar o dono único do consentimento** (DE12), de modo que nenhum outro domínio precise — ou
   consiga — falar com o aluno fora da plataforma.
3. **Deixar o mecanismo pronto para o segundo PRD** sem reescrita: acrescentar um terceiro e um
   quarto modelo de mensagem, no passo 8, deve ser acrescentar modelo, não mudar o mecanismo.
4. **Não vazar dado pessoal** por nenhum caminho de observabilidade, num domínio cujo trabalho é
   justamente manipular endereço de e-mail.

---

## Histórias de Usuário

- Como **visitante que acabou de se cadastrar**, eu quero receber um e-mail com um link de
  confirmação para que eu prove que o endereço é meu e possa entrar na plataforma.
- Como **visitante que não recebeu o e-mail**, eu quero pedir o reenvio sem criar outra conta para
  que um problema de caixa de entrada não me custe o cadastro.
- Como **aluno que esqueceu a senha**, eu quero receber um link de redefinição para que eu recupere
  o acesso sem falar com ninguém.
- Como **pessoa que não tem conta na plataforma**, eu quero que um pedido de recuperação feito com o
  meu endereço não revele que eu não tenho conta — nem gere um e-mail que eu não entenda.
- Como **operação**, eu quero ver se uma mensagem foi entregue ou falhou, e por quê, para que eu
  consiga atender "não recebi o e-mail" com fato em vez de suposição.
- Como **encarregado de dados**, eu quero que o consentimento e o descadastro tenham um único ponto
  de verificação para que a obrigação legal não dependa de seis implementações concordarem.

---

## Funcionalidades Principais

### RF-01: Aceitar um pedido de envio

**Descrição**: Notificação recebe pedidos de envio endereçados a ela, vindos de qualquer domínio de
origem, e os aceita ou recusa por validade — nunca por mérito. Ela não julga se a mensagem deveria
ser enviada; julga se o pedido está completo e se o consentimento permite.

**Critérios de Aceitação**:

- **Given** um pedido de envio com destinatário, modelo conhecido, dados completos e finalidade
  declarada
  **When** Notificação o recebe
  **Then** o pedido é aceito e passa a ter um Registro de Entrega na situação "aceito"

- **Given** um pedido cuja finalidade não está declarada
  **When** Notificação o recebe
  **Then** o pedido é recusado e registrado como recusado, com o motivo — e nenhuma mensagem é
  enviada

- **Given** um pedido que indica um modelo que Notificação não conhece
  **When** Notificação o recebe
  **Then** o pedido é recusado com motivo, e a recusa é observável pela operação

- **Given** um pedido cujos dados não preenchem todos os campos que o modelo exige
  **When** Notificação o recebe
  **Then** o pedido é recusado com motivo, e **nenhuma mensagem parcial é enviada** — um e-mail com
  um link vazio é pior do que nenhum e-mail

- **Given** um pedido de envio já processado anteriormente, reentregue pelo broker
  **When** Notificação o recebe de novo
  **Then** nenhuma segunda mensagem é enviada ao destinatário, e o Registro de Entrega original
  permanece o único

**Prioridade**: Must Have

**Rastreabilidade**: RN-N01, RN-N02, RN-N09 · RN-27 (Identidade e Acesso)

---

### RF-02: Entregar mensagem de confirmação de conta

**Descrição**: O modelo de mensagem que leva ao aluno o link de confirmação do endereço de e-mail,
a partir do pedido publicado por Identidade e Acesso quando uma conta é criada.

**Critérios de Aceitação**:

- **Given** um pedido aceito com finalidade `confirmacao-de-conta`
  **When** Notificação o processa
  **Then** o aluno recebe uma mensagem contendo o link de confirmação e o prazo de validade dele,
  e o Registro de Entrega passa a "entregue"

- **Given** que a mensagem é de confirmação de conta
  **When** ela é composta
  **Then** ela **não** contém link de descadastro, porque descadastrar-se de uma mensagem que é
  pré-condição de usar o produto não é uma opção coerente (RN-N06)

- **Given** que o aluno pediu o reenvio da confirmação
  **When** um novo pedido chega com a mesma finalidade e destinatário
  **Then** uma nova mensagem é entregue, e os dois Registros de Entrega coexistem — reenvio é fato
  novo, não correção do anterior

**Prioridade**: Must Have

**Rastreabilidade**: RN-N03, RN-N06 · RN-02, RN-03 (Identidade e Acesso)

---

### RF-03: Entregar mensagem de recuperação de senha

**Descrição**: O modelo de mensagem que leva ao aluno o link de redefinição de senha.

**Critérios de Aceitação**:

- **Given** um pedido aceito com finalidade `recuperacao-de-senha`
  **When** Notificação o processa
  **Then** o aluno recebe uma mensagem contendo o link de redefinição e o prazo de validade dele

- **Given** que a recuperação foi solicitada para um endereço **sem conta** na plataforma
  **When** Identidade e Acesso trata a solicitação
  **Then** **nenhum pedido de envio é publicado**, e portanto nenhuma mensagem sai — a resposta ao
  solicitante é idêntica à do caso com conta (RN-04 de Identidade e Acesso), e essa igualdade é
  garantida na origem, não aqui

- **Given** que a mensagem é de recuperação de senha
  **When** ela é composta
  **Then** ela **não** contém link de descadastro, pelo mesmo motivo de RF-02

**Prioridade**: Must Have

**Rastreabilidade**: RN-N03, RN-N06 · RN-04, RN-05 (Identidade e Acesso)

---

### RF-04: Verificar consentimento antes de entregar

**Descrição**: Notificação é dona única do consentimento (DE12). Toda entrega passa por essa
verificação, mesmo quando a resposta é sempre "pode" — porque é o mecanismo, não o resultado, que
precisa existir desde o primeiro envio.

**Critérios de Aceitação**:

- **Given** um pedido com finalidade **transacional**
  **When** o consentimento é verificado
  **Then** a entrega é permitida, porque mensagem transacional é consequência de ato do próprio
  aluno e não depende de consentimento de marketing (RN-N05)

- **Given** um destinatário que se descadastrou de mensagens promocionais
  **When** chega um pedido transacional para ele
  **Then** a mensagem **é entregue** — o descadastro não alcança o transacional, e o aluno não pode
  se trancar para fora da própria recuperação de senha

- **Given** um pedido com finalidade **promocional**
  **When** Notificação o recebe **nesta fatia**
  **Then** ele é recusado com motivo, porque não existe finalidade promocional nesta entrega e
  aceitar uma abriria caminho para envio sem base legal registrada

**Prioridade**: Must Have

**Rastreabilidade**: RN-N04, RN-N05, RN-N06

---

### RF-05: Registrar o que aconteceu com cada mensagem

**Descrição**: Todo pedido de envio, entregue ou não, deixa um Registro de Entrega consultável pela
operação. É o que transforma "não recebi o e-mail" em diagnóstico.

**Critérios de Aceitação**:

- **Given** qualquer pedido de envio recebido
  **When** ele termina em qualquer desfecho
  **Then** existe um Registro de Entrega com: finalidade, modelo, situação, momento de cada
  transição e, em caso de falha, o motivo

- **Given** que a operação precisa diagnosticar um caso
  **When** ela consulta os registros de um destinatário
  **Then** ela vê a situação de cada mensagem **sem** ver o corpo enviado, que pode conter um token
  ainda válido (RN-N08)

- **Given** um Registro de Entrega
  **When** ele é escrito em log, métrica ou span
  **Then** o endereço de e-mail aparece **mascarado**, e o endereço em claro existe apenas no
  registro, que tem acesso restrito (RN-N07 · G10 · G23)

**Prioridade**: Must Have

**Rastreabilidade**: RN-N07, RN-N08, RN-N10

---

### RF-06: Tratar falha de entrega sem perder o pedido

**Descrição**: O provedor de e-mail é externo e falha. A entrega é repetida por um número finito de
vezes e, esgotado, o pedido termina em falha registrada e observável — nunca em silêncio.

**Critérios de Aceitação**:

- **Given** uma falha temporária do provedor
  **When** Notificação tenta entregar
  **Then** a entrega é repetida com espaçamento crescente, até um limite, e cada tentativa aparece
  no Registro de Entrega

- **Given** uma falha definitiva — endereço inexistente, recusa permanente
  **When** Notificação a recebe
  **Then** nenhuma nova tentativa é feita, o registro passa a "falhou" com o motivo, e
  `notificacao.entrega-falhou` é publicado

- **Given** que o limite de tentativas se esgotou
  **When** a última falha
  **Then** o pedido vai para tratamento manual (DLQ) e a operação é alertada — um aluno que não
  consegue confirmar a conta é um cadastro perdido, e isso não pode depender de alguém olhar um log

- **Given** que o provedor está indisponível
  **When** chegam novos pedidos
  **Then** eles são aceitos e retidos, não recusados — a indisponibilidade do provedor não pode
  virar erro no cadastro do aluno

**Prioridade**: Must Have

**Rastreabilidade**: RN-N09, RN-N11, RN-N12

---

### RF-07: Publicar o desfecho como fato de negócio

**Descrição**: O resultado da entrega interessa a outros domínios — hoje a Inteligência de Negócio,
amanhã o atendimento. Notificação publica o fato consumado e não espera resposta.

**Critérios de Aceitação**:

- **Given** uma mensagem entregue
  **When** a entrega se confirma
  **Then** `notificacao.mensagem-entregue` é publicado, com finalidade e destinatário, **sem o
  corpo da mensagem**

- **Given** uma entrega definitivamente falha
  **When** o desfecho se confirma
  **Then** `notificacao.entrega-falhou` é publicado com o motivo

- **Given** qualquer publicação deste domínio
  **When** ela ocorre
  **Then** é feita pelo outbox, nunca direto do caso de uso (G06)

**Prioridade**: Should Have — o mecanismo é Must Have porque muda o desenho; o consumidor só existe
na Fase 2

**Rastreabilidade**: RN-N10, RN-N12

---

## Regras de Negócio nascidas neste PRD

> O domínio Notificação não tem domain doc porque rende um único PRD no horizonte visível. Estas
> regras nascem aqui e serão absorvidas pelo domain doc quando ele existir (D15, com `CAP-027`).

| ID | Regra |
|---|---|
| RN-N01 | Notificação **não decide o que comunicar nem quando**. Ela recebe pedidos e os executa; a regra de negócio que originou a mensagem é do domínio de origem |
| RN-N02 | Todo pedido de envio declara **finalidade**. Pedido sem finalidade é recusado, porque é a finalidade que permite aplicar consentimento sem conhecer o conteúdo |
| RN-N03 | O texto da mensagem é do **Modelo de Mensagem**, que pertence a Notificação. Nenhum domínio de origem envia assunto ou corpo — envia o identificador do modelo e os dados que o preenchem |
| RN-N04 | Notificação é **dona única do consentimento e do descadastro**. Nenhum outro domínio verifica um nem registra o outro |
| RN-N05 | **Mensagem transacional não depende de consentimento de marketing.** Ela é consequência direta de um ato do próprio aluno e é necessária para ele concluir o que começou |
| RN-N06 | **Mensagem transacional não carrega link de descadastro.** Descadastrar-se dela deixaria o aluno sem confirmar a conta e sem recuperar a senha. O descadastro existe e protege o promocional — que não existe nesta fatia |
| RN-N07 | O endereço de e-mail aparece **mascarado** em log, span, métrica e payload de erro. Em claro, existe apenas no Registro de Entrega e no pedido de envio, ambos de acesso restrito |
| RN-N08 | O **corpo enviado não é conservado** de forma consultável: ele pode conter um token ainda válido, e o Registro de Entrega é visível para a operação. Guarda-se o que aconteceu, não o que foi escrito |
| RN-N09 | Um pedido de envio é **idempotente**: reentrega do mesmo pedido não produz segunda mensagem ao destinatário |
| RN-N10 | Todo pedido termina em um desfecho **registrado**: entregue, falhou ou recusado, sempre com motivo. Não existe pedido que simplesmente some |
| RN-N11 | Falha do provedor externo **não vira erro para o aluno**. O pedido é aceito e retido; a indisponibilidade de terceiro não interrompe o cadastro |
| RN-N12 | Esgotadas as tentativas, o pedido vai para tratamento manual **e alerta a operação**. Falha silenciosa em confirmação de conta é cadastro perdido sem ninguém saber |
| RN-N13 | **Remetente, domínio de envio e validade de cada link são parâmetros de operação**, nunca valores escritos no modelo ou no código. Trocá-los é mudança de configuração, e não exige nova entrega |
| RN-N14 | O modelo de mensagem **renderiza a validade real do link** a partir do parâmetro vigente. Nenhum texto de modelo afirma um prazo em prosa: um modelo que diz "24 horas" enquanto o parâmetro diz outra coisa mente para o aluno no pior momento possível |
| RN-N15 | **A validade é parâmetro por finalidade, não um valor único.** Confirmação de conta e recuperação de senha têm prazos independentes, porque o segundo é um credencial temporário e o primeiro não |
| RN-N16 | O Registro de Entrega tem retenção em **duas camadas**: o registro com dado pessoal (destinatário em claro, motivo da falha) é descartado ao fim do prazo operacional; o fato agregado por finalidade e desfecho, sem dado pessoal, permanece. Retenção é parâmetro, e o prazo vigente é declarado, não implícito |

---

## Experiência do Usuário

O aluno não interage com este domínio; ele interage com **o que chega na caixa de entrada dele**. A
experiência que esta entrega define é curta e inteira:

1. O aluno se cadastra e é informado, **na própria tela**, de que um e-mail foi enviado e de qual
   endereço — para que ele saiba onde procurar e perceba um erro de digitação ali, não meia hora
   depois.
2. A mensagem chega em segundos, não em minutos. Confirmação de conta é o momento de maior atrito do
   funil inteiro: o aluno está parado esperando.
3. A mensagem diz **o que fazer**, **até quando** e **o que fazer se o link expirar**. Link expirado
   sem caminho de saída é o desenho que produz chamado de suporte.
4. Se o aluno não recebeu, a tela de onde ele veio oferece reenvio — sem recriar conta e sem
   inventar um segundo cadastro.
5. Quem não tem conta e pediu recuperação **não recebe mensagem alguma** e vê na tela a mesma
   resposta de quem tem. Nada no comportamento observável distingue os dois casos.

**Acessibilidade e forma:** a mensagem precisa ser legível sem imagens e sem HTML — cliente de
e-mail que bloqueia imagem é comum, e o link de confirmação não pode depender de renderização. O
link é texto visível, não um botão que vira nada quando o estilo não carrega. Sem identidade visual
definida (OD3 · A2), os modelos nascem em layout neutro; trocar a identidade depois é entrega do
time de design, não retrabalho desta capacidade — mesmo tratamento dado a `CAP-003` em R4.

---

## Decisões de Produto

| ID | Decisão confirmada | Alternativas descartadas e motivo | Impacto no PRD |
|---|---|---|---|
| DP-01 | A fatia entrega **dois** modelos de mensagem, dimensionada por `CAP-001` | Entregar os quatro do escopo mínimo de `CAP-026`: escreveria agora dois e-mails cujo consumidor (`CAP-011`, passo 8) não existe e que dependem de gateway não contratado (OD5) | Define RF-02 e RF-03; põe confirmação de compra e liberação de acesso em "Fora desta entrega" |
| DP-02 | Notificação é **carteiro**: recebe modelo + dados + finalidade, e não conhece o motivo do envio | Notificação assinar `identidade.conta-criada` e montar a mensagem: faria o token de confirmação viajar num fato difundido, legível por `analytics` e Matrícula, e obrigaria Notificação a conhecer as regras de identidade | Define RF-01 e a junta; origem de RN-N01, RN-N02, RN-N03 |
| DP-03 | O **texto mora em Notificação**, não na origem | A origem compor assunto e corpo: contraria o Domain Map, que põe Modelo de Mensagem em Notificação, e a restrição herdada "nenhum domínio monta mensagem de canal" (DE12); espalharia marca, rodapé legal e descadastro por seis serviços | RN-N03 · RN-28 do domain doc de Identidade e Acesso |
| DP-04 | Consentimento e Registro de Entrega entram **nesta fatia**, não depois | Adiá-los para `CAP-027`: ambos mudam o desenho do mecanismo, não o acrescentam. Enxertar dono de consentimento depois do primeiro envio é reescrever, e DE12 existe justamente para não haver um segundo lugar | RF-04 e RF-05 como Must Have |
| DP-05 | Mensagem transacional **não carrega descadastro** e não depende de consentimento de marketing | Tratar todo envio sob a mesma regra: deixaria o aluno se trancar para fora da própria recuperação de senha | RN-N05, RN-N06; critérios negativos de RF-02, RF-03, RF-04 |
| DP-07 | Remetente, domínio e validade dos links entram como **parâmetro**, não como decisão desta entrega | Fixá-los agora: travaria a construção em decisões de plataforma e de negócio ainda abertas, sem ganho — os três são valores, não comportamento | RN-N13, RN-N14, RN-N15; destrava QP-02 e QP-03 |
| DP-08 | A retenção do Registro de Entrega é **em duas camadas**, com o prazo como parâmetro | Prazo longo único sobre o registro inteiro: conservaria endereço em claro por anos para um dado cuja finalidade — diagnosticar "não recebi o e-mail" — se esgota em semanas, contra o princípio da necessidade da LGPD | RN-N16; reformula QP-01 |
| DP-06 | O **corpo enviado não é conservado** de forma consultável | Guardar o corpo para diagnóstico: o Registro de Entrega é visível à operação e o corpo contém token ainda válido — seria um caminho de escalonamento de privilégio por tela de suporte | RN-N08; segundo critério de RF-05 |

---

## Restrições Técnicas de Alto Nível

- **Provedor de e-mail transacional externo** — dependência declarada na visão, risco baixo, ainda
  não contratada. É a única dependência externa desta entrega.
- **Consentimento e descadastro são obrigação legal (LGPD)** com ponto único de verificação (DE12).
  A base legal do transacional é a execução do contrato com o aluno, não o consentimento — e é por
  isso que RN-N05 é válida.
- **Dado pessoal em domínio que manipula endereço** — G10 e G23 valem integralmente aqui, e a
  exceção declarada em RN-26 do domain doc de Identidade e Acesso cobre o payload do pedido, não o
  log.
- **Entrega percebida em segundos**, porque o aluno está parado esperando na tela de cadastro.
- **Retenção do Registro de Entrega** é parâmetro, em duas camadas (RN-N16). O prazo numérico
  aguarda o encarregado de dados — ver QP-01.
- **Remetente, domínio de envio e validade dos links são parâmetros de operação** (RN-N13). O código
  não assume nenhum dos três. Isso destrava a construção sem esperar a plataforma, mas **não
  substitui** o trabalho de reputação de domínio: SPF, DKIM e DMARC precisam estar no ar antes de
  tráfego real, e nenhuma configuração de aplicação compensa a falta deles.

---

## Não-Objetivos (Fora de Escopo)

- Push no navegador e WhatsApp — `CAP-027`, Fase 4.
- Preferência de contato por tipo de mensagem — `CAP-027`. Nesta fatia o aluno não escolhe o que
  recebe, porque tudo o que ele recebe é consequência de um ato dele.
- Modelo de mensagem editável pelo negócio — `CAP-027`. Aqui o modelo é parte da entrega.
- Campanha, automação de funil, segmentação, teste A/B de mensagem — fora de `CAP-026` inteira, é
  non-goal da visão.
- Confirmação de compra e liberação de acesso — segundo PRD de `CAP-026`, passo 8.
- Caixa de entrada dentro do produto, notificação in-app, central de mensagens — não é este domínio.
- Métrica de abertura e de clique — exige rastreamento por pixel e link reescrito, que são
  tratamento de dado pessoal sem finalidade declarada nesta fatia.
- Internacionalização dos modelos — a plataforma é mono-idioma nesta versão.

---

## Plano de Rollout Faseado

> O faseamento de `CAP-026` é o do backlog. Esta seção registra onde **esta** entrega se encaixa.

### Esta entrega — passo 1 da ordem de ataque do MVP

- **Funcionalidades incluídas:** RF-01 a RF-07.
- **Critério para o passo 1 ser declarado pronto:** *"um e-mail transacional é entregue e
  registrado"* — critério do próprio backlog. Concretamente: um pedido de confirmação de conta
  percorre aceitação, consentimento, entrega e registro, e a operação consegue ver o desfecho.
- **Critério para liberar `CAP-001`:** RF-02 e RF-03 entregando de ponta a ponta. Sem os dois,
  `CAP-001` não pode começar.

### Segundo PRD de `CAP-026` — passo 8, junto de `CAP-011`

- **Acrescenta:** modelos de confirmação de compra e de liberação de acesso.
- **Prova de que o mecanismo está certo:** acrescentar esses dois modelos deve ser acrescentar
  modelo e finalidade, sem tocar em RF-01, RF-04, RF-05 ou RF-06. **Se tocar, o desenho desta fatia
  estava errado** — e esse é o teste retrospectivo mais honesto desta entrega.

### `CAP-027` — Fase 4

- Canais adicionais, preferência de contato e modelo editável pelo negócio. É quando o domain doc de
  Notificação nasce (D15) e absorve RN-N01…RN-N12.

---

## Métricas de Sucesso

| Métrica | Definição | Alvo | Prazo |
|---|---|---|---|
| Taxa de entrega | Mensagens entregues ÷ pedidos aceitos | ≥ 99% | Primeira semana com tráfego real |
| Tempo até a caixa de entrada | Do pedido aceito à confirmação de entrega do provedor, percentil 95 | ≤ 30 s | Primeira semana com tráfego real |
| Confirmação de conta concluída | Contas confirmadas ÷ contas criadas, em 24 h | ≥ 80% | Primeiro mês do MVP |
| Recuperação sem atendimento | Redefinições concluídas sozinhas ÷ pedidos de recuperação | ≥ 95% | Primeiro mês do MVP |
| Pedidos em tratamento manual | Pedidos que esgotaram tentativas, por semana | 0 como normal; qualquer ocorrência é investigada | Contínuo |
| Vazamento de dado pessoal | Ocorrências de e-mail em claro em log, span ou métrica | 0 | Contínuo, verificado no gate de release |

**Duas dessas métricas são de produto, não do domínio** — confirmação concluída e recuperação sem
atendimento medem o ciclo de `CAP-001`, que é o consumidor. Estão aqui porque são a única prova de
que esta entrega serviu para o que foi antecipada ao MVP. Os alvos são **hipóteses**, não
compromissos: a definição das métricas de sucesso do produto é A1/OD2, em aberto com o negócio, e
estes números serão reconciliados quando ela fechar.

---

## Riscos e Mitigações

| Risco | Mitigação |
|---|---|
| **Provedor de e-mail não contratado** atrasa o passo 1 e, com ele, todo o MVP — esta é a primeira capacidade da ordem | Risco baixo e barato de resolver (a visão o classifica assim), mas é **caminho crítico**: contratar antes de começar. Diferente do gateway (R3), não há alternativa de contornar com cortesia |
| **Mensagem cair em spam**, tornando a taxa de entrega irrelevante como métrica | Reputação de remetente é trabalho de infraestrutura de domínio (SPF, DKIM, DMARC), não de código, e precisa ser conduzida junto da Fase 0. Sem isso, a métrica de entrega mede o provedor, não o aluno |
| **Descadastro mal desenhado trancar o aluno** para fora da recuperação de senha | RN-N05 e RN-N06, com critérios negativos explícitos em RF-04 |
| **O segundo PRD exigir reescrita** do mecanismo, revelando que a fatia foi mal cortada | Critério explícito no rollout: acrescentar modelo não pode tocar RF-01, RF-04, RF-05 nem RF-06 |
| **Token válido exposto pela tela de diagnóstico** da operação | RN-N08: guarda-se o desfecho, não o corpo |
| **Identidade visual indefinida** (OD3) atrasar os modelos | Layout neutro, mesmo tratamento de R4 para `CAP-003`: a troca é entrega do design, não retrabalho |

---

## Alternativas Consideradas

### Abordagem escolhida: serviço `notification` próprio, recebendo pedidos endereçados

- **Descrição:** `notification` nasce como serviço do grupo inicial (BA02), consome pedidos de envio
  publicados pelo outbox das origens, e é dono do modelo, do consentimento e do registro.
- **Por que foi escolhida:** é a única que satisfaz DE12 sem criar junta proibida, e a que faz o
  segundo PRD ser acréscimo em vez de reescrita.

### Alternativa rejeitada 1: Notificação como módulo dentro de `identity`

- **Trade-offs:** um serviço a menos no grupo inicial, o que importa para um time de dois
  engenheiros.
- **Por que foi rejeitada:** `CAP-011` também precisa de e-mail no MVP e forçaria `commerce →
  identity`, junta que o Domain Map não permite. Decisão já registrada em OD1 e BA02; não reaberta
  aqui.

### Alternativa rejeitada 2: Notificação assinando os fatos de domínio das origens

- **Trade-offs:** menos mensagens no broker e origens que não precisam saber que Notificação existe.
- **Por que foi rejeitada:** obrigaria Notificação a conhecer as regras de cada domínio e faria o
  Token de Verificação viajar dentro de um fato difundido, legível por todo assinante. DP-02.

### Alternativa rejeitada 3: cada domínio enviando seu próprio e-mail

- **Trade-offs:** o caminho mais curto para o primeiro e-mail sair.
- **Por que foi rejeitada:** o consentimento passaria a ser verificado em cada domínio — e violado
  em pelo menos um, que é a justificativa textual do Domain Map para Notificação existir.

---

## Questões em Aberto

- [ ] **QP-01 — Prazo de retenção do Registro de Entrega.** A **forma** está decidida (RN-N16, duas
      camadas, parametrizada); falta o número da camada com dado pessoal. **Dono:** encarregado de
      dados, com o negócio. **Recomendação a validar:** prazo curto, da ordem de 90 dias — é o que
      cobre a finalidade real do registro, que é diagnosticar "não recebi o e-mail". **Prazo de
      guarda longo não se aplica a este registro:** ele vale para documento fiscal e contrato, não
      para log de entrega de e-mail, e a LGPD cobra necessidade, não simetria com a contabilidade.
      **Impacto se não resolvido:** o parâmetro nasce com um valor provisório e o registro acumula
      endereço em claro sem política; `CAP-031` (AB03) vai precisar da resposta de qualquer forma.
- [x] **QP-02 — Validade dos links. Resolvida em 2026-09-20 como parâmetro.** Um valor por
      finalidade (RN-N15), renderizado no modelo a partir do parâmetro vigente (RN-N14). **Resta um
      número, não uma decisão de desenho**, e ele não trava esta entrega. **Nota de segurança que
      acompanha o parâmetro:** o link de recuperação é uma credencial temporária, e prazo longo nele
      é uma senha alternativa de vida longa — os dois valores não devem ser iguais por conveniência.
- [x] **QP-03 — Remetente e domínio de envio. Resolvida em 2026-09-20 como parâmetro.** A aplicação
      não assume remetente nem domínio (RN-N13); a configuração é entregue pelo time de plataforma.
      **O que continua sendo tarefa da plataforma, e não é parametrização:** SPF, DKIM e DMARC no ar
      antes de tráfego real. Rastreado na fundação e no ADR-0002.
- [ ] **QP-04 — Alerta de pedido em tratamento manual: para quem?** RN-N12 exige alertar a operação,
      mas a operação ainda não existe como papel definido (`CAP-002` é o passo 4). **Dono:** time.
      **Impacto:** no passo 1 o destinatário do alerta é o próprio time; formalizar quando `CAP-002`
      existir.

---

*PRD gerado com a skill `tsg-flow-prd-creator`. Fatia confirmada com o usuário antes da escrita.
Próximo passo depois da aprovação: `tsg-flow-contract-creator`, porque a junta com Identidade e
Acesso é um contrato de mensagem compartilhado entre dois serviços e `CAP-001` será escrita contra
ele.*
