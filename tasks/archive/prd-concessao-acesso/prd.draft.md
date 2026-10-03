---
tsg_artifact: prd
product: code-4-coders
capability: CAP-008
version: 1.1
status: approved
updated: 2026-10-01
sources: backlog/capabilities.md@1.4, vision.md@1.2, context/domain-map.md@1.2, context/architecture-baseline.md@1.2, domains/matricula-e-direito-de-acesso/domain.md@1.0, domains/identidade-e-acesso/domain.md@1.1, domains/auditoria-e-conformidade/domain.md@1.2, domains/conteudo-e-curriculo/domain.md@1.1, domains/catalogo-e-oferta/domain.md@1.1
---

# Concessão de acesso — quem pode assistir a qual curso, e até quando

## Visão Geral

A escola já publica cursos (`CAP-005`) e já os põe à venda (`CAP-003`), mas nada diz **quem pode
assistir a quê**. Sem isso, a reprodução protegida (`CAP-007`) não tem como decidir e o progresso
(`CAP-017`) não tem a quem pertencer. Esta entrega cria o conceito que falta, com dono próprio:
o **direito de acesso** de um aluno a um curso, com origem e vigência.

Ela entrega três coisas:

- no **backoffice**, o financeiro concede uma **cortesia** — acesso a um curso sem compra, por
  N meses ou vitalício, com motivo obrigatório — a um aluno que já tem conta;
- a **resposta única** à pergunta "este aluno pode acessar este curso agora?", que o player e o
  progresso passam a consultar. O acesso vence sozinho no fim da vigência, sem rotina nem ação humana;
- os **fatos** de acesso concedido e de acesso expirado, e o ato de cortesia na trilha de auditoria.

O recorte é deliberado: `CAP-008` vem **antes** da venda (`CAP-011`) para que o direito nasça como
conceito com dono, origem e vigência próprios, e nunca como flag do pedido (DE01). A cortesia é a
primeira origem porque é a única que dispensa gateway de pagamento (OD5): com ela, `CAP-007` e
`CAP-017` podem ser validados de ponta a ponta. A compra entra com `CAP-011` sobre o mesmo modelo,
sem caso especial.

---

## Rastreabilidade

### Capacidade e fronteiras

- **Capacidade:** `CAP-008` — Concessão e vigência de direito de acesso.
- **Escopo desta entrega (primeiro PRD de `CAP-008`, fatia mínima):** matrícula e concessão com
  origem e vigência; cortesia como ato administrativo auditado, concedida pelo financeiro; decisão
  "pode acessar agora?" com expiração automática; fatos `matricula.acesso-concedido` e
  `matricula.acesso-expirado`; tela mínima no backoffice para conceder cortesia e ver as concessões
  do aluno; fechamento de QD-04 (término da vigência).
- **Fora desta entrega:**
  - ordem de concessão por compra → `CAP-011` (o nome do fato "compra concluída" é de Vendas, que
    ainda não tem domain doc; o modelo já aceita a origem *compra*, mas só *cortesia* é concedível aqui);
  - revogação, suspensão e restauração → `CAP-009` (Fase 2);
  - turma → `CAP-010`;
  - concessão por assinatura → `CAP-013`/`CAP-014` (Fase 2);
  - diagnóstico de concessões para o suporte → `CAP-025`;
  - aviso "você já tem este curso" na vitrine → PRD seguinte de `CAP-003`;
  - qualquer superfície do **aluno** sobre o próprio acesso → `CAP-007` e `CAP-017`;
  - aviso por e-mail ao aluno → Notificação, ao consumir o fato (DP-08).
- **Domínios atravessados:**
  - Matrícula e Direito de Acesso (dono) —
    [domain.md](../../../domains/matricula-e-direito-de-acesso/domain.md) v1.0;
  - Identidade e Acesso — [domain.md](../../../domains/identidade-e-acesso/domain.md) v1.1, pela
    permissão nova e pela confirmação de que o beneficiário é conta de aluno;
  - Auditoria e Conformidade — [domain.md](../../../domains/auditoria-e-conformidade/domain.md) v1.2,
    pelo ato de cortesia;
  - Conteúdo e Currículo — [domain.md](../../../domains/conteudo-e-curriculo/domain.md) v1.1, pelo curso
    publicado a que a concessão se refere;
  - Catálogo e Oferta — [domain.md](../../../domains/catalogo-e-oferta/domain.md) v1.1, **só** pela junta
    de vigência (RN-O08, RN-O09): esta entrega não o consulta.
- **Juntas entre os domínios:**
  - *Identidade → Matrícula (RN-D09, Identidade RN-13).* Identidade é dona de quem é a conta e de
    ela ser de aluno; Matrícula só concede a conta de aluno e nunca guarda cópia de dado da conta.
  - *Conteúdo → Matrícula (RN-D08, RN-C08, RN-C09).* O direito é sobre o **curso**, não sobre uma
    versão; publicar de novo não cria, altera nem encerra concessão.
  - *Catálogo → Matrícula (RN-D03, RN-O09).* A vigência de uma concessão por compra chega congelada
    no pedido. Cortesia não tem pedido nem oferta: a vigência é a declarada por quem concede.
  - *Matrícula → Auditoria (RN-A14).* Matrícula pratica o ato e comunica no envelope da Auditoria;
    a Auditoria grava.
  - *Matrícula → Mídia, Aprendizagem (RN-D01, BA07).* Eles perguntam; Matrícula responde. Nenhum
    guarda réplica do direito.
