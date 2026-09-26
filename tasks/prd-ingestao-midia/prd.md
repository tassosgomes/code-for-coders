---
tsg_artifact: prd
product: code-4-coders
capability: CAP-006
version: 1.0
status: approved
updated: 2026-09-25
sources: backlog/capabilities.md@1.4, vision.md@1.2, context/domain-map.md@1.2, context/architecture-baseline.md@1.2, domains/entrega-de-midia-e-protecao/domain.md@1.0, domains/identidade-e-acesso/domain.md@1.1
---

# Ingestão de mídia — o professor envia o vídeo e ele fica pronto para a aula

## Visão Geral

O professor grava a aula e precisa colocá-la na plataforma. Hoje não há como: o vídeo, que é o ativo
do negócio, não tem onde entrar. Esta entrega dá ao professor uma tela no backoffice para **enviar o
vídeo, acompanhar a preparação e ver quando ele está pronto**. Por baixo, a plataforma transforma o
arquivo em uma versão segmentada e cifrada, em três qualidades, sem que nada fique público em momento
algum.

É o primeiro passo do ciclo "o que se aprende" do MVP: sem vídeo pronto, `CAP-005` não tem o que
vincular à aula, e `CAP-007` não tem o que reproduzir. Esta entrega **não** reproduz nada para o
aluno. Ela termina quando o vídeo está pronto e pode ser consultado para o vínculo.

---

## Rastreabilidade

### Capacidade e fronteiras

- **Capacidade:** `CAP-006` — Ingestão e preparação de mídia protegida.
- **Escopo desta entrega (OD29):** o professor envia o vídeo; a plataforma prepara a Versão de
  Reprodução em HLS com AES-128 e guarda a chave; o estado do ativo (recebido, em preparação, pronto,
  falhou) fica visível no backoffice; o ativo pronto pode ser consultado para vínculo; nenhum objeto
  de vídeo é público.
- **Fora desta entrega:**
  - reprodução, acesso de vida curta, entrega da chave ao player e marca d'água → `CAP-007`;
  - vínculo à aula e Referência de Uso → `CAP-005` (vínculo) e `CAP-007` (uso na sessão);
  - Material (envio, guarda e entrega) → a decidir no planejamento de `CAP-005` (QM-05 do domain doc);
  - substituir, excluir ou reter o original de um vídeo → depois do MVP (QM-04 do domain doc);
  - pré-visualização do vídeo pelo professor → junto com `CAP-007`, que cria a reprodução.
- **Domínios atravessados:**
  - Entrega de Mídia e Proteção — [domain.md](../../domains/entrega-de-midia-e-protecao/domain.md);
  - Identidade e Acesso — [domain.md](../../domains/identidade-e-acesso/domain.md), só pela permissão
    de envio.
- **Junta entre os domínios:** Identidade diz quem é o ator e se o papel dele carrega a permissão de
  envio. Mídia decide o resto. A permissão é do catálogo de Identidade; o ativo e o estado dele são
  de Mídia.
- **Junta com o consumidor seguinte (OD30):** Conteúdo e Currículo, em `CAP-005`, consulta se um ativo
  existe, é da escola e está pronto, antes de vinculá-lo. Esta entrega disponibiliza essa consulta;
  a Referência de Uso não entra aqui.
- **Dependências entre capacidades:** `CAP-006` depende de `CAP-002` (papel de professor e catálogo
  de permissões). As tasks desta entrega começam depois de `CAP-002` integrada.
- **Restrições do baseline:** nenhum objeto de vídeo público (G21); versão única para todos os alunos,
  nenhum derivado por aluno (G22); sem DRM, proteção é dissuasão (BA15, R8); publicação por outbox
  (G06); `tenant_id` em tudo (G07); provedor atrás de camada anticorrupção (BA14); preparação com
  ffmpeg dentro do serviço (OD28).

### Vision Doc

- **Objetivos de negócio atendidos:** solução 2, "Aprender — player de vídeo proprietário com proteção
  de conteúdo", pelo lado da entrada do ativo; `C04` (entrega de vídeo e proteção). Tira o ativo de
  plataforma de terceiro, que é a motivação do projeto.
