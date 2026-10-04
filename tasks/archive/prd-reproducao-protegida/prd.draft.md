---
tsg_artifact: prd
product: code-4-coders
capability: CAP-007
version: 1.0
status: approved
updated: 2026-10-03
sources: backlog/capabilities.md@1.4, vision.md@1.2, context/domain-map.md@1.2, context/architecture-baseline.md@1.2, domains/entrega-de-midia-e-protecao/domain.md@1.0, domains/matricula-e-direito-de-acesso/domain.md@1.0, domains/identidade-e-acesso/domain.md@1.1, domains/conteudo-e-curriculo/domain.md@1.1
---

# Reprodução protegida — o aluno com direito assiste à aula, identificado e sem link solto

## Visão Geral

A escola já prepara o vídeo (`CAP-006`), já o vincula a aulas e publica o curso (`CAP-005`) e já sabe
quem pode assistir a quê (`CAP-008`). Falta o momento em que o produto é **consumido**: o aluno abre a
aula e o vídeo toca. É aqui que mora a qualidade percebida da escola e toda a estratégia de proteção
que substituiu o DRM não contratado.

Esta entrega cria a **tela da aula** do aluno e a **reprodução protegida** por trás dela:

- o aluno abre uma aula e o vídeo começa, se — e só se — a plataforma confirma, **naquele momento**,
  que ele tem direito ao curso;
- a entrega é individual e de vida curta: nenhum endereço de vídeo, segmento ou chave funciona fora da
  sessão do aluno, e um link copiado deixa de servir sozinho;
- o **e-mail do aluno aparece sobre o vídeo**, em posição que muda, e dissuade o repasse casual;
- a tela traz a **lista de aulas do curso** para trocar de aula;
- a reprodução informa seu **avanço** como fato bruto, para que o progresso (`CAP-017`) tenha o que
  consumir.

A proteção é **dissuasão declarada**: desencoraja o repasse casual e a gravação de tela, e não detém
quem quer extrair o conteúdo (BA15, R8). Nenhum texto desta entrega a apresenta como garantia.

Com a cortesia de `CAP-008`, a entrega é validável de ponta a ponta sem depender do gateway de
pagamento.

---

## Rastreabilidade

### Capacidade e fronteiras

- **Capacidade:** `CAP-007` — Reprodução protegida da aula.
- **Escopo desta entrega (primeiro PRD de `CAP-007`, e hoje o único previsto):** o aluno com direito
  vigente abre a aula e a assiste. Inclui: tela da aula com player e lista das aulas do curso;
  abertura de **Sessão de Reprodução** individual com decisão de acesso e falha fechada; entrega de
  segmentos e chave por acesso de vida curta; **Marca d'Água** do e-mail no cliente; renovação da
  sessão com nova decisão; informe do **avanço** da reprodução; sinais de operação da entrega (custo
  por aluno ativo, erro de sessão, cache).
- **Fora desta entrega:**
  - pré-visualização do vídeo pelo professor (adiada em `CAP-006` para "junto com `CAP-007`") →
    PRD futuro próprio: usa outra autorização (ator interno, sem decisão de Matrícula) e pode sair
    depois sem retrabalho (DP-07);
  - download e visualização de **material complementar** → segundo PRD de `CAP-005` (OD43, passa por
    Mídia sob RN-M16);
  - área do aluno ("meus cursos"), percentual, aula concluída e **retomar de onde parou** → `CAP-017`;
  - liberação progressiva da próxima aula e condicionamento por avaliação → `CAP-018` (Fase 3, G20);
  - medir sobreposição de aulas distintas e agir sobre compartilhamento de conta → `CAP-029` (G25);
    esta entrega só garante que o avanço traz o necessário (QA-01);
  - aula sem vídeo (artigo, texto, links) → revisão de Conteúdo antes do PRD que a introduzir (QC-03);
  - substituição, exclusão e retenção do original de um vídeo → QM-04, fora do MVP;
  - anotação no minuto da aula → Aprendizagem e Progresso, em fase posterior.
- **Domínios atravessados:**
  - Entrega de Mídia e Proteção (dono) —
    [domain.md](../../domains/entrega-de-midia-e-protecao/domain.md) v1.0;
  - Matrícula e Direito de Acesso — [domain.md](../../domains/matricula-e-direito-de-acesso/domain.md)
    v1.0, só pela decisão "pode acessar agora?";
  - Identidade e Acesso — [domain.md](../../domains/identidade-e-acesso/domain.md) v1.1, pelo aluno
    corrente e pelo e-mail da Marca d'Água;
  - Conteúdo e Currículo — [domain.md](../../domains/conteudo-e-curriculo/domain.md) v1.1, pela
    estrutura publicada (módulos e aulas) e pela Referência de Uso;
  - Aprendizagem e Progresso — **sem domain doc** (um único PRD no horizonte: `CAP-017`; `CAP-018` e
    `CAP-019` são da Fase 3). Aqui é só destinatário do avanço.
- **Juntas entre os domínios:**
  - *Matrícula → Mídia (RN-D01, RN-M10).* Matrícula decide e responde; Mídia pergunta a cada abertura
    e renovação de sessão e **nunca guarda a decisão** além de 30 s. Sem resposta, não há sessão.
  - *Conteúdo → Mídia (RN-C12, RN-M09).* Conteúdo é dono do vínculo aula–vídeo e da estrutura; Mídia
    guarda só a Referência de Uso opaca (curso e aula) e confere que a aula pedida usa aquele vídeo
    **antes** de perguntar a Matrícula.
  - *Identidade → Mídia (Identidade RN-21).* Identidade é dona do aluno e do e-mail; o e-mail chega
    ao player dentro da sessão — exposição declarada — e não aparece em mais lugar nenhum.
  - *Mídia → Aprendizagem (RN-M14).* Mídia informa o avanço como fato bruto; concluir aula e calcular
    progresso são de Aprendizagem.
