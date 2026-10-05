---
tsg_artifact: prd
product: code-4-coders
capability: CAP-017
version: 1.0
status: approved
updated: 2026-10-04
sources: backlog/capabilities.md@1.4, vision.md@1.2, context/domain-map.md@1.2, context/architecture-baseline.md@1.2, domains/entrega-de-midia-e-protecao/domain.md@1.0, domains/matricula-e-direito-de-acesso/domain.md@1.0, domains/conteudo-e-curriculo/domain.md@1.1
---

# Progresso do aluno: meus cursos, aula concluída e retomar de onde parou

## Visão Geral

O aluno com direito já assiste à aula com proteção (`CAP-007`), e cada reprodução já informa seu
**avanço** como fato bruto, retido desde o primeiro dia sem consumidor. Ainda assim, para o aluno
assistir não deixa rastro: toda aula começa do início (DP-06 de `CAP-007`), nada diz o que ele já
viu, e não existe um lugar onde ele encontre os cursos a que tem acesso.

Esta entrega transforma o avanço em **progresso**:

- cada avanço recebido é registrado contra o aluno e a aula, sem perder nenhum dos que foram
  emitidos antes desta entrega existir;
- a aula passa a contar como **concluída** quando o aluno chega a 90% do vídeo ou ao fim dele;
- o curso ganha um **percentual** de aulas concluídas;
- o aluno passa a ter **"meus cursos"**: a lista dos cursos a que tem ou teve acesso, com o quanto
  avançou em cada um, e um botão que o leva de volta à aula em que parou;
- abrir uma aula já começada **retoma do ponto em que ele parou**.

É o critério de conclusão da Fase 1 na visão ("o aluno assiste a uma aula com progresso persistido")
e o passo 10 do backlog: *o aluno fecha o navegador e volta exatamente onde parou*. Progresso é
também a base de certificação, gamificação e indicadores de engajamento, que entram em fases
posteriores.

---

## Rastreabilidade

### Capacidade e fronteiras

- **Capacidade:** `CAP-017` — Percurso e progresso do aluno.
- **Escopo desta entrega (primeiro PRD de `CAP-017`):** consumir o avanço da reprodução emitido por
  `CAP-007` (inclusive o retido antes desta entrega) e registrá-lo; concluir aula; calcular o
  percentual do curso; tela **"meus cursos"** com cursos de acesso vigente e encerrado; **continuar
  de onde parou**, no curso e dentro da aula; indicar na tela da aula quais aulas estão concluídas.
- **Fora desta entrega:**
  - **curso concluído** como marco formal, que seria publicado para Certificação, Comunidade e
    Inteligência de Negócio → Fase 3, com `CAP-018`/`CAP-022` (OD72);
  - liberação progressiva da próxima aula e condicionamento por avaliação → `CAP-018` (Fase 3, G20);
  - medir a sobreposição de aulas distintas e agir sobre compartilhamento de conta → `CAP-029`
    (Fase 2, OD71). Esta entrega só preserva o que essa medição vai consultar (RF-01);
  - anotação pessoal no minuto da aula → `CAP-019` (Fase 3);
  - aula sem vídeo e o que significa concluí-la → revisão do domain doc de Conteúdo (QC-03) antes
    do PRD que a introduzir;
  - marcar ou desmarcar aula como concluída à mão (DP-06);
  - progresso visto pela equipe da escola (painel por aluno ou por curso) → Inteligência de Negócio,
    Fase 5;
  - estado "suspenso" em "meus cursos": suspensão só nasce de fato financeiro (RN-D12), e Cobrança
    entra na Fase 2.
- **Domínios atravessados:**
  - Aprendizagem e Progresso (dono): **sem domain doc**. `CAP-018` e `CAP-019` são da Fase 3, fora
    do horizonte visível (OD72). As regras nascem aqui (RN-P01 a RN-P10) e migram para o domain doc
    quando ele existir;
  - Entrega de Mídia e Proteção ([domain.md](../../domains/entrega-de-midia-e-protecao/domain.md)
    v1.0): o fato de avanço e a duração do vídeo;
  - Matrícula e Direito de Acesso
    ([domain.md](../../domains/matricula-e-direito-de-acesso/domain.md) v1.0): os cursos a que o
    aluno tem ou teve acesso, e o término;
  - Conteúdo e Currículo ([domain.md](../../domains/conteudo-e-curriculo/domain.md) v1.1): a
    estrutura da versão vigente, ou seja, módulos, aulas, ordem e títulos.
