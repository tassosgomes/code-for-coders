---
tsg_artifact: prd
product: code-4-coders
capability: CAP-032
version: 1.1
status: approved
updated: 2026-10-12
sources: backlog/capabilities.md@1.4, vision.md@1.2, context/domain-map.md@1.2, context/architecture-baseline.md@1.2, domains/entrega-de-midia-e-protecao/domain.md@1.1, domains/conteudo-e-curriculo/domain.md@1.1, domains/matricula-e-direito-de-acesso/domain.md@1.0
---

# Transcrição e legenda de aula: achar o ponto da aula sem assistir a aula toda

## Visão Geral

Hoje o aluno só tem o vídeo. Para rever "onde o professor explicou o `docker compose`" ele precisa
arrastar a barra de progresso e adivinhar, e quem prefere ou precisa de legenda não tem nenhuma.
Em curso gravado, de aulas longas, isso é atrito diário de quem volta ao conteúdo.

Esta entrega faz a plataforma **transcrever a fala de cada vídeo pronto** e usar o texto de duas
formas, para o aluno que tem direito à aula:

1. **Legenda no player**, que o aluno liga e desliga.
2. **Transcrição completa na página da aula**, em texto, que o aluno **pesquisa** para achar um
   ponto sem assistir à aula toda; **clicar no trecho leva o vídeo àquele tempo**.

Para o professor, a transcrição acontece sozinha quando o vídeo fica pronto. Na tela **Vídeos** ele
vê o estado dela e pode **transcrever** um vídeo que ainda não tem transcrição ou **refazer** a que
existe.

Quem usa: o **aluno** com direito ao curso (legenda e transcrição) e o ator interno com a permissão
`midia.enviar`, o professor (estado, transcrever e refazer). O valor é o aluno reencontrar o que
procura em segundos e poder assistir com legenda, sem que o professor tenha trabalho extra.

## Rastreabilidade

### Capacidade e fronteiras

- **Capacidade:** `CAP-032` — Transcrição e legenda de aula. A capacidade nasce de decisão do
  negócio em 2026-10-12; não consta da visão e foi acrescentada ao backlog como adendo à v1.4.
  **Pós-MVP** (decidido em 2026-10-12), posicionada na Fase 3, lote L12 "Aprofundar".
- **Escopo desta entrega:** **a capacidade inteira, em um corte**: transcrever vídeo pronto
  (automático, sob demanda e refeito), guardar o texto, mostrá-lo ao professor como estado,
  entregar legenda e transcrição pesquisável ao aluno autorizado. Não há "fatia mínima" menor
  que isto: legenda sem transcrição, ou transcrição sem disparo, não fecha o valor. Os cortes
  de menor risco estão em **Plano de Rollout**.
- **Fora desta entrega:** edição ou correção do texto pelo professor (decidido em 2026-10-12);
  busca em várias aulas ou no curso inteiro; outros idiomas, tradução e legenda enviada à mão;
  resumo, capítulos e qualquer recurso de IA sobre o texto; exportar ou baixar a transcrição;
  transcrição de Material; vídeos acima do limite do serviço de transcrição (limitação aceita em 2026-10-12). Voltam
  quando o negócio pedir, em PRD próprio de `CAP-032`.
- **Domínios atravessados:**
  - Entrega de Mídia e Proteção — `domains/entrega-de-midia-e-protecao/domain.md` (**v1.1,
    em revisão**; dono da Transcrição, da Legenda e do disparo).
  - Conteúdo e Currículo — `domains/conteudo-e-curriculo/domain.md` (a aula referencia o vídeo
    e a página da aula é onde a transcrição aparece; nada muda no vínculo nem na publicação).
  - Matrícula e Direito de Acesso — `domains/matricula-e-direito-de-acesso/domain.md` (continua
    decidindo quem pode acessar; a transcrição não cria decisão nova).
- **Junta entre os domínios:** a Transcrição pertence à Mídia (RN-M18). O aluno a obtém **pelo
  mesmo caminho da reprodução**: a página da aula informa vídeo e curso, Mídia confere a Referência
  de Uso, Matrícula decide **agora** (RN-M10, RN-M21). Conteúdo e Currículo não guarda texto.
  Herdada do domain doc de Mídia (junta da Transcrição); não é decidida aqui.
- **Dependências entre capacidades:** `CAP-006` (vídeo pronto), `CAP-007` (player, sessão e a
  mesma decisão de acesso), `CAP-002` (permissão `midia.enviar`). Dependência externa: o serviço
  de transcrição próprio, já publicado na rede privada, com duas pendências externas (ver
  Restrições): a liberação de destinos privados e a correção de um defeito do deploy atual.
- **Restrições do baseline:** falha fechada na decisão de acesso (BA07/G11); nenhum ativo público
  (G21); nada derivado por aluno (G22); sem dado do aluno em URL, log ou chave de cache (G23);
  isolamento por escola (G07); nenhuma peça apresenta a proteção como garantia (R8, BA15).

### Vision Doc

- **Objetivos de negócio atendidos:** `C04` (entrega de vídeo). Reduz o atrito de revisitar
  conteúdo gravado, que sustenta a conclusão e o retorno do aluno.
- **Restrições globais aplicáveis:** multi-escola (tenant) desde o início; conteúdo é o ativo
  central e continua sob controle próprio, sem passar a terceiro.
- **Non-Goals globais respeitados:** "não fará produção, edição ou transcodificação autoral de
  vídeo além do necessário para a entrega". A transcrição é texto derivado da fala: não altera,
  corta nem recompõe o vídeo.

### Domain Docs

