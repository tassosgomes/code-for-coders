---
tsg_artifact: prd
product: code-4-coders
capability: CAP-005
version: 1.0
status: approved
updated: 2026-09-28
sources: backlog/capabilities.md@1.4, vision.md@1.2, context/domain-map.md@1.2, context/architecture-baseline.md@1.2, domains/conteudo-e-curriculo/domain.md@1.0, domains/entrega-de-midia-e-protecao/domain.md@1.0, domains/auditoria-e-conformidade/domain.md@1.2, domains/identidade-e-acesso/domain.md@1.1, tasks/prd-acesso-interno/prd.md@1.1, tasks/prd-ingestao-midia/prd.md@1.0
---

# Autoria de curso — o professor monta o currículo e publica

## Visão Geral

O professor já envia vídeos e os vê ficarem prontos (`CAP-006`), mas eles ainda não formam um curso:
não há onde organizar módulos e aulas, nem como dizer "este é o curso que o aluno vai percorrer".
Esta entrega preenche a área **Autoria** do backoffice, hoje reservada (`CAP-002`): o professor cria
o curso, organiza módulos e aulas na ordem pedagógica, escolhe o vídeo pronto de cada aula e
**publica uma versão**. Depois, pode corrigir e republicar sem quebrar quem já está no curso.

Por baixo, cada publicação é um fato com três destinos: a Mídia passa a saber quais vídeos cada aula
usa (a Referência de Uso que a reprodução de `CAP-007` confere), a Auditoria grava o ato, e os
domínios que vêm depois — Catálogo (`CAP-003`) e Aprendizagem (`CAP-017`) — têm uma estrutura
publicada e estável sobre a qual trabalhar.

Esta entrega **não** mostra nada ao aluno. Ela termina quando um curso com módulos, aulas e vídeo
está publicado, auditado e referenciado na Mídia.

---

## Rastreabilidade

### Capacidade e fronteiras

- **Capacidade:** `CAP-005` — Autoria e publicação de curso.
- **Escopo desta entrega (primeiro PRD de `CAP-005`):** criar curso; estruturar módulos e aulas em
  ordem pedagógica; vincular vídeo pronto da escola a cada aula; publicar versão imutável, com uma
  única versão vigente; editar o curso publicado e republicar; excluir curso nunca publicado;
  comunicar a versão publicada; registrar o ato na Auditoria; registrar as Referências de Uso na
  Mídia; telas de autoria no backoffice.
- **Fora desta entrega:**
  - Material Complementar (envio, guarda, vínculo e entrega) → segundo PRD de `CAP-005` (OD43);
  - aula sem vídeo — artigo, texto, só links → revisão do domain doc antes do PRD que a introduzir
    (QC-03);
  - despublicar ou retirar curso de circulação, excluir curso publicado → depois do MVP (OD45);
  - encaixe de avaliação no currículo → com `CAP-020` (RN-C16);
  - preço, nível, pré-requisito, vigência e vitrine → `CAP-003` (RN-C14);
  - o que o aluno vê da estrutura e o efeito de nova versão no progresso → `CAP-017`;
  - assistir ao vídeo, inclusive a pré-visualização do professor → `CAP-007`.
- **Domínios atravessados:**
  - Conteúdo e Currículo (dono) — [domain.md](../../../domains/conteudo-e-curriculo/domain.md);
  - Entrega de Mídia e Proteção — [domain.md](../../../domains/entrega-de-midia-e-protecao/domain.md),
    pela consulta de vídeo pronto e pelo registro da Referência de Uso;
  - Auditoria e Conformidade — [domain.md](../../../domains/auditoria-e-conformidade/domain.md), pelo
    ato de publicação;
  - Identidade e Acesso — [domain.md](../../../domains/identidade-e-acesso/domain.md), pela permissão de
    autoria.