- **Dependências entre capacidades:** `CAP-006` (`done`: vídeo pronto, Versão de Reprodução,
  adaptadores reais de armazenamento e distribuição), `CAP-008` (`done`: decisão de acesso e cortesia),
  `CAP-001` (`done`: conta e sessão do aluno), `CAP-005` (`done`: currículo publicado e Referência de
  Uso). Consumida por `CAP-017`.
- **Restrições do baseline:** nenhum objeto de vídeo público (G21); marca d'água no cliente, nenhum
  derivado por aluno (G22); e-mail fora de log, URL, nome de objeto, chave de cache e routing key
  (G23); nenhuma trava de concorrência de sessão ou de reprodução (BA16, G24) e IP não é sinal (G26);
  decisão de acesso por consulta síncrona ao dono, cache de até 30 s e **falha fechada** (BA07, G11,
  G08); fato por outbox (G06); `tenant_id` em tudo (G07); o SPA fala só com o BFF (G15); direito de
  acesso nunca se confunde com liberação de aula (G20); provedor atrás de camada anticorrupção (BA14).

### Vision Doc

- **Objetivos de negócio atendidos:** o ativo de conteúdo sob controle próprio, entregue só a quem
  tem direito (C04); fluxo "assistir → progredir" da Fase 1 validável antes do gateway de pagamento.
- **Restrições globais aplicáveis:** sem DRM (BA15); custo de armazenamento e banda como risco
  declarado (R10); proteção como dissuasão, nunca garantia (R8).
- **Non-Goals globais respeitados:** não promete exclusividade do conteúdo; não restringe o aluno
  pagante por suspeita de compartilhamento.

### Domain Docs

- **Entidades envolvidas (Mídia):** Mídia, Versão de Reprodução, Referência de Uso, Sessão de
  Reprodução, Marca d'Água.
- **Entidades envolvidas (Matrícula):** Concessão de Acesso, Vigência — só pela decisão.
- **Entidades envolvidas (Identidade):** Conta (de aluno) — só leitura.
- **Entidades envolvidas (Conteúdo):** Curso, Módulo, Aula, Versão de Publicação — só leitura.
- **Regras de negócio referenciadas:** Mídia RN-M01, RN-M02, RN-M05, RN-M07, RN-M08, RN-M09, RN-M10,
  RN-M11, RN-M12, RN-M13, RN-M14, RN-M15, RN-M17; Matrícula RN-D01, RN-D06, RN-D07, RN-D08, RN-D15;
  Identidade RN-09, RN-21; Conteúdo RN-C07, RN-C09, RN-C12.
- **Regras precisadas por esta entrega:** validade da sessão e cadência do avanço (QM-07 do domain
  doc de Mídia, fechada em DP-01 e DP-02).
- **Regras nascidas neste PRD:** `RN-R01` a `RN-R08` (Reprodução), na seção Funcionalidades Principais.
  O domain doc de Mídia as absorve na próxima revisão; as que tocam o percurso migram para o domain
  doc de Aprendizagem quando ele existir.
- **Eventos consumidos:** nenhum novo (a Referência de Uso e o fato de vídeo pronto já são consumidos
  por Mídia).
- **Eventos produzidos:** `midia.reproducao-avancou` (RF-07).

## Termos Canônicos

| Termo | Definição de negócio | Escopo/Fonte |
|---|---|---|
| Tela da aula | A tela do aluno com o player, o título da aula e a lista das aulas do curso | Esta entrega |
| Aula atual | A aula cujo vídeo está aberto na tela da aula | Esta entrega |
| Sessão de Reprodução | Autorização temporária e individual de um aluno para reproduzir **um** vídeo no contexto de **uma** aula | Domain doc de Mídia |
| Validade da sessão | Quanto tempo a sessão vale sem nova decisão de acesso: 5 minutos (DP-01) | Esta entrega |
| Renovação | Estender a reprodução em andamento repetindo a decisão de acesso | Domain doc de Mídia (RN-M11) |
| Marca d'Água | O e-mail do aluno sobreposto ao vídeo, composto no player, em posição que varia | Domain doc de Mídia |
| Avanço | Fato bruto de que uma sessão chegou a certa posição da aula; não é progresso nem conclusão | Domain doc de Mídia (RN-M14) |
| Decisão negada | Matrícula respondeu que o aluno **não** pode acessar o curso agora | `CAP-008` |
| Decisão indisponível | Matrícula não respondeu a tempo ou respondeu com erro; **não** é uma negação | Esta entrega (DP-08) |

---

## Objetivos

- O aluno com direito vigente abre uma aula e o vídeo começa a tocar, sem ajuda do time.
- Quem não tem direito, ou deixou de ter, não obtém vídeo, segmento nem chave — por tela, por link
  copiado, por cache ou por sessão reaproveitada.
- Revogar ou vencer um acesso interrompe a reprodução em andamento em até 5 minutos.
- Todo vídeo reproduzido identifica o aluno por e-mail, sem criar versão do vídeo por aluno.
- O avanço da reprodução chega a `CAP-017` com granularidade controlada.
- A equipe enxerga o custo de armazenamento e distribuição por aluno ativo desde o primeiro aluno.
- `CAP-007` e `CAP-017` são validados de ponta a ponta com uma cortesia real.

---

## Histórias de Usuário

- Como **aluno com acesso vigente**, eu quero abrir a aula e ver o vídeo começar para que eu estude
  sem fricção.
- Como **aluno**, eu quero trocar de aula pela lista do curso para que eu siga a ordem do professor ou
  volte a uma aula anterior.
- Como **aluno sem acesso ou com acesso vencido**, eu quero uma mensagem clara sobre o que aconteceu
  para que eu saiba se devo procurar a escola ou tentar de novo.