- **Restrições globais aplicáveis:** AWS S3 + CloudFront para armazenamento e distribuição de vídeo;
  proteção sem DRM, declarada como dissuasão.
- **Non-Goals globais respeitados:** não faz produção, edição nem transcodificação autoral além do
  necessário para entrega; não define identidade visual.

### Domain Docs

- **Entidades envolvidas (Entrega de Mídia e Proteção):** Ativo Protegido (só do tipo vídeo), Mídia,
  Versão de Reprodução.
- **Entidades envolvidas (Identidade e Acesso):** Papel, Permissão, apenas como o que é consultado.
- **Regras de negócio referenciadas:** Mídia RN-M01 a RN-M07, RN-M15, RN-M17; Identidade RN-12,
  RN-16, RN-18.
- **Regras precisadas por esta entrega:** as qualidades da Versão de Reprodução (RN-M07, DP-01), o
  limite de envio (DP-02) e a visibilidade dos ativos (RN-M04, DP-03).
- **Eventos produzidos:** `midia.ativo-pronto`, `midia.preparacao-falhou`.
- **Eventos consumidos:** nenhum.

## Termos Canônicos

| Termo | Definição de negócio | Escopo/Fonte |
|---|---|---|
| Vídeo | Como o professor chama a Mídia no backoffice. Na tela se diz "vídeo"; nos documentos, Mídia | Esta entrega |
| Envio | O ato de o professor transferir o arquivo gravado para a plataforma, do início até o arquivo estar recebido por inteiro | Esta entrega |
| Preparação | O trabalho da plataforma de transformar o arquivo recebido na Versão de Reprodução | Domain doc de Mídia |
| Título | Nome que o professor dá ao vídeo para reconhecê-lo depois, no vínculo à aula. Não é título de aula | Esta entrega |

---

## Objetivos

1. **Destravar o passo 4 do backlog:** ao fim desta entrega, existe vídeo pronto na plataforma para
   `CAP-005` vincular, sem nenhum trabalho de mídia pendente naquela capacidade.
2. **Nada público, por construção:** nenhum arquivo enviado nem gerado pode ser obtido sem acesso
   individual. Como a reprodução ainda não existe, nesta entrega **ninguém** de fora obtém nada.
3. **O professor não fica no escuro:** todo envio termina em um estado que o professor entende,
   pronto ou falhou com motivo, sem precisar pedir ajuda ao time.
4. **Custo visível desde o primeiro vídeo:** o volume guardado por escola é medido a partir desta
   entrega (RN-M17), antes que o custo por aluno exista para ser comparado.

---

## Histórias de Usuário

- **US-01** — Como **professor**, eu quero enviar a gravação da aula para que ela entre na
  plataforma sem depender de serviço de terceiro.
- **US-02** — Como **professor**, eu quero que um envio interrompido continue de onde parou para que
  eu não perca uma hora de upload por causa de uma queda de rede.
- **US-03** — Como **professor**, eu quero ver em que ponto cada vídeo está, e o motivo quando algo
  falha, para saber se posso seguir ou se preciso reenviar.
- **US-04** — Como **professor**, eu quero ver os vídeos enviados pelos outros professores da escola
  para montar uma aula com a gravação de um colega.
- **US-05** — Como **aluno** (no futuro, em `CAP-007`), eu quero que o vídeo se adapte à minha rede
  para assistir no 4G sem travar e no Wi-Fi com o código legível.
- **US-06** — Como **escola**, eu quero que nenhum vídeo possa ser baixado por link direto, para que o
  meu ativo não circule fora da plataforma.
- **US-07** — Como **escola**, eu quero que um ator sem a permissão de envio não consiga enviar nem
  ver vídeos, nem por tela nem por chamada direta.

---

## Funcionalidades Principais

### RF-01: Permissão de envio de mídia