- **Entidades envolvidas:** Mídia, Transcrição, Trecho, Legenda, Sessão de Reprodução, Referência
  de Uso (Entrega de Mídia e Proteção); Aula (Conteúdo e Currículo, só como ponto de exibição).
- **Regras de negócio referenciadas:** RN-M01, RN-M03, RN-M04, RN-M05, RN-M10, RN-M11, RN-M13,
  RN-M15, RN-M17 (Mídia); **RN-M18 a RN-M27, nascidas no domain doc v1.1 para esta capacidade**;
  RN-C04 (Conteúdo); RN-D01 (Matrícula).
- **Regras nascidas neste PRD:** `RN-TL01` a `RN-TL08`, abaixo. Devem ser absorvidas pelo domain
  doc de Mídia na próxima revisão, se o negócio as confirmar.
- **Eventos consumidos / produzidos:** nenhum evento novo para outros domínios (domain doc, §7).

#### Regras desta entrega

| ID | Regra | Origem |
|---|---|---|
| RN-TL01 | **Busca na transcrição ignora maiúsculas, acentos e pontuação.** "introducao" encontra "Introdução". A busca é por trecho de palavra (contém), na aula aberta, e nunca atravessa para outra aula | DP-06 |
| RN-TL02 | **Clicar num Trecho leva o vídeo ao início dele e continua (ou começa) a reprodução**, sem reabrir a aula. Só vale com sessão de reprodução ativa; sem ela, o clique primeiro abre a sessão pelo caminho normal (RN-M10) | DP-02 |
| RN-TL03 | **A transcrição da aula é a da Mídia vinculada na versão vigente do curso que o aluno vê.** Trocar o vídeo da aula (nova publicação) troca a transcrição junto; o aluno nunca vê texto de um vídeo e imagem de outro | RN-C07 · RN-C12 |
| RN-TL04 | **A preferência de legenda (ligada ou desligada) é do aluno, no dispositivo, e não é dado do domínio.** Padrão: desligada. Mudar de dispositivo não a leva junto | DP-05 |
| RN-TL05 | **Aula sem transcrição pronta mostra o estado em linguagem do aluno**, sem confundir "ainda não disponível" com "você não tem acesso". O player funciona normalmente, só sem o botão de legenda | RN-M19 · RN-R02 de `prd-reproducao-protegida` |
| RN-TL06 | **O professor vê, por vídeo, um de quatro estados:** sem transcrição, transcrevendo, pronta, falhou; e o motivo da falha em linguagem de quem produz o vídeo (por exemplo, "vídeo mais longo que o limite de 2 horas") | RN-M06 · RN-M18 · RN-M24 |
| RN-TL08 | **O professor lê a transcrição no backoffice, somente leitura.** Vale para vídeo da escola dele com transcrição pronta, com a permissão `midia.enviar`. Não abre sessão de reprodução, não passa por Matrícula, não edita | RN-M27 · DP-08 |
| RN-TL07 | **Refazer não tira a transcrição do ar.** Enquanto a nova roda, o aluno continua vendo a vigente; se a nova falha, a vigente permanece e o professor vê que a refeita falhou | RN-M18 |

## Termos Canônicos

| Termo | Definição de negócio | Escopo/Fonte |
|---|---|---|
| Transcrição | Texto da fala de um vídeo, em português do Brasil, dividido em trechos com início e fim. Um por vídeo, igual para todos | Domain doc de Mídia (RN-M18, RN-M22) |
| Trecho | Pedaço da transcrição com início, fim e texto; unidade da legenda, da busca e do clique | Domain doc de Mídia |
| Legenda | A transcrição aparecendo sobre o vídeo, no player, ligada pelo aluno | Domain doc de Mídia |
| Transcrever | Pedir a transcrição de um vídeo pronto que ainda não tem uma | Esta entrega (DP-03) |
| Refazer | Pedir nova transcrição de um vídeo que já tem uma pronta (ou que falhou) | Esta entrega (DP-03) |
| Serviço de transcrição | Sistema próprio, na rede privada da escola, que reconhece a fala. Não é um terceiro | DP-01 |

## Objetivos

- O aluno **acha um ponto da aula pesquisando uma palavra** e chega nele com um clique, sem
  assistir à aula inteira.
- O aluno que precisa ou prefere **assiste com legenda** em qualquer vídeo que tenha transcrição.
- O professor **não faz nada** para que isso aconteça com vídeo novo, e consegue **resolver**
  sozinho o vídeo que ficou sem transcrição ou com transcrição ruim.
- **Nenhum efeito colateral** na entrega do vídeo: nem no tempo de ficar pronto, nem na
  reprodução, nem na proteção do conteúdo.

## Histórias de Usuário

- Como **aluno**, eu quero ligar a legenda no player para acompanhar a aula lendo, em ambiente
  barulhento ou sem som.
- Como **aluno**, eu quero ver o texto completo da aula na própria página para revisar o conteúdo
  lendo, mais rápido que assistindo.
- Como **aluno**, eu quero pesquisar uma palavra na transcrição e ver todos os trechos em que ela
  aparece para chegar direto ao que procuro.
- Como **aluno**, eu quero clicar num trecho e o vídeo ir àquele ponto para não precisar adivinhar
  o minuto.
- Como **professor**, eu quero que a transcrição saia sozinha quando o vídeo fica pronto para não
  ter mais um passo ao publicar aula.
- Como **professor**, eu quero ver na tela Vídeos se a transcrição de cada vídeo está pronta,
  em andamento ou falhou, e por quê, para saber se posso confiar nela.
- Como **professor**, eu quero ler a transcrição de um vídeo na tela Vídeos, sem poder editá-la,
  para avaliar a qualidade antes de confiar nela e decidir se refaço.