- Como **aluno**, eu quero que a plataforma não me desconecte no meio da aula por falha momentânea para
  que eu não perca o fio do estudo.
- Como **escola**, eu quero que quem copiar um link de vídeo não consiga assistir com ele, e que o
  e-mail de quem assiste apareça no vídeo, para que o conteúdo não circule solto.
- Como **escola**, eu quero que o acesso revogado ou vencido pare a reprodução em minutos para que o
  direito comprado seja o direito exercido.
- Como **produto de aprendizagem (`CAP-017`)**, eu quero receber o avanço da reprodução de cada aula
  para que eu calcule progresso sem medir o vídeo por conta própria.
- Como **equipe de plataforma**, eu quero ver o custo e os erros da entrega para que o custo por aluno
  não cresça sem ninguém ver.

---

## Funcionalidades Principais

### RF-01: Abrir a aula

**Descrição**: O aluno logado abre uma aula pelo endereço dela (link da aula ou a lista de aulas de
RF-06). A tela mostra o título da aula, o player e a lista das aulas do curso. Antes de qualquer
vídeo, a plataforma confere nesta ordem: (1) a aula pertence a um curso publicado e usa aquele vídeo
(Referência de Uso, RN-M10); (2) o vídeo está pronto (RN-M05); (3) Matrícula responde que o aluno pode
acessar o curso **agora**. O aluno começa sempre do início da aula (DP-06).

Estados que a tela precisa distinguir, cada um com mensagem própria:

| Estado | O que o aluno vê |
|---|---|
| Aluno sem sessão de login | É levado ao login e, depois de entrar, volta à mesma aula |
| Decisão positiva | O vídeo carrega e começa a tocar |
| Decisão negada, nunca teve acesso | "Você não tem acesso a este curso." Sem revelar título ou conteúdo de aula do curso |
| Decisão negada, acesso vencido | "Seu acesso a este curso terminou em dd/mm/aaaa." |
| Decisão indisponível | "Não foi possível confirmar seu acesso agora. Tente de novo em instantes." com ação *Tentar de novo*; **não** diz que o aluno não tem acesso |
| Aula inexistente, de outra escola, de curso não publicado ou removida da versão vigente | "Esta aula não está disponível." Não distingue entre os motivos |
| Vídeo não pronto ou indisponível | "Esta aula está indisponível no momento." Nenhum detalhe técnico |

**Critérios de Aceitação**:

- **Given** um aluno com cortesia vigente sobre o curso e uma aula publicada com vídeo pronto
  **When** abre a aula
  **Then** o player carrega, o vídeo começa do início e a lista de aulas do curso aparece.

- **Given** um aluno cuja concessão terminou ontem
  **When** abre a aula
  **Then** vê a mensagem de acesso encerrado com a data do término, nenhum vídeo é entregue e nenhuma
  sessão é aberta.

- **Given** um aluno sem nenhuma concessão sobre o curso
  **When** abre a aula
  **Then** vê "Você não tem acesso a este curso" e nada do conteúdo da aula (nem o título) é revelado.

- **Given** que Matrícula não responde dentro do tempo limite
  **When** o aluno abre a aula
  **Then** nenhuma sessão abre, a mensagem é a de indisponibilidade (não a de acesso negado) e a ação
  *Tentar de novo* está disponível.

- **Given** um aluno que pede uma aula usando o vídeo de **outra** aula do mesmo ou de outro curso
  **When** a abertura é processada
  **Then** a plataforma recusa antes de consultar Matrícula e nenhum vídeo é entregue (RN-M10, passo 1).

- **Given** um visitante sem login
  **When** abre o link de uma aula
  **Then** é levado ao login e, depois de entrar, volta à aula; nada da aula é mostrado antes.

- **Given** uma aula e um aluno de **outra escola**
  **When** o aluno abre o link
  **Then** vê "Esta aula não está disponível", igual ao caso de aula inexistente.

**Prioridade**: Must Have

**Rastreabilidade**: RN-M05, RN-M10, RN-D01, RN-D06, RN-R02, RN-R07

---

### RF-02: Sessão de Reprodução individual e renovação

**Descrição**: Decisão positiva abre uma Sessão de Reprodução: um aluno, um vídeo, uma aula, vida curta.
A sessão vale **5 minutos** (DP-01). Enquanto o aluno assiste, a plataforma **renova** a sessão antes
do fim, repetindo a decisão de acesso, sem interromper o vídeo quando o direito continua. A renovação
é tentada com antecedência e repetida se a decisão estiver indisponível, até o fim da validade.

Vale a decisão que Matrícula dá no momento da abertura ou da renovação; a plataforma **não** concede
tolerância ao término da vigência (DP-09). O aluno pode ter várias sessões ao mesmo tempo, em
dispositivos e abas diferentes: não há trava de concorrência (RN-M13).

**Critérios de Aceitação**:

- **Given** um aluno com acesso vigente assistindo há 20 minutos
  **When** a sessão chega perto do fim
  **Then** ela é renovada sem pausa nem recarga perceptível do vídeo, e uma nova decisão de acesso é
  consultada a cada renovação.

- **Given** um aluno assistindo e a concessão revogada ou vencida durante a reprodução
  **When** a próxima renovação consulta Matrícula e a resposta é negada
  **Then** a reprodução para em até 5 minutos depois da revogação (mais o trecho que o player já
  tinha carregado), e o aluno vê a mensagem do estado correspondente de RF-01.

- **Given** que a decisão fica indisponível na hora da renovação
  **When** a plataforma tenta de novo antes do fim da validade e Matrícula volta a responder positivo
  **Then** a reprodução continua sem o aluno perceber.

- **Given** que a decisão continua indisponível até o fim da validade
  **When** a validade termina
  **Then** a reprodução para com a mensagem de indisponibilidade e a ação *Tentar de novo*; nenhuma
  sessão é estendida por padrão (falha fechada).

