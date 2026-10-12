---
status: in_progress
task_kind: enabling
blocked_by: []
gate: 'rg -q "^> \*\*Status:\*\* aprovado \(ASCII( e Figma)?\) em [0-9]{4}-[0-9]{2}-[0-9]{2}" docs/design/wireframes-transcricao.md'
gate_expect: "exit 0: o cabeçalho de docs/design/wireframes-transcricao.md registra a aprovação do ASCII pelo responsável"
---

# 1.0 Wireframe ASCII da transcrição (tela Vídeos e página da aula) aprovado

**Fatia:** EN-01 (ASCII) · **Cobre:** RF-02, RF-03, RF-04, RF-05, RF-08, RN-TL01, RN-TL02, RN-TL04, RN-TL05, RN-TL06, RN-TL08 (desenho) · **Spec:** `techspec.md#habilitadores-inevitáveis` · **ADR:** —

## Comportamento

Existe o documento `docs/design/wireframes-transcricao.md`, no formato de `docs/design/wireframes-acesso-interno.md`
(cabeçalho com `> **Status:**`, inventário do que o PRD entrega em tela, uma seção numerada por tela,
desktop e celular, texto exato de cada mensagem). Depois da aprovação explícita do responsável, o
cabeçalho traz a linha `> **Status:** aprovado (ASCII) em AAAA-MM-DD`, que é o que o gate confere.

**Tela Vídeos do `admin-spa`** (sobre o desenho aprovado em `docs/design/wireframes-videos.md`,
sem reabri-lo):

- **Estado da transcrição na linha**, ao lado do estado da preparação e das tags: os quatro estados
  do professor (sem transcrição, transcrevendo, pronta, falhou), mais o caso "refeita em andamento"
  com a anterior ainda legível, e a refeita que falhou com a anterior vigente.
- **Ações:** *Transcrever* (sem transcrição) e *Refazer* (pronta ou falhou); quando nenhuma é
  oferecida (vídeo não pronto, falha por limite, recurso desligado), o que a linha mostra no lugar.
- **Motivos de falha**, um texto por motivo, em linguagem de quem produz o vídeo: `duration-limit`
  ("vídeo mais longo que o limite de 2 horas", PRD RN-TL06), `size-limit`, `service-unavailable`,
  `processing-failed`, `timed-out`. Os dois de limite deixam claro que Refazer não resolve.
- **Leitura somente leitura:** a superfície que abre a transcrição (painel ou diálogo), lista de
  trechos com tempo, sem busca, sem clique para o vídeo, sem edição; o aviso de que há uma nova em
  andamento; o erro de carga e o estado "ainda não há transcrição pronta".
- **Atualização automática:** como o estado muda sozinho na linha, sem piscar nem perder o foco.
- Navegação por teclado e leitor de tela das ações e do estado.

**Página da aula do `student-spa`** (sobre `docs/design/wireframes-aula.md`, sem reabri-lo):

- **Painel de transcrição:** posição na página em desktop e celular, lista de trechos com o tempo,
  trecho atual destacado, acompanhamento automático que pausa quando o aluno rola e a ação "ir para
  o trecho atual" que o retoma.
- **Busca:** campo, destaque da ocorrência no texto original, contagem, anterior e próxima
  (circular), nenhum resultado, texto só com espaços (busca vazia), limpar.
- **Clique no trecho:** o que o aluno vê quando a sessão de reprodução ainda não existe ou venceu.
- **Estados do painel:** carregando, transcrição indisponível com a mensagem neutra do PRD (sem
  confundir "ainda não disponível" com "você não tem acesso", RN-TL05), erro 403 e 503 com as
  mensagens que a aula já usa.
- **Legenda no player:** o controle de ligar e desligar (só existe com transcrição pronta), a
  aparência da legenda, a convivência com os controles do player e com a **marca d'água em tela
  cheia**, e a decisão entre faixa nativa do navegador e camada própria (Q-G da TechSpec).
- Navegação por teclado e leitor de tela do painel, da busca e do controle de legenda.

O desenho reaproveita componentes e tokens existentes (`DESIGN.md`, `docs/design/Components.md`).

## Fora do escopo desta task

- Figma → 2.0. Nenhum código de tela.
- Edição da transcrição, exportação, download e compartilhamento: fora do PRD (a proteção é dissuasão, risco R8).

## Decisões fechadas

- Texto do aluno e do professor em português do Brasil, linguagem de quem usa (PRD, Experiência do Usuário).
- A leitura do professor é somente leitura (RN-TL08); a busca e o clique existem só para o aluno (RN-TL01, RN-TL02).
- Legenda desligada por padrão, preferência por dispositivo (RN-TL04).
- Sem transcrição pronta não há botão de legenda e o player funciona como hoje (RN-TL05).
- Os cinco motivos de falha são os do contrato (`api-contract.md`); a lista não cresce no design.

## Modificar / Referenciar

- **modificar:** nenhum arquivo existente; o documento `docs/design/wireframes-transcricao.md` é o produto desta task e nasce aqui.
- **ref:** `docs/design/wireframes-acesso-interno.md` (formato e registro de aprovação); `docs/design/wireframes-videos.md`; `docs/design/wireframes-aula.md`; `DESIGN.md`; `docs/design/Components.md`; `prd.md` (Experiência do Usuário, RF-02 a RF-05, RF-08); `techspec.md` (Bloco Frontend, Q-G); `api-contract.md` e `api-contract-student.md`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| Documentação | `—` | Sem check automatizado para documento de design; o gate estático é a evidência | AGENTS.md (não há lint de Markdown no CI) |

## Pronto quando

- [ ] Gate passa (exit 0).
- [ ] Todas as telas e estados listados estão desenhados em desktop e celular, com o texto exato das mensagens e dos cinco motivos.
- [ ] A decisão de Q-G (faixa nativa ou camada própria, e a marca d'água em tela cheia) está registrada no documento.
- [ ] O responsável aprovou o ASCII, e a aprovação está registrada no cabeçalho com a data.