- **Juntas entre os domínios:**
  - *Mídia → Aprendizagem (RN-M14).* Mídia informa o avanço como fato bruto (sessão, aluno, curso,
    aula, posição, motivo, instante). Concluir aula e calcular percentual são daqui. **A duração do
    vídeo é de Mídia** (entidade Mídia, atributo duração); Aprendizagem a obtém de Mídia e nunca a
    estima nem a pede a Conteúdo, que não a guarda (RN-C04, RN-C15). O Domain Map lista hoje só "informar o
    avanço"; a duração é uma extensão da mesma junta, registrada em QA-02.
  - *Matrícula → Aprendizagem (RN-D01, G11).* Matrícula é a fonte única de "quais cursos este aluno
    pode acessar agora". Aprendizagem pergunta e **nunca guarda nem infere** o direito a partir do
    progresso: ter progresso num curso não põe o curso em "meus cursos".
  - *Conteúdo → Aprendizagem (RN-C07, RN-C08, RN-C09).* Conteúdo é dono da estrutura. Aprendizagem
    lê a versão vigente e ancora o progresso na aula, que tem identidade estável entre versões.
- **Dependências entre capacidades:** `CAP-007` (`done`: avanço retido, tela da aula, lista de
  aulas), `CAP-008` (`done`: decisão e concessões do aluno), `CAP-005` (`done`: versão vigente e
  identidade estável da aula). Consumida por `CAP-018`, `CAP-020`, `CAP-022`, `CAP-024` e `CAP-029`.
- **Restrições do baseline:** domínio write-heavy, com gravação de granularidade controlada e nunca
  um registro por segundo assistido (premissa 4, RN-M14); não decide direito de acesso (G20); consulta
  síncrona ao dono do direito, sem réplica (BA07, G11); consumo idempotente (G09); `tenant_id` em tudo
  (G07); o SPA do aluno fala só com o BFF (G15); dado pessoal fora de log e de chave de roteamento
  (G23); detecção de compartilhamento só agregada (G25).

### Vision Doc

- **Objetivos de negócio atendidos:** C05 (progresso) na Fase 1. Fecha o critério "o aluno assiste a
  uma aula com progresso persistido" e o fluxo "assistir → progredir" validável com cortesia antes do
  gateway.
- **Restrições globais aplicáveis:** time de dois engenheiros (fatia mínima); métricas de sucesso do
  negócio ainda em aberto (OD2, A1).
- **Non-Goals globais respeitados:** não restringe o aluno pagante por suspeita de compartilhamento;
  não é ferramenta de BI.

### Domain Docs

- **Entidades envolvidas (Aprendizagem):** Progresso, Conclusão de Aula. *Percurso na Trilha*,
  *Liberação de Aula* e *Anotação Pessoal* não entram nesta entrega.
- **Entidades envolvidas (Mídia):** Sessão de Reprodução, Referência de Uso e Mídia (a duração), só
  pelo fato de avanço e pela duração.
- **Entidades envolvidas (Matrícula):** Concessão de Acesso e Vigência, só por leitura.
- **Entidades envolvidas (Conteúdo):** Curso, Módulo, Aula e Versão de Publicação, só por leitura.
- **Regras de negócio referenciadas:** Mídia RN-M02, RN-M14; Matrícula RN-D01, RN-D06, RN-D07,
  RN-D08, RN-D13, RN-D15, RN-D17; Conteúdo RN-C04, RN-C07, RN-C08, RN-C09, RN-C12, RN-C15; Reprodução
  (`CAP-007`) RN-R05, RN-R06, RN-R08.
- **Regras nascidas neste PRD:** `RN-P01` a `RN-P10` (Progresso), em Funcionalidades Principais.
- **Eventos consumidos:** `midia.reproducao-avancou.v1` (`contracts/media/asyncapi.yaml` 1.2.0),
  inclusive os fatos retidos desde `CAP-007`.
- **Eventos produzidos:** nenhum nesta entrega. Conclusão de aula e de curso como fato publicado
  vêm com a primeira consumidora (Fase 3).

## Termos Canônicos

| Termo | Definição de negócio | Escopo/Fonte |
|---|---|---|
| Avanço | Fato bruto de que uma sessão de reprodução chegou a certa posição da aula. Não é progresso nem conclusão | `CAP-007` (RN-M14) |
| Progresso na aula | O que se sabe do aluno numa aula: a última posição em que ele esteve e se a aula está concluída | Esta entrega |
| Última posição | A posição do avanço mais recente do aluno naquela aula, ordenado pelo instante do avanço e não pela ordem de chegada | Esta entrega |
| Aula concluída | Aula em que algum avanço do aluno chegou a 90% da duração do vídeo ou foi de fim (DP-01). Uma vez concluída, continua concluída | Esta entrega |
| Percentual do curso | Aulas concluídas da versão vigente divididas pelo total de aulas da versão vigente (DP-03) | Esta entrega |
| Meus cursos | Tela do aluno com os cursos a que ele tem acesso (vigentes) e os que teve (encerrados), com o progresso de cada um | Esta entrega |
| Continuar | A ação, em "meus cursos", que leva o aluno à aula em que ele deve seguir (DP-04) | Esta entrega |
| Acesso encerrado | Curso em que o aluno teve concessão e nenhuma está vigente agora, por término da vigência ou por revogação | Matrícula (RN-D06, RN-D13), visto pelo aluno |

---

## Objetivos