- Como **professor**, eu quero transcrever um vídeo antigo, que ficou pronto antes desta entrega, e
  refazer uma transcrição que falhou ou saiu ruim.
- Como **dono da escola**, eu quero que o texto da aula só chegue a quem tem direito a ela para que
  a transcrição não abra uma porta lateral ao conteúdo pago.

## Funcionalidades Principais

### RF-01: Transcrição automática quando o vídeo fica pronto

**Descrição**: Assim que a preparação de um vídeo termina com sucesso (estado **pronto**), a
plataforma pede a transcrição dele sem ação de ninguém. O estado do vídeo continua sendo "pronto"
no mesmo instante de hoje; a transcrição corre depois e em paralelo (RN-M19). Não há transcrição
para vídeo que falhou.

**Critérios de Aceitação**:

- **Given** um professor envia um vídeo e a preparação termina com sucesso
  **When** o vídeo passa a **pronto**
  **Then** ele pode ser vinculado e reproduzido imediatamente, e a transcrição aparece como
  "transcrevendo" na tela Vídeos.

- **Given** a transcrição termina com sucesso
  **When** o professor abre a tela Vídeos
  **Then** o vídeo mostra "transcrição pronta" e a aula em que ele está vinculado passa a oferecer
  legenda e transcrição ao aluno.

- **Given** o serviço de transcrição está fora do ar ou lento quando o vídeo fica pronto
  **When** o professor ou o aluno usam o vídeo
  **Then** nada muda no vídeo: continua pronto, vinculável e reproduzível; a transcrição fica
  como "transcrevendo" e conclui quando o serviço voltar, ou "falhou" se esgotar as tentativas.

- **Given** o vídeo falhou na preparação
  **When** o professor olha o vídeo
  **Then** não existe transcrição nem ação de transcrever.

- **Given** o mesmo vídeo fica pronto e o aviso de pronto chega duas vezes
  **When** o sistema processa os dois
  **Then** existe uma só transcrição em andamento (RN-M20).

**Prioridade**: Must Have

**Rastreabilidade**: RN-M05, RN-M18, RN-M19, RN-M20, RN-M23, RN-M25

---

### RF-02: Estado da transcrição e ações na tela Vídeos

**Descrição**: Cada vídeo na tela **Vídeos** mostra o estado da transcrição (RN-TL06): sem
transcrição, transcrevendo, pronta ou falhou, com o motivo da falha em linguagem do professor.
Em vídeo **pronto sem transcrição** aparece a ação **Transcrever**; em vídeo com transcrição
pronta, ou que falhou, aparece **Refazer**. As ações exigem a permissão `midia.enviar`, valem
para vídeo de qualquer professor da escola e não existem para vídeo que não esteja pronto.

**Critérios de Aceitação**:

- **Given** um vídeo que ficou pronto antes desta entrega e nunca foi transcrito
  **When** o professor abre a tela Vídeos
  **Then** o vídeo mostra "sem transcrição" e a ação **Transcrever**.

- **Given** o professor aciona **Transcrever**
  **When** o pedido é aceito
  **Then** o vídeo passa a "transcrevendo" e a ação some até haver resultado.

- **Given** o professor aciona **Transcrever** duas vezes seguidas, ou dois professores acionam
  quase ao mesmo tempo
  **When** os pedidos chegam
  **Then** existe uma só transcrição em andamento e a tela mostra o estado real (RN-M20).

- **Given** um vídeo com transcrição pronta
  **When** o professor aciona **Refazer**
  **Then** o vídeo mostra "transcrevendo" mas a transcrição vigente continua disponível ao aluno
  até a nova ficar pronta (RN-TL07).

- **Given** a transcrição falhou porque o vídeo é mais longo que o limite do serviço
  **When** o professor olha o vídeo
  **Then** vê o motivo "vídeo mais longo que o limite de 2 horas" e **não** a ação de tentar de novo
  como se fosse resolver (RN-M24).

- **Given** a transcrição falhou por indisponibilidade do serviço
  **When** o professor olha o vídeo
  **Then** vê o motivo em linguagem simples e a ação **Refazer**.

- **Given** um ator sem `midia.enviar`
  **When** tenta transcrever ou refazer por qualquer caminho
  **Then** a ação é recusada, como acontece hoje com o título e as tags.

- **Given** um vídeo de outra escola
  **When** o professor tenta transcrever por qualquer caminho
  **Then** a resposta é a mesma de vídeo inexistente (G07).

- **Given** um vídeo em preparação, recebido ou que falhou na preparação
  **When** o professor olha a tela
  **Then** a ação de transcrever não é oferecida.

- **Given** a tela está aberta enquanto a transcrição corre
  **When** ela termina
  **Then** a tela atualiza o estado sozinha, como já faz com o estado da preparação.

- **Given** um vídeo com transcrição pronta
  **When** o professor aciona **Refazer** e a nova fica pronta
  **Then** o aluno passa a ver o texto novo na aula, sem republicar o curso (DP-09).

**Prioridade**: Must Have

**Rastreabilidade**: RN-M03, RN-M04, RN-M18, RN-M20, RN-M24, RN-TL06, RN-TL07

---

### RF-03: Legenda no player

**Descrição**: O player ganha um controle para ligar e desligar a legenda quando o vídeo tem
transcrição pronta. A legenda mostra, durante a reprodução, o texto do trecho do momento atual,
acompanha pausa, avanço, retrocesso e mudança de velocidade, e funciona em tela cheia. A preferência
do aluno (RN-TL04) vale nas próximas aulas, no mesmo dispositivo. A legenda nunca cobre os
controles nem a marca d'água do aluno, que continua visível e trocando de posição (DP-03 de
`prd-reproducao-protegida`).