- **Given** o mesmo aluno com a aula aberta em duas abas e em outro dispositivo
  **When** as três reproduzem
  **Then** as três funcionam, cada uma com a sua sessão, e nenhuma é interrompida pelas outras.

- **Given** uma sessão aberta para a aula A
  **When** alguém tenta usar essa sessão para obter vídeo ou chave de **outra** mídia
  **Then** a plataforma recusa.

**Prioridade**: Must Have

**Rastreabilidade**: RN-M10, RN-M11, RN-M13, RN-D01, RN-R01, RN-R02, BA07, G11, G24

---

### RF-03: Entrega sem link solto

**Descrição**: Nenhum arquivo de vídeo é público. Segmentos e chave de cifra só são obtidos por quem tem
Sessão de Reprodução válida **daquela** mídia, por acesso individual de vida curta. A chave é servida
pela plataforma mediante a mesma sessão, nunca por endereço estável nem pela distribuição (RN-M08).
Um endereço copiado deixa de funcionar ao fim da validade e não serve para outro vídeo.

**Critérios de Aceitação**:

- **Given** o endereço de um segmento ou da chave, sem sessão válida
  **When** é acessado diretamente
  **Then** é recusado.

- **Given** um endereço de segmento copiado de uma reprodução
  **When** é usado depois da validade
  **Then** é recusado.

- **Given** um endereço válido de um vídeo X
  **When** é adaptado para pedir um segmento ou a chave de um vídeo Y
  **Then** é recusado.

- **Given** o armazenamento do vídeo
  **When** se tenta listar ou ler objetos sem passar pelo acesso individual
  **Then** nenhum objeto é acessível publicamente.

- **Given** dois alunos diferentes reproduzindo o mesmo vídeo
  **When** se comparam os arquivos entregues
  **Then** são os mesmos objetos e a mesma Versão de Reprodução, sem cópia ou chave por aluno (RN-M07).

- **Given** qualquer endereço de vídeo, segmento ou chave
  **When** se inspeciona o endereço, o nome do objeto, a chave de cache e as mensagens publicadas
  **Then** o e-mail do aluno não aparece (G23).

**Prioridade**: Must Have

**Rastreabilidade**: RN-M01, RN-M07, RN-M08, G21, G22, G23

---

### RF-04: Marca d'água com o e-mail do aluno

**Descrição**: Durante a reprodução, o e-mail do aluno é sobreposto ao vídeo pelo player, **nunca
gravado no arquivo** (RN-M12). A marca troca de posição ao longo da reprodução (DP-03), fica visível
também em tela cheia e com o vídeo pausado, é legível sem esconder os controles e nunca fica na mesma
posição duas trocas seguidas. O e-mail chega ao player dentro da sessão, é o do próprio aluno e não é
copiado para lugar nenhum além disso.

Sob o player, a tela traz um aviso curto e fixo de que o conteúdo é de uso pessoal e de que o e-mail
aparece no vídeo (DP-05). O texto **não** afirma que o conteúdo é protegido contra cópia (RN-M15).

**Sem marca, sem vídeo (RN-R03):** se a sessão não puder trazer o e-mail do aluno, a reprodução não
começa.

**Critérios de Aceitação**:

- **Given** um aluno reproduzindo
  **When** o vídeo está tocando, pausado ou em tela cheia
  **Then** o e-mail dele está visível sobre o vídeo.

- **Given** a reprodução em andamento
  **When** passam 30 segundos
  **Then** a marca mudou de posição, diferente da anterior.

- **Given** a marca visível
  **When** o aluno usa os controles do player
  **Then** a marca não cobre os controles nem impede clicar neles.

- **Given** dois alunos diferentes reproduzindo o mesmo vídeo
  **When** se compara o arquivo entregue
  **Then** ele é idêntico; o e-mail de cada um só existe no player de cada um.

- **Given** uma sessão sem e-mail do aluno
  **When** o player tenta iniciar
  **Then** a reprodução não começa e o aluno vê "Não foi possível iniciar a aula. Tente de novo."

- **Given** a tela da aula
  **When** se lê o texto do aviso
  **Then** ele diz que o conteúdo é de uso pessoal e que o e-mail aparece no vídeo, e não usa as
  palavras "protegido contra cópia", "seguro contra cópia" ou equivalente.

- **Given** log, métrica, trace e mensagens publicadas desta entrega
  **When** se busca o e-mail de um aluno de teste
  **Then** ele não aparece em nenhum (G10, G23).

**Prioridade**: Must Have

**Rastreabilidade**: RN-M12, RN-M15, Identidade RN-21, G22, G23, RN-R03, RN-R04

---

### RF-05: Player

**Descrição**: O player oferece o essencial para estudar: tocar e pausar, avançar e voltar na linha do
tempo, volume, tela cheia e **velocidade de reprodução** (0,5x, 1x, 1,25x, 1,5x e 2x; DP-10). A
qualidade é escolhida automaticamente conforme a rede, entre as três que o vídeo tem (DP-01 de
`CAP-006`); o aluno não escolhe manualmente. Não há botão de baixar o vídeo.

Acessibilidade: todos os controles por teclado, com nome acessível, foco visível e operação por leitor
de tela; a marca d'água não é anunciada como conteúdo a ser lido.

**Critérios de Aceitação**:

- **Given** o vídeo tocando
  **When** o aluno pausa, volta 10 segundos, muda a velocidade para 1,5x e entra em tela cheia
  **Then** cada ação tem efeito imediato e a marca d'água continua visível.

- **Given** uma rede que piora no meio da aula
  **When** o player precisa de menos banda
  **Then** passa a uma qualidade menor sem interromper o vídeo e sem o aluno precisar agir.

