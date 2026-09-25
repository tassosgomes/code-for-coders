---
tsg_artifact: prd
product: code-4-coders
capability: CAP-002
version: 1.0
status: approved
updated: 2026-09-25
sources: backlog/capabilities.md@1.4, vision.md@1.2, context/domain-map.md@1.2, context/architecture-baseline.md@1.2, domains/identidade-e-acesso/domain.md@1.1, domains/auditoria-e-conformidade/domain.md@1.1
---

# Acesso interno — convite, papel e permissão no backoffice

## Visão Geral

A escola precisa de gente para operar: professor que publica curso, suporte que atende aluno,
financeiro que cuida do dinheiro e administrador que governa quem faz o quê. Hoje só existe o lado
do aluno (`CAP-001`); nenhum ator interno consegue entrar no backoffice, e não há como dar a alguém
poder de operação sem dar poder demais.

Esta entrega cria a porta do backoffice. O primeiro administrador nasce por provisionamento; todo
ator interno seguinte nasce por convite dele. Cada ator recebe papéis, cada papel agrupa
permissões, e cada área do backoffice só abre para quem tem a permissão dela — o professor não
enxerga dado financeiro. Convidar, aceitar, conceder e revogar papel são atos administrativos: cada
um é comunicado à trilha de auditoria (`CAP-030`), com autor e motivo, desde o primeiro convite.

Quem se beneficia agora é o administrador, que passa a delegar com segurança. Quem se beneficia
logo depois são as capacidades que precisam de um ator interno: `CAP-005` (professor publica
curso) e o segundo PRD de `CAP-030` (administrador consulta a trilha).

---

## Rastreabilidade

### Capacidade e fronteiras

- **Capacidade:** `CAP-002` — Acesso interno por papel e permissão.
- **Escopo desta entrega:** primeiro PRD da capacidade (fatia confirmada em **OD25**): modelo de
  Papel e Permissão com os quatro papéis internos; seed do primeiro administrador; Convite de Acesso
  Interno (emitir e aceitar, com ativação da conta interna); conceder e revogar papel, com *alterar*
  expresso como revogar + conceder (**OD22**); comunicação dos quatro atos à Auditoria; backoffice
  mínimo — entrada do ator interno, área de gestão de acesso e uma área reservada que prova o menor
  privilégio.
- **Fora desta entrega:**
  - Telas de trabalho de cada papel → com a capacidade que as usa (autoria em `CAP-005`,
    financeiro em `CAP-011`, atendimento nas capacidades de suporte). Aqui o papel é declarado e a
    permissão existe; a tela vem depois.
  - Consulta da trilha pelo administrador → segundo PRD de `CAP-030` (NA18), logo após esta entrega.
  - Cancelar convite e reenviar o mesmo convite → fora do MVP. Convite expirado exige novo (RN-15);
    ver DP-04 para o convite ainda pendente.
  - Desativar conta interna (RN-23) → `CAP-031`. Revogar todos os papéis já retira o acesso (RF-08).
  - Delegar a emissão de convite a outro papel → mudança posterior deliberada (RN-14).
  - Política de sessão própria do ator interno → revisão de segurança do `admin-spa` (baseline,
    Sessão). Até lá vale RN-08 (QT-01).
- **Domínios atravessados:**
  - Identidade e Acesso — [domain.md](../../domains/identidade-e-acesso/domain.md) v1.1. Dono de
    tudo o que esta entrega decide.
  - Auditoria e Conformidade — [domain.md](../../domains/auditoria-e-conformidade/domain.md) v1.1,
    como destino dos atos, pelo contrato já integrado
    ([asyncapi-contract.yaml](../prd-trilha-auditoria/asyncapi-contract.yaml) v1.0.1).
  - Notificação — sem domain doc; regras no [PRD de `CAP-026`](../prd-notificacao-transacional/prd.md),
    como entregador do e-mail de convite (um modelo de mensagem novo, RF-04).
- **Junta entre os domínios:**
  - Identidade → Auditoria: Identidade é dona do **conteúdo** do ato (o que, quem, sobre quem, por
    quê); Auditoria é dona da **forma**, `auditoria.ato-praticado`, e do registro (RN-A14). Identidade
    não guarda trilha própria nem consulta a trilha para decidir (RN-A10).
  - Identidade → Notificação: Identidade pede o envio informando modelo, dados e finalidade;
    Notificação é dona do texto e da entrega (RN-26 a RN-28, RN-N03).