**Descrição**: Passa a existir a permissão `midia.enviar`, concedida ao papel **professor**. Ela
permite enviar vídeo, ver a lista de vídeos da escola e editar o título. O administrador **não**
recebe a permissão, pelo mesmo princípio de DP-03 de `CAP-002`: quem governa acesso não herda as
áreas dos outros papéis. Suporte e financeiro também não a recebem. A permissão entra no catálogo de
Identidade e Acesso, que é o dono dele.

**Critérios de Aceitação**:

- **Given** um ator com o papel professor
  **When** a sua permissão efetiva é calculada
  **Then** ela contém `midia.enviar`, além das permissões que o papel já tinha

- **Given** um ator só com o papel administrador, suporte ou financeiro
  **When** ele tenta abrir a área de vídeos, pela tela ou por chamada direta à borda ou ao serviço
  **Then** o acesso é negado, e nenhum dado de vídeo é devolvido

- **Given** um professor que perde o papel professor (revogação de `CAP-002`)
  **When** ele faz a próxima ação na área de vídeos
  **Then** o acesso é negado, e os vídeos que ele enviou continuam na escola, inalterados (RN-M04)

**Prioridade**: Must Have

**Rastreabilidade**: Identidade RN-12, RN-16, RN-17, RN-18; Mídia RN-M03, RN-M04

---

### RF-02: Enviar vídeo

**Descrição**: O professor escolhe um arquivo de vídeo, informa um título e envia. A plataforma aceita
**MP4, MOV e MKV de até 5 GB** (DP-02). Formato e tamanho são verificados **antes** de a transferência
começar, para o professor não esperar à toa. O título é obrigatório e começa preenchido com o nome do
arquivo. O professor vê o progresso da transferência. Ao fim da transferência, o vídeo aparece na
lista no estado **recebido**, com o professor registrado como autor do envio. O vídeo pertence à
escola (RN-M04).

**Critérios de Aceitação**:

- **Given** um professor com um arquivo MP4 de 3 GB
  **When** ele o seleciona, confirma o título e envia
  **Then** vê o progresso, e ao terminar o vídeo aparece na lista como *recebido*, com título, autor
  e momento do envio

- **Given** um professor que seleciona um arquivo de 6 GB
  **When** tenta enviar
  **Then** o envio é recusado antes de começar, com a mensagem de que o limite é 5 GB, e nenhum dado é
  transferido

- **Given** um professor que seleciona um arquivo que não é MP4, MOV nem MKV
  **When** tenta enviar
  **Then** o envio é recusado antes de começar, com os formatos aceitos na mensagem

- **Given** um professor que apaga o título
  **When** tenta enviar
  **Then** o envio não começa, e o campo título indica que é obrigatório

- **Given** o mesmo envio repetido por uma falha de rede na confirmação
  **When** a plataforma recebe a segunda confirmação
  **Then** existe **um** vídeo na lista, não dois

**Prioridade**: Must Have

**Rastreabilidade**: Mídia RN-M03, RN-M04; DP-02

---

### RF-03: Retomar envio interrompido

**Descrição**: Se a transferência for interrompida (queda de rede, aba fechada, computador em
suspensão), o professor retoma **de onde parou**, selecionando o mesmo arquivo de novo, em até **24
horas** depois da última parte recebida. Depois disso, o envio incompleto é descartado e não aparece
na lista. Um envio incompleto nunca vira vídeo e nunca é preparado.

**Critérios de Aceitação**:

- **Given** um envio de 4 GB interrompido com 3 GB transferidos
  **When** o professor volta à tela em até 24 horas e seleciona o mesmo arquivo
  **Then** a transferência continua a partir dos 3 GB, e o progresso mostra isso

- **Given** um envio interrompido
  **When** o professor seleciona um arquivo **diferente**
  **Then** a plataforma trata como um envio novo, sem misturar com o interrompido

- **Given** um envio interrompido há mais de 24 horas
  **When** o professor tenta retomá-lo
  **Then** ele começa do zero, e o envio antigo já não existe

- **Given** um envio interrompido
  **When** qualquer pessoa consulta a lista de vídeos
  **Then** ele não aparece como vídeo; no máximo, o próprio professor vê que há um envio a retomar