- **Given** o player
  **When** se percorrem os controles só com teclado
  **Then** todos são alcançáveis, têm nome acessível e foco visível.

- **Given** o player
  **When** se procura qualquer ação de baixar o vídeo
  **Then** ela não existe.

**Prioridade**: Must Have para tocar, pausar, avançar/voltar, volume, tela cheia e teclado; Should Have
para velocidade.

**Rastreabilidade**: RN-M07, DP-01 de `CAP-006`

---

### RF-06: Lista de aulas do curso e troca de aula

**Descrição**: A tela da aula traz a estrutura do curso na **versão vigente**: módulos e aulas na ordem
do currículo, com a aula atual destacada. Clicar numa aula a abre — com nova sessão e nova decisão,
como em RF-01. Todas as aulas do curso estão abertas para quem tem direito: **não há liberação
progressiva** nesta entrega (RN-R06, G20). Quem tem direito vê a versão vigente (RN-D08, QC-06); uma
nova publicação não derruba o direito.

Se a aula aberta saiu da versão vigente por republicação, a próxima abertura ou renovação mostra "Esta
aula não está disponível" e deixa a lista de aulas à mão.

**Critérios de Aceitação**:

- **Given** um curso com 2 módulos e 5 aulas
  **When** o aluno abre a aula 3
  **Then** a lista mostra os 2 módulos e as 5 aulas na ordem do currículo, com a aula 3 destacada.

- **Given** o aluno na aula 3
  **When** escolhe a aula 5
  **Then** uma nova sessão é aberta para a aula 5, com nova decisão, e a aula 3 deixa de tocar.

- **Given** um aluno com direito vigente que acabou de abrir a aula 1
  **When** escolhe a aula 5 sem ter assistido às aulas 2, 3 e 4
  **Then** ela abre (não há liberação progressiva nesta entrega).

- **Given** que o curso foi republicado e a aula 3 foi removida
  **When** o aluno que assistia à aula 3 tem a sessão renovada
  **Then** vê "Esta aula não está disponível", a reprodução para e a lista da nova versão está visível.

- **Given** que o curso foi republicado com a aula 3 renomeada e reordenada
  **When** o aluno abre a lista
  **Then** vê a nova ordem e o novo nome; o endereço da aula 3 continua apontando para a mesma aula
  (identidade estável, RN-C07).

- **Given** um aluno cuja concessão cobre o curso A
  **When** abre uma aula do curso B
  **Then** a decisão do curso B é consultada e negada; nada do curso B é revelado.

**Prioridade**: Must Have

**Rastreabilidade**: RN-C07, RN-C09, RN-D08, RN-D15, RN-R06, RN-R07, G20

---

### RF-07: Informar o avanço da reprodução

**Descrição**: Enquanto o aluno assiste, a reprodução informa seu **avanço** como fato bruto
(`midia.reproducao-avancou`): a sessão, a Referência de Uso (curso e aula, como identificadores
opacos), o identificador opaco do aluno, a posição na aula e o momento. O fato **não** leva e-mail,
título, percentual nem conclusão. A cadência é de **um avanço a cada 30 segundos** de reprodução
contínua, mais um avanço ao **pausar, sair e chegar ao fim**, com intervalo mínimo de 10 segundos entre
dois avanços da mesma sessão (DP-02). **Nunca um fato por segundo assistido.**

Esta entrega não calcula progresso, não conclui aula e não devolve ao aluno "onde parou": isso é de
`CAP-017`, que consome este fato. Até lá, os fatos emitidos **não podem ser perdidos**: precisam
estar disponíveis para o consumidor que ainda não existe (RN-R05).

**Critérios de Aceitação**:

- **Given** um aluno assistindo continuamente por 10 minutos
  **When** se contam os avanços da sessão
  **Then** são cerca de 20 (um a cada 30 s), nunca centenas.

- **Given** o aluno que pausa aos 4 min 12 s
  **When** a pausa ocorre
  **Then** um avanço com a posição 4 min 12 s é informado.

- **Given** o aluno que fecha a aba no meio da aula
  **When** a aba é fechada
  **Then** o último avanço conhecido foi informado, no máximo 30 segundos antes da saída.

- **Given** um aluno que pausa e retoma várias vezes em poucos segundos
  **When** se contam os avanços
  **Then** nunca há dois avanços da mesma sessão com menos de 10 segundos entre si.

- **Given** o mesmo avanço entregue duas vezes por falha de rede
  **When** o consumidor o processa
  **Then** ele conta uma vez (entrega idempotente, G09).

- **Given** o fato de avanço publicado
  **When** se inspeciona seu conteúdo
  **Then** não há e-mail, título de aula nem percentual; o aluno e a aula são identificadores opacos.

- **Given** avanços emitidos hoje e `CAP-017` entregue depois
  **When** `CAP-017` começa a consumir
  **Then** os avanços anteriores podem ser reaproveitados e nenhuma sessão assistida antes se perdeu.

- **Given** uma sessão que terminou por decisão negada
  **When** o último avanço é informado
  **Then** ele conta o que foi assistido até ali; a negação não apaga nem invalida avanço anterior.

**Prioridade**: Must Have

**Rastreabilidade**: RN-M14, RN-R05, G06, G09, BA16

---

### RF-08: Sinais de operação da entrega

**Descrição**: A entrega emite, desde a primeira reprodução, os sinais que a visão e o baseline
exigem (RN-M17): **custo de armazenamento e distribuição por aluno ativo**, **erro de sessão de
reprodução** (por motivo), **taxa de acerto de cache da distribuição**, **volume de decisões
indisponíveis** e **tempo até o vídeo começar** (observado, sem alvo: A1/OD2 em aberto). Nenhum sinal
carrega e-mail, título ou dado pessoal. Coleta, painel e alerta seguem o padrão já adotado para a
ingestão de mídia (ADR-0008): a aplicação garante a emissão; o painel e a regra de alerta ficam no
Kibana.