- **Nenhum avanço perdido.** Todo avanço emitido desde `CAP-007`, inclusive antes desta entrega, é
  registrado uma única vez.
- **Voltar onde parou.** O aluno que fecha o navegador e volta abre a aula a no máximo 30 segundos
  de onde estava (cadência de RN-R05), sem procurar.
- **Ver o próprio avanço.** Em "meus cursos" o aluno vê, para cada curso, quantas aulas concluiu e o
  percentual, e chega à aula seguinte com um clique.
- **Fechar a Fase 1 com cortesia.** Um aluno com cortesia real assiste, sai, volta e vê o progresso
  persistido, sem depender do gateway (`CAP-011`).

---

## Histórias de Usuário

- Como **aluno**, eu quero ver os cursos a que tenho acesso num só lugar para não depender do link de
  cada aula.
- Como **aluno**, eu quero que a aula volte do ponto em que parei para não procurar o minuto no vídeo
  toda vez.
- Como **aluno**, eu quero um botão "Continuar" em cada curso que me leve à próxima aula a assistir,
  para retomar o estudo sem pensar em onde estava.
- Como **aluno**, eu quero ver quais aulas já concluí e quanto do curso falta, para ter noção do meu
  avanço e me organizar.
- Como **aluno cujo acesso terminou**, eu quero ver o curso como encerrado, com a data e o que eu já
  tinha feito, para entender por que não consigo mais assistir.
- Como **aluno que estuda em dois aparelhos**, eu quero que o ponto de retomada seja o do último
  lugar em que assisti, para continuar no celular o que comecei no notebook.
- Como **escola**, eu quero que o progresso preserve sessão, aula e instante de cada avanço, para que
  a medição de compartilhamento de conta (`CAP-029`) tenha dado quando chegar.

---

## Funcionalidades Principais

### RF-01: Registrar o avanço da reprodução

**Descrição**: Cada avanço recebido de Mídia é registrado contra o aluno, o curso e a aula, na escola
do fato. Avanço repetido conta uma vez; avanço que chega fora de ordem não faz a última posição andar
para trás. Os avanços retidos desde `CAP-007` são processados como qualquer outro, e o progresso
resultante é o mesmo que seria se esta entrega já existisse quando foram emitidos.

O registro preserva, de cada avanço, **sessão, aluno, aula, posição e instante**. É isso que
`CAP-029` vai consultar para medir sobreposição de aulas distintas (OD71). Esta entrega não calcula
sobreposição, não a mostra e não age sobre ela.

Avanço de uma aula que não está mais na versão vigente (removida numa republicação) é registrado do
mesmo jeito: o histórico do aluno não depende da versão (RN-C09).

**Critérios de Aceitação**:

- **Given** avanços emitidos e retidos durante a vigência de `CAP-007`, antes desta entrega
  **When** esta entrega começa a consumir
  **Then** todos são registrados, e o progresso de cada aluno reflete o que ele assistiu antes, sem
  avanço perdido nem contado duas vezes.

- **Given** o mesmo avanço entregue duas vezes
  **When** é processado
  **Then** é registrado uma vez e o progresso não muda na segunda entrega.

- **Given** um avanço de 8 min 00 s às 14:10 e outro de 4 min 12 s às 14:02 da mesma aula, chegando
  nesta ordem
  **When** os dois são processados
  **Then** a última posição é 8 min 00 s, a do avanço mais recente.

- **Given** um aluno assistindo continuamente por 10 minutos
  **When** se contam os registros de avanço da sessão
  **Then** há um registro por avanço recebido (cerca de 20) e nenhum registro por segundo assistido.

- **Given** um avanço de uma aula removida da versão vigente
  **When** é processado
  **Then** é registrado, e a aula não aparece na lista nem no percentual (RF-03).

- **Given** um avanço de um curso e de uma aula de outra escola em relação ao aluno
  **When** é processado
  **Then** fica registrado só na escola do fato; nada dele aparece para outra escola.

- **Given** um avanço registrado
  **When** se consulta o que foi guardado
  **Then** constam sessão, aluno, curso, aula, posição, motivo e instante, e nenhum e-mail, nome ou
  título.

**Prioridade**: Must Have

**Rastreabilidade**: RN-M14, RN-R05, RN-C09, RN-P01, RN-P02, RN-P03, G06, G07, G09, OD71

---

### RF-02: Concluir aula

**Descrição**: Uma aula passa a estar **concluída** para o aluno quando um avanço dele naquela aula
chega a **90% da duração do vídeo** ou é um avanço **de fim** (DP-01). A duração é a do vídeo que a
aula usa, informada por Mídia. A conclusão é **permanente**: rever a aula, voltar ao início ou parar
antes dos 90% numa sessão nova não a desfaz (DP-05). Pular para o fim conta como concluir; este
produto mede o que o aluno percorreu, não comprova que ele aprendeu, e comprovação é de Avaliação
(`CAP-020`).