- **Dependências entre capacidades:** `CAP-003` (`done`; é de onde vem o curso publicado e a forma da
  vigência). Consumida por `CAP-007`, `CAP-017`, `CAP-011`, `CAP-009`, `CAP-010`.
- **Restrições do baseline:** consulta síncrona ao dono com cache de até 30 s e **falha fechada**
  (BA07, G11); `tenant_id` em toda entidade, consulta, chave de cache e evento (G07); fato por outbox
  (G06); escrita idempotente (G09); auditoria só por evento (G13); e-mail do aluno fora de log, URL,
  chave de cache e métrica (G10, G23); escrita por cookie exige CSRF (G18); o SPA fala só com o BFF
  (G15); direito de acesso nunca se confunde com liberação de aula (G20).

### Vision Doc

- **Objetivos de negócio atendidos:** direito de acesso configurável por período ou vitalício (§5);
  habilita o fluxo "Assistir → progredir" da Fase 1 antes de existir gateway de pagamento.
- **Restrições globais aplicáveis:** fonte única do direito de acesso (DE01); sem limite de
  sessões nem de dispositivos (BA16, G24); IP não é sinal de nada (G26).
- **Non-Goals globais respeitados:** não bloqueia acesso por pré-requisito; não é ferramenta de
  campanha.

### Domain Docs

- **Entidades envolvidas (Matrícula):** Matrícula, Concessão de Acesso, Vigência.
- **Entidades envolvidas (Identidade):** Conta (de aluno), Papel, Permissão — só leitura e a
  permissão nova.
- **Entidades envolvidas (Conteúdo):** Curso, Versão de Publicação — só leitura.
- **Entidades envolvidas (Auditoria):** Ato Administrativo, Registro de Auditoria.
- **Regras de negócio referenciadas:** Matrícula RN-D01, RN-D02, RN-D03, RN-D04, RN-D06, RN-D07,
  RN-D08, RN-D09, RN-D10, RN-D11, RN-D15, RN-D17; Identidade RN-12, RN-13, RN-16, RN-17, RN-18,
  RN-21, RN-22; Auditoria RN-A02, RN-A05, RN-A06, RN-A08, RN-A14; Conteúdo RN-C08, RN-C09;
  Catálogo RN-O08, RN-O09.
- **Regras precisadas por esta entrega:** nome da permissão de cortesia (DP-01); como o beneficiário
  é identificado (DP-02); **fechamento de QD-04** — término ao fim do dia, mês sem o dia (DP-03);
  limites de vigência e de motivo (DP-04); a concessão não depende de oferta (DP-05); o que a decisão
  devolve (DP-09).
- **Eventos produzidos:** `matricula.acesso-concedido`, `matricula.acesso-expirado`; ato
  `cortesia-concedida` no envelope `auditoria.ato-praticado`.
- **Eventos consumidos:** nenhum nesta entrega. (A "compra concluída" de Vendas entra com `CAP-011`.)

## Termos Canônicos

| Termo | Definição de negócio | Escopo/Fonte |
|---|---|---|
| Concessão de Acesso | Um direito de um aluno sobre um curso, com origem, vigência e situação | Matrícula · Domain Doc |
| Matrícula | O vínculo do aluno com o curso; agrupa todas as concessões dele sobre aquele curso. Nasce na primeira concessão | Matrícula · Domain Doc |
| Cortesia | Concessão sem compra, dada pelo financeiro com motivo, por N meses ou vitalícia. Não há pedido nem oferta | RN-D11 |
| Decisão de acesso | A resposta a "este aluno pode acessar este curso agora?" — única, de Matrícula | RN-D01 |
| Término da vigência | O último instante em que a concessão vale: o fim do dia calculado a partir da concessão (DP-03) | Esta entrega · QD-04 |
| Acesso vitalício | Concessão sem término | RN-O08 · RN-D04 |
| Beneficiário | A conta de aluno que recebe a concessão | Esta entrega |

---

## Objetivos

- O financeiro concede acesso a um aluno, sem ajuda do time e sem passar por compra, e a concessão
  vale na próxima decisão.
- Existe **uma só** resposta para "este aluno pode assistir a este curso agora?", correta no fim da
  vigência sem rotina nem ação humana.
- Toda cortesia tem autor, motivo e vigência na trilha de auditoria, e nenhuma existe sem ela.
- `CAP-007` e `CAP-017` podem ser validados de ponta a ponta com acesso de cortesia, sem depender do
  gateway de pagamento.
- `CAP-011` recebe um conceito pronto: só publica o fato "compra concluída".

---

## Histórias de Usuário

- Como **financeiro**, quero conceder acesso a um curso a um aluno que já tem conta, por um período
  ou vitalício, informando o motivo, para atender parceria, bolsa ou compensação sem passar por compra.
- Como **financeiro**, quero ver as concessões que o aluno já tem antes de conceder outra, para não
  dar acesso em duplicidade sem perceber.
- Como **financeiro**, quero ver na confirmação até quando o acesso vai valer, para ter certeza do
  que estou concedendo.
- Como **administrador**, quero ver na trilha quem concedeu cada cortesia, a quem, de qual curso, por
  quanto tempo e por qual motivo.