**Critérios de Aceitação**:

- **Given** reproduções ocorrendo
  **When** a equipe abre o painel
  **Then** vê custo por aluno ativo, sessões abertas e recusadas por motivo, taxa de acerto de cache e
  tempo até o vídeo começar.

- **Given** uma mudança que passe a furar o cache da distribuição (por exemplo, sessão na chave de
  cache)
  **When** a taxa de acerto cai abaixo do limiar configurado
  **Then** o alerta acende (G22).

- **Given** qualquer sinal emitido
  **When** se inspecionam seus atributos
  **Then** não há e-mail, nome de aluno nem título de curso ou aula.

**Prioridade**: Must Have para a emissão dos sinais; Should Have para o painel e o alerta.

**Rastreabilidade**: RN-M17, G22, G23, ADR-0008

---

### Regras nascidas neste PRD

| ID | Regra |
|---|---|
| RN-R01 | A Sessão de Reprodução vale 5 minutos; estender a reprodução repete a decisão de acesso (DP-01). |
| RN-R02 | **Decisão indisponível não é decisão negada.** Sem resposta de Matrícula, nenhuma sessão abre nem se estende, e a mensagem ao aluno é a de indisponibilidade; nunca a de "sem acesso" (BA07, DP-08). |
| RN-R03 | Sem e-mail do aluno na sessão, não há reprodução. |
| RN-R04 | A Marca d'Água muda de posição ao menos a cada 30 segundos, nunca repete a posição anterior, não cobre os controles e vale em tela cheia e com o vídeo pausado (DP-03). |
| RN-R05 | O avanço da reprodução é emitido a cada 30 s de reprodução contínua e ao pausar, sair e chegar ao fim, com no mínimo 10 s entre dois avanços da mesma sessão, e não se perde antes de existir consumidor (DP-02). |
| RN-R06 | Todas as aulas do curso estão abertas a quem tem direito. Liberar aula é regra pedagógica e nunca é inferida do direito de acesso (G20, RN-D15). |
| RN-R07 | A mensagem para aula inexistente, de outra escola, de curso não publicado ou removida da versão vigente é a mesma; a tela não revela se a aula existe. |
| RN-R08 | O aluno vê o curso na versão vigente. Publicação nova não cria, altera nem encerra direito, e a aula tem identidade estável entre versões (RN-D08, RN-C07). |

---

## Experiência do Usuário

**Persona.** *Aluno:* estuda em sessões longas, às vezes no celular, às vezes em notebook, às vezes com
várias abas abertas. Quer que o vídeo comece logo, que a aula certa esteja onde ele largou o olhar e
que a plataforma não o trate como suspeito por estudar em dois lugares.

**Fluxo.** Link da aula (ou lista de aulas) → login, se preciso → **Tela da aula**:

1. **Verificação silenciosa.** O player aparece com indicação de carregamento enquanto o acesso é
   conferido. Se tudo vale, o vídeo começa; se não, a mensagem do estado correspondente (RF-01) ocupa
   o lugar do player, com a lista de aulas visível quando fizer sentido (decisão indisponível).
2. **Assistir.** Controles essenciais, marca d'água com o e-mail, aviso de uso pessoal sob o player.
3. **Trocar de aula.** Lista de módulos e aulas ao lado (ou abaixo, no celular), aula atual
   destacada. Escolher outra aula a abre do início.
4. **Renovar sem notar.** A renovação não aparece para o aluno. Só se vê algo se a decisão virar
   negada ou indisponível até o fim da validade.

**Linguagem.** Mensagens em português simples, sem termo técnico (sem "sessão", "decisão", "token",
"CDN"). O aluno nunca lê "protegido", "seguro contra cópia" ou promessa de exclusividade (RN-M15).
Datas absolutas ("terminou em 15/03/2028").

**Design.** SPA do aluno sobre o design system do aluno. Fluxo: wireframe ASCII → Figma → aprovação →
código. Layouts de desktop e de celular. A tela do player em tela cheia faz parte do desenho.

**Acessibilidade.** Controles do player por teclado, com nome acessível e foco visível; mensagem de
estado anunciada (região de status) quando o vídeo não abre ou a reprodução para; contraste da marca
d'água suficiente para ler sem comprometer a leitura do vídeo; a marca não entra na ordem de leitura de
leitor de tela; lista de aulas navegável como lista, com a aula atual indicada em texto.

---

## Decisões de Produto