Conclusão não depende do acesso no momento: um avanço de uma sessão que terminou por acesso negado
ainda conta o que foi assistido até ali (RN-R05 de `CAP-007`).

Se a aula trocar de vídeo numa republicação (mesma aula, RN-C07), a conclusão continua valendo.

**Critérios de Aceitação**:

- **Given** uma aula de 10 minutos
  **When** chega um avanço do aluno na posição 9 min 00 s
  **Then** a aula fica concluída.

- **Given** uma aula de 10 minutos
  **When** o aluno fecha a aba e o último avanço é 8 min 50 s
  **Then** a aula não está concluída e a última posição é 8 min 50 s.

- **Given** um avanço de fim
  **When** é processado
  **Then** a aula fica concluída, qualquer que seja a posição informada.

- **Given** um aluno que pula do minuto 1 para o fim e o player informa o fim
  **When** o avanço é processado
  **Then** a aula fica concluída.

- **Given** uma aula concluída
  **When** o aluno a revê e para no minuto 2
  **Then** ela continua concluída; a última posição passa a ser o minuto 2.

- **Given** a duração do vídeo de uma aula ainda não conhecida por Aprendizagem
  **When** chega um avanço que não é de fim
  **Then** a posição é registrada e a conclusão é decidida assim que a duração for conhecida, sem
  depender de novo avanço.

- **Given** uma aula concluída cujo vídeo foi trocado numa republicação
  **When** o aluno abre "meus cursos"
  **Then** a aula continua concluída.

**Prioridade**: Must Have

**Rastreabilidade**: RN-M14, RN-C07, RN-P04, RN-P05, RN-P06

---

### RF-03: Percentual do curso

**Descrição**: O percentual do curso é o número de aulas **concluídas da versão vigente** dividido
pelo total de aulas **da versão vigente**, arredondado para baixo, em número inteiro (DP-03). Todas as
aulas pesam igual, qualquer que seja a duração. 100% só aparece quando todas as aulas da versão
vigente estão concluídas.

Quando o curso é republicado, o percentual passa a refletir a nova versão: aula nova entra no total,
aula removida sai do total e do numerador, e a conclusão dela fica guardada (DP-07). O percentual
pode cair quando a escola acrescenta aulas, e isso é esperado.

**Critérios de Aceitação**:

- **Given** um curso de 8 aulas com 3 concluídas
  **When** o aluno vê o curso
  **Then** o percentual é 37% e aparece "3 de 8 aulas".

- **Given** um curso de 3 aulas com 2 concluídas
  **When** o aluno vê o curso
  **Then** o percentual é 66%, nunca arredondado para 67%.

- **Given** um curso de 10 aulas, todas concluídas, republicado com 2 aulas novas
  **When** o aluno vê o curso
  **Then** o percentual é 83% e aparece "10 de 12 aulas".

- **Given** um aluno que concluiu a aula 4, removida na republicação
  **When** vê o curso
  **Then** a aula 4 não conta no numerador nem no total, e a conclusão dela continua guardada.

- **Given** uma aula removida que volta numa versão seguinte (mesma aula, RN-C07)
  **When** o aluno vê o curso
  **Then** ela volta a contar como concluída.

**Prioridade**: Must Have

**Rastreabilidade**: RN-C07, RN-C08, RN-C09, RN-P07

---

### RF-04: Meus cursos

**Descrição**: Área do aluno com os cursos dele, em duas seções.

- **Cursos com acesso vigente**: os cursos sobre os quais Matrícula responde que o aluno tem acesso
  agora. Cada curso mostra título, percentual, "N de M aulas" e a ação **Continuar** (RF-05), ou
  **Começar** se o aluno não tem progresso nele. Ordem: o curso com avanço mais recente primeiro; os
  nunca começados por último, do acesso mais recente para o mais antigo.
- **Acesso encerrado**: cursos em que o aluno teve concessão e nenhuma está vigente (término da
  vigência ou revogação). Cada curso mostra título, percentual e "Seu acesso terminou em
  dd/mm/aaaa", sem ação de abrir aula (DP-02). O progresso é mostrado como estava.

A lista de cursos vem **de Matrícula, sempre**. Ter progresso num curso não o coloca na lista, e um
curso só aparece em "Acesso encerrado" se houve concessão (RN-P08). Um curso com várias concessões
aparece uma vez, como vigente se ao menos uma vale agora (RN-D07).

"Meus cursos" é o destino do aluno depois do login quando não havia outra página a que voltar, e fica
acessível pelo menu da área do aluno e pela tela da aula (DP-08).

**Critérios de Aceitação**:

- **Given** um aluno com cortesia vigente em 2 cursos, um com 3 de 8 aulas concluídas e outro nunca
  começado
  **When** abre "meus cursos"
  **Then** vê os 2 cursos em "vigentes": o começado primeiro, com 37%, "3 de 8 aulas" e Continuar; o
  outro com 0%, "0 de N aulas" e Começar.