- **Juntas entre os domínios:**
  - *Conteúdo → Mídia (OD30, OD35).* O vídeo é enviado e preparado na Mídia, sem saber a que aula
    serve. O vínculo é ato de Conteúdo, que só oferece e aceita vídeo que a Mídia declarou pronto.
    Ao publicar, Conteúdo informa por evento quais vídeos cada aula usa; a Mídia guarda o registro
    opaco. Conteúdo é dono do vínculo; Mídia, do registro.
  - *Conteúdo → Auditoria (OD19, OD23).* Conteúdo comunica o ato no envelope da Auditoria; a
    Auditoria grava. O conteúdo do ato é de Conteúdo; a forma, da Auditoria.
  - *Identidade → Conteúdo.* Identidade diz quem é o ator e que permissões o papel dele carrega; o
    resto é decidido aqui.
- **Dependências entre capacidades:** `CAP-005` depende de `CAP-002` (papel de professor) e de
  `CAP-006` (vídeo pronto), ambas `done`. `CAP-003` e `CAP-017` consomem o que esta entrega publica.
- **Restrições do baseline:** `tenant_id` em toda entidade e toda consulta (G07, BA10); publicação de
  fato por outbox, na mesma transação da mudança (G06); a Auditoria só escreve por consumo de evento
  (G13); o serviço dono autoriza o fino a partir das claims, o BFF não é fronteira de confiança
  (RN-18 de Identidade); um salto síncrono no caminho quente (G08).

### Vision Doc

- **Objetivos de negócio atendidos:** `C03` (autoria e conteúdo, mínimo), pelo ciclo "o que se
  aprende" da Fase 1; destrava a promessa do MVP de que "a operação consegue publicar o curso".
- **Restrições globais aplicáveis:** mono-tenant sem decisão irreversível (DE10); proteção é
  dissuasão declarada (BA15).
- **Non-Goals globais respeitados:** não define preço nem identidade visual; não produz nem edita
  vídeo.

### Domain Docs

- **Entidades envolvidas (Conteúdo e Currículo):** Curso, Módulo, Aula, Rascunho, Versão de
  Publicação.
- **Entidades envolvidas (Entrega de Mídia e Proteção):** Mídia, Referência de Uso.
- **Entidades envolvidas (Auditoria e Conformidade):** Ato Administrativo, Registro de Auditoria.
- **Entidades envolvidas (Identidade e Acesso):** Papel, Permissão, só como o que é consultado.
- **Regras de negócio referenciadas:** Conteúdo RN-C01 a RN-C13, RN-C17; Mídia RN-M02, RN-M04,
  RN-M05, RN-M09; Auditoria RN-A02, RN-A03, RN-A05, RN-A06, RN-A08, RN-A14; Identidade RN-12,
  RN-16, RN-18.
- **Regras precisadas por esta entrega:** nome e dono da permissão de edição (RN-C10, DP-02); a
  Referência de Uso vale para a versão vigente e é substituída a cada publicação (RN-M09, DP-04).
- **Eventos produzidos:** `conteudo.versao-publicada`; ato `conteudo.versao-publicada` no envelope
  `auditoria.ato-praticado`.
- **Eventos consumidos:** `midia.ativo-pronto`, `midia.preparacao-falhou` (por Conteúdo);
  `conteudo.versao-publicada` (pela Mídia).

## Termos Canônicos

| Termo | Definição de negócio | Escopo/Fonte |
|---|---|---|
| Curso em rascunho | Curso que nunca foi publicado. O professor edita livremente e pode excluir | Esta entrega |
| Curso publicado | Curso com versão vigente. Continua editável no rascunho, sem afetar o que foi publicado até nova publicação | Esta entrega |
| Alterações não publicadas | Diferença entre o rascunho e a versão vigente de um curso publicado | Esta entrega |
| Nota de versão | Texto opcional do professor sobre o que mudou numa publicação. Não é motivo de auditoria | Domain doc de Conteúdo (RN-C13) |
| Vídeo | Como o backoffice chama a Mídia, igual a `CAP-006` | PRD de `CAP-006` |

---

## Objetivos

1. **Cumprir a prova do passo 6 do backlog:** "um curso com módulos, aulas e mídia é publicado".
2. **Destravar o passo 7:** ao fim desta entrega, `CAP-003` encontra curso publicado para ofertar,
   sem trabalho de autoria pendente.