**Prioridade**: Must Have

**Rastreabilidade**: DP-02

---

### RF-04: Preparar o vídeo

**Descrição**: Depois de recebido, o vídeo é preparado **automaticamente**, sem ação do professor, e
passa ao estado **em preparação**. A preparação gera a Versão de Reprodução: segmentada, cifrada com
**uma chave por vídeo**, em **três qualidades, 1080p, 720p e 480p**, para o player escolher pela rede
do aluno (DP-01). Se o original for menor que 1080p, gera só as qualidades até a resolução do
original, **sem ampliar**. A Versão de Reprodução é a mesma para todos os alunos (RN-M07). A duração
máxima aceita é de **3 horas** (DP-02). Como só dá para medir a duração depois de receber o arquivo,
um vídeo mais longo falha na preparação, com esse motivo.

**O arquivo original é descartado** assim que o vídeo chega a *pronto* ou a *falhou* (DP-06). Só a
Versão de Reprodução permanece. Consequência aceita: preparar de novo com outra escada de qualidades
exige que o professor reenvie o arquivo.

Falhas passageiras são tentadas de novo automaticamente. O vídeo só é declarado **falhou** depois de
esgotadas as tentativas (RN-M05). Vários vídeos enviados ao mesmo tempo esperam a vez em *recebido*,
e nenhum envio é recusado por fila cheia.

**Critérios de Aceitação**:

- **Given** um vídeo 1080p recebido
  **When** a preparação termina
  **Then** existem as três qualidades, cifradas, e o vídeo passa a *pronto*, com a duração registrada

- **Given** um vídeo 720p recebido
  **When** a preparação termina
  **Then** existem só as qualidades 720p e 480p, e o vídeo passa a *pronto*

- **Given** um vídeo de 3h20 recebido
  **When** a preparação o avalia
  **Then** o vídeo passa a *falhou*, com o motivo "duração acima de 3 horas"

- **Given** um arquivo MP4 corrompido ou sem faixa de vídeo
  **When** a preparação o avalia
  **Then** o vídeo passa a *falhou*, com o motivo "arquivo de vídeo ilegível", e não fica em
  preparação para sempre

- **Given** uma interrupção passageira no meio da preparação
  **When** a plataforma retoma
  **Then** a preparação é tentada de novo sem ação do professor, e o vídeo só vai a *falhou* se as
  tentativas se esgotarem

- **Given** dois vídeos iguais, byte a byte, enviados por professores diferentes
  **When** ambos são preparados
  **Then** cada um é um vídeo próprio, com chave própria

- **Given** um vídeo que acabou de chegar a *pronto* ou a *falhou*
  **When** o armazenamento da escola é inspecionado
  **Then** o arquivo original daquele vídeo já não existe; no caso de *pronto*, só as qualidades
  geradas permanecem

**Prioridade**: Must Have

**Rastreabilidade**: Mídia RN-M05, RN-M07; DP-01, DP-02, DP-06; OD28

---

### RF-05: Estado do vídeo e motivo da falha

**Descrição**: Todo vídeo está em um de quatro estados: **recebido**, **em preparação**, **pronto** ou
**falhou**. O estado só anda para frente; *pronto* e *falhou* são finais (RN-M05). O motivo de uma
falha é dito em linguagem de quem produz vídeo (RN-M06), nunca como erro técnico. Um vídeo que falhou
permanece na lista, marcado como falhou, e o caminho é reenviar como um vídeo novo. Os motivos
previstos são: "arquivo de vídeo ilegível", "formato de vídeo não suportado", "duração acima de 3
horas" e, para o que não se encaixa, "não foi possível preparar este vídeo — envie novamente".

**Critérios de Aceitação**:

- **Given** um vídeo *pronto*
  **When** qualquer evento posterior acontece
  **Then** ele não volta a *em preparação* nem a *recebido*

- **Given** um vídeo *falhou*
  **When** o professor o vê na lista
  **Then** vê o motivo em linguagem de produção e a orientação de enviar novamente, sem código de
  erro, pilha nem nome de ferramenta