**Critérios de Aceitação**:

- **Given** um vídeo com transcrição pronta
  **When** o aluno abre a aula
  **Then** o player mostra o controle de legenda, desligada por padrão.

- **Given** a legenda ligada
  **When** o vídeo toca, o aluno pausa, volta 30 segundos e retoma
  **Then** o texto exibido corresponde ao ponto atual do vídeo.

- **Given** a legenda ligada em tela cheia
  **When** a marca d'água troca de posição
  **Then** legenda e marca d'água não se sobrepõem e nenhuma cobre os controles.

- **Given** o aluno ligou a legenda numa aula
  **When** abre outra aula do mesmo curso no mesmo dispositivo
  **Then** a legenda já vem ligada.

- **Given** o aluno ligou a legenda no celular
  **When** abre a aula no computador
  **Then** a legenda vem desligada (RN-TL04).

- **Given** o vídeo não tem transcrição pronta
  **When** o aluno abre a aula
  **Then** o player toca normalmente e o controle de legenda não aparece (RN-TL05).

- **Given** a sessão de reprodução expira e é renovada com nova decisão de acesso
  **When** a renovação acontece
  **Then** a legenda continua ligada, sem piscar nem reiniciar.

- **Given** a decisão de acesso é negada ou indisponível na renovação
  **When** a reprodução para (RN-R02 de `prd-reproducao-protegida`)
  **Then** a legenda para junto e nenhum texto novo é entregue (RN-M21).

**Prioridade**: Must Have

**Rastreabilidade**: RN-M21, RN-M22, RN-TL04, RN-TL05, RN-R03 e RN-R04 de `prd-reproducao-protegida`

---

### RF-04: Transcrição completa na página da aula

**Descrição**: A página da aula mostra a transcrição inteira, em texto, como uma lista de trechos
em ordem, cada um com o seu tempo (mm:ss ou h:mm:ss). Enquanto o vídeo toca, o trecho atual fica
destacado e a lista o acompanha, a menos que o aluno tenha rolado manualmente (então para de
acompanhar até ele pedir). **Clicar num trecho leva o vídeo àquele ponto** (RN-TL02). O aluno
pode selecionar e copiar texto como em qualquer página (a plataforma não promete impedir cópia,
RN-M15). Aula sem transcrição pronta mostra o estado em linguagem do aluno (RN-TL05).

**Critérios de Aceitação**:

- **Given** a aula de um vídeo com transcrição pronta e o aluno com direito
  **When** a página abre
  **Then** o aluno vê a transcrição completa, em ordem, com o tempo de cada trecho.

- **Given** o vídeo tocando
  **When** o tempo avança
  **Then** o trecho do momento fica destacado e visível na lista.

- **Given** o aluno rolou a lista manualmente durante a reprodução
  **When** o vídeo continua
  **Then** a lista não volta sozinha ao trecho atual; um controle "ir para o trecho atual" a
  recoloca.

- **Given** o aluno clica num trecho
  **When** a sessão de reprodução está ativa
  **Then** o vídeo vai ao início do trecho e toca (RN-TL02).

- **Given** o aluno clica num trecho antes de ter iniciado a reprodução
  **When** o clique acontece
  **Then** a reprodução abre pelo caminho normal e começa naquele ponto.

- **Given** a transcrição ainda está em andamento
  **When** o aluno abre a aula
  **Then** vê "A transcrição desta aula ainda não está disponível", sem mensagem de erro nem de
  falta de acesso, e o vídeo toca normalmente.

- **Given** a transcrição falhou
  **When** o aluno abre a aula
  **Then** vê a mesma mensagem de "ainda não disponível" (o aluno não precisa saber do motivo
  técnico) e o vídeo toca normalmente.

- **Given** uma aula usa um vídeo e, na publicação seguinte, passa a usar outro
  **When** o aluno abre a aula na versão nova
  **Then** vê a transcrição do vídeo novo (RN-TL03).

**Prioridade**: Must Have

**Rastreabilidade**: RN-M21, RN-M22, RN-TL02, RN-TL03, RN-TL05

---

### RF-05: Pesquisar na transcrição

**Descrição**: Acima da transcrição há um campo de busca. Ao digitar, a lista mostra só os trechos
que contêm o texto (RN-TL01), com o trecho achado em destaque, a contagem de ocorrências e
controles para ir à anterior e à próxima. Clicar numa ocorrência leva o vídeo ao ponto (RN-TL02).
Limpar a busca restaura a lista inteira. A busca é sobre a aula aberta, não sobre o curso.

**Critérios de Aceitação**:

- **Given** a transcrição tem "Introdução ao Docker" aos 02:10 e "docker compose" aos 14:30
  **When** o aluno digita "docker"
  **Then** vê os dois trechos, com "docker" destacado, e a contagem "2 resultados".

- **Given** o aluno digita "introducao" sem acento
  **When** a busca roda
  **Then** o trecho com "Introdução" aparece (RN-TL01).

- **Given** nenhum trecho contém o texto digitado
  **When** a busca roda
  **Then** a tela diz "Nenhum trecho encontrado para essa busca" e oferece limpar a busca.

- **Given** vários resultados
  **When** o aluno usa "próxima ocorrência" repetidamente
  **Then** a lista percorre as ocorrências em ordem do vídeo e volta à primeira depois da última.

- **Given** o aluno clica num resultado
  **When** o clique acontece
  **Then** o vídeo vai ao início do trecho e a busca permanece aplicada.