| ID | Decisão | Alternativas descartadas e motivo | Impacto no PRD | Registro |
|---|---|---|---|---|
| DP-01 | **Sessão de 5 minutos, renovada com nova decisão de acesso.** A revogação interrompe a reprodução em até 5 minutos (mais o trecho já carregado) | 2 minutos: mais decisões no caminho quente (G08) e mais risco de interromper o aluno por falha curta, para ganho pequeno na revogação. Sessão longa (1 hora): o acesso revogado continuaria tocando (RF-M04) | RF-02, RN-R01 | Confirmada (2026-10-03) |
| DP-02 | **Avanço a cada 30 s de reprodução contínua e ao pausar, sair e chegar ao fim; mínimo 10 s entre dois avanços da mesma sessão** | 15 s: dobra a escrita no domínio write-heavy para um ponto de retomada que o aluno não percebe. Um fato por segundo: proibido (RN-M14) | RF-07, RN-R05 | Confirmada (2026-10-03) |
| DP-03 | **Marca d'água muda de posição ao menos a cada 30 s, nunca repete a posição anterior e fica fora dos controles; presente em tela cheia e com o vídeo pausado** | Posição fixa: o recorte de tela a elimina (baseline). Troca contínua em movimento: distrai o aluno e dificulta ler o vídeo | RF-04, RN-R04 | Confirmada (2026-10-03) |
| DP-04 | **A tela da aula traz a lista de aulas do curso** para trocar de aula; "meus cursos" e retomar ficam em `CAP-017` | Só o player por link direto: a validação com cortesia ficaria manual aula a aula | RF-06 | Confirmada (2026-10-03) |
| DP-05 | **Aviso fixo sob o player**: conteúdo de uso pessoal e e-mail visível no vídeo | Sem aviso: a dissuasão só funciona se quem assiste sabe que é identificado. Aviso modal a cada abertura: atrito para o aluno pagante | RF-04 | Confirmada (2026-10-03) |
| DP-06 | **A aula sempre começa do início** até `CAP-017` entregar o retomar | Guardar a posição aqui: duplicaria o dado de progresso, que pertence a Aprendizagem (RN-M02) | RF-01 | Confirmada (2026-10-03) |
| DP-07 | **Pré-visualização do professor fica fora**; vira PRD próprio | Entrar aqui: abriria um segundo caminho de autorização (ator interno, sem decisão de Matrícula) | Fora desta entrega | Confirmada (2026-10-03) |
| DP-08 | **Decisão indisponível é tratada como indisponibilidade, não como negação**; a mensagem é distinta e nunca libera por padrão | Mostrar "sem acesso" quando Matrícula cai: o aluno pagante acharia que perdeu o acesso por uma falha nossa | RF-01, RF-02, RN-R02 | Confirmada (2026-10-03) |
| DP-09 | **Sem tolerância ao término da vigência**: vale a decisão do momento da abertura ou da renovação | Deixar terminar a aula em curso depois do término: dois critérios de vigência e uma exceção de produto que o domínio não prevê (RN-D06, RN-M11) | RF-02 | Confirmada (2026-10-03) |
| DP-10 | **Velocidade de reprodução entra; escolha manual de qualidade não** | Escolha manual: mais um controle para uma decisão que o player toma melhor pela rede. Sem velocidade: lacuna que o aluno de curso técnico sente nos primeiros dias | RF-05 | Confirmada (2026-10-03) |

---

## Restrições Técnicas de Alto Nível

- A decisão de acesso é sempre perguntada a Matrícula, com cache de **até 30 s**, e falha fechada;
  nenhum domínio guarda réplica do direito (BA07, G11, RN-D01).
- O caminho "aluno autorizado → vídeo começa" é o caminho crítico (b) do baseline; a cadeia síncrona
  tem no máximo 1 salto até Matrícula (G08).
- A marca d'água é composta no player; nenhuma versão, cópia ou chave por aluno existe (G22).
- E-mail do aluno só no player; fora de log, métrica, URL, nome de objeto, chave de cache e routing key
  (G10, G23).
- Nenhuma trava de concorrência de sessão ou de reprodução e nenhum uso de IP (BA16, G24, G26).
- O avanço é publicado por outbox, é idempotente e não se perde enquanto não houver consumidor (G06,
  G09).
- O provedor de armazenamento e distribuição fica atrás de camada anticorrupção; nenhum nome do
  provedor aparece em contrato ou em outro domínio (BA14, RF-M05).

---

## Não-Objetivos (Fora de Escopo)

- Tela "meus cursos", percentual, aula concluída, curso concluído e retomar de onde parou (`CAP-017`).
- Liberação progressiva e condicionamento por avaliação (`CAP-018`, G20).
- Pré-visualização do vídeo pelo professor (PRD futuro, DP-07).
- Material complementar para baixar (segundo PRD de `CAP-005`, OD43).
- Medir sobreposição de aulas distintas e qualquer ação sobre compartilhamento de conta (`CAP-029`, G25).
- Limite de sessões simultâneas, de dispositivos, sessão única, lease de reprodução ou limite por IP
  (BA16, G24, G26).
- DRM e qualquer promessa de impedir cópia (BA15).
- Download do vídeo e escolha manual de qualidade.
- Legendas, transcrição, anotação no minuto da aula, comentários e dúvidas.
- Aulas sem vídeo (QC-03).
- Marca d'água gravada no arquivo ou versão por aluno (G22).
- Substituir, excluir ou reter o original de um vídeo (QM-04).
- Reprodução offline.

---

## Plano de Rollout Faseado

### MVP (Fase 1)

- **Funcionalidades incluídas**: RF-01 a RF-08.
- **Critérios de sucesso para liberar `CAP-017`**:
  - uma cortesia real concedida pelo financeiro, e o aluno assistindo a uma aula de ponta a ponta
    com a marca d'água visível;
  - acesso vencido ou revogado interrompendo a reprodução em até 5 minutos;
  - acesso direto a segmento ou chave, sem sessão, recusado em teste;
  - avanços chegando como fato, na cadência definida, sem e-mail.

### Depois desta entrega

- `CAP-017` consome o avanço, entrega "meus cursos" e o retomar de onde parou.
- Pré-visualização do professor e material complementar saem em PRDs próprios.
- `CAP-029` mede sobreposição de aulas distintas sobre o avanço coletado.

---

## Métricas de Sucesso