- Como **aluno**, quero que meu acesso acabe exatamente quando prometido, e não antes.
- Como **time de produto** (consumidor: `CAP-007`, `CAP-017`), quero perguntar a um único lugar se o
  aluno pode acessar o curso agora, para nunca decidir por conta própria.

---

## Funcionalidades Principais

### RF-01: Permissão de concessão de cortesia

**Descrição**: Passa a existir a permissão `cortesia.conceder`, concedida ao papel **financeiro**
(RN-D11), que abre a área **Cortesias** do backoffice e autoriza localizar um aluno, ver as concessões
dele e conceder cortesia. Ninguém sem ela vê ou usa essa área. A decisão é tomada pelo serviço dono a
partir das claims, não pelo BFF nem pela tela. O administrador governa acesso interno e **não herda**
a área do financeiro (DP-03 de `CAP-002`).

**Critérios de Aceitação**:

- **Given** um ator com o papel financeiro
  **When** as permissões dele são consultadas
  **Then** ele tem `financeiro.ler`, `oferta.editar` e `cortesia.conceder`

- **Given** um ator com o papel professor, suporte ou administrador, sem o papel financeiro
  **When** tenta abrir a área Cortesias ou chamar qualquer operação de cortesia diretamente
  **Then** é recusado, e a área não aparece no menu

- **Given** um financeiro que teve o papel revogado
  **When** tenta conceder cortesia na sessão que tinha aberta
  **Then** é recusado (RN-17 de Identidade), e as cortesias que ele concedeu continuam valendo

- **Given** um aluno autenticado
  **When** tenta qualquer operação de cortesia
  **Then** é recusado

**Prioridade**: Must Have

**Rastreabilidade**: Matrícula RN-D11; Identidade RN-12, RN-16, RN-17, RN-18; DP-01; DP-03 de `CAP-002`

---

### RF-02: Localizar o aluno beneficiário

**Descrição**: O financeiro identifica o beneficiário **pelo e-mail** da conta de aluno (DP-02). O
sistema confirma que existe uma conta de aluno com esse e-mail, mostra o **e-mail e o nome** da conta
e as **concessões que o aluno já tem** (curso, origem, vigência e situação), para o financeiro
conferir antes de seguir. Não há lista nem busca aproximada de alunos: o e-mail exato é a única
entrada, e nada além de e-mail e nome da conta é mostrado (RN-16 de Identidade).

**Critérios de Aceitação**:

- **Given** um e-mail de conta de aluno ativa
  **When** o financeiro o informa
  **Then** vê o e-mail e o nome da conta e a lista de concessões dela (vazia, quando não há)

- **Given** o e-mail digitado com maiúsculas ou espaços nas bordas
  **When** o financeiro o informa
  **Then** é normalizado como o e-mail da conta (RN-01 de Identidade) e a conta é encontrada

- **Given** um e-mail que não pertence a nenhuma conta de aluno — inexistente ou de ator interno
  **When** o financeiro o informa
  **Then** vê "Não há conta de aluno com este e-mail", sem distinguir os dois casos, e não consegue
  seguir

- **Given** uma conta de aluno desativada
  **When** o financeiro informa o e-mail dela
  **Then** vê que a conta está desativada e não consegue conceder

- **Given** uma conta de aluno que ainda não confirmou o e-mail
  **When** o financeiro informa o e-mail dela
  **Then** vê o aviso "e-mail ainda não confirmado" e pode conceder; o aluno só usará o acesso depois
  de confirmar e entrar

- **Given** uma conta de aluno de outra escola
  **When** o financeiro informa o e-mail dela
  **Then** a conta não é encontrada (RN-D17)

- **Given** qualquer busca
  **When** é feita
  **Then** o e-mail não aparece em endereço de página, em log nem em métrica (RN-21 de Identidade)

**Prioridade**: Must Have

**Rastreabilidade**: Matrícula RN-D09, RN-D17; Identidade RN-01, RN-13, RN-16, RN-21; DP-02

---

### RF-03: Conceder cortesia

**Descrição**: Com o aluno localizado, o financeiro escolhe o **curso**, a **vigência** e informa o
**motivo**, revê um resumo e confirma. A cortesia vale **a partir do momento da confirmação**.

- **Curso:** qualquer curso da escola com versão vigente (DP-05). Não depende de o curso ter oferta
  publicada: cortesia não tem pedido (RN-D11).
- **Vigência:** *por período*, de 1 a 60 meses inteiros, ou *vitalícia* (DP-04). A tela mostra a data
  exata do término antes da confirmação (RF-05).
- **Motivo:** obrigatório, texto livre de 1 a 500 caracteres, que não deve conter dado pessoal de
  terceiros (RN-A08); a tela orienta isso.
- **Várias concessões convivem** (RN-D07, DP-06): se o aluno já tem acesso ativo ao curso, a tela
  avisa — "este aluno já tem acesso até DD/MM/AAAA" — e deixa seguir. Uma concessão não estende nem
  encerra a outra.
- **Vitalícia exige confirmação reforçada**, que diz que **a cortesia não pode ser desfeita pela tela
  nesta entrega** (DP-07).
- **Reenvio não duplica** (RN-D10): a mesma solicitação confirmada duas vezes — duplo clique, nova
  tentativa depois de falha de rede — gera **uma** concessão e **um** ato. Duas cortesias deliberadas,
  em momentos distintos, são duas concessões.

**Critérios de Aceitação**:

- **Given** um aluno localizado e um curso publicado
  **When** o financeiro confirma 6 meses e um motivo
  **Then** a concessão existe com origem *cortesia*, vigência de 6 meses a partir do momento da
  confirmação, e a Matrícula do aluno naquele curso passa a existir (ou ganha mais uma concessão)

- **Given** a confirmação de uma cortesia
  **When** ela é concluída
  **Then** a tela mostra a concessão criada (curso, vigência, término) e a decisão de acesso do aluno
  àquele curso passa a ser "pode" na próxima consulta

- **Given** o motivo vazio ou só com espaços
  **When** o financeiro tenta confirmar
  **Then** é impedido, e nada é concedido

- **Given** um motivo com mais de 500 caracteres
  **When** o financeiro tenta confirmar
  **Then** é impedido com a indicação do limite, e o texto digitado é preservado

- **Given** uma vigência de 0 ou de 61 meses, ou não numérica
  **When** o financeiro tenta confirmar
  **Then** é impedido com a indicação do limite

- **Given** um curso que nunca foi publicado, ou de outra escola
  **When** o financeiro tenta conceder (inclusive chamando a operação diretamente)
  **Then** é recusado e nada é concedido

- **Given** um curso publicado **sem** nenhuma oferta
  **When** o financeiro concede cortesia
  **Then** a concessão é criada normalmente

- **Given** um aluno com concessão ativa ao curso
  **When** o financeiro prepara outra cortesia para o mesmo curso
  **Then** vê o aviso com o término da concessão existente e pode confirmar; as duas passam a existir

- **Given** o financeiro escolhe vigência vitalícia
  **When** vai confirmar
  **Then** a confirmação reforçada aparece, com o aviso de que não há como desfazer pela tela

- **Given** a mesma confirmação enviada duas vezes
  **When** a segunda chega
  **Then** continua existindo uma única concessão e um único ato, e a tela mostra a mesma concessão

- **Given** o beneficiário que virou conta desativada, ou deixou de ser conta de aluno, entre a
  localização e a confirmação
  **When** o financeiro confirma
  **Then** é recusado e nada é concedido (RN-D09)

**Prioridade**: Must Have

**Rastreabilidade**: Matrícula RN-D02, RN-D04, RN-D07, RN-D09, RN-D10, RN-D11, RN-D17; Auditoria
RN-A08; DP-04, DP-05, DP-06, DP-07

---

### RF-04: Matrícula e concessão com origem e vigência

**Descrição**: Toda concessão tem **origem identificada** (RN-D02) e **vigência**. O modelo aceita
as origens *compra*, *cortesia*, *assinatura* e *turma*, mas **nesta entrega só a cortesia é
concedível**; as demais nascem nas capacidades que as definem. A matrícula agrupa as concessões do
aluno sobre um curso e nasce na primeira. Cada concessão guarda: aluno, curso, origem, referência da
origem, vigência (início e término, ausente se vitalícia), situação e o momento em que foi concedida.
Nenhuma concessão existe "avulsa", sem origem. A concessão vale para o curso, nunca para uma versão
(RN-D08), e carrega a escola (RN-D17).

**Critérios de Aceitação**:

- **Given** um aluno sem matrícula no curso
  **When** recebe a primeira cortesia
  **Then** a matrícula nasce com o momento da primeira concessão e com essa concessão dentro dela

- **Given** um aluno com cortesia ativa e uma segunda concedida depois
  **When** as concessões são consultadas
  **Then** são duas, cada uma com a própria vigência, na mesma matrícula

- **Given** o curso é publicado de novo, com módulos e aulas diferentes
  **When** as concessões do aluno são consultadas
  **Then** continuam as mesmas, sem alteração

- **Given** uma tentativa de criar concessão sem origem, ou de origem diferente de cortesia por
  qualquer caminho desta entrega
  **When** é feita
  **Then** é recusada

- **Given** um aluno de outra escola
  **When** consulta ou recebe concessão desta
  **Then** nada é visto nem concedido (RN-D17)

**Prioridade**: Must Have

**Rastreabilidade**: Matrícula RN-D02, RN-D07, RN-D08, RN-D17; Conteúdo RN-C08, RN-C09

---

### RF-05: Vigência e término

**Descrição**: Fecha **QD-04** do domain doc. A vigência é:

- **Por período:** N meses inteiros, contados a partir do **momento da concessão** (RN-D04). O término
  é o **fim do dia** — 23:59:59 — do dia que cai N meses depois do dia da concessão, no **fuso da
  escola** (DP-03). Se o mês de destino não tem esse dia, o término é o **último dia do mês**.
- **Vitalícia:** não tem término.

O fuso da escola na Fase 1 é o horário de Brasília.

Exemplos que a regra precisa reproduzir:

| Concedida em | Vigência | Acesso vale até (fim do dia) |
|---|---|---|
| 15/03/2027 14:10 | 12 meses | 15/03/2028 |
| 15/03/2027 14:10 | 1 mês | 15/04/2027 |
| 31/01/2027 09:00 | 1 mês | 28/02/2027 |
| 31/01/2028 09:00 | 1 mês | 29/02/2028 (ano bissexto) |
| 31/03/2027 23:30 | 6 meses | 30/09/2027 |
| qualquer | vitalícia | não expira |

**Critérios de Aceitação**:

- **Given** cada linha da tabela acima
  **When** a concessão é criada
  **Then** o término gravado e mostrado na tela é o da última coluna