- **Given** o texto digitado tem só espaços
  **When** a busca roda
  **Then** é tratada como busca vazia: a lista inteira é mostrada.

- **Given** o aluno digita em ritmo normal
  **When** cada tecla é digitada
  **Then** a lista responde sem atraso perceptível, mesmo numa aula de 2 horas.

**Prioridade**: Must Have

**Rastreabilidade**: RN-TL01, RN-TL02

---

### RF-06: Acesso protegido como a reprodução

**Descrição**: Legenda e transcrição são entregues **só a quem hoje pode assistir à aula**, pela
mesma verificação da reprodução: o vídeo pertence ao conteúdo alegado (Referência de Uso) e
Matrícula responde que o aluno pode acessar o curso **agora**; sem resposta, nada é entregue
(falha fechada). O texto não tem endereço público nem estável e não é guardado em cache
compartilhado. Quem perde o direito deixa de receber, no máximo no prazo da renovação da sessão.

**Critérios de Aceitação**:

- **Given** um visitante sem sessão, ou um aluno sem direito ao curso
  **When** tenta obter legenda ou transcrição de uma aula por qualquer caminho
  **Then** não recebe nenhum texto: o visitante sem sessão recebe a recusa de autenticação, e o
  aluno sem direito recebe a mesma recusa de acesso da aula e da reprodução (403 `ACCESS_DENIED`).
  Quem não tem direito à aula não tem a transcrição. Aula que não existe, de outra escola ou fora da
  versão vigente continua respondendo como recurso inexistente (RN-R07 de `prd-reproducao-protegida`).

- **Given** um aluno com direito ao curso A
  **When** informa o identificador de um vídeo do curso B com o conteúdo de A
  **Then** nada é entregue (Referência de Uso, RN-M10 passo 1).

- **Given** Matrícula não responde
  **When** a página da aula tenta carregar a transcrição
  **Then** nenhum texto é entregue e a mensagem é a de indisponibilidade, nunca a de "sem acesso"
  (RN-R02 de `prd-reproducao-protegida`).

- **Given** a vigência do aluno termina
  **When** a próxima renovação da sessão acontece
  **Then** a legenda para e a transcrição deixa de ser atualizada ou carregada.

- **Given** um link da transcrição copiado e aberto depois, por outra pessoa ou fora da sessão
  **When** é acessado
  **Then** não devolve texto.

- **Given** um aluno de outra escola
  **When** pede a transcrição de uma aula desta escola
  **Then** a resposta é a de recurso inexistente (G07).

- **Given** a transcrição entregue
  **When** se examina o conteúdo, log e URL
  **Then** não há e-mail ou dado pessoal do aluno nela (RN-M22, G23).

- **Given** qualquer texto da interface sobre a transcrição
  **When** ele é lido
  **Then** não afirma que o conteúdo é protegido contra cópia (RN-M15).

**Prioridade**: Must Have

**Rastreabilidade**: RN-M01, RN-M10, RN-M11, RN-M13, RN-M15, RN-M21, RN-M22, BA07, G11, G21, G23

---

### RF-07: Confiabilidade: resultado guardado, falha isolada e sinais

**Descrição**: A plataforma guarda o texto pronto por conta própria, sem depender de quanto tempo
o serviço de transcrição o retém (RN-M23); qualquer material intermediário é apagado ao fim, com
sucesso ou falha. Pedido que fica sem resposta é reconciliado e não some em silêncio: termina como
pronto ou como falhou (RN-M18). A operação enxerga os sinais da transcrição (RN-M26).

**Critérios de Aceitação**:

- **Given** a transcrição termina
  **When** passam-se mais de 24 horas
  **Then** ela continua disponível ao aluno e ao professor, sem nova chamada ao serviço de
  transcrição.

- **Given** o serviço de transcrição terminou, mas o aviso de término se perdeu
  **When** passa o tempo de reconciliação
  **Then** a plataforma descobre sozinha o resultado e o vídeo sai de "transcrevendo".

- **Given** a transcrição terminou com sucesso ou falhou
  **When** se inspeciona o armazenamento de trabalho
  **Then** não resta material intermediário da transcrição.

- **Given** a operação consulta os sinais
  **When** abre o painel de observabilidade da mídia
  **Then** vê tempo entre pronto e transcrição pronta, taxa de falha por motivo, duração
  transcrita e fila em espera, sem texto de aula nem dado pessoal (RN-M26).

- **Given** uma tentativa de transcrição que nunca termina
  **When** passa o limite de espera definido na TechSpec
  **Then** o vídeo termina em "falhou", com motivo, e pode ser refeito.

**Prioridade**: Must Have

**Rastreabilidade**: RN-M17, RN-M18, RN-M23, RN-M26

---

### RF-08: Ler a transcrição no backoffice (somente leitura)

**Descrição**: Na tela **Vídeos**, o vídeo com transcrição pronta permite ao professor **abrir e ler
o texto completo**, com o tempo de cada trecho, para avaliar a qualidade. É somente leitura: não há
edição, correção nem exclusão de trecho (DP-07). A leitura não é reprodução: não abre sessão, não
consulta Matrícula e não mostra marca d'água (RN-TL08). Também não tem busca nem clique que leve a
um vídeo; é o texto, só.

**Critérios de Aceitação**:

- **Given** um vídeo com transcrição pronta
  **When** o professor abre a transcrição na tela Vídeos
  **Then** vê o texto completo em ordem, com o tempo de cada trecho, sem nenhum controle de edição.

- **Given** um vídeo sem transcrição pronta (sem transcrição, transcrevendo ou falhou)
  **When** o professor olha o vídeo
  **Then** a leitura não é oferecida; vê o estado (RF-02).