- **Given** um vídeo *falhou*
  **When** o professor reenvia o mesmo arquivo corrigido
  **Then** surge um vídeo novo, e o que falhou continua registrado como falhou

**Prioridade**: Must Have

**Rastreabilidade**: Mídia RN-M05, RN-M06

---

### RF-06: Lista de vídeos da escola

**Descrição**: A área de vídeos mostra **todos os vídeos da escola** a quem tem `midia.enviar` (DP-03),
do mais recente ao mais antigo, com título, estado, autor do envio, momento do envio e, quando pronto,
a duração. O professor pode filtrar por estado e buscar pelo título. **Enquanto a tela está aberta, o
estado se atualiza sozinho**: o professor vê o vídeo passar de *recebido* a *pronto* sem recarregar
(DP-04). Não há aviso por e-mail.

**Critérios de Aceitação**:

- **Given** a professora A e o professor B, cada um com vídeos enviados
  **When** A abre a lista
  **Then** vê os vídeos dela e os de B, cada um com o autor indicado

- **Given** um vídeo em preparação e a lista aberta
  **When** a preparação termina
  **Then** a linha passa a *pronto* sem que o professor recarregue a página

- **Given** um vídeo enviado por um professor cujo papel foi revogado
  **When** outro professor abre a lista
  **Then** o vídeo continua visível, com o autor original

- **Given** vídeos de duas escolas (tenants)
  **When** um professor de uma delas abre a lista
  **Then** só vê os da sua escola

- **Given** a lista com vídeos em vários estados
  **When** o professor filtra por *falhou*
  **Then** só os que falharam aparecem

**Prioridade**: Must Have

**Rastreabilidade**: Mídia RN-M04, RN-M06; DP-03, DP-04

---

### RF-07: Editar o título

**Descrição**: Quem tem `midia.enviar` pode corrigir o título de qualquer vídeo da escola, em
qualquer estado. O título só serve para reconhecer o vídeo, e mudá-lo não altera nada do vídeo em si.

**Critérios de Aceitação**:

- **Given** um vídeo *pronto* com o título "aula 3 final FINAL"
  **When** um professor o troca por "Aula 3 — Injeção de dependência"
  **Then** a lista mostra o título novo, e o vídeo continua *pronto*

- **Given** um professor que apaga o título
  **When** tenta salvar
  **Then** a edição é recusada, porque o título é obrigatório

**Prioridade**: Should Have

**Rastreabilidade**: Mídia RN-M04

---

### RF-08: Consulta de vídeo para vínculo

**Descrição**: A plataforma responde, para um identificador de vídeo, se ele **existe, pertence à
escola informada e está pronto**, junto com o título e a duração. É a consulta que Conteúdo e
Currículo faz em `CAP-005` antes de vincular um vídeo a uma aula (OD30). Também permite listar os
vídeos prontos da escola, para o seletor de vídeo da tela de aula. A consulta não revela nada que
permita reproduzir o vídeo: nem endereço, nem chave, nem nome de arquivo guardado.

**Critérios de Aceitação**:

- **Given** um vídeo *pronto* da escola X
  **When** a consulta pergunta por ele em nome da escola X
  **Then** responde que existe, está pronto, com título e duração

- **Given** um vídeo *em preparação*
  **When** a consulta pergunta por ele
  **Then** responde que existe e não está pronto, com o estado atual

- **Given** um vídeo da escola X
  **When** a consulta pergunta por ele em nome da escola Y
  **Then** responde como se o vídeo não existisse

- **Given** qualquer resposta da consulta
  **When** ela é inspecionada
  **Then** não contém endereço de armazenamento, de distribuição, chave nem nome de arquivo do
  provedor

**Prioridade**: Must Have

**Rastreabilidade**: Mídia RN-M01, RN-M02, RN-M05; OD30

---

### RF-09: Comunicar pronto e falhou

**Descrição**: Quando um vídeo fica pronto ou falha, a plataforma publica o fato
(`midia.ativo-pronto`, `midia.preparacao-falhou`) com o identificador do vídeo e a escola, para que
`CAP-005` possa reagir sem perguntar repetidamente. O fato não carrega título, nome do arquivo nem
dado do autor além do identificador opaco.