3. **Deixar `CAP-007` pronta para conferir:** toda versão publicada tem suas Referências de Uso
   registradas na Mídia, sem carga retroativa depois.
4. **Corrigir sem medo:** o professor republica um curso sem que a identidade das aulas mude, que é
   o que protege o progresso quando `CAP-017` existir.
5. **Nenhuma publicação sem rastro:** toda versão publicada aparece na trilha de auditoria.

---

## Histórias de Usuário

- **US-01** — Como **professor**, eu quero criar um curso e organizá-lo em módulos e aulas para que
  o aluno percorra o conteúdo na ordem que eu planejei.
- **US-02** — Como **professor**, eu quero escolher, para cada aula, um vídeo já pronto da escola —
  inclusive gravado por um colega — para montar a aula sem reenviar nada.
- **US-03** — Como **professor**, eu quero saber exatamente o que falta antes de publicar, para não
  descobrir o problema por tentativa e erro.
- **US-04** — Como **professor**, eu quero corrigir um curso já publicado e só mostrar a correção
  quando eu decidir, para não expor alunos a um curso pela metade.
- **US-05** — Como **professor**, eu quero apagar um curso que comecei e abandonei, para a lista
  mostrar só o que é real.
- **US-06** — Como **professor**, eu quero ver quem publicou cada versão e quando, para entender o
  histórico de um curso em que vários colegas mexem.
- **US-07** — Como **administrador**, eu quero ver cada publicação na trilha de auditoria, para saber
  quem mudou o que os alunos veem.
- **US-08** — Como **escola**, eu quero que só quem tem a permissão de autoria altere cursos, e que
  nenhum curso de outra escola apareça, nem por tela nem por chamada direta.

---

## Funcionalidades Principais

### RF-01: Permissão de edição de autoria

**Descrição**: Passa a existir a permissão `autoria.editar`, concedida ao papel **professor**, que
autoriza criar, editar, publicar e excluir curso (DP-02). A permissão `autoria.ler`, que o professor
já tem, abre a área Autoria e permite ver cursos e versões. Ninguém fora desses dois casos vê ou
altera curso. A decisão é tomada pelo serviço dono a partir das claims, não pelo BFF nem pela tela.

**Critérios de Aceitação**:

- **Given** um ator com o papel professor
  **When** as permissões dele são consultadas
  **Then** ele tem `autoria.ler` e `autoria.editar`

- **Given** um ator com `autoria.ler` e sem `autoria.editar`
  **When** abre a área Autoria
  **Then** vê cursos e versões, sem nenhuma ação de alteração disponível, e uma alteração pedida por
  chamada direta é recusada

- **Given** um ator com o papel suporte, financeiro ou administrador, sem o papel professor
  **When** tenta abrir a área Autoria ou chamar qualquer operação de autoria diretamente
  **Then** é recusado, e a área não aparece no menu

- **Given** um professor que teve o papel revogado
  **When** tenta qualquer operação de autoria na sessão que tinha aberta
  **Then** é recusado (RN-17 de Identidade), e os cursos que ele criou ou publicou continuam
  intactos (RN-C02)

**Prioridade**: Must Have

**Rastreabilidade**: Conteúdo RN-C03, RN-C10; Identidade RN-12, RN-16, RN-17, RN-18; DP-03 de `CAP-002`

---

### RF-02: Criar curso

**Descrição**: O professor cria um curso informando título e descrição pedagógica. O curso nasce em
rascunho, pertence à escola e registra o autor original. Descrição pedagógica é o que se aprende no
curso, não texto de venda: preço, nível, pré-requisito e chamada comercial são de `CAP-003`.

**Critérios de Aceitação**:

- **Given** um professor
  **When** cria um curso com título e descrição
  **Then** o curso aparece na lista da escola como *rascunho*, com ele como autor e a data

- **Given** um professor
  **When** tenta criar um curso sem título
  **Then** a criação é recusada com a indicação do campo

- **Given** um curso criado pelo professor A
  **When** o professor B, da mesma escola, abre a lista
  **Then** vê e pode editar o curso de A (RN-C03)