- **Given** um vídeo com transcrição vigente e uma refeita em andamento
  **When** o professor abre a transcrição
  **Then** lê a vigente, e a tela diz que há uma nova em andamento (RN-TL07).

- **Given** um ator sem `midia.enviar`
  **When** tenta ler a transcrição por qualquer caminho
  **Then** a leitura é recusada.

- **Given** um vídeo de outra escola
  **When** o professor tenta ler a transcrição por qualquer caminho
  **Then** a resposta é a mesma de vídeo inexistente (G07).

- **Given** a transcrição lida no backoffice
  **When** se examina log e URL
  **Then** não há texto de aula nem dado pessoal em log ou URL (RN-M26).

**Prioridade**: Must Have

**Rastreabilidade**: RN-M03, RN-M04, RN-M27, RN-TL08, G07

---

## Experiência do Usuário

**Aluno.** A legenda e a transcrição moram onde o aluno já está: o player e a página da aula,
sem tela nova. O controle de legenda vive na barra do player junto de velocidade; a transcrição
é um painel da página da aula (ao lado do vídeo em tela larga, abaixo dele em tela estreita), com
o campo de busca no topo. O aluno nunca vê palavras como "Whisper", "serviço", "fila" ou "áudio".
Mensagens de ausência de transcrição são tranquilas e distintas de falta de acesso (RN-TL05).

**Professor.** Na tela Vídeos, o estado da transcrição é uma informação a mais por vídeo, junto
do estado da preparação e das tags, com as ações Transcrever e Refazer e a leitura do texto (RF-08). O professor não precisa
agir para vídeo novo.

**Acessibilidade.** A legenda tem contraste suficiente sobre qualquer imagem; a lista de trechos é
navegável por teclado, com foco visível, e cada trecho é acionável por Enter; o campo de busca tem
rótulo; o destaque do trecho atual e dos resultados não depende só de cor; a lista rola sem
roubar o foco; funciona em celular.

**Dependência de design.** As telas envolvidas — **tela Vídeos do admin** e **página da aula do
aluno** (player com legenda, painel de transcrição e busca) — seguem a preferência do projeto:
**wireframe ASCII e Figma aprovados vêm antes de qualquer código**. Esta entrega não gera
tela; a TechSpec e as tasks só podem abrir o plano de implementação das telas depois da
aprovação de ASCII e Figma, e cada plano abre com a tela correspondente.

## Decisões de Produto

> DP-01 a DP-09 foram decididas pelo responsável em 2026-10-12. O PRD em si ainda aguarda a
> aprovação dele.

| ID | Decisão confirmada | Alternativas descartadas e motivo | Impacto no PRD | Registro |
|---|---|---|---|---|
| DP-01 | **Transcrição por serviço próprio**, publicado na mesma rede privada, em português do Brasil. O conteúdo não sai para terceiro | Serviço de transcrição de nuvem de terceiro: custo por minuto e o ativo central do negócio (a fala dos vídeos) sairia do controle próprio, contra o motivo do projeto | Restrições; RN-M25 | Decidido 2026-10-12 |
| DP-02 | **Duas finalidades:** (1) legenda no player; (2) transcrição inteira na página da aula, pesquisável, com clique que leva o vídeo ao tempo | Só legenda: não resolve "achar um ponto sem assistir". Só transcrição: perde quem quer assistir lendo | RF-03, RF-04, RF-05 | Decidido 2026-10-12 |
| DP-03 | **Disparo:** automático quando o vídeo fica pronto, mais ação do professor na tela Vídeos para **transcrever** vídeo sem transcrição e **refazer** a existente | Só automático: vídeos anteriores à entrega e falhas ficariam sem saída. Só manual: o professor teria mais um passo por vídeo | RF-01, RF-02 | Decidido 2026-10-12 |
| DP-04 | **Transcrição e legenda são conteúdo protegido:** só quem tem direito à aula agora as recebe, pela mesma regra da reprodução | Entregar para qualquer aluno logado, ou deixar a transcrição pública para ajudar o SEO: abriria uma porta lateral ao conteúdo pago | RF-06 | Decidido 2026-10-12 |
| DP-05 | **Legenda vem desligada por padrão** e a preferência é do aluno, no dispositivo | Ligada por padrão: polui a imagem de quem não quer. Preferência na conta: dado novo em Identidade/Aprendizagem para um ganho pequeno | RF-03, RN-TL04 | Decidido 2026-10-12 |
| DP-06 | **A busca é só na aula aberta**; busca no curso inteiro fica fora | Busca no curso: exigiria índice por curso e nova junta com Conteúdo; o valor declarado é achar o ponto *na aula* | RF-05, RN-TL01 | Decidido 2026-10-12 |
| DP-07 | **A transcrição sai como o serviço a produziu**; o professor não edita o texto neste corte | Editor de texto: mais uma tela, versão do texto e regra de quem pode editar | Fora desta entrega | Decidido 2026-10-12 |
| DP-08 | **O professor lê a transcrição no backoffice, somente leitura**, sem passar pela decisão de Matrícula | Sem leitura: sem edição, o professor não teria como avaliar a qualidade. Leitura com o player do aluno: abriria um segundo caminho de autorização (DP-07 de `prd-reproducao-protegida`) | RF-08, RN-TL08 | Decidido 2026-10-12 |
| DP-09 | **Refazer troca o texto para o aluno ao ficar pronto, sem republicar o curso**: a transcrição é da Mídia, não da versão do curso | Amarrar à publicação: o professor precisaria republicar o curso inteiro para corrigir um texto | RF-02, RN-TL07 | Decidido 2026-10-12 |
| DP-10 | **Vídeo acima de 2 horas falha com motivo claro ao professor**, sem dividir o vídeo. Limitação aceita | Dividir o vídeo em partes: complexidade de áudio, de tempos e de junção para pouco ganho | RF-02, RN-M24 | Decidido 2026-10-12 |