- **Given** uma concessão concedida às 23:59 do dia 15
  **When** o término é calculado
  **Then** a contagem parte do dia 15 do fuso da escola, e não do dia seguinte em outro fuso

- **Given** uma concessão com término em 28/02/2027
  **When** a decisão é consultada às 23:59:59 de 28/02/2027 e às 00:00:00 de 01/03/2027
  **Then** a primeira diz "pode" e a segunda "não pode" (observados o TTL de RF-06)

- **Given** uma vigência vitalícia
  **When** a concessão é consultada, em qualquer data futura
  **Then** continua válida e a tela mostra "Acesso vitalício"

**Prioridade**: Must Have

**Rastreabilidade**: Matrícula RN-D04, RN-D06; Catálogo RN-O08; QD-04; DP-03

---

### RF-06: Decisão "pode acessar agora?"

**Descrição**: Matrícula é a **fonte única** da resposta (RN-D01). Consumidores — a reprodução, o
progresso e, adiante, avaliação e comunidade — perguntam: dado a **escola**, o **aluno**, o **curso**
e o **momento**, ele pode acessar? A resposta diz:

- **pode**, com o **término efetivo** (ou "vitalício"), que é o da concessão ativa que dura mais;
- **não pode**, com um de dois motivos: **sem direito** (nunca teve concessão a esse curso) ou
  **vigência encerrada** (já teve, nenhuma está ativa), com o término da última.

Regras:

- O aluno **tem acesso se ao menos uma concessão dele sobre o curso está ativa e dentro da vigência**
  (RN-D07).
- **Expira sozinha** (RN-D06): a decisão compara a vigência com o momento da pergunta. Nenhuma
  rotina ou ação humana é necessária para que um acesso vencido deixe de valer.
- **Tolerância:** o consumidor pode guardar a resposta por até **30 segundos** (BA07). Uma concessão
  que acaba de vencer, ou de ser concedida, pode levar até esse tempo para ser percebida; **fora
  isso, não há atraso**.
- **Falha fechada:** quem não conseguir a resposta **não libera** o acesso. Não existe resposta padrão
  "pode".
- A decisão é **só sobre o curso**. Ela nunca diz se uma aula está liberada (RN-D15, G20) e não se
  altera por publicar nova versão (RN-D08) nem por mudar ou despublicar oferta (RN-D03).
- Só Matrícula expõe essa decisão (G11); ninguém guarda réplica do direito.

**Critérios de Aceitação**:

- **Given** um aluno com uma cortesia ativa de 6 meses
  **When** a decisão é consultada para aquele curso
  **Then** é "pode", com o término da cortesia

- **Given** um aluno sem nenhuma concessão ao curso
  **When** a decisão é consultada
  **Then** é "não pode", motivo *sem direito*

- **Given** um aluno cuja única concessão terminou ontem
  **When** a decisão é consultada
  **Then** é "não pode", motivo *vigência encerrada*, com o término dela, **sem que nenhuma rotina
  tenha rodado**

- **Given** um aluno com uma concessão vencida e outra ativa
  **When** a decisão é consultada
  **Then** é "pode", com o término da ativa

- **Given** um aluno com uma concessão de 3 meses e outra vitalícia
  **When** a decisão é consultada
  **Then** é "pode", e o término efetivo é "vitalício"

- **Given** o mesmo aluno e curso, em outra escola
  **When** a decisão é consultada
  **Then** é "não pode" (RN-D17)

- **Given** uma conta que não é de aluno
  **When** a decisão é consultada
  **Then** é "não pode"

- **Given** o curso é publicado de novo, ou uma oferta dele é alterada ou despublicada
  **When** a decisão é consultada
  **Then** é a mesma de antes

- **Given** Matrícula indisponível
  **When** um consumidor precisa decidir
  **Then** o acesso **não** é liberado, e o consumidor exibe estado de indisponibilidade em vez de
  assumir "pode"

- **Given** uma consulta
  **When** é feita
  **Then** não revela o e-mail do aluno, e nenhum identificador de pessoa vai para log ou métrica
  (G10, G23)

**Prioridade**: Must Have

**Rastreabilidade**: Matrícula RN-D01, RN-D03, RN-D06, RN-D07, RN-D08, RN-D15, RN-D17; BA07; G11; DP-09

---

### RF-07: Fatos de acesso concedido e de acesso expirado

**Descrição**: Matrícula publica dois fatos, no mesmo padrão dos demais fatos do sistema:

- **`matricula.acesso-concedido`** — uma concessão passou a valer, de **qualquer origem**. Carrega a
  escola, o aluno, o curso, a origem, a concessão, a vigência e o momento. É publicado uma vez por
  concessão, e só depois de ela estar gravada.
- **`matricula.acesso-expirado`** — a vigência de uma concessão terminou. É **informativo**: a decisão
  de acesso nunca depende dele (RN-D06). Refere-se à **concessão**, não ao acesso do aluno ao curso,
  que pode continuar por outra concessão. Concessão vitalícia nunca o gera. Sai em **até uma hora**
  depois do término (DP-10).

Nenhum dos dois leva e-mail, nome ou outro dado pessoal: o aluno vai **por referência** (RN-21 de
Identidade). Reentrega do mesmo fato é segura para quem consome (G09).

**Critérios de Aceitação**:

- **Given** uma cortesia concedida
  **When** ela é gravada
  **Then** um fato `acesso-concedido` é publicado, com origem *cortesia*, e não existe cortesia
  gravada sem ele