- **Given** um aluno cuja cortesia terminou em 15/03/2028 e que tinha concluído 5 de 10 aulas
  **When** abre "meus cursos"
  **Then** o curso aparece em "Acesso encerrado" com 50% e "Seu acesso terminou em 15/03/2028", sem
  ação de abrir aula.

- **Given** um aluno cujo acesso foi revogado
  **When** abre "meus cursos"
  **Then** o curso aparece em "Acesso encerrado" com a data da revogação, sem revelar o motivo.

- **Given** um aluno com acesso encerrado que recebe nova cortesia sobre o mesmo curso
  **When** abre "meus cursos"
  **Then** o curso volta para "vigentes" com o progresso que já tinha.

- **Given** um aluno sem nenhuma concessão
  **When** abre "meus cursos"
  **Then** vê "Você ainda não tem cursos" e um caminho para a vitrine.

- **Given** um aluno com progresso registrado num curso sobre o qual nunca teve concessão (por
  exemplo, dado de teste)
  **When** abre "meus cursos"
  **Then** o curso não aparece.

- **Given** que Matrícula não responde
  **When** o aluno abre "meus cursos"
  **Then** vê "Não foi possível carregar seus cursos agora. Tente de novo em instantes." com *Tentar
  de novo*, e **não** vê "Você ainda não tem cursos".

- **Given** que Matrícula responde e o progresso não está disponível
  **When** o aluno abre "meus cursos"
  **Then** vê os cursos com o aviso "Seu progresso não pôde ser carregado agora", sem percentual e com
  Começar/Continuar levando à primeira aula do curso.

- **Given** um aluno que entra sem página de origem
  **When** o login termina
  **Then** chega a "meus cursos".

- **Given** um aluno que entra vindo do link de uma aula
  **When** o login termina
  **Then** volta à aula, como em `CAP-007`.

**Prioridade**: Must Have

**Rastreabilidade**: RN-D01, RN-D06, RN-D07, RN-D13, RN-D15, RN-D17, RN-P07, RN-P08, G11, G20

---

### RF-05: Continuar de onde parou

**Descrição**: Retomar funciona em dois níveis.

**No curso** ("Continuar" em "meus cursos", DP-04):

1. se a aula com o avanço mais recente do aluno no curso **não está concluída**, abre essa aula;
2. se está concluída, abre a **próxima aula não concluída** depois dela, na ordem do currículo
   vigente;
3. se não houver nenhuma depois dela, abre a **primeira aula não concluída** do curso;
4. se todas estão concluídas, abre a aula com o avanço mais recente;
5. se a aula com o avanço mais recente saiu da versão vigente, segue a mesma regra a partir da
   primeira aula não concluída do curso.

**Na aula** (ao abrir qualquer aula, pela lista, pelo link ou por Continuar): a reprodução começa na
**última posição** do aluno naquela aula, exceto quando (a) o último avanço é de fim, (b) a última
posição está a menos de 10 segundos do fim do vídeo ou (c) a última posição passa da duração do vídeo
atual (aula com vídeo trocado). Nesses casos começa do início. Encerra a DP-06 de `CAP-007`.

Ao retomar no meio, o aluno vê o aviso **"Retomando de 4:12"** com a ação **Começar do início**.

A última posição é a do avanço mais recente do aluno naquela aula, **em qualquer aparelho** (RN-P02).

**Critérios de Aceitação**:

- **Given** um aluno que parou a aula 3 em 4 min 12 s e fechou o navegador
  **When** volta e abre a aula 3
  **Then** a reprodução começa em 4 min 12 s e aparece "Retomando de 4:12" com Começar do início.

- **Given** um aluno que assistia continuamente e fechou a aba sem pausar
  **When** volta à aula
  **Then** a reprodução começa a no máximo 30 segundos de onde ele estava.

- **Given** o aluno vendo "Retomando de 4:12"
  **When** escolhe Começar do início
  **Then** a aula volta para 0:00.

- **Given** um aluno cujo último avanço na aula foi de fim
  **When** abre a aula
  **Then** a reprodução começa do início, sem aviso de retomada.

- **Given** um aluno que assistiu até 5 min no notebook às 10:00 e até 2 min no celular às 11:00
  **When** abre a aula em qualquer aparelho
  **Then** a reprodução começa em 2 min.

- **Given** um aluno com avanço mais recente na aula 3, ainda não concluída
  **When** clica Continuar
  **Then** abre a aula 3, retomando da última posição.

- **Given** um aluno que concluiu a aula 3, a mais recente, e não concluiu a 4
  **When** clica Continuar
  **Then** abre a aula 4.

- **Given** um aluno que concluiu a última aula do curso, mas pulou a aula 2
  **When** clica Continuar
  **Then** abre a aula 2.

- **Given** um aluno que concluiu todas as aulas
  **When** clica Continuar
  **Then** abre a aula com o avanço mais recente.

- **Given** um aluno cujo avanço mais recente é de uma aula removida na republicação
  **When** clica Continuar
  **Then** abre a primeira aula não concluída da versão vigente.