**Critérios de Aceitação**:

- **Given** um vídeo que passa a *pronto*
  **When** a mudança é gravada
  **Then** `midia.ativo-pronto` é publicado uma única vez, mesmo que a publicação precise ser repetida

- **Given** um vídeo que passa a *falhou*
  **When** a mudança é gravada
  **Then** `midia.preparacao-falhou` é publicado com a categoria do motivo

**Prioridade**: Should Have

**Rastreabilidade**: Mídia §7 (Eventos); G06

---

### RF-10: Nada público

**Descrição**: Nenhum arquivo enviado, nenhuma qualidade gerada e nenhuma chave pode ser obtida por
endereço direto, nem por quem conhece o endereço (RN-M01). Nesta entrega não existe caminho legítimo
de leitura do conteúdo, que só chega com `CAP-007`; portanto, **toda** tentativa de obtê-lo falha. A
chave de cada vídeo fica guardada pela plataforma e não sai dela nesta entrega (RN-M08).

**Critérios de Aceitação**:

- **Given** o endereço de armazenamento de uma qualidade de um vídeo pronto, ou de um original
  ainda em preparação
  **When** alguém tenta obtê-lo sem acesso assinado
  **Then** a tentativa é negada

- **Given** o endereço de distribuição de um segmento
  **When** alguém tenta obtê-lo sem acesso assinado
  **Then** a tentativa é negada

- **Given** qualquer resposta, mensagem publicada, log ou métrica desta entrega
  **When** é inspecionada
  **Then** não contém a chave de cifra de nenhum vídeo

**Prioridade**: Must Have

**Rastreabilidade**: Mídia RN-M01, RN-M07, RN-M08; G21

---

### RF-11: Sinal de volume guardado

**Descrição**: A plataforma mede, por escola, o **volume guardado** (Versões de Reprodução, mais os
originais ainda não preparados) e o **número de vídeos por estado**. É a parte de RN-M17 que existe antes de haver aluno
assistindo: o custo por aluno ativo passa a ser composto quando `CAP-007` trouxer a reprodução.

**Critérios de Aceitação**:

- **Given** vídeos prontos na escola
  **When** o sinal é consultado
  **Then** mostra o volume guardado da escola, e a dimensão é a escola, nunca o vídeo nem o autor

**Prioridade**: Must Have

**Rastreabilidade**: Mídia RN-M17; R10

---

## Experiência do Usuário

**Persona.** Professor, em ciclos de produção de curso: grava várias aulas e envia em lote. Precisa
confiar que o envio não se perde e saber, sem perguntar a ninguém, se o vídeo ficou pronto.

**Fluxo de envio.** Área de vídeos → *Enviar vídeo* → escolhe o arquivo (a validação de formato e
tamanho é imediata) → confere o título → *Enviar* → barra de progresso com o volume transferido e o
restante. Pode sair da tela durante a **preparação**; durante a **transferência**, sair interrompe
o envio, e a tela avisa isso antes.

**Retomada.** Ao voltar à área com um envio interrompido, o professor vê *"envio incompleto de
aula-3.mp4 — selecione o mesmo arquivo para continuar"* e até quando pode fazer isso.

**Lista.** Uma linha por vídeo: título, estado (com cor e texto, nunca só cor), autor, data e, se
pronto, a duração. Um vídeo que falhou mostra o motivo na própria linha.

**Linguagem.** Nenhuma tela promete que o vídeo está "protegido contra cópia" (RN-M15). Quando for
necessário falar de proteção, o texto diz que o vídeo **não fica público** e que é entregue só a quem
tem acesso.

**Design.** A tela segue o fluxo já usado no backoffice: wireframe ASCII → Figma → aprovação →
código, sobre o design system do backoffice de `CAP-002`.

**Acessibilidade.** O progresso é anunciado a leitor de tela em intervalos, não a cada byte; a mudança
de estado na lista é anunciada; tudo é navegável por teclado.