- **Dependências entre capacidades:** `CAP-001` (conta, credencial, sessão) — entregue.
  `CAP-030` — primeiro PRD entregue (PR #56); a dependência circular foi resolvida em **OD19**:
  `CAP-030` saiu antes como provedora e esta entrega publica no envelope dela, sem stub.
  `CAP-026` — entregue; recebe um modelo novo.
- **Restrições do baseline:** RBAC com papel e permissão em claim, nunca string solta (RN-18); BFF
  por audiência — o backoffice tem a sua (BA05); sessão opaca no BFF, token nunca no navegador
  (BA06); o BFF autoriza o grosso e o serviço dono autoriza de novo, sempre (Autorização); todo ato
  administrativo gera registro de auditoria com autor, momento, alvo e motivo; `tenant_id` em toda
  entidade (BA10 · G07); nenhum dado pessoal em log, span, métrica ou erro (G10 · G23); publicação
  só por outbox (G06).

### Vision Doc

- **Objetivos de negócio atendidos:** objetivo 5 da visão, "ser operada — backoffice com permissões
  por papel, trilha de auditoria"; `C01` (identidade e acesso) na Fase 1. Destrava o objetivo da
  Fase 1 ao permitir que o professor exista (`CAP-005`).
- **Restrições globais aplicáveis:** autenticação própria, sem SSO nem provedor externo; LGPD —
  dado pessoal do convidado e do ator não vaza para telemetria nem para a trilha (RN-A08).

### Domain Docs

- **Entidades envolvidas (Identidade e Acesso):** Conta (lado ator interno), Credencial, Sessão,
  Papel, Permissão, Convite de Acesso Interno, Token de Verificação (para recuperação de senha,
  DP-06).
- **Entidades envolvidas (Auditoria e Conformidade):** Ato Administrativo, apenas como o que é
  comunicado.
- **Regras de negócio referenciadas:** Identidade RN-01, RN-05 a RN-08, RN-10, RN-11, RN-12 a RN-20,
  RN-21, RN-24 a RN-28; Auditoria RN-A05, RN-A06, RN-A08, RN-A12, RN-A14.
- **Regras precisadas por esta entrega:** RN-19 passa a ler *alterar papel = revogar o papel anterior
  + conceder o novo* (OD22); ver DP-01. Demais regras novas estão nos RFs e na tabela de decisões.
- **Mensagens produzidas:**
  - `auditoria.ato-praticado` (contrato de Auditoria), com origem `identidade` e os tipos
    `convite-interno-emitido`, `convite-interno-aceito`, `papel-concedido`, `papel-revogado`.
  - `notificacao.envio-solicitado` (contrato de Notificação), com o modelo de convite interno.
- **Mensagens consumidas:** nenhuma.

## Termos Canônicos

| Termo | Definição de negócio | Escopo/Fonte |
|---|---|---|
| Ator interno | Pessoa que opera a escola pelo backoffice: professor, suporte, financeiro ou administrador | Identidade §2 |
| Conta interna | Conta de ator interno. Nunca é conta de aluno, e vice-versa; exige e-mail próprio | Identidade RN-13, RN-13a |
| Convite pendente | Convite emitido, dentro da validade, ainda não aceito e não substituído | Esta entrega |
| Área do backoffice | Conjunto de telas protegido por uma permissão. Só abre para quem tem a permissão dela | Esta entrega |
| Alterar papel | Revogar o papel anterior e conceder o novo, no mesmo ato do administrador. Não é um tipo de ato próprio | OD22 |

---

## Objetivos

1. **Destravar o passo 4 do backlog:** *"um professor entra no backoffice e não enxerga dado
   financeiro"* — dito no backlog como critério da `CAP-002`.
2. **Todo ator interno tem origem rastreável:** nasce por convite de um administrador identificado,
   com motivo, e isso está na trilha. A única exceção é o seed, declarada (RN-25, RN-A12).
3. **Menor privilégio por construção:** uma área do backoffice só abre com a permissão dela, e a
   recusa acontece duas vezes — na borda e no serviço dono.
4. **Revogação que vale na hora:** quem perde um papel perde o acesso na próxima ação, sem esperar a
   sessão expirar.
5. **Deixar `CAP-005` e o segundo PRD de `CAP-030` sem trabalho de identidade:** o papel de professor
   e o de administrador existem e são verificáveis.

---

## Histórias de Usuário

- **US-01** — Como **administrador**, eu quero convidar uma pessoa por e-mail oferecendo um papel e
  dizendo por quê, para que ela passe a operar sem que eu crie senha por ela.
- **US-02** — Como **convidado**, eu quero aceitar o convite definindo meu nome e minha senha, para
  entrar no backoffice já com o papel que me ofereceram.
- **US-03** — Como **administrador**, eu quero ver quem são os atores internos, seus papéis e os
  convites pendentes, para saber quem pode fazer o quê.
- **US-04** — Como **administrador**, eu quero conceder um papel a mais ou revogar um papel, com
  motivo, para ajustar o acesso quando a função da pessoa muda.
- **US-05** — Como **administrador**, eu quero trocar o papel de alguém numa ação só, para não deixar
  a pessoa com os dois papéis nem sem nenhum no meio do caminho.
- **US-06** — Como **ator interno**, eu quero entrar no backoffice com e-mail e senha e ver só as
  áreas que o meu papel permite.
- **US-07** — Como **escola**, eu quero que o professor não consiga abrir área financeira nem por
  link direto, para que o menor privilégio seja regra e não convenção.
- **US-08** — Como **administrador**, eu quero que o acesso de quem teve papel revogado acabe na hora,
  para que uma demissão não deixe uma sessão aberta.

---

## Funcionalidades Principais

### RF-01: Papéis internos e permissões

**Descrição**: Existem quatro papéis internos — professor, suporte, financeiro e administrador. Cada
papel agrupa permissões; uma conta interna tem permissão **somente** por meio dos seus papéis
(RN-12). Uma conta interna pode ter mais de um papel (RN-13, OD26), e a permissão efetiva é a união
das permissões dos seus papéis. O conjunto inicial de permissões e a matriz papel × permissão estão
em DP-03; novas permissões nascem com as capacidades que criam as telas correspondentes.

**Critérios de Aceitação**:

- **Given** uma conta interna com o papel professor
  **When** a sua permissão efetiva é calculada
  **Then** ela contém apenas as permissões do papel professor

- **Given** uma conta interna com os papéis professor e suporte
  **When** a sua permissão efetiva é calculada
  **Then** ela é a união das permissões dos dois papéis, sem duplicidade

- **Given** qualquer ação de gestão
  **When** alguém tenta atribuir uma permissão diretamente a uma conta, sem papel
  **Then** a operação não existe — não há caminho de produto para isso (RN-12)

- **Given** uma conta de aluno
  **When** alguém tenta conceder a ela um papel interno
  **Then** a concessão é recusada: conta de aluno não recebe papel interno (RN-13)

**Prioridade**: Must Have

**Rastreabilidade**: RN-12, RN-13, RN-16, RN-18

---

### RF-02: Primeiro administrador por provisionamento

**Descrição**: O primeiro administrador de um tenant é criado por um procedimento de provisionamento
executado pelo time, uma única vez, fora do fluxo de convite (RN-25). Ele nasce como conta interna
ativa com o papel administrador e define a própria senha no primeiro acesso, sem que a senha passe
por quem executou o procedimento. O ato **não** aparece na trilha de auditoria — lacuna declarada
(RN-25, RN-A12). A execução fica datada e identificada no registro operacional do time.

**Critérios de Aceitação**:

- **Given** um tenant sem nenhum administrador
  **When** o provisionamento é executado com o e-mail institucional informado
  **Then** existe uma conta interna com o papel administrador para esse e-mail, e ela consegue
  definir a senha e entrar no backoffice

- **Given** um tenant que já tem administrador
  **When** o provisionamento é executado de novo
  **Then** nada é criado nem alterado, e a execução informa que o tenant já tem administrador

- **Given** o provisionamento concluído
  **When** a trilha de auditoria é inspecionada
  **Then** não há registro da criação do primeiro administrador, e isso é o comportamento esperado

- **Given** o e-mail informado pertence a uma conta de aluno
  **When** o provisionamento é executado
  **Then** ele é recusado (RN-13a)

**Prioridade**: Must Have

**Rastreabilidade**: RN-25, RN-13a, RN-A12

---

### RF-03: Emitir Convite de Acesso Interno

**Descrição**: Somente o administrador emite convite (RN-14). O convite informa o e-mail do
convidado, **um** papel ofertado e o motivo, obrigatório. Ele é de uso único, vinculado ao e-mail e
tem validade (RN-15; duração em QT-02). A emissão é um ato administrativo: é comunicada à Auditoria
como `convite-interno-emitido`, com autor (o administrador), alvo (o convite), papel ofertado e
motivo — sem o e-mail do convidado (RN-A08).

**Critérios de Aceitação**:

- **Given** um administrador autenticado
  **When** ele convida um e-mail sem conta, com papel e motivo
  **Then** o convite fica pendente, o e-mail de convite é pedido a Notificação (RF-04) e o ato
  `convite-interno-emitido` é comunicado à Auditoria com autor, alvo, papel e motivo

- **Given** um administrador autenticado
  **When** ele tenta emitir o convite sem motivo, ou com motivo só de espaços
  **Then** o convite não é emitido e o campo é apontado como obrigatório

- **Given** um ator interno sem o papel administrador
  **When** ele tenta emitir um convite, inclusive por chamada direta
  **Then** a emissão é recusada, nada é comunicado à Auditoria (RN-14)

- **Given** o e-mail informado já pertence a uma conta interna
  **When** o administrador tenta convidá-lo
  **Then** o convite é recusado com a orientação de conceder papel à conta existente (RF-06)

- **Given** o e-mail informado pertence a uma conta de aluno
  **When** o administrador tenta convidá-lo
  **Then** o convite é recusado com a orientação de usar o endereço institucional (RN-13a, DP-05)

- **Given** já existe convite pendente para o mesmo e-mail
  **When** o administrador emite um novo convite
  **Then** o anterior deixa de valer, o novo é enviado, e o novo é comunicado como
  `convite-interno-emitido` (DP-04)

- **Given** o e-mail informado com maiúsculas ou espaços nas bordas
  **When** o convite é emitido
  **Then** o e-mail é normalizado como em RN-01 antes de qualquer verificação

**Prioridade**: Must Have

**Rastreabilidade**: RN-01, RN-13a, RN-14, RN-15, RN-19, RN-A05, RN-A08, RN-A14

---

### RF-04: E-mail de convite

**Descrição**: O convite chega ao convidado por e-mail. Identidade pede o envio a Notificação com um
**modelo de mensagem novo, de convite interno**, os dados que o preenchem (papel ofertado, link de
aceite, validade) e a finalidade declarada. O texto do e-mail é de Notificação (RN-28, RN-N03), e
acrescentar o modelo não muda o mecanismo de entrega (objetivo 3 do PRD de `CAP-026`). O e-mail do
convidado aparece no pedido de envio porque é o destinatário, na mesma exceção de RN-26; fora dele,
é mascarado em todo lugar (RN-21).

**Critérios de Aceitação**:

- **Given** um convite emitido
  **When** Notificação entrega a mensagem
  **Then** o convidado recebe um e-mail com o papel ofertado, o link de aceite e a data de validade

- **Given** o pedido de envio
  **When** ele é inspecionado
  **Then** traz modelo, dados e finalidade, e nenhum assunto ou corpo composto por Identidade

- **Given** qualquer log, métrica ou rastreamento do fluxo de convite
  **When** ele é inspecionado
  **Then** não contém o e-mail do convidado nem o link de aceite

**Prioridade**: Must Have

**Rastreabilidade**: RN-21, RN-26, RN-27, RN-28 · PRD `CAP-026` RN-N03

---

### RF-05: Aceitar convite e ativar a conta interna

**Descrição**: O convidado abre o link, informa nome e senha, e passa a ter uma conta interna ativa
com o papel ofertado. Abrir o link recebido no e-mail já comprova o endereço — não há confirmação de
e-mail separada. O aceite é comunicado à Auditoria como `convite-interno-aceito`, com autor (a conta
recém-ativada), alvo (o convite) e sem motivo (RN-A05). O aceite **não** gera `papel-concedido`: a
concessão já foi o ato de emitir o convite, com o seu motivo.

**Critérios de Aceitação**:

- **Given** um convite pendente
  **When** o convidado informa nome e senha válidos
  **Then** a conta interna é criada ativa com o papel ofertado, o convite passa a aceito, o
  convidado entra no backoffice e `convite-interno-aceito` é comunicado à Auditoria

- **Given** um convite expirado, já aceito ou substituído
  **When** o link é aberto
  **Then** a página informa que o convite não vale mais e orienta a pedir um novo ao
  administrador, sem revelar dados da conta nem do convite

- **Given** um convite pendente
  **When** a senha informada não atende à política de senha da plataforma
  **Then** a conta não é criada e a regra não atendida é apontada

- **Given** um convite aceito
  **When** o mesmo link é usado de novo
  **Then** nada acontece além da mensagem de convite sem validade (RN-15, uso único)

- **Given** entre a emissão e o aceite o e-mail passou a ter conta (interna ou de aluno)
  **When** o convidado tenta aceitar
  **Then** o aceite é recusado e orienta procurar o administrador (RN-01, RN-13a)

**Prioridade**: Must Have

**Rastreabilidade**: RN-01, RN-06, RN-13a, RN-15, RN-19, RN-A05

---

### RF-06: Conceder papel

**Descrição**: O administrador concede um papel a mais a uma conta interna, com motivo obrigatório.
A concessão vale na próxima decisão de autorização do ator. É comunicada à Auditoria como
`papel-concedido`, com autor, alvo (a conta), papel e motivo.

**Critérios de Aceitação**:

- **Given** um administrador e uma conta interna de outra pessoa sem o papel suporte
  **When** ele concede suporte com motivo
  **Then** a conta passa a ter suporte, as permissões de suporte valem na próxima ação do ator e
  `papel-concedido` é comunicado à Auditoria

- **Given** a conta já tem o papel
  **When** o administrador tenta concedê-lo de novo
  **Then** nada muda e nada é comunicado à Auditoria

- **Given** o administrador
  **When** ele tenta conceder papel à própria conta
  **Then** a concessão é recusada (RN-20), inclusive por chamada direta

- **Given** um ator interno sem o papel administrador
  **When** ele tenta conceder papel
  **Then** a concessão é recusada

- **Given** a concessão sem motivo
  **When** o administrador confirma
  **Then** a concessão não acontece e o motivo é apontado como obrigatório

**Prioridade**: Must Have

**Rastreabilidade**: RN-12, RN-19, RN-20, RN-A05

---

### RF-07: Revogar papel

**Descrição**: O administrador revoga um papel de uma conta interna, com motivo obrigatório. A
revogação vale na **próxima decisão de autorização** e **encerra as sessões** do ator afetado
(RN-17), mesmo que ele mantenha outros papéis. É comunicada à Auditoria como `papel-revogado`.

**Critérios de Aceitação**:

- **Given** um ator com os papéis professor e suporte, com sessão aberta
  **When** o administrador revoga suporte com motivo
  **Then** as sessões do ator são encerradas, no próximo acesso ele entra só com as permissões de
  professor, e `papel-revogado` é comunicado à Auditoria

- **Given** um ator com sessão aberta numa área de suporte
  **When** o papel suporte é revogado
  **Then** a próxima ação dele é recusada, sem esperar a sessão expirar

- **Given** o administrador
  **When** ele tenta revogar papel da própria conta
  **Then** a revogação é recusada (RN-20)

- **Given** a conta não tem o papel
  **When** alguém tenta revogá-lo
  **Then** nada muda e nada é comunicado à Auditoria

**Prioridade**: Must Have

**Rastreabilidade**: RN-17, RN-19, RN-20, RN-08, RN-A05

---

### RF-08: Conta interna sem papel

**Descrição**: Uma conta interna pode ficar sem nenhum papel — por revogação do último. Ela continua
existindo (desativar é de `CAP-031`), mas não abre nenhuma área do backoffice. Ao entrar, o ator vê
que não tem acesso e que deve procurar o administrador. Um novo papel é concedido pelo RF-06, não por
novo convite.

**Critérios de Aceitação**:

- **Given** uma conta interna sem papel
  **When** o ator entra com credencial correta
  **Then** ele vê a mensagem de conta sem acesso e nenhuma área do backoffice

- **Given** uma conta interna sem papel
  **When** o administrador lhe concede um papel
  **Then** o ator passa a ver as áreas desse papel no próximo acesso

**Prioridade**: Must Have

**Rastreabilidade**: RN-17, RN-23

---

### RF-09: Trocar papel

**Descrição**: Na gestão de acesso, o administrador troca um papel de uma conta por outro numa só
ação, com um só motivo. Para o produto é uma ação; para a trilha são dois atos, `papel-revogado` do
papel anterior e `papel-concedido` do novo, cada um com o mesmo autor e motivo (OD22). Ou os dois
acontecem, ou nenhum.

**Critérios de Aceitação**:

- **Given** um ator professor
  **When** o administrador troca professor por financeiro, com motivo
  **Then** a conta passa a ter financeiro e não professor, as sessões do ator são encerradas (RN-17),
  e a Auditoria recebe `papel-revogado` (professor) e `papel-concedido` (financeiro), ambos com o
  motivo informado

- **Given** qualquer falha no meio da troca
  **When** a troca não se completa
  **Then** a conta mantém o papel anterior e nenhum dos dois atos é comunicado

- **Given** o destino da troca é um papel que a conta já tem
  **When** o administrador confirma
  **Then** o resultado equivale a revogar o papel de origem (RF-07), com um único ato comunicado

**Prioridade**: Must Have

**Rastreabilidade**: RN-17, RN-19 (precisada por OD22), RN-20

---

### RF-10: Entrar e sair do backoffice

**Descrição**: O ator interno entra no backoffice com e-mail e senha, numa entrada própria, separada
da entrada do aluno. Conta de aluno não entra no backoffice, e conta interna não entra na área do
aluno (RN-13). A resposta a credencial inválida não distingue e-mail inexistente de senha errada
(RN-05). Sessão, expiração por inatividade, saída explícita e proteção de escrita seguem as regras
já vigentes para sessão (RN-08, RN-11), até a revisão de segurança do backoffice definir política
própria (QT-01).

**Critérios de Aceitação**:

- **Given** um ator interno com papel
  **When** ele entra com credencial correta
  **Then** ele chega ao início do backoffice, que mostra somente as áreas das suas permissões

- **Given** uma conta de aluno
  **When** ela tenta entrar no backoffice com credencial correta
  **Then** a resposta é a mesma de credencial inválida

- **Given** uma conta interna
  **When** ela tenta entrar na área do aluno
  **Then** a resposta é a mesma de credencial inválida

- **Given** e-mail inexistente ou senha errada
  **When** alguém tenta entrar
  **Then** a resposta é idêntica nos dois casos

- **Given** um ator autenticado
  **When** ele sai
  **Then** a sessão é encerrada e a próxima ação exige nova entrada

**Prioridade**: Must Have

**Rastreabilidade**: RN-05, RN-08, RN-10, RN-11, RN-13, RN-18

---

### RF-11: Recuperar a senha do ator interno

**Descrição**: O ator interno que esquece a senha a redefine sozinho pelo mesmo comportamento já
vigente para o aluno: pedido por e-mail sem revelar se a conta existe, link de uso único e vida
curta, e redefinição que encerra as outras sessões. Sem isso, a única saída seria o administrador
recriar a pessoa — o que esta entrega não oferece.

**Critérios de Aceitação**:

- **Given** uma conta interna
  **When** o ator pede recuperação pela entrada do backoffice
  **Then** ele recebe o e-mail de redefinição, e a resposta na tela é a mesma de um e-mail sem conta

- **Given** um link de redefinição válido
  **When** o ator define uma nova senha
  **Then** a senha é trocada, as demais sessões são encerradas e o link deixa de valer

- **Given** um e-mail de conta de aluno
  **When** a recuperação é pedida pela entrada do backoffice
  **Then** a resposta na tela é a mesma, e nenhum e-mail é enviado para redefinir acesso ao
  backoffice

**Prioridade**: Should Have

**Rastreabilidade**: RN-03, RN-04, RN-06, RN-07

---

### RF-12: Gestão de acesso

**Descrição**: Área do backoffice, somente do administrador, que lista os atores internos do tenant
com seus papéis e os convites pendentes (e-mail, papel ofertado, validade), e de onde partem convidar
(RF-03), conceder (RF-06), revogar (RF-07) e trocar (RF-09). Ações sobre a própria conta não são
oferecidas (RN-20). O e-mail aparece nesta tela porque é como o administrador reconhece a pessoa;
continua fora de log, URL e telemetria (RN-21).

**Critérios de Aceitação**:

- **Given** um administrador
  **When** ele abre a gestão de acesso
  **Then** vê todos os atores internos do seu tenant, cada um com seus papéis, e os convites
  pendentes — nunca de outro tenant (RN-24)

- **Given** um administrador vendo a própria linha
  **When** a lista é exibida
  **Then** não há ação de conceder, revogar nem trocar para ele

- **Given** um ator sem o papel administrador
  **When** ele tenta abrir a gestão de acesso, inclusive por link direto
  **Then** o acesso é recusado

**Prioridade**: Must Have

**Rastreabilidade**: RN-14, RN-20, RN-21, RN-24

---

### RF-13: Área reservada por permissão — prova do menor privilégio

**Descrição**: Cada área do backoffice é protegida por uma permissão, e a recusa acontece na borda
**e** no serviço dono — a borda não é fronteira de confiança. Nesta entrega existe uma **área
financeira reservada**, sem dado ainda, que só abre para quem tem a permissão financeira (DP-03);
ela é preenchida por `CAP-011`. É a prova observável de RN-16 antes que exista dado financeiro.

**Critérios de Aceitação**:

- **Given** um ator só com o papel professor
  **When** ele tenta abrir a área financeira pelo menu, por link direto ou por chamada direta ao
  serviço
  **Then** o acesso é recusado nas três vias, e o menu nem oferece a área

- **Given** um ator com o papel financeiro
  **When** ele abre a área financeira
  **Then** o acesso é concedido

- **Given** um ator professor e suporte
  **When** ele abre o início do backoffice
  **Then** a área financeira não aparece

- **Given** o papel financeiro revogado de um ator com sessão aberta na área financeira
  **When** ele faz a próxima ação
  **Then** o acesso é recusado (RN-17)

**Prioridade**: Must Have

**Rastreabilidade**: RN-16, RN-17, RN-18

---

### RF-14: Comunicar os atos à Auditoria

**Descrição**: Os quatro atos administrativos desta entrega são comunicados à Auditoria no envelope
`auditoria.ato-praticado` (contrato 1.0.1), na mesma transação que muda o estado — nenhum ato
acontece sem ser comunicado e nenhum é comunicado sem ter acontecido. Autor e alvo vão por
referência, nunca por e-mail ou nome (RN-A08). O momento informado é o do ato. Cada ato tem
identificação própria, e a reentrega não gera ato novo.

| Ato | Autor | Alvo | Papel | Motivo |
|---|---|---|---|---|
| `convite-interno-emitido` | administrador | convite | ofertado | obrigatório |
| `convite-interno-aceito` | conta recém-ativada | convite | — | não se aplica |
| `papel-concedido` | administrador | conta interna | concedido | obrigatório |
| `papel-revogado` | administrador | conta interna | revogado | obrigatório |

**Critérios de Aceitação**:

- **Given** cada um dos quatro atos praticado com sucesso
  **When** a trilha é inspecionada
  **Then** há exatamente um registro **conforme** por ato, com autor, alvo, papel e motivo corretos

- **Given** um ato que falhou e não mudou o estado
  **When** a trilha é inspecionada
  **Then** não há registro dele

- **Given** o broker indisponível no momento do ato
  **When** o ato é praticado
  **Then** o ato vale, e a comunicação é entregue quando o broker voltar, sem perda

- **Given** qualquer ato comunicado
  **When** ele é inspecionado
  **Then** não contém e-mail, nome nem texto além do motivo

**Prioridade**: Must Have

**Rastreabilidade**: RN-19, RN-A05, RN-A06, RN-A08, RN-A14 · G06

---

## Experiência do Usuário

**Personas.** Administrador — uso raro, alto impacto; precisa de clareza sobre o efeito de cada ação.
Professor, suporte, financeiro — uso diário; precisam entrar e ver só o que é seu. Convidado — primeiro
contato com a plataforma pelo e-mail.

**Fluxo do convite.** Gestão de acesso → *Convidar* → e-mail, papel (um), motivo → confirmação que
mostra a validade. O convidado recebe o e-mail, abre o link, vê o papel que lhe foi ofertado, informa
nome e senha e entra no backoffice.

**Fluxo de gestão.** Na linha de cada ator: *Conceder papel*, *Revogar* (por papel) e *Trocar papel*.
Toda ação pede motivo e mostra, antes de confirmar, o efeito: *"a pessoa será desconectada agora"*
quando houver revogação ou troca. O motivo é texto livre com orientação para não citar dado pessoal de
terceiros (RN-A08).

**Entrada.** A entrada do backoffice é visivelmente distinta da do aluno, para que quem tem as duas
contas (RN-13a) não confunda os endereços.

**Acessibilidade.** Formulários navegáveis por teclado, erros associados ao campo e anunciados a
leitor de tela, contraste do design system do backoffice.

## Decisões de Produto

| ID | Decisão confirmada | Alternativas descartadas e motivo | Impacto no PRD | Registro |
|---|---|---|---|---|
| DP-01 | **Alterar papel = revogar + conceder**, dois atos com o mesmo autor e motivo, atômicos, sem campo de correlação. RN-19 é precisada nesse sentido | Tipo próprio `papel-alterado`: reabriria `CAP-030` e deixaria o ato não conforme até subir | RF-09, RF-14 | OD22 |
| DP-02 | **Papéis internos acumulam** (RN-13). O convite oferta um papel; outros vêm por concessão | Um papel por conta: contradiria RN-13 | RF-01, RF-03, RF-06 | OD26 |
| DP-03 | Permissões iniciais: `acesso.gerir` (administrador), `financeiro.ler` (financeiro), `autoria.ler` (professor), `suporte.atender` (suporte). **O administrador governa acesso e não herda as áreas dos outros papéis**; se precisar, recebe o papel de outro administrador | Administrador com todas as permissões: contraria o menor privilégio e faz da conta do administrador o alvo mais valioso do sistema | RF-01, RF-12, RF-13 | — |
| DP-04 | **Novo convite para o mesmo e-mail substitui o pendente**, que deixa de valer | Recusar enquanto houver pendente: e-mail perdido obrigaria esperar a validade | RF-03, RF-05 | — |
| DP-05 | A recusa de convite por e-mail de aluno ou de conta interna **diz o motivo** ao administrador | Resposta neutra: a proteção contra enumeração (RN-04, RN-05) é para a superfície pública; o administrador já é o ator mais confiável | RF-03 | — |
| DP-06 | **Recuperação de senha do ator interno entra nesta entrega**, reusando o comportamento de `CAP-001` | Deixar fora: senha esquecida não teria saída, porque não há reinício pelo administrador | RF-11 | — |

---

## Restrições Técnicas de Alto Nível

- O backoffice é superfície própria, com borda própria, separada da do aluno (BA05); o navegador não
  guarda token (BA06).
- Autorização duas vezes: na borda (pode estar nesta área?) e no serviço dono (pode fazer isto?).
- Os atos seguem o contrato `auditoria.ato-praticado` 1.0.1 como está; esta entrega não o altera.
- O modelo de convite é acrescentado a Notificação sem mudar o mecanismo de entrega.
- `tenant_id` em papel, permissão, convite e conta interna; nenhuma listagem cruza tenant.
- Nenhum dado pessoal (e-mail, nome, link de convite ou de redefinição, motivo) em log, span, métrica,
  URL ou mensagem de erro.

---

## Não-Objetivos (Fora de Escopo)

- Telas de trabalho de professor, suporte e financeiro — vêm com as capacidades que as usam.
- Consulta da trilha — segundo PRD de `CAP-030`.
- Cancelar convite, reenviar o mesmo convite, listar convites expirados.
- Desativar ou excluir conta interna (`CAP-031`); editar nome ou e-mail de conta interna.
- Segundo fator de autenticação, SSO, login social.
- Papel ou permissão configurável pelo administrador — o catálogo é do produto.
- Registro do seed na trilha (RN-A12).
- Restrição de sessões simultâneas para ator interno (fica com a revisão de segurança do backoffice).

---

## Plano de Rollout Faseado

### MVP (Fase 1) — este PRD

- **Funcionalidades incluídas:** RF-01 a RF-14 .
- **Critério para seguir:** um administrador convida um professor, o professor aceita, entra, não
  abre a área financeira por nenhuma via, e a trilha tem os registros conformes de convite emitido e
  aceito. Uma troca de papel produz os dois registros e desconecta o ator.

### Depois deste PRD

- **Segundo PRD de `CAP-030`** — consulta da trilha pelo administrador (usa `acesso.gerir` ou uma
  permissão própria, decidido lá).
- **`CAP-005`** — preenche a área de autoria do professor; **`CAP-011`**, a financeira.
- **Revisão de segurança do backoffice** — política de sessão do ator interno e segundo fator.

---

## Métricas de Sucesso

| Métrica | Definição | Alvo | Prazo |
|---|---|---|---|
| Ato sem registro | Atos praticados (estado de Identidade) sem registro conforme na trilha | Zero | Contínuo, desde o primeiro convite |
| Acesso indevido | Tentativas de área sem permissão que foram atendidas, em teste e em produção | Zero | Contínuo |
| Revogação efetiva | Ações atendidas a um ator depois da revogação do papel que as permitia | Zero | Contínuo |
| Convite aceito | Convites aceitos sobre convites emitidos na validade | Observado, sem alvo no MVP — baixo aceite indica problema de entrega ou de validade | Primeiros 90 dias |

---

## Riscos e Mitigações

- **Administrador único indisponível:** sem ele ninguém convida nem concede. Mitigação: recomendar,
  no roteiro de implantação, um segundo administrador convidado logo após o seed. O último
  administrador não pode ser revogado por ninguém — RN-20 impede a si mesmo e só administradores
  revogam — então o tenant nunca fica sem administrador por esta via.
- **Motivo vazio de sentido** ("ok", "."): a trilha fica formalmente conforme e inútil. Mitigação:
  orientação na tela; a qualidade do motivo é tema de governança, não de validação.
- **Papel virar catálogo de telas** (Identidade RF-02): cada capacidade quer um papel novo.
  Mitigação: DP-03 — capacidade nova acrescenta **permissão**, não papel.
- **Convite para endereço pessoal** de quem também é aluno: bloqueado (RN-13a), mas vira atrito na
  primeira semana. Mitigação: mensagem clara (DP-05) e orientação no e-mail de convite.

---

## Alternativas Consideradas

### Abordagem Escolhida: acesso por convite com papéis acumuláveis e troca expressa em dois atos

- **Descrição:** RF-01 a RF-14.
- **Por que foi escolhida:** respeita RN-13 e RN-14 como aprovadas, usa o contrato de Auditoria sem
  alterá-lo (OD22) e entrega a prova do menor privilégio antes que exista dado a proteger.

### Alternativa Rejeitada 1: `CAP-002` inteira, com as telas de cada papel

- **Trade-offs:** entregaria permissões finas já usadas, mas dependeria de autoria, financeiro e
  atendimento, que não existem.
- **Por que foi rejeitada:** OD25 — as telas vêm com as capacidades que as justificam.

### Alternativa Rejeitada 2: administrador cria a conta com senha provisória

- **Trade-offs:** mais simples que convite; a senha passaria pelo administrador.
- **Por que foi rejeitada:** contraria RN-14 (convite como único caminho) e RN-06 (segredo nunca em
  claro).

---

## Questões em Aberto

- **QT-01 — Política de sessão do ator interno.** Expiração por inatividade e duração máxima para o
  backoffice. Dono: revisão de segurança do `admin-spa` (baseline). Até lá vale RN-08 com os mesmos
  valores do aluno. Não bloqueia.
- **QT-02 — Validade do convite.** Recomendação: 7 dias, parametrizado. Dono: produto + segurança.
  Precisa de número antes da TechSpec fechar os testes; não muda comportamento.
- **QT-03 — Revisão dos domain docs.** Identidade RN-19 (DP-01) e §7, onde os quatro atos passam a
  ser comunicados em `auditoria.ato-praticado` (OD23) e não como eventos `identidade.*` próprios.
  Dono: Identidade e Acesso. Revisão do domain doc após a aprovação deste PRD; não bloqueia a TechSpec.