## Restrições Técnicas de Alto Nível

- **Serviço de transcrição próprio**, de repositório separado, já publicado na rede privada.
  Aceita áudio e vídeo (`.mp4`, `.mkv`, `.webm`, `.mp3`, `.wav`, `.m4a`) até 5 GiB e 2 horas, em
  português do Brasil, devolve texto em trechos com início e fim, avisa o término por webhook
  assinado e retém o resultado por 24 horas depois de terminar. Seu contrato é do repositório dele;
  este PRD só o respeita como limite de produto.
- **Dependências externas (repositório do serviço de transcrição), entregues em 2026-10-12:**
  - **Destinos privados.** Para aceitar o armazenamento de objetos e o receptor de aviso da mídia
    na rede privada, o serviço ganhou uma lista de destinos privados confiáveis (variáveis
    `TRUSTED_PRIVATE_SOURCES` e `TRUSTED_PRIVATE_WEBHOOKS`; ADR-003 dele). Implementada no PR
    `tassosgomes/whisper-api#2`, **mergeado e implantado em 2026-10-12**. As variáveis ainda não
    estão configuradas no deploy: sem elas a entrega não valida fim a fim com origem e receptor
    privados.
  - **Defeito do deploy atual.** O deploy anterior do serviço tinha um defeito de PyAV que fazia todo
    trabalho falhar; a correção, `tassosgomes/whisper-api#3`, foi **mergeada e implantada em
    2026-10-12**, e um teste ponta a ponta contra o serviço implantado concluiu com sucesso. Era
    pré-requisito do piloto (métricas de cobertura e de falha só valem depois dela).
- **O arquivo original do vídeo não está disponível depois da preparação** (a preparação o
  apaga ao terminar). A transcrição não pode depender dele (RN-M24). Direção proposta para a
  TechSpec, a validar: o worker da mídia, que já guarda a chave da cifra (ADR-0006), extrai o áudio
  da própria Versão de Reprodução, em arquivo leve, mono, em prefixo privado, e entrega ao serviço
  por URL assinada de leitura; o texto é copiado para guarda própria antes das 24 h; conciliação por
  consulta além do aviso; o áudio é apagado ao fim. Um só caminho serve ao automático, ao
  reprocessamento e aos vídeos antigos.
- **Dados:** a fala do professor é o único conteúdo transcrito; a transcrição não contém dado do
  aluno. Nenhum texto de aula em log, métrica ou rótulo de observabilidade.
- **Sem dados legados até o MVP:** não há carga em massa de vídeos antigos. A ação **Transcrever**
  é o caminho para vídeo existente; qualquer carga em lote é decisão separada.

## Não-Objetivos (Fora de Escopo)

- Editar, corrigir ou revisar o texto da transcrição (DP-07). A leitura pelo professor, sem edição, entra (RF-08).
- Buscar em várias aulas, no módulo ou no curso inteiro (DP-06).
- Outros idiomas, tradução, escolha de idioma e legenda enviada pelo professor (`.srt`, `.vtt`).
- Baixar ou exportar a transcrição ou a legenda; botão de copiar tudo.
- Resumo, capítulos, palavras-chave, perguntas e qualquer recurso de IA sobre o texto.
- Identificar quem fala; pontuação ou parágrafos além do que o serviço entrega.
- Transcrever Material (PDF, código) ou áudio sem vídeo.
- Notas do aluno vinculadas ao minuto (`CAP-019`) e dúvida sobre o trecho (`CAP-023`).
- Pré-visualização da aula pelo professor com o player do aluno (DP-07 de `prd-reproducao-protegida`).
- Carga em massa de todos os vídeos já existentes.
- Fazer da transcrição um critério de conclusão da aula ou de progresso.
- Mudar a proteção do vídeo: marca d'água, sessão, chave e entrega seguem como estão.

## Plano de Rollout Faseado

### Fase única, com uma ordem de menor risco para entrega

- **Funcionalidades incluídas:** RF-01 a RF-08.
- **Ordem de menor risco sugerida (se o negócio quiser dividir em dois PRDs):**
  1. RF-01, RF-02, RF-07, RF-08 (professor e pipeline), sem efeito visível ao aluno; permite validar
     qualidade, custo e tempo do serviço com vídeos reais antes de expor ao aluno.
  2. RF-03 a RF-06 (aluno).
  A divisão é opção do responsável; o corte único é o recomendado, porque a entrega só tem valor
  completo com as duas partes.
- **Critério de sucesso:** ver Métricas.
- **Fase de produto:** **pós-MVP**, decidido em 2026-10-12. O backlog a posiciona na Fase 3, lote L12 "Aprofundar", ao lado de `CAP-019`; a Fase 2 (receita recorrente) não tem relação com ela.

## Métricas de Sucesso

Alvos de adoção são hipóteses a calibrar com dados da primeira escola; o que é medido é definido
já (todo sinal é agregado e sem texto, RN-M26).