## Decisões de Produto

| ID | Decisão confirmada | Alternativas descartadas e motivo | Impacto no PRD | Registro |
|---|---|---|---|---|
| DP-01 | **Três qualidades, 1080p, 720p e 480p**, escolhidas pelo player conforme a rede; sem ampliar original menor | Só 1080p: trava no 4G e gasta mais banda por aluno. 1080p + 720p: sem saída para rede fraca. A escada custa ~1,75× em armazenamento e ~3× em preparação, mas reduz a banda de CDN, que é o custo que cresce (R10) | RF-04 | QM-01 do domain doc |
| DP-02 | **MP4, MOV e MKV; até 5 GB e 3 horas; envio retomável por 24 horas** | 2 GB e 1 h: recusa live-coding longo. 10 GB sem limite: arquivo gigante disputa CPU com a entrega (RF-M06). Sem retomada: perder um upload de 4 GB é o pior momento para o professor | RF-02, RF-03, RF-04 | QM-02 do domain doc |
| DP-03 | **Todo ator com `midia.enviar` vê e titula todos os vídeos da escola**, com o autor indicado | Só quem enviou: coautoria impossível, e o vídeo de professor que saiu fica invisível até existir reatribuição | RF-06, RF-07 | QM-03 do domain doc; RN-M04 |
| DP-04 | **O estado é visto só na lista, que se atualiza com a tela aberta**; sem e-mail | E-mail de pronto/falhou: exige modelo novo em Notificação e consentimento a tratar, por um ganho que a lista resolve no MVP | RF-06 | — |
| DP-05 | **`midia.enviar` é do professor, não do administrador** | Administrador com envio: contraria DP-03 de `CAP-002` (quem governa acesso não herda áreas) | RF-01 | DP-03 de `CAP-002` |
| DP-06 | **O original é descartado ao chegar a *pronto* ou *falhou*.** Preparar de novo exige reenvio | Manter o original: permitiria preparar de novo sem o professor, ao custo de ~2× o armazenamento necessário para entregar | RF-04, RF-10, RF-11 | OD32 (era QP-01) |

---

## Restrições Técnicas de Alto Nível

- Borda e serviço autorizam duas vezes: a borda (pode estar na área de vídeos?) e o serviço de mídia
  (pode enviar?). O navegador não fala com o serviço nem com o provedor pelo nome.
- Armazenamento e distribuição ficam atrás de camada anticorrupção; nenhum conceito do provedor
  aparece em contrato ou em outro domínio.
- A preparação roda com ffmpeg dentro do serviço de mídia (OD28) e não pode degradar o caminho de
  entrega que `CAP-007` vai acrescentar (RF-M06 do domain doc).
- Toda escrita externa aceita chave de idempotência (G09); a publicação de fatos é por outbox (G06).
- `tenant_id` em vídeo, envio e fato publicado; nenhuma listagem cruza escola.
- Nenhum dado pessoal (nome ou e-mail do autor) em log, span, métrica, URL, nome de objeto ou
  mensagem; o autor é referido pelo identificador opaco.

---

## Não-Objetivos (Fora de Escopo)

- Reproduzir o vídeo, para aluno ou para professor.
- Vincular vídeo a aula; qualquer noção de curso, módulo ou aula dentro de Mídia (RN-M02).
- Material complementar (PDF, código-fonte).
- Excluir, substituir ou arquivar vídeo; cancelar uma preparação em andamento.
- Aviso por e-mail ou por qualquer canal além da lista.
- Legenda, miniatura, capítulos, corte ou qualquer edição.
- Registro na trilha de auditoria: enviar vídeo não é ato administrativo com efeito sobre aluno ou
  dinheiro.
- Permissões mais finas que `midia.enviar` (por exemplo, "só ver" e "enviar").

---

## Plano de Rollout Faseado

### MVP (Fase 1) — este PRD