**Prioridade**: Must Have

**Rastreabilidade**: Conteúdo RN-C02, RN-C03, RN-C14; G07

---

### RF-03: Estruturar módulos e aulas

**Descrição**: No rascunho, o professor cria, renomeia, reordena e remove módulos e aulas. Pode mover
uma aula de um módulo para outro. Toda aula está em um módulo e todo módulo em um curso, cada um com
posição única (RN-C01). Renomear, reordenar, mover e trocar o vídeo mantêm a **mesma** aula; só
remover e criar produz aula nova (RN-C07). Cada alteração é salva no rascunho, sem publicar.

**Critérios de Aceitação**:

- **Given** um curso em rascunho
  **When** o professor cria dois módulos e três aulas e reordena as aulas
  **Then** a estrutura exibida reflete exatamente a ordem escolhida, sem posições repetidas

- **Given** uma aula existente
  **When** o professor a renomeia, a move para outro módulo e depois publica
  **Then** a versão publicada identifica aquela aula pelo mesmo identificador que ela tinha antes da
  mudança

- **Given** uma aula removida e outra criada no lugar com o mesmo título
  **When** o curso é publicado
  **Then** a nova aula tem identificador diferente da removida

- **Given** um módulo com aulas
  **When** o professor o remove
  **Then** a plataforma pede confirmação, informando quantas aulas saem junto

**Prioridade**: Must Have

**Rastreabilidade**: Conteúdo RN-C01, RN-C05, RN-C07

---

### RF-04: Vincular vídeo pronto à aula

**Descrição**: Em cada aula, o professor escolhe um vídeo entre os **prontos da escola**, vendo
título, duração e quem enviou (consulta de `CAP-006`, RF-08 daquela entrega). Pode trocar o vídeo
depois. Vídeo recebido, em preparação ou que falhou não aparece para escolha. Vídeo de outra escola
nunca é aceito, mesmo que o identificador chegue por chamada direta. Conteúdo confere o estado por
uma visão própria, alimentada pelos fatos de pronto e de falha da Mídia (OD35).

**Critérios de Aceitação**:

- **Given** vídeos da escola nos estados pronto, em preparação e falhou
  **When** o professor abre o seletor de vídeo da aula
  **Then** só os prontos aparecem, com título, duração e autor

- **Given** um vídeo que acabou de ficar pronto
  **When** o professor abre o seletor
  **Then** o vídeo aparece sem que o professor precise fazer nada além de abrir ou atualizar a tela

- **Given** um identificador de vídeo em preparação, que falhou, inexistente ou de outra escola
  **When** chega a pedido de vínculo por chamada direta
  **Then** o vínculo é recusado e a aula fica como estava

- **Given** uma aula com vídeo
  **When** o professor troca o vídeo
  **Then** a aula continua a mesma (RF-03) e passa a apontar o novo vídeo no rascunho

**Prioridade**: Must Have

**Rastreabilidade**: Conteúdo RN-C04, RN-C07; Mídia RN-M04, RN-M05; OD30, OD35

---

### RF-05: Lista de cursos da escola

**Descrição**: A área Autoria abre na lista de cursos da escola. Cada linha mostra título, estado
(*rascunho*, *publicado* ou *publicado com alterações não publicadas*), número da versão vigente
quando houver, e quem editou por último e quando. Estado é dito em texto, nunca só por cor.

**Critérios de Aceitação**:

- **Given** um curso nunca publicado, um publicado sem mudanças e um publicado com rascunho alterado
  **When** o professor abre a lista
  **Then** cada um aparece com o estado correspondente, e os publicados com o número da versão
  vigente

- **Given** cursos de outra escola
  **When** qualquer ator abre a lista ou consulta um curso pelo identificador
  **Then** esses cursos não aparecem e a consulta responde como se não existissem

**Prioridade**: Must Have

**Rastreabilidade**: Conteúdo RN-C02, RN-C05; G07

---

### RF-06: Publicar versão