| Métrica | Definição | Alvo | Prazo |
|---|---|---|---|
| Segmento ou chave entregue sem sessão válida | Entregas de segmento ou chave sem sessão válida daquele vídeo | 0 | Desde a primeira reprodução |
| Sessão aberta sem decisão positiva | Sessões abertas sem decisão positiva de Matrícula no limite de 30 s | 0 | Contínuo |
| Reprodução após revogação | Reproduções que continuam mais de 5 min (mais o trecho carregado) depois de a decisão virar negada | 0 | Contínuo |
| Reprodução sem marca d'água | Reproduções iniciadas sem e-mail do aluno disponível ao player | 0 | Contínuo |
| Versões de vídeo por aluno | Arquivos ou chaves derivados por aluno | 0 | Contínuo |
| E-mail fora do player | Ocorrências de e-mail de aluno em log, métrica, trace, URL, nome de objeto, chave de cache ou mensagem publicada | 0 | Contínuo |
| Avanços por hora assistida | Fatos de avanço por hora de reprodução contínua | ≤ 125 (120 + pausa, saída e fim) | Desde a primeira reprodução |
| Tempo até o vídeo começar | Da abertura da aula ao primeiro quadro, mediana e p95 | Medido, sem alvo (A1/OD2 em aberto) | Desde a primeira reprodução |
| Taxa de acerto de cache da distribuição | Requisições de segmento atendidas pelo cache | Medido; alerta por limiar definido na TechSpec | Desde a primeira reprodução |
| Primeira aula assistida com cortesia | Aula assistida de ponta a ponta por aluno com cortesia real, sem ajuda do time | 1 | Antes de iniciar `CAP-017` |

---

## Riscos e Mitigações

- **A proteção ser lida como garantia** — em tela, texto de venda ou na própria equipe. — Mitigação:
  RN-M15 e o aviso de DP-05; critério de aceite de linguagem em RF-04; nenhum texto de produto diz
  "protegido".
- **A marca d'água ser removida por quem abre as ferramentas do navegador.** — Aceito e declarado
  (limite honesto do baseline): dissuade a gravação de tela casual e o repasse de arquivo, e não detém
  quem quer extrair.
- **A renovação falhar por instabilidade de Matrícula e o aluno ser interrompido no meio da aula.** —
  Mitigação: renovação tentada com antecedência e repetida até o fim da validade (RF-02); mensagem de
  indisponibilidade com *Tentar de novo*, distinta de "sem acesso" (DP-08).
- **Custo de distribuição subir sem ninguém ver** por mudança que fure o cache da CDN (sessão na chave
  de cache, marca no arquivo). — Mitigação: RF-08 e o alerta de acerto de cache (G22).
- **O avanço emitido hoje se perder antes de `CAP-017` existir**, e o progresso nascer sem histórico. —
  Mitigação: RN-R05 e o critério de reaproveitamento em RF-07.
- **A tela da aula crescer para virar a área do aluno** (percentual, retomar, concluir) e invadir
  `CAP-017`. — Mitigação: DP-04 e DP-06; a lista traz só estrutura, sem estado de progresso.
- **Aluno estudando legitimamente em dois lugares ser tratado como suspeito.** — Mitigação: nenhuma
  trava de concorrência (BA16, RN-M13); só se mede depois, com dado (`CAP-029`).
- **Até 30 s de acesso depois da revogação**, por causa do cache da decisão, somados à validade da
  sessão. — Aceito como tolerância do baseline (BA07); o teto observável é o de DP-01.

---

## Alternativas Consideradas

### Abordagem Escolhida: sessão curta renovada com nova decisão, marca d'água no cliente e lista de aulas na tela

- **Descrição**: cada reprodução tem uma sessão individual de 5 minutos, renovada repetindo a decisão
  de acesso; o e-mail do aluno é composto no player; a tela traz as aulas do curso.
- **Por que foi escolhida**: é a pilha de menor custo que cumpre a dissuasão declarada sem criar
  versão por aluno, sem trava de concorrência e sem guardar uma segunda cópia do direito; e a lista de
  aulas permite validar com cortesia sem esperar `CAP-017`.

### Alternativa Rejeitada 1: sessão longa, uma decisão por aula

- **Descrição**: decidir uma vez ao abrir e valer até o fim da aula.
- **Trade-offs**: menos consultas e nenhuma interrupção por renovação; porém a revogação só valeria na
  aula seguinte, que pode ter horas (RF-M04).
- **Por que foi rejeitada**: contraria BA07 (revogação com efeito em segundos ou minutos) e RN-M11.

### Alternativa Rejeitada 2: marca d'água gravada no vídeo por aluno

- **Descrição**: transcodificar uma cópia por aluno com o e-mail queimado.
- **Trade-offs**: a marca não pode ser removida do arquivo; custo de transcodificação por aluno e fim
  do cache compartilhado da distribuição.
- **Por que foi rejeitada**: multiplica o custo registrado como risco na visão (R10) e foi decidida
  contra no baseline (G22).

### Alternativa Rejeitada 3: só o player, por link direto

- **Descrição**: tela mínima, sem lista de aulas.
- **Trade-offs**: entrega menor; porém a validação com cortesia exigiria um link por aula.
- **Por que foi rejeitada**: DP-04.

---

## Questões em Aberto

- **QA-01 — Quem materializa a medição de sobreposição de aulas distintas (BA16).** O baseline a lista
  como sinal obrigatório desde a Fase 1 ("decisão de produto, não alerta operacional"). Ela é uma
  consulta sobre o avanço que esta entrega emite, mas não pertence a `media`. *Recomendação:* decidir
  no PRD de `CAP-017`, que consome o avanço, ou em fatia própria de `CAP-029`. *Responsável:*
  Tasso. *Prazo:* antes do PRD de `CAP-017`. *Impacto se não resolvida:* o sinal que a visão exige
  nasce sem dono, e o avanço pode não trazer o necessário.
- **QA-02 — Limiar de alerta de acerto de cache** e o **tempo limite** da consulta de decisão antes de
  declarar indisponibilidade. São valores de operação, não de produto. *Responsável:* TechSpec.
  *Impacto se não resolvidas:* alerta e mensagem de indisponibilidade sem critério.
- **QA-03 — SLO do caminho crítico "aluno autorizado → vídeo começa a tocar".** O valor-alvo depende
  das métricas de sucesso do negócio (OD2, A1). Aqui só se mede. *Responsável:* negócio. *Prazo:* fim
  da Fase 1.

**Para outro momento:** pré-visualização do professor (DP-07), material complementar (OD43) e retomar
de onde parou (`CAP-017`).