| Métrica | Definição | Alvo | Prazo |
|---|---|---|---|
| Cobertura | % de vídeos prontos (até 2 h) com transcrição pronta | ≥ 95% | 7 dias depois do vídeo ficar pronto |
| Tempo até pronta | Do vídeo ficar pronto até a transcrição ficar pronta (p90) | a definir após piloto: medir com vídeos reais antes de fixar | Piloto de 30 dias |
| Falha de transcrição | % de pedidos que terminam em "falhou" por causa que não seja o limite de duração | ≤ 3% | 30 dias |
| Uso da busca | % de aulas abertas, de vídeo com transcrição, em que o aluno usa a busca ou clica num trecho | linha de base a medir; alvo a definir | 30 dias |
| Uso da legenda | % de reproduções com a legenda ligada | linha de base a medir; alvo a definir | 30 dias |
| Efeito colateral | Variação do tempo de "pronto" do vídeo e da taxa de falha de preparação depois da entrega | nenhuma piora | 30 dias |

## Riscos e Mitigações

- **A transcrição entrega o conteúdo pago em forma mais fácil de copiar que o vídeo.** O texto
  completo, pesquisável e selecionável vira o curso inteiro em poucos cliques, sem marca d'água.
  É consequência direta da finalidade escolhida (DP-02). **Risco aceito pelo produto em
  2026-10-12, em favor da feature.** — Mitigação parcial: acesso exatamente como o do
  vídeo (RF-06), sem download nem exportação, texto sem endereço estável, e a mensagem de que a
  proteção é dissuasão (R8). A aceitação está registrada acima; não há mitigação que o elimine sem perder a finalidade.
- **Qualidade ruim do texto** para termos técnicos, siglas e inglês no meio da fala (comum em
  cursos de tecnologia), com a transcrição exibida sem revisão (DP-07). — Mitigação: o professor vê
  o estado, lê o texto (RF-08) e pode refazer; a métrica de uso mostra se a falta de edição pesa.
- **Capacidade do serviço de transcrição**: ele roda em CPU; um lote de vídeos novos ou a ação de
  transcrever muitos vídeos antigos forma fila e atrasa. — Mitigação: a transcrição é
  assíncrona e nunca atrasa o vídeo (RN-M19); a fila é sinal obrigatório (RN-M26); sem carga em
  massa.
- **Vídeo acima de 2 horas** fica sem transcrição. — Mitigação: falha com motivo claro (RN-M24) e
  limitação aceita em 2026-10-12 (DP-10).
- **Dependência de outro repositório** (liberação de destinos privados, estabilidade do contrato).
  — Mitigação: camada anticorrupção; PR de destinos privados (`#2`) e correção do defeito do deploy
  (`#3`) foram entregues e implantados em 2026-10-12; resta configurar as listas de destinos
  privados no deploy para a validação fim a fim.
- **Janela de 24 h do resultado**: se a plataforma não buscar a tempo, o texto se perde. —
  Mitigação: RF-07 (cópia própria, reconciliação, refazer).

## Alternativas Consideradas

### Abordagem Escolhida: serviço de transcrição próprio, pipeline da mídia, texto guardado com o vídeo

- **Descrição**: a Mídia dona do vídeo pede a transcrição ao serviço próprio depois que o vídeo
  fica pronto, guarda o texto, e o entrega ao aluno pela mesma verificação da reprodução.
- **Por que foi escolhida**: mantém o conteúdo sob controle próprio, não toca no estado do vídeo,
  reaproveita a proteção que existe e serve a vídeo novo, a refazer e a vídeo antigo pelo mesmo
  caminho.

### Alternativa Rejeitada 1: serviço de transcrição de terceiro

- **Descrição**: enviar o áudio de cada vídeo a uma API de nuvem.
- **Trade-offs**: sem operar infraestrutura × custo recorrente por minuto e saída do conteúdo
  para terceiro.
- **Por que foi rejeitada**: contraria o motivo do projeto (conteúdo sob controle próprio) e já
  existe serviço próprio na rede privada (DP-01).

### Alternativa Rejeitada 2: transcrever só sob demanda do professor

- **Descrição**: sem disparo automático; o professor aciona Transcrever em cada vídeo.
- **Trade-offs**: custo e fila só onde alguém quis × mais um passo por vídeo e aulas sem
  transcrição por esquecimento.
- **Por que foi rejeitada**: a promessa ao aluno de que "toda aula tem" não se sustenta; DP-03.

### Alternativa Rejeitada 3: transcrição pública (visível sem login)

- **Descrição**: publicar o texto da aula na vitrine para atrair busca.
- **Trade-offs**: alcance × entrega o conteúdo pago.
- **Por que foi rejeitada**: contradiz DP-04 e RN-M01.

## Questões em Aberto

Nenhuma questão de produto em aberto. As que existiam foram respondidas pelo responsável em
2026-10-12 e estão registradas em **Decisões de Produto** (DP-05 a DP-10), em **Riscos** (risco
aceito do texto completo) e em **Restrições** (dependências externas em andamento).

PRD 1.0 e domain doc v1.1 aprovados pelo responsável em 2026-10-12.
Detalhes de implementação (extração do áudio, reconciliação, limites de espera, formato da
legenda) são da TechSpec.

## Histórico de revisões

| Versão | Data | Mudança |
|---|---|---|
| 1.0 | 2026-10-12 | Aprovada pelo responsável |
| 1.1 | 2026-10-12 | Errata mínima por decisão do responsável no mesmo dia, ao fechar o recorte de contratos: RF-06 deixa de dizer que o aluno sem direito recebe "a mesma resposta de recurso inexistente" e passa a dizer que recebe a mesma recusa de acesso da aula e da reprodução (403): "quem não tem direito à aula não tem a transcrição". Registra também que `whisper-api#2` e `#3` foram mergeados e implantados em 2026-10-12. Nenhuma outra regra ou escopo mudou. Status segue `approved` |