**Descrição**: O professor publica o curso. Antes, a plataforma confere se o currículo está completo
— ao menos um módulo, todo módulo com ao menos uma aula, toda aula com vídeo vinculado (RN-C11) — e,
se não estiver, lista **o que falta, aula por aula**, sem publicar. Na publicação o professor pode
escrever uma nota de versão, opcional. A publicação cria uma Versão de Publicação imutável, com
número sequencial por curso, autor e momento; ela passa a ser a **única vigente** (RN-C08). A versão,
o fato da publicação (RF-10) e o ato de auditoria (RF-11) nascem juntos: ou os três acontecem, ou
nenhum.

**Critérios de Aceitação**:

- **Given** um curso com um módulo sem aula e uma aula sem vídeo
  **When** o professor pede para publicar
  **Then** nada é publicado e a tela mostra as duas pendências, indicando módulo e aula

- **Given** um curso completo em rascunho
  **When** o professor publica, com ou sem nota
  **Then** o curso passa a *publicado*, versão 1, com ele como autor da publicação e o momento

- **Given** uma versão publicada
  **When** qualquer ator tenta alterá-la
  **Then** não há como: mudanças só acontecem no rascunho (RN-C06)

- **Given** o professor que clica duas vezes em publicar, ou uma repetição do mesmo pedido
  **When** a plataforma processa
  **Then** existe uma única versão nova, não duas

- **Given** dois professores que publicam o mesmo curso quase ao mesmo tempo
  **When** a plataforma processa
  **Then** as versões recebem números distintos e sequenciais, e a vigente é a última publicada

**Prioridade**: Must Have

**Rastreabilidade**: Conteúdo RN-C05, RN-C06, RN-C08, RN-C11, RN-C13; G06

---

### RF-07: Editar curso publicado e republicar

**Descrição**: Num curso publicado, o professor continua editando no rascunho, que parte da versão
vigente. Enquanto não republica, o que foi publicado não muda para ninguém. Ao republicar, a nova
versão substitui a anterior para todos (RN-C08), com as mesmas validações de RF-06. Aulas mantidas
conservam o identificador (RN-C07); aula removida sai da versão vigente. O professor pode também
**descartar as alterações** e voltar o rascunho à versão vigente.

**Critérios de Aceitação**:

- **Given** um curso publicado na versão 1
  **When** o professor renomeia uma aula e não republica
  **Then** a versão vigente continua a 1, com o título antigo, e o curso aparece como *publicado com
  alterações não publicadas*

- **Given** o mesmo curso
  **When** o professor republica
  **Then** a versão 2 passa a vigente, a aula renomeada tem o mesmo identificador da versão 1, e a
  versão 1 continua consultável no histórico, inalterada

- **Given** uma aula removida no rascunho
  **When** o curso é republicado
  **Then** a aula não faz parte da versão vigente

- **Given** alterações não publicadas
  **When** o professor as descarta, confirmando
  **Then** o rascunho volta a ser igual à versão vigente

**Prioridade**: Must Have (republicar) · Should Have (descartar alterações)

**Rastreabilidade**: Conteúdo RN-C05 a RN-C09; OD41

---

### RF-08: Histórico de versões

**Descrição**: O curso mostra suas versões publicadas: número, quem publicou, quando e a nota de
versão, quando houver. A vigente é indicada.

**Critérios de Aceitação**:

- **Given** um curso publicado três vezes, por dois professores
  **When** o histórico é aberto
  **Then** aparecem as três versões com número, autor, momento e nota, e a 3 como vigente

**Prioridade**: Should Have

**Rastreabilidade**: Conteúdo RN-C02, RN-C06

---

### RF-09: Excluir curso nunca publicado

**Descrição**: Curso em rascunho que nunca foi publicado pode ser excluído, com confirmação. Curso
que já teve qualquer versão publicada não tem exclusão (OD45). A exclusão de rascunho não é ato
administrativo: não afeta aluno, ator interno nem dinheiro, e não vai à trilha.

**Critérios de Aceitação**:

- **Given** um curso nunca publicado
  **When** o professor o exclui, confirmando
  **Then** o curso some da lista