- **Given** o fato publicado
  **When** o conteúdo é inspecionado
  **Then** não contém e-mail nem nome, apenas referências

- **Given** uma concessão por período cujo término passou
  **When** a hora seguinte ao término chega
  **Then** um único `acesso-expirado` foi publicado para ela, mesmo se a rotina que o emite tiver
  rodado mais de uma vez

- **Given** uma concessão vencida e outra ativa do mesmo aluno e curso
  **When** a primeira vence
  **Then** `acesso-expirado` é publicado para a primeira, e a decisão continua "pode"

- **Given** o fato de expiração atrasado ou perdido
  **When** a decisão é consultada
  **Then** é "não pode" do mesmo jeito

- **Given** uma concessão vitalícia
  **When** o tempo passa
  **Then** nenhum `acesso-expirado` é publicado para ela

**Prioridade**: Must Have

**Rastreabilidade**: Matrícula §7 (Eventos), RN-D06; Identidade RN-21; G06, G09; DP-10

---

### RF-08: Ato de cortesia na trilha de auditoria

**Descrição**: Conceder cortesia é **ato administrativo auditado** (RN-D11). Matrícula comunica o ato
no envelope `auditoria.ato-praticado`, com o tipo **`cortesia-concedida`**, o **autor** (o financeiro,
por referência), o **alvo** (a concessão, com o aluno e o curso por referência — nunca e-mail ou nome,
RN-A08) e o **motivo obrigatório**. A vigência declarada vai no detalhe. A concessão e o ato nascem
juntos: **não existe cortesia sem ato comunicado**.

A consulta da trilha (`CAP-030`) passa a mostrar o tipo com o rótulo *Cortesia concedida*, o motivo e
a vigência no detalhe, e a oferecê-lo no filtro por tipo.

**Critérios de Aceitação**:

- **Given** uma cortesia concedida
  **When** o administrador consulta a trilha
  **Then** vê "Cortesia concedida", o autor, o aluno (como a trilha mostra os demais alvos), o curso,
  a vigência e o motivo

- **Given** o filtro por tipo da consulta
  **When** o administrador o abre
  **Then** *Cortesia concedida* está disponível

- **Given** a mesma confirmação enviada duas vezes
  **When** a trilha é consultada
  **Then** há um único registro (RN-A03)

- **Given** um ato `cortesia-concedida`
  **When** é gravado
  **Then** não é marcado **não conforme**: autor, alvo e motivo estão presentes (RN-A06)

- **Given** o ato
  **When** o detalhe é aberto
  **Then** não contém e-mail do aluno nem texto do motivo copiado para fora do detalhe

**Prioridade**: Must Have

**Rastreabilidade**: Matrícula RN-D11; Auditoria RN-A02, RN-A03, RN-A05, RN-A06, RN-A08, RN-A14;
`CAP-030`; C-01/C-08 de `CAP-005` e RF-10 de `CAP-003` como precedentes

---

## Experiência do Usuário

**Persona.** *Financeiro:* concede cortesia de vez em quando, quase sempre a pedido de alguém
(parceiro, bolsa, compensação); precisa ter certeza de **quem** recebe, **o quê** e **até quando**, e
de que errar é difícil.

**Fluxo.** Backoffice → **Cortesias** → *Conceder cortesia*:

1. **Aluno** — informa o e-mail → vê e-mail e nome da conta e as concessões que ela já tem.
2. **Curso** — escolhe entre os cursos publicados da escola (com busca por título).
3. **Vigência** — *Por período* (número de meses) ou *Vitalícia*; a tela mostra a data do término.
4. **Motivo** — texto obrigatório, com orientação para não incluir dado pessoal de terceiros.
5. **Revisão** — resumo de uma frase: *"Conceder a [e-mail] acesso a [curso] até [data] — motivo: …"*,
   com o aviso de concessão já existente, quando houver.
6. **Confirmar** — para vitalícia, confirmação reforçada. Sucesso mostra a concessão criada.

**Linguagem.** Vigência sempre com data absoluta ("até 15/03/2028") e "Acesso vitalício". A tela
nunca fala em "pedido" nem "compra" para cortesia. A tela nunca promete ao aluno nada além do que a
concessão diz.

**Design.** Backoffice sobre o design system do backoffice. Fluxo: wireframe ASCII → Figma →
aprovação → código.

**Acessibilidade.** Passos como formulário sequencial com rótulo, erro anunciado e foco no primeiro
campo inválido; data do término em texto; confirmação reforçada focável e anunciada; resumo da
revisão legível por leitor de tela como uma frase.

## Decisões de Produto

> **Status.** *Herdada*: já decidida em artefato aprovado. *Confirmada*: decidida nesta entrega, com
> a aprovação do PRD pelo responsável em 2026-10-01.