- **Given** uma aula com vídeo trocado, mais curto que a última posição guardada
  **When** o aluno a abre
  **Then** a reprodução começa do início.

**Prioridade**: Must Have (retomar na aula, Continuar); Should Have (aviso com Começar do início)

**Rastreabilidade**: RN-C07, RN-R05, RN-P02, RN-P09, RN-P10

---

### RF-06: Aula concluída visível na tela da aula

**Descrição**: A lista de aulas da tela da aula (RF-06 de `CAP-007`) passa a indicar as aulas
concluídas e a mostrar o percentual do curso. Concluir a aula enquanto o aluno está nela atualiza a
indicação sem que ele precise recarregar a página. Nada muda na abertura: **todas as aulas continuam
abertas** a quem tem direito, concluídas ou não (RN-R06, G20).

**Critérios de Aceitação**:

- **Given** um curso com as aulas 1 e 2 concluídas
  **When** o aluno abre a aula 3
  **Then** a lista mostra as aulas 1 e 2 como concluídas, com indicação em texto além do ícone, e o
  percentual do curso.

- **Given** o aluno assistindo à aula 3
  **When** chega a 90% e o avanço é registrado
  **Then** a aula 3 aparece como concluída na lista em até 1 minuto, sem recarregar.

- **Given** a aula 5 não concluída
  **When** o aluno a escolhe
  **Then** ela abre normalmente; não concluir não bloqueia aula nenhuma.

**Prioridade**: Must Have (indicação na lista); Should Have (atualizar sem recarregar)

**Rastreabilidade**: RN-R06, RN-D15, G20, RN-P04

---

### Regras nascidas neste PRD

| ID | Regra |
|---|---|
| RN-P01 | Todo avanço é registrado uma única vez, inclusive os emitidos antes de existir consumidor. Reentrega não muda o progresso (G09). |
| RN-P02 | A última posição do aluno numa aula é a do avanço mais recente pelo **instante do avanço**, em qualquer sessão e aparelho, e não a do último a chegar. |
| RN-P03 | O registro do avanço preserva sessão, aluno, curso, aula, posição, motivo e instante, porque é o insumo de `CAP-029` (OD71). Não guarda e-mail, nome nem título (G23). |
| RN-P04 | Aula concluída: algum avanço do aluno naquela aula chegou a 90% da duração do vídeo ou foi de fim (DP-01). |
| RN-P05 | A conclusão de aula é permanente: rever, voltar ao início ou trocar o vídeo da aula não a desfaz (DP-05). |
| RN-P06 | A duração do vídeo é de Mídia. Aprendizagem a obtém de Mídia; nunca a estima nem a obtém de outra fonte. |
| RN-P07 | O percentual do curso conta aulas da versão vigente: concluídas sobre total, todas com o mesmo peso, arredondado para baixo (DP-03). Aula removida sai das duas contas e sua conclusão é preservada (DP-07). |
| RN-P08 | Os cursos de "meus cursos" vêm de Matrícula. Progresso nunca coloca curso na lista nem concede acesso (RN-D01, G20). |
| RN-P09 | Abrir uma aula retoma da última posição, salvo avanço de fim, posição a menos de 10 s do fim ou posição além da duração atual, quando começa do início (DP-04). |
| RN-P10 | Continuar leva à aula do avanço mais recente se ela não está concluída; senão à próxima não concluída depois dela; senão à primeira não concluída; senão à mais recente (DP-04). |

---

## Experiência do Usuário

**Persona.** *Aluno:* estuda em sessões curtas e longas, às vezes no celular, às vezes no notebook.
Quer abrir a plataforma e cair onde parou, sem procurar curso, aula ou minuto, e quer sentir que está
avançando.

**Fluxo.**

1. **Entrar.** Depois do login, sem página de origem, o aluno chega a **meus cursos**.
2. **Escolher.** Vê seus cursos vigentes, cada um com percentual, "N de M aulas" e Continuar ou
   Começar. Abaixo, se houver, os cursos com acesso encerrado, visualmente secundários e sem ação.
3. **Continuar.** Abre a tela da aula da regra de RF-05. Se retoma no meio, o aviso "Retomando de
   4:12 · Começar do início" aparece perto do player e some sozinho depois de alguns segundos ou
   quando o aluno interage.
4. **Assistir e concluir.** A lista de aulas ao lado mostra as concluídas e o percentual. Ao passar
   dos 90%, a aula atual ganha a marca de concluída.
5. **Voltar.** A tela da aula tem um caminho para "meus cursos".