- **Given** um curso com versão publicada
  **When** o professor procura excluir, pela tela ou por chamada direta
  **Then** a ação não está disponível na tela e é recusada na chamada

**Prioridade**: Must Have

**Rastreabilidade**: QC-02 do domain doc de Conteúdo; OD45; Auditoria (definição de Ato Administrativo)

---

### RF-10: Comunicar a versão publicada

**Descrição**: Toda publicação comunica o fato `conteudo.versao-publicada`, com o curso, o número da
versão e a estrutura completa da versão — módulos e aulas em ordem, com seus identificadores
estáveis, e o vídeo de cada aula. É o fato do qual Mídia (RF-12), Aprendizagem, Catálogo e
Inteligência de Negócio partem. O fato não carrega dado pessoal: o autor aparece por referência de
identidade, nunca por nome ou e-mail. Um consumidor que receba o mesmo fato duas vezes, ou uma versão
mais antiga depois de uma mais nova, consegue reconhecer isso pelo número da versão.

**Critérios de Aceitação**:

- **Given** uma publicação concluída
  **When** o fato é observado
  **Then** contém curso, escola, versão, momento, autor por referência e a estrutura completa com os
  identificadores de módulo, aula e vídeo

- **Given** uma publicação que não se concluiu
  **When** o fato é procurado
  **Then** não existe (RF-06: versão e fato nascem juntos)

- **Given** qualquer fato publicado
  **When** é inspecionado
  **Then** não contém nome, e-mail nem outro dado pessoal

**Prioridade**: Must Have

**Rastreabilidade**: Conteúdo RN-C12, RN-C13; G06; G23

---

### RF-11: Ato de publicação na trilha de auditoria

**Descrição**: Toda publicação comunica o ato `conteudo.versao-publicada` no envelope
`auditoria.ato-praticado`, com autor e alvo (o curso) por referência. **Não há motivo obrigatório**
(OD42); a nota de versão não é motivo e não vai para a trilha como tal. O ato chega à trilha como
conforme e aparece na consulta do administrador (segundo PRD de `CAP-030`).

**Critérios de Aceitação**:

- **Given** uma publicação concluída, com ou sem nota
  **When** o administrador consulta a trilha
  **Then** encontra um registro do ato de publicação com autor, curso e momento, marcado como conforme

- **Given** o mesmo ato comunicado mais de uma vez
  **When** a Auditoria o recebe
  **Then** existe um único registro (RN-A03)

**Prioridade**: Must Have

**Rastreabilidade**: Conteúdo RN-C13; Auditoria RN-A02, RN-A03, RN-A05, RN-A08, RN-A14; OD19, OD23, OD42

---

### RF-12: Mídia registra as Referências de Uso

**Descrição**: A Mídia consome `conteudo.versao-publicada` e registra, para cada vídeo da versão, a
Referência de Uso com o curso e a aula que o usam, tratados como identificadores opacos. As
referências de um curso **são as da versão vigente**: uma nova versão substitui as anteriores, e a
aula removida deixa de ser referência válida (DP-04). Nada é exposto ao aluno nesta entrega; é o que
`CAP-007` confere antes de abrir sessão (RN-M10).

**Critérios de Aceitação**:

- **Given** a versão 1 de um curso, com o vídeo V na aula A
  **When** a Mídia processa o fato
  **Then** V tem a Referência de Uso (curso, A)

- **Given** a versão 2 do mesmo curso, sem a aula A e com V na aula B
  **When** a Mídia processa o fato
  **Then** V tem a referência (curso, B) e a referência (curso, A) deixa de valer

- **Given** a versão 1 chegando depois da versão 2
  **When** a Mídia processa o fato atrasado
  **Then** as referências continuam as da versão 2

- **Given** o mesmo fato entregue duas vezes
  **When** a Mídia processa
  **Then** as referências são as mesmas de uma entrega só

- **Given** o registro da Mídia
  **When** é inspecionado
  **Then** não contém título de curso ou de aula, só os identificadores (RN-M02)

**Prioridade**: Must Have

**Rastreabilidade**: Mídia RN-M02, RN-M09, RN-M10; Conteúdo RN-C12; OD30