| ID | Decisão | Alternativas descartadas e motivo | Impacto no PRD | Status |
|---|---|---|---|---|
| DP-01 | **Permissão `cortesia.conceder`, concedida ao financeiro**; o administrador não herda | Reaproveitar `financeiro.ler` (ler ≠ conceder; ampliaria leitura para ato); papel novo "comercial" (fechado em QD-01) | RF-01 | Confirmada |
| DP-02 | **Beneficiário identificado pelo e-mail exato da conta de aluno**, com confirmação de e-mail e nome antes de conceder | Lista ou busca aproximada de alunos (expõe dado pessoal em massa ao financeiro, contra RN-16); informar identificador (inviável para quem opera); convidar quem não tem conta (fluxo novo, fora do recorte) | RF-02, RF-03 | Confirmada |
| DP-03 | **Término ao fim do dia**, no fuso da escola (Brasília na Fase 1); **mês sem o dia → último dia do mês** (fecha QD-04) | Instante exato, N meses depois (o aluno perde o acesso no meio da tarde e é difícil de comunicar); início do dia seguinte (acesso a menos do que o prometido) | RF-03, RF-05, RF-06 | Confirmada |
| DP-04 | **Vigência por período de 1 a 60 meses inteiros ou vitalícia; motivo obrigatório de 1 a 500 caracteres** | Sem limite de meses (erro de digitação concederia anos); motivo opcional (contra RN-D11) | RF-03 | Confirmada |
| DP-05 | **Cortesia só sobre curso com versão vigente; não exige oferta** | Só cursos com oferta (acoplaria cortesia à vitrine, e cortesia existe justamente sem pedido); curso nunca publicado (direito a algo que não existe) | RF-03 | Confirmada |
| DP-06 | **Várias concessões convivem; a tela avisa da existente, sem impedir** | Bloquear nova cortesia se há ativa (impede renovar); estender a existente (uma não estende a outra, RN-D07) | RF-03, RF-04 | Confirmada |
| DP-07 | **Cortesia não pode ser desfeita pela tela nesta entrega; vitalícia exige confirmação reforçada** | Incluir revogação mínima (invade `CAP-009` e sua semântica de ato definitivo, RN-D13, já decidida fora do recorte) | RF-03; QA-01 | Confirmada |
| DP-08 | **O aluno não é avisado por e-mail nesta entrega**; o fato `acesso-concedido` fica pronto para Notificação | E-mail agora via `CAP-026` (cria modelo de mensagem, finalidade e consentimento antes de existir o consumidor natural) | Fora de escopo; RF-07 | Confirmada |
| DP-09 | **A decisão devolve pode/não pode, o término efetivo e, quando nega, o motivo em duas categorias** (*sem direito* · *vigência encerrada*) | Só booleano (`CAP-007` não conseguiria dizer ao aluno que o acesso venceu, nem quando) | RF-06 | Confirmada |
| DP-10 | **`acesso-expirado` sai em até uma hora do término**; a decisão nunca depende dele | Emissão exata no segundo do término (exige agendamento fino para um fato informativo) | RF-07 | Confirmada |

**Herdadas e não reabertas:** cortesia é do financeiro (QD-01, RN-D11); direito é a fonte única,
com consulta síncrona, cache de até 30 s e falha fechada (RN-D01, BA07); vigência em meses inteiros
(RN-O08), contada da concessão (RN-D04); a vigência por compra chega congelada no pedido (RN-D03);
cortesia é ato auditado com motivo obrigatório (RN-D11); várias concessões convivem (RN-D07); só
conta de aluno recebe concessão (RN-D09); a ordem de compra fica para `CAP-011`.

---

## Restrições Técnicas de Alto Nível

- Serviços com mudança: `commerce` (módulo Matrícula/`Entitlement`, com schema, contratos e casos de
  uso próprios desde o primeiro commit, como o baseline ressalva — é o primeiro candidato a extração),
  `identity` (permissão nova; confirmação de que o e-mail é de conta de aluno, devolvendo só o mínimo
  de RF-02), `audit` (um tipo de ato novo, aditivo, como em `CAP-005` e `CAP-003`),
  `bff-admin`/`admin-spa` (área Cortesias e rótulo na trilha).
- Os consumidores da decisão — `media` e `learning` — **não mudam nesta entrega**; passam a consultá-la
  em `CAP-007` e `CAP-017`.
- A lista de cursos publicados que o financeiro vê deve vir do que Conteúdo já comunica, sem nova
  dependência síncrona entre serviços de domínio (precedente OD56); a forma é da TechSpec.
- A decisão de acesso está no caminho crítico da reprodução: precisa tolerar leitura frequente e
  responder dentro do cache de 30 s sem nunca virar réplica (G11).
- Ordem de implantação: `audit` com o tipo novo **antes** de Matrícula comunicar o primeiro ato, senão
  o ato chega como não conforme (precedente de `CAP-005` e `CAP-003`).

---

## Não-Objetivos (Fora de Escopo)

- Conceder acesso por compra, pedido, assinatura ou turma (`CAP-011`, `CAP-013`/`CAP-014`, `CAP-010`).
- Revogar, suspender ou restaurar acesso, por qualquer causa (`CAP-009`).
- Estender ou editar uma concessão existente.
- Tela do aluno sobre o próprio acesso, "meus cursos", "você já tem este curso" (`CAP-007`, `CAP-017`,
  PRD seguinte de `CAP-003`).
- Diagnóstico de concessões para o suporte (`CAP-025`).
- Aviso ao aluno por e-mail ou outro canal (Notificação).
- Cortesia em lote, importação de planilha, cortesia para quem ainda não tem conta.
- Condicionar o acesso a pré-requisito, a nível ou à liberação de aulas (DE04, DE05, G20).
- Limite de dispositivos ou de sessões simultâneas (BA16, G24).
- Concessão a ator interno; o acesso do professor ao que autora vem da permissão de autoria (RN-D09).

---

## Plano de Rollout Faseado

### MVP (Fase 1)