- **Funcionalidades incluídas:** RF-01 a RF-11.
- **Critério para seguir:** um professor envia um vídeo 1080p de 40 minutos, interrompe a
  transferência e a retoma. O vídeo chega a *pronto* com as três qualidades, visível a outro
  professor e respondido como pronto pela consulta de vínculo. Nenhum arquivo, segmento ou chave é
  obtido por endereço direto, e um ator sem `midia.enviar` não vê a área.

### Depois deste PRD

- **`CAP-005`** — vincula vídeo pronto à aula (usa RF-08 e RF-09) e informa a Referência de Uso ao
  publicar; decide o ciclo de Material (QM-05).
- **`CAP-007`** — reprodução, acesso de vida curta, chave ao player, marca d'água, pré-visualização
  pelo professor; completa o custo por aluno ativo (RN-M17).
- **Depois do MVP** — excluir e substituir vídeo (QM-04).

---

## Métricas de Sucesso

| Métrica | Definição | Alvo | Prazo |
|---|---|---|---|
| Objeto exposto | Arquivos, segmentos ou chaves obtidos sem acesso assinado, em teste e em produção | Zero | Contínuo |
| Vídeo preso | Vídeos em *recebido* ou *em preparação* há mais de 4× a própria duração | Zero sem alerta | Contínuo |
| Envio concluído | Envios que chegam a *pronto* sobre envios de arquivos válidos (formato e duração aceitos) | ≥ 95% | Primeiros 90 dias |
| Tempo até pronto | Tempo entre *recebido* e *pronto*, relativo à duração do vídeo | Observado, sem alvo no MVP — define a capacidade de preparação a revisar | Primeiros 90 dias |

---

## Riscos e Mitigações

- **Professor grava em formato ou codec exótico** (tela do celular, codec raro dentro de um MOV) —
  Mitigação: formatos aceitos anunciados antes do envio; falha com motivo claro (RF-05); a taxa de
  falha por motivo é acompanhada em "Envio concluído".
- **Produção em lote congestiona a preparação** (20 aulas enviadas numa tarde) — Mitigação: fila sem
  recusa (RF-04); o tempo até pronto é observado para dimensionar a capacidade, e o professor vê o
  estado na lista.
- **Proteção ser apresentada como garantia na comunicação do produto** (R8) — Mitigação: linguagem de
  RN-M15 na tela; nenhuma tela menciona proteção contra cópia.

---

## Alternativas Consideradas

### Abordagem Escolhida: biblioteca de vídeos da escola, independente da aula

- **Descrição:** o professor envia vídeos para uma biblioteca da escola; a aula, em `CAP-005`,
  escolhe um vídeo pronto dessa biblioteca.
- **Por que foi escolhida:** mantém Mídia cega a curso (DE03, OD30), permite validar `CAP-006` antes
  de `CAP-005` existir, e deixa o vídeo sobreviver à reorganização do currículo e à saída do autor.

### Alternativa Rejeitada 1: envio dentro da tela de aula

- **Descrição:** o professor envia o vídeo já na aula; Conteúdo pede a Mídia a preparação.
- **Trade-offs:** um passo a menos para o professor; em troca, `CAP-006` só seria testável depois de
  `CAP-005`, e Mídia passaria a receber o contexto da aula.
- **Por que foi rejeitada:** inverte a ordem do backlog (passo 3 antes do 4) e aproxima Mídia de
  conhecer curso (RF-M01 do domain doc). Nada impede `CAP-005` de oferecer um atalho "enviar e
  vincular" usando esta mesma biblioteca.

### Alternativa Rejeitada 2: transcodificação gerenciada do provedor

- **Descrição:** preparar o vídeo com um serviço gerenciado de transcodificação da AWS.
- **Por que foi rejeitada:** OD28. Custo por minuto e mais uma dependência externa, quando o volume do
  MVP cabe no compute já provisionado.

---

## Questões em Aberto

- **QP-02 — Nome e posição da área de vídeos no backoffice.** A área de autoria de `CAP-002`
  (`autoria.ler`) já existe como prova de permissão. Os vídeos podem ficar dentro dela ou ser uma área
  própria. Responder: design, no wireframe desta entrega. Impacto: só navegação, não bloqueia a
  TechSpec.