---

## Experiência do Usuário

**Persona.** Professor em ciclo de produção: envia os vídeos em lote (`CAP-006`) e depois monta o
curso. Às vezes monta a aula com o vídeo de um colega. Publica quando o curso fica completo e
republica para corrigir.

**Fluxo principal.** Autoria → *Novo curso* (título, descrição) → editor do curso → cria módulos e
aulas, arrasta para reordenar, abre cada aula e escolhe o vídeo no seletor → *Publicar* → se faltar
algo, a janela lista as pendências com link para cada aula; se estiver completo, mostra o que será
publicado e o campo opcional de nota → confirma → o curso aparece como *publicado, versão 1*.

**Republicação.** Num curso publicado, o editor avisa que as alterações ficam no rascunho até nova
publicação. O estado *publicado com alterações não publicadas* aparece no editor e na lista, com a
ação *Descartar alterações*.

**Reordenação.** Arrastar deve ter alternativa por teclado (mover para cima/baixo, mover para outro
módulo). Remover módulo ou aula pede confirmação.

**Linguagem.** Nenhuma tela promete proteção contra cópia (RN-M15). O editor não tem campo de preço,
nível ou pré-requisito; se o professor procurar, a ajuda contextual diz que isso é da oferta.

**Design.** Fluxo já usado no backoffice: wireframe ASCII → Figma → aprovação → código, sobre o
design system do backoffice.

**Acessibilidade.** Estados em texto, não só cor; reordenação operável por teclado; pendências de
publicação anunciadas a leitor de tela e focáveis.

## Decisões de Produto

| ID | Decisão confirmada | Alternativas descartadas e motivo | Impacto no PRD | Registro |
|---|---|---|---|---|
| DP-01 | **Republicação entra neste PRD** | Deixá-la para o segundo PRD: o curso publicado congelaria, e um erro de digitação exigiria outro curso. A regra (RN-C07/C08) já estava decidida; fica para `CAP-017` só o efeito no progresso | RF-07, RF-08 | — |
| DP-02 | **Uma permissão, `autoria.editar`, cobre editar e publicar**, concedida ao professor; `autoria.ler` continua abrindo a área | Editar e publicar separadas: prepararia revisão editorial, mas cria permissão sem uso distinto hoje | RF-01 | DP-03 de `CAP-002` |
| DP-03 | **Excluir curso nunca publicado entra**; curso publicado não tem exclusão | Sem exclusão: rascunhos abandonados poluem a lista | RF-09 | QC-02 do domain doc; OD45 |
| DP-04 | **A Mídia registra as Referências de Uso já nesta entrega**, e elas são as da versão vigente | Deixar para `CAP-007`: exigiria carga retroativa dos cursos já publicados, com eventos que podem não estar mais no broker | RF-12 | RN-M09 |

Herdadas e não reabertas: curso da escola, todo autor edita (OD40); nova versão vale para todos, com
aula estável (OD41); publicação sem motivo obrigatório (OD42); material no segundo PRD (OD43); edição
concorrente por processo interno (OD44); sem despublicar no MVP (OD45); seletor de vídeo e validação
por visão local (OD35).

---

## Restrições Técnicas de Alto Nível

- Três serviços com mudança: o dono de Conteúdo e Currículo, `media` (consumidor de RF-12) e
  `bff-admin`/`admin-spa` (telas). A Auditoria não muda (RN-A14).
- Toda consulta e alteração é isolada por escola (G07); curso de outra escola responde como
  inexistente.
- Edição concorrente não é controlada pelo produto nesta entrega (OD44): o que foi gravado por último
  prevalece, e a escola combina quem edita o quê.

---

## Não-Objetivos (Fora de Escopo)