- **Funcionalidades incluídas:** RF-01 a RF-08.
- **Critério para liberar `CAP-007` e `CAP-017`:** uma cortesia real concedida pelo financeiro sem
  ajuda do time; a decisão devolvendo "pode" enquanto vale e "não pode" depois do término; o ato na
  trilha.

### Depois desta entrega

- `CAP-007` e `CAP-017` passam a consultar a decisão.
- `CAP-011` acrescenta a concessão por compra sobre o mesmo modelo, com a vigência congelada no pedido.
- `CAP-009` acrescenta suspensão, restauração e revogação, inclusive para cortesia concedida por engano.

---

## Métricas de Sucesso

| Métrica | Definição | Alvo | Prazo |
|---|---|---|---|
| Cortesia sem ato na trilha | Concessões de origem *cortesia* sem ato `cortesia-concedida` correspondente | 0 | Desde a primeira cortesia |
| Concessão sem origem | Concessões gravadas sem origem identificada | 0 | Contínuo |
| Acesso vencido aceito | Decisões "pode" para concessão cujo término é anterior ao momento da pergunta menos 30 s | 0 | Contínuo |
| Primeira cortesia sem ajuda | Cortesia real concedida pelo financeiro sem ajuda do time | 1 | Antes de iniciar `CAP-007` |
| Tempo para conceder | Da abertura da tela de cortesia à confirmação, mediana | Medido (sem alvo: A1/OD2 em aberto) | Desde a primeira cortesia |

---

## Riscos e Mitigações

- **Cortesia vitalícia ou longa concedida por engano, sem como desfazer** até `CAP-009`. —
  Mitigação: resumo de uma frase antes de confirmar; confirmação reforçada para vitalícia; trilha
  com autor e motivo. Ver QA-01.
- **Beneficiário errado por e-mail digitado.** — Mitigação: a tela mostra e-mail e nome da conta
  antes de seguir e de novo no resumo.
- **Cortesia vira venda informal**, acesso sem pagamento e sem rastro. — Mitigação: permissão só do
  financeiro, motivo obrigatório, ato auditado e consulta pela administração.
- **Dado pessoal exposto ao financeiro pela busca por e-mail** (e-mail e nome de uma conta de aluno). —
  Mitigação: entrada exata, sem lista, sem busca aproximada, devolução mínima, nada em log, URL ou
  métrica.
- **Direito virar flag ou réplica** em outro serviço, "por performance" (DE01). — Mitigação:
  decisão só em Matrícula, cache de até 30 s como aceleração, nunca como fonte (RN-D01, G11).
- **Até 30 s de acesso depois do término**, por causa do cache do consumidor. — Aceito como
  tolerância do baseline (BA07); não há atraso fora dela.

---

## Alternativas Consideradas

### Abordagem Escolhida: cortesia como primeira origem de concessão, com a compra preparada no modelo

- **Descrição:** RF-01 a RF-08. O direito nasce com origem e vigência próprios e já responde à
  decisão; a cortesia o exercita sem gateway.
- **Por que foi escolhida:** mantém `CAP-008` antes da venda (R2), valida `CAP-007` e `CAP-017` sem
  esperar OD5 e não obriga a escrever contra um produtor (Vendas) que ainda não existe.

### Alternativa Rejeitada 1: incluir já a ordem de concessão por compra

- **Trade-offs:** a concessão por compra nasceria com dono desde o início, e `CAP-011` teria só de
  publicar o fato.
- **Por que foi rejeitada:** o nome do fato "compra concluída" é de Vendas, que não tem domain doc; o
  contrato nasceria sem o outro lado e seria refeito em `CAP-011`. O modelo já aceita a origem
  *compra*, que é o que mantém a ordem pretendida.

### Alternativa Rejeitada 2: cortesia como pedido de valor zero

- **Trade-offs:** reaproveitaria o caminho da compra e já teria "pedido" para auditar.
- **Por que foi rejeitada:** reintroduz o risco central — acesso derivado de pedido (DE01) — e
  contraria RN-D11: cortesia não tem pedido. Faria `CAP-008` depender de `CAP-011`, que é o oposto da
  ordem da fila.

---

## Questões em Aberto

- **QA-01 — Desfazer cortesia concedida por engano até `CAP-009`.** **Resolvida em 2026-10-01 com a
  aprovação do PRD:** o risco é aceito, com a confirmação reforçada de DP-07. Erro de cortesia só
  acaba no término, ou nunca, se vitalícia, e o time o corrige por procedimento operacional, fora do
  produto, até `CAP-009` (Fase 2).
- **Fechada neste PRD:** QD-04 (DP-03). O domain doc de Matrícula deve absorver o fechamento na
  próxima revisão dele.

**Para outro momento (2026-10-01):** cortesia em lote e para quem ainda não tem conta, e cursos
públicos (acesso sem compra nem concessão por aluno), podem vir a ser necessários. Nenhum entra nesta
entrega; cada um terá o próprio PRD e, no caso de curso público, decisão sobre a fronteira de Matrícula
e Catálogo.

**Revisão 1.1 (2026-10-01), errata de nome:** a área do backoffice passa de **Acessos** para **Cortesias** (RF-01 e Experiência do Usuário). "Acessos" e a rota `/acessos` já são da gestão de acesso interno do administrador (CAP-002); duas áreas com o mesmo nome confundiriam quem acumula papéis (D-01 da TechSpec). Nenhum comportamento, requisito ou critério muda.