**Linguagem.** Português simples, sem "sessão", "avanço" nem "registro". Datas absolutas ("terminou em
15/03/2028"). Percentual sempre acompanhado da contagem ("37% · 3 de 8 aulas").

**Design.** SPA do aluno sobre o design system do aluno. Fluxo: wireframe ASCII, depois Figma,
aprovação e só então código. Telas: meus cursos (vigentes, encerrados, vazio, indisponível, progresso
indisponível), alterações na tela da aula (lista com concluídas e percentual, aviso de retomada), em
desktop e celular.

**Acessibilidade.** Concluída indicada em texto, não só por ícone ou cor; percentual com texto
equivalente para leitor de tela ("3 de 8 aulas concluídas"); aviso de retomada anunciado numa região
de status, com Começar do início alcançável por teclado antes de sumir (não some enquanto tem foco);
seções de "meus cursos" com títulos navegáveis; curso encerrado não parece clicável.

---

## Decisões de Produto

| ID | Decisão | Alternativas descartadas e motivo | Impacto no PRD | Registro |
|---|---|---|---|---|
| DP-01 | **Aula concluída a 90% da duração ou no avanço de fim** | Só no fim: quem fecha nos créditos finais nunca conclui. Com marcação manual: o percentual deixa de refletir o que foi assistido | RF-02, RN-P04 | Confirmada (2026-10-04) |
| DP-02 | **Curso com acesso encerrado continua em "meus cursos"**, em seção própria, com progresso e data do término, sem abrir aula | Sumir da lista: o aluno não entende por que perdeu o curso e perde a referência do que fez | RF-04 | Confirmada (2026-10-04) |
| DP-03 | **Percentual por contagem de aulas da versão vigente**, todas com o mesmo peso, arredondado para baixo | Ponderado pelo tempo de vídeo: mais fiel, porém exige duração de todas as aulas para mostrar qualquer número e muda a cada troca de vídeo. Arredondar para o mais próximo: mostraria 100% com aula faltando | RF-03 | Confirmada (2026-10-04) |
| DP-04 | **Retomar na última posição; Continuar segue a regra de RN-P10** | Continuar sempre na aula mais recente: leva o aluno de volta a uma aula que ele já terminou. Continuar sempre na primeira não concluída: ignora onde ele estava e o arrasta para a aula pulada de propósito | RF-05 | Confirmada (2026-10-04) |
| DP-05 | **Conclusão permanente** | Conclusão que se desfaz ao rever: o percentual oscilaria sem o aluno ter feito nada de errado | RF-02 | Confirmada (2026-10-04) |
| DP-06 | **Sem marcar ou desmarcar à mão** nesta entrega | Marcação manual: pedida em plataformas de curso, mas desacopla o percentual do que foi assistido antes de existir avaliação (`CAP-020`) | Fora de escopo | Confirmada (2026-10-04, junto com DP-01) |
| DP-07 | **Aula removida sai do percentual e sua conclusão é preservada**; volta a contar se a aula voltar | Manter a removida no percentual: curso com mais de 100% ou total que não bate com a lista | RF-03 | Confirmada (2026-10-04) |
| DP-08 | **"Meus cursos" é o destino pós-login sem página de origem** | Manter o destino atual: o aluno cai numa tela sem conteúdo dele | RF-04 | Confirmada (2026-10-04) |
| DP-09 | **A medição de sobreposição de aulas distintas é de `CAP-029`**; aqui só se preserva o avanço | Calcular aqui: mistura progresso com sinal de compartilhamento e aumenta a fatia | RF-01 | Confirmada (OD71, 2026-10-04) |

---

## Restrições Técnicas de Alto Nível

- **Integrações:** consome o fato `midia.reproducao-avancou.v1` (`contracts/media/asyncapi.yaml`
  1.2.0) e os fatos já retidos. Lê de Matrícula os cursos e as concessões do aluno, de Conteúdo a
  versão vigente e de Mídia a duração do vídeo.
- **Volume:** é o dado mais escrito do sistema. Grava por avanço recebido (cerca de 2 por minuto
  assistido por aluno), nunca por segundo.
- **Percepção do aluno:** o que foi assistido aparece em "meus cursos" e na lista de aulas em até
  1 minuto depois do avanço.
- **Privacidade:** progresso e avanço são dado pessoal do aluno, ligado a ele por identificador
  opaco. Ficam sujeitos à solicitação do titular (`CAP-031`) e não entram em log nem em chave de
  roteamento (G23). Prazo de guarda do avanço bruto em QA-01.
- **Escola:** todo progresso pertence a uma escola (G07, RN-D17).

---

## Não-Objetivos (Fora de Escopo)

- Curso concluído como marco ou fato publicado; certificado (`CAP-022`).
- Liberação progressiva; toda aula continua aberta a quem tem direito (`CAP-018`, G20).
- Marcar ou desmarcar aula à mão.
- Medir ou agir sobre compartilhamento de conta (`CAP-029`).
- Progresso para a equipe da escola (painel, relatório, exportação).
- Badges, pontos, sequência de dias, lembretes ou e-mail sobre progresso.
- Aula sem vídeo.
- Estado "suspenso" em "meus cursos".
- Restringir reprodução simultânea em dois aparelhos (BA16).

---

## Plano de Rollout Faseado

### MVP (Fase 1)

- **Funcionalidades incluídas:** RF-01 a RF-06.
- **Critérios de sucesso:**
  - os avanços retidos desde `CAP-007` registrados, sem perda nem duplicidade;
  - um aluno com cortesia real assiste a uma aula, fecha o navegador, volta e retoma a no máximo
    30 s de onde parou;
  - "meus cursos" mostra percentual coerente com as aulas concluídas, inclusive depois de uma
    republicação que acrescenta e remove aulas;
  - um curso com cortesia vencida aparece em "Acesso encerrado" com a data.

### Depois desta entrega

- `CAP-029` (Fase 2) mede sobreposição sobre o avanço preservado em RF-01.
- `CAP-018`, `CAP-020` e `CAP-022` (Fase 3) trazem curso concluído, liberação progressiva e
  certificado sobre o progresso desta entrega.

---

## Métricas de Sucesso

As metas de negócio dependem de OD2 (métricas de sucesso do produto), ainda em aberto. Até lá, esta
entrega **mede**:

| Métrica | Definição | Alvo | Prazo |
|---|---|---|---|
| Avanço registrado | Avanços registrados ÷ avanços emitidos por Mídia | 100% | Desde a entrega |
| Atraso de progresso | Tempo entre o avanço e o reflexo em "meus cursos" e na lista de aulas | ≤ 1 min (p95) | Desde a entrega |
| Retomada usada | Aberturas de aula com retomada no meio ÷ aberturas de aula com posição guardada | Só observar | Fim da Fase 1 (OD2) |
| Conclusão de aula | Aulas concluídas ÷ aulas iniciadas, por curso | Só observar | Fim da Fase 1 (OD2) |

---

## Riscos e Mitigações

- **Percentual que cai sozinho e assusta o aluno** quando a escola acrescenta aulas. Mitigação:
  percentual sempre acompanhado de "N de M aulas"; curso republicado não perde nenhuma conclusão.
- **Conclusão por pular para o fim**, que infla o percentual. Mitigação: risco aceito (DP-01);
  comprovação de aprendizado é de Avaliação e Certificação, não de progresso.
- **Aluno que estuda em dois aparelhos e vê a retomada "errada"**: retoma do último lugar em que
  assistiu, que pode não ser o que ele esperava. Mitigação: aviso com Começar do início.
- **Avanço retido grande demais na primeira carga**, com "meus cursos" atrasado nos primeiros
  minutos. Hoje o volume é só de teste (sem dados legados até o MVP). Mitigação: a TechSpec dimensiona
  o reprocessamento.

---

## Alternativas Consideradas

### Abordagem Escolhida: progresso por aula sobre o avanço bruto, com "meus cursos" vindo de Matrícula

- **Descrição:** Aprendizagem registra cada avanço, deriva conclusão e última posição por aula e
  calcula o percentual sobre a versão vigente; a lista de cursos é sempre a de Matrícula.
- **Por que foi escolhida:** respeita as três juntas sem réplica de direito nem de estrutura e
  preserva o insumo de `CAP-029` sem um segundo armazenamento.

### Alternativa Rejeitada 1: guardar só a última posição e a conclusão, descartando o avanço bruto

- **Trade-offs:** escrita e armazenamento menores; mas `CAP-029` ficaria sem a linha do tempo por
  sessão que a medição exige.
- **Por que foi rejeitada:** OD71 atribui a medição a `CAP-029` *sobre o avanço coletado aqui*.

### Alternativa Rejeitada 2: "meus cursos" a partir do progresso

- **Trade-offs:** dispensa consultar Matrícula; mas mostraria curso sem acesso e esconderia curso com
  acesso nunca começado.
- **Por que foi rejeitada:** RN-D01, G11 e G20, já que progresso não diz nada sobre direito.

---

## Questões em Aberto

- **QA-01 — Prazo de guarda do avanço bruto.** É dado pessoal pseudonimizado, guardado como insumo de
  `CAP-029`. *Recomendação:* o mesmo critério de OD11, com prazo que cubra a janela de observação de
  `CAP-029` (ordem de meses) e progresso por aula (última posição e conclusão) mantido enquanto
  houver conta. *Responsável:* encarregado de dados + negócio. *Prazo:* antes da TechSpec de
  `CAP-029`. *Impacto se não resolvida:* avanço guardado sem prazo definido. Não bloqueia esta
  entrega.
- **QA-02 — Registrar no Domain Map a duração do vídeo como parte da junta Mídia → Aprendizagem.**
  Hoje a linha diz só "informar o avanço da reprodução". *Responsável:* Tasso. *Prazo:* próxima
  revisão do Domain Map, junto de QC-05. *Impacto se não resolvida:* a junta existe só neste PRD.
- **QA-03 — Data exibida para revogação.** RF-04 mostra "Seu acesso terminou em" com a data da
  revogação. Se Matrícula não expuser essa data ao aluno, a frase fica sem data. *Responsável:*
  contrato (`tsg-flow-contract-creator`). *Impacto:* mensagem sem data para revogados, raros no MVP.