- Material complementar em qualquer parte do ciclo (OD43).
- Aula sem vídeo: artigo, texto, só links (QC-03).
- Despublicar, retirar de circulação ou excluir curso publicado (OD45).
- Preço, nível, pré-requisito, capa de vitrine, oferta (`CAP-003`).
- Encaixe de avaliação no currículo (`CAP-020`).
- Qualquer tela do aluno e o efeito de nova versão no progresso (`CAP-017`).
- Assistir ao vídeo, inclusive pré-visualização no editor (`CAP-007`).
- Fluxo de revisão ou aprovação editorial antes de publicar.
- Aviso ou bloqueio de edição concorrente (OD44).
- Avisar alunos de que saiu nova versão.

---

## Plano de Rollout Faseado

### MVP (Fase 1)

- **Funcionalidades incluídas:** RF-01 a RF-12.
- **Critério para liberar `CAP-003`:** um curso real, com ao menos um módulo e aulas com vídeo, está
  publicado, com o ato na trilha e as Referências de Uso registradas na Mídia.

### Depois desta entrega

- **Segundo PRD de `CAP-005`:** Material Complementar (OD43).
- **Revisão do domain doc:** aula sem vídeo (QC-03), antes do PRD que a introduzir.

---

## Métricas de Sucesso

| Métrica | Definição | Alvo | Prazo |
|---|---|---|---|
| Publicação sem rastro | Versões publicadas sem ato correspondente na trilha | 0 | Desde a primeira publicação |
| Referência órfã | Versões publicadas cujas Referências de Uso não estão registradas na Mídia | 0 | Desde a primeira publicação |
| Aula que muda de identidade | Aulas mantidas entre versões que aparecem com identificador novo | 0 | Desde a primeira republicação |
| Primeiro curso publicado | Curso real da escola publicado pelo professor sem ajuda do time | 1 | Antes de iniciar `CAP-003` |

---

## Riscos e Mitigações

- **Professor publica curso pedagogicamente incompleto** (aula de teste, título provisório). A
  validação só confere a estrutura. — Mitigação: republicar é barato (RF-07), e nenhum aluno vê o
  curso antes de existir oferta (`CAP-003`).
- **Dois professores sobrescrevem o trabalho um do outro** (OD44). — Mitigação: processo interno
  combinado; "quem editou por último" na lista e no editor torna a sobreposição visível. Revisitar
  se acontecer.
- **Professor procura no editor preço ou nível**, que a visão atribui a ele. — Mitigação: ajuda
  contextual apontando a oferta; o desenho de `CAP-003` decide como o professor sugere esses dados.

---

## Alternativas Consideradas

### Abordagem Escolhida: rascunho editável + versão publicada imutável, vigente única

- **Descrição:** RF-01 a RF-12.
- **Por que foi escolhida:** separa o que o professor mexe do que o aluno vê (RN-C05), dá histórico
  sem apagar nada (RN-C06) e mantém a aula estável para o progresso (OD41).

### Alternativa Rejeitada 1: edição direta no curso publicado, sem versão

- **Trade-offs:** mais simples para o professor, que vê a mudança na hora.
- **Por que foi rejeitada:** o aluno veria o curso pela metade durante a edição; sem versão, a
  Referência de Uso e o ato de auditoria não têm momento definido.

### Alternativa Rejeitada 2: primeira publicação agora, republicação no segundo PRD

- **Trade-offs:** PRD menor.
- **Por que foi rejeitada:** DP-01.

---

## Questões em Aberto

- **QP-01 — Rótulo do novo tipo de ato na consulta da trilha.** A consulta de `CAP-030` foi
  desenhada com os atos de gestão de acesso. Precisa confirmar que o filtro por tipo e o rótulo em
  português aceitam `conteudo.versao-publicada` sem mudança na Auditoria, ou se o rótulo exige
  ajuste pequeno no `admin-spa`. → TechSpec desta entrega. Impacto se não resolvido: o ato aparece
  com o código em vez do nome.
- **QP-02 — Limites de tamanho** (título, descrição, número de módulos e aulas, nota de versão). →
  TechSpec, com base no maior curso real previsto pela escola. Não bloqueia.
- **QP-03 — Como Catálogo e Aprendizagem leem a estrutura vigente** (fato, leitura ou os dois). A
  junta está no domain doc; o formato é do contrato. → `tsg-flow-contract-creator` desta entrega,
  sem consumidor implementado ainda.
