# Wireframes ASCII — Transcrição e legenda de aula (CAP-032) · proposta para aprovação

> **Status:** aprovado (ASCII e Figma) em 2026-10-12 · implementação não iniciada
> **Figma:** [Índice](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=347-922) · frames por tela na [seção 8](#8-handoff-do-figma--aprovado) · arquivo Code4Coders — Design System, página `Screens — Transcrição`.
> **Objetivo:** desenhar, antes de qualquer código, o estado e as ações da transcrição na tela **Vídeos** do
> `admin-spa` (incluindo a leitura somente leitura) e o **painel de transcrição, a busca e a legenda** na
> página da aula do `student-spa`. Aprovado este ASCII → desenho no Figma (task 2.0) → aprovação →
> código de tela (tasks 5.0, 8.0, 9.0, 10.0 e 11.0).
> **Fontes:** `tasks/archive/prd-transcricao-legenda-aula/prd.md` 1.1 (RF-02 a RF-08, RN-TL01 a RN-TL08, DP-01 a DP-10,
> Experiência do Usuário), `techspec.md` 1.0 (Bloco Frontend, Q-G), `api-contract.md` (vídeos, transcrição,
> motivos de falha), `api-contract-student.md` (transcrição da aula, legenda), `docs/design/wireframes-videos.md`
> (tela Vídeos aprovada, **não reaberta**), `docs/design/wireframes-aula.md` (página da aula aprovada, **não
> reaberta**), `docs/design/wireframes-acesso-interno.md` (formato do documento e do registro de aprovação),
> `DESIGN.md`, `docs/design/Components.md`.

---

## 1. Inventário

### 1.1 O que o PRD entrega em tela

| Entrega | RF | Tem tela? | Observação |
|---|---|---|---|
| Disparo automático e pedido do professor | RF-01, RF-02 | **Sim** (professor) | O vídeo novo nasce "Transcrevendo" sem ação de ninguém; o professor ganha **Transcrever** e **Refazer** |
| Estado da transcrição por vídeo | RF-02 | **Sim** (professor) | Quatro estados + refeita em andamento + refeita que falhou, junto do estado da preparação e das tags |
| Motivos de falha | RF-02, RN-TL06 | **Sim** (professor) | Cinco motivos do contrato, um texto cada |
| Atualização automática | RF-02 | **Sim** (professor) | O estado muda sozinho na linha |
| Ler o texto no backoffice | RF-08 | **Sim** (professor) | Diálogo somente leitura: trechos com tempo, sem busca, sem clique, sem edição |
| Legenda no player | RF-03 | **Sim** (aluno) | Botão na barra do player (só com transcrição pronta) e camada de legenda sobre a imagem |
| Transcrição na página da aula | RF-04 | **Sim** (aluno) | Painel com trechos e tempo, trecho atual, acompanhamento e "ir para o trecho atual" |
| Busca na transcrição | RF-05 | **Sim** (aluno) | Campo, destaque, contagem, anterior/próxima, nenhum resultado, limpar |
| Acesso protegido como a reprodução | RF-06 | Indireta | Mensagens de 403/503 iguais às da aula; texto sem ligação com cópia |
| Resultado guardado e sinais | RF-07 | Não | Sem tela (Kibana, task 12.0) |

### 1.2 Telas

| # | Tela | App · rota | RF | Permissão | Estados |
|---|---|---|---|---|---|
| TV1 | Estado da transcrição na linha de V1 | `admin-spa` · `/admin/videos` | RF-02 | `midia.enviar` | sem transcrição · transcrevendo · pronta · falhou · refeita em andamento · refeita que falhou · limite (sem Refazer) · vídeo não pronto · recurso indisponível |
| TV2 | Refazer (AlertDialog) | sobre V1 | RF-02 | `midia.enviar` | confirmação · pedindo |
| TV3 | Ler a transcrição (Dialog, somente leitura) | sobre V1 | RF-08 | `midia.enviar` | carregando · lida · lida com nova em andamento · ainda não há transcrição pronta · erro |
| TV4 | Recusas e falhas do pedido | sobre V1 | RF-02 | `midia.enviar` | 409 · 403 · 404 · 502/504 |
| TA1 | Painel de transcrição | `student-spa` · `/aulas/:lessonId` | RF-04 | sessão do aluno com direito | carregando · pronta · acompanhando · acompanhamento pausado · indisponível · 403 · 503 · erro |
| TA2 | Busca no painel | dentro de TA1 | RF-05 | idem | vazia · resultados · uma ocorrência atual · nenhum resultado · só espaços |
| TA3 | Clique no trecho | dentro de TA1 | RF-04, RF-05 | idem | sessão ativa · sem sessão · sessão vencida · acesso encerrado |
| TA4 | Legenda no player | dentro de A1 (`wireframes-aula.md`) | RF-03 | idem | sem transcrição (sem botão) · desligada · ligada · tela cheia · celular · falha ao carregar |

Rotas, permissões e demais telas de `wireframes-videos.md` (V1 a V8) e `wireframes-aula.md` (A1.a a A1.o)
**não mudam**. Este documento só acrescenta o que está na coluna "Tela".

### 1.3 Decisões de desenho propostas (PRD × TechSpec × contrato × DS)

| # | Ponto | Origem | Proposta no wireframe |
|---|---|---|---|
| G1 | Onde fica o estado da transcrição na linha | PRD, UX do professor | **Coluna nova "Transcrição"** entre *Estado* e *Autor* (desktop); bloco próprio no `VideoCard` (celular). O estado da preparação e as tags ficam onde estão |
| G2 | Estado com cor e texto, nunca só cor | PRD, Acessibilidade | `TranscriptionBadge` = `Badge` + ícone + texto: *Sem transcrição* (secondary, `file-minus`), *Transcrevendo* (info, `loader` girando — parado com `prefers-reduced-motion`), *Pronta* (success, `check`), *Falhou* (destructive, `alert-triangle`) |
| G3 | Quais ações aparecem | Contrato: `canStart`, `readable`, `status` | **Transcrever** quando `status = not-requested` e `canStart`; **Refazer** quando `canStart` e `status` é `ready` ou `failed`; **Ler** quando `readable`. Nada de botão desabilitado: quando a ação não existe, a célula explica em texto |
| G4 | Refazer e a transcrição vigente | RN-TL07, DP-09 | Refazer mantém **Ler** e a vigente; a célula diz "A anterior continua valendo para os alunos" |
| G5 | Falha por limite | RN-M24, RN-TL06 | `duration-limit` e `size-limit` **não têm Refazer** e dizem que refazer não resolve, com o que fazer (enviar um vídeo menor) |
| G6 | Refazer pede confirmação só quando há o que substituir | Proposta | `AlertDialog` quando `readable = true` (troca o texto dos alunos ao ficar pronta); sem confirmação para Transcrever e para Refazer depois de falha sem vigente. **Decisão D-02** |
| G7 | Atualização automática | RF-02, G7 de `wireframes-videos.md` | A linha "● Atualizando automaticamente" passa a valer também com vídeo "Transcrevendo". Mudança de estado anunciada **uma vez por vídeo** em região `aria-live="polite"`, sem piscar e sem mover o foco |
| G8 | Leitura do professor | RF-08, RN-TL08 | `Dialog` grande somente leitura. Sem campo de busca, sem clique que leve a vídeo, sem selo de edição; tempo à esquerda, texto à direita |
| G9 | Painel do aluno: posição | PRD, UX do aluno | Desktop: a coluna direita (360) passa a ter **duas abas** — *Transcrição* e *Aulas do curso* — ao lado do vídeo. Celular: as mesmas abas abaixo do player. **Decisão D-03** (aba que abre primeiro) |
| G10 | Trecho atual não depende só de cor | PRD, Acessibilidade | Fundo `bg-secondary` + barra lateral + ícone ▶ + `aria-current="true"` |
| G11 | Acompanhar sem roubar o foco | PRD, Acessibilidade | A lista rola sozinha até o trecho atual **sem mover o foco**. Rolagem manual (roda, toque, teclado na lista) pausa o acompanhamento e mostra **Ir para o trecho atual** |
| G12 | Busca filtra e destaca | RF-05, RN-TL01 | Com busca ativa a lista mostra **só os trechos que contêm o texto**; ocorrência destacada com `mark` (fundo + sublinhado), ocorrência atual com contorno extra. Com busca ativa o acompanhamento fica desligado |
| G13 | Contagem | RF-05 | "2 resultados" ao digitar; depois de *Próxima/Anterior*, "1 de 2 resultados". Circular |
| G14 | Texto selecionável | RF-04, RN-M15 | O texto do trecho se seleciona e copia como em qualquer página; **nenhuma frase da tela afirma proteção contra cópia** |
| G15 | Ausência de transcrição tranquila | RN-TL05 | "A transcrição desta aula ainda não está disponível." — mesma frase para em andamento, falhou e não pedida; sem alerta vermelho, sem "erro", sem falar em acesso; o vídeo toca normalmente |
| G16 | **Q-G — legenda: camada própria** | TechSpec Q-G | **Recomendação: camada própria** sobre o contêiner do player, alimentada pelo mesmo WebVTT (faixa de texto em modo oculto + evento de troca de trecho). Fundamento na seção 5.4. Alternativa descrita lá. **Decisão D-01** |
| G17 | Legenda e marca d'água | RF-03, DP-03 de reprodução | Em tela larga a legenda ocupa uma **faixa fixa acima da faixa inferior da marca d'água**, que por sua vez fica acima da barra de controles: nunca se sobrepõem, a legenda não "pula" quando a marca troca de zona. No celular fora de tela cheia a legenda vai **abaixo da imagem** |
| G18 | Preferência de legenda | RN-TL04, D-08 | Desligada por padrão; ligar/desligar fica no dispositivo (sem sincronizar). Texto de ajuda do botão: "Legenda" |
| G19 | Texto do aluno nunca cita a máquina | PRD, UX do aluno | Nenhuma tela do aluno contém "Whisper", "serviço", "fila", "áudio", "IA" ou "automática". Estados de ausência não distinguem em andamento de falha |
| G20 | Erro de carga da transcrição | TechSpec (403/503/401) | 403 e 503 usam **as frases que a aula já usa** (A1.f e A1.h); qualquer outro erro: "Não foi possível carregar a transcrição agora." + *Tentar de novo*, sem tirar o vídeo do ar |
| G21 | Sessão encerrada tira o texto | RF-06 | Quando a reprodução para por direito encerrado (A1.m), o painel de transcrição **some junto** e a legenda para; só a lista de aulas fica |

### 1.4 Decisões que o responsável precisa tomar (com recomendação)

| # | Decisão | Opções | Recomendação |
|---|---|---|---|
| **D-01** | **Q-G · Legenda: faixa nativa do navegador ou camada própria; convivência com a marca d'água em tela cheia** | (a) Faixa nativa de texto do `<video>`; (b) **camada própria** sobre o contêiner do player | **(b)** camada própria, faixa fixa acima da faixa da marca d'água (seção 5.4). Motivos: o player tem controles e marca próprios que o navegador desconhece; a posição, o tamanho e o contraste da faixa nativa variam por navegador e não garantem "nunca cobrir controles nem marca" (RF-03); o contêiner já é quem entra em tela cheia (G6 de `wireframes-aula.md`). Custo: o texto da legenda fica `aria-hidden` (o texto completo está no painel) |
| **D-02** | Refazer pede confirmação? | (a) nunca; (b) só quando há transcrição vigente (substitui o texto dos alunos); (c) sempre | **(b)** — evita refazer sem querer uma transcrição que já serve, e não atrapalha quem refaz depois de uma falha |
| **D-03** | Aba que abre primeiro no painel da aula | (a) *Aulas do curso* (como hoje); (b) *Transcrição* quando há transcrição pronta | **(b)** — o recurso precisa ser descoberto; sem transcrição pronta, abre *Aulas do curso* e a aba *Transcrição* mostra a frase neutra (G15). A escolha vale só para a abertura da aula |
| **D-04** | Painel da aula em abas ou como terceira coluna | (a) abas na coluna direita; (b) terceira coluna em tela larga | **(a)** — 1440 px não comporta três colunas sem espremer o player; (b) empurra o player abaixo de 560 px |
| **D-05** | Leitura do professor: Dialog ou Sheet | (a) Dialog 720; (b) Sheet lateral | **(a)** — igual aos demais diálogos da tela Vídeos (V2, V6, V8); no celular vira tela cheia |
| **D-06** | Mensagem quando o recurso está desligado no ambiente (`canStart = false`, vídeo pronto, nunca pedido) | (a) texto "Transcrição indisponível neste ambiente."; (b) nada na célula | **(a)** — não deixa a célula vazia sem explicação (G3) |

---

## 2. Fluxos do usuário

### 2.1 Professor: estado, pedido e leitura

```text
  ┌──────────────┐  vídeo fica pronto (RF-01)
  │ Vídeo novo   │──────────────────────────────┐
  └──────────────┘                              ▼
                                      ┌───────────────────┐
  vídeo antigo, nunca transcrito ───▶ │ (Sem transcrição) │──[ Transcrever ]──┐
                                      └───────────────────┘                   │
                                                                              ▼
                                      ┌───────────────────┐   sozinho    ┌──────────────────┐
                                      │ (Transcrevendo)   │◀─────────────│ pedido aceito    │
                                      │ ● atualizando     │              │ (202, estado real)│
                                      └───┬───────────┬───┘              └──────────────────┘
                              pronta      │           │ falhou
                                          ▼           ▼
                           ┌───────────────────┐   ┌───────────────────────────────┐
                           │ (Pronta)          │   │ (Falhou) + motivo             │
                           │ [ Ler ][ Refazer ]│   │ limite → sem Refazer          │
                           └───┬────────────┬──┘   │ demais → [ Refazer ]          │
                       Ler ▼   │            │      └───────────────┬───────────────┘
                  TV3 leitura  │ Refazer    │                      │ Refazer
                               ▼ (TV2 confirma)                    ▼
                           (Transcrevendo) com a anterior legível  (Transcrevendo)
                               │  fica pronta → troca o texto dos alunos (sem republicar)
                               │  falha → (Falhou) com a anterior ainda vigente
```

### 2.2 Aluno: transcrição, busca, clique e legenda

```text
  Aula aberta (A1) ── em paralelo: aula · sessão de reprodução · transcrição
         │
         ├─ transcrição pronta ──▶ botão [CC] na barra do player + painel (aba Transcrição)
         │        │
         │        ├─ vídeo toca ──▶ trecho atual destacado, lista acompanha
         │        │       └─ aluno rola ──▶ acompanhamento pausa + [ Ir para o trecho atual ]
         │        ├─ digita na busca ──▶ lista filtrada + destaque + "2 resultados" + ‹ ›
         │        ├─ clica num trecho ──▶ sessão ativa: vídeo vai ao início do trecho e toca
         │        │                       sem sessão: abre a sessão pelo caminho normal e começa ali
         │        └─ liga [CC] ──▶ legenda sobre o vídeo (dispositivo lembra)
         │
         ├─ em andamento / falhou / não pedida ──▶ painel: "A transcrição desta aula ainda não está
         │                                          disponível." · sem botão de legenda · vídeo toca
         ├─ 403 ──▶ mesma frase da aula · 503 ──▶ mesma frase da aula + [ Tentar de novo ]
         └─ direito encerrado na renovação ──▶ vídeo para, legenda para, painel some (G21)
```

---

## 3. Layout base e componentes

### 3.1 Base

Tela Vídeos: AppShell do backoffice, tabela e `VideoCard` aprovados em `wireframes-videos.md` (V1.a a V1.k).
Tela da aula: AppShell do aluno, `LessonPlayer`, `PlayerControls`, `WatermarkTag`, `LessonList` aprovados em
`wireframes-aula.md` (A1.a a A1.o). Os desenhos abaixo mostram **só o que muda**.

### 3.2 Componentes do DS usados

`Badge` · `Button` (default, outline, ghost icon, link, `sm`) · `Alert` (info, destructive) · `AlertDialog` ·
`Dialog` · `Skeleton` · `Sonner` · `Tabs` · `Input` (busca, com ícone e botão limpar) · `Tooltip` · `Table` ·
`EmptyState`.

**Composições novas propostas (sujeitas à regra de três usos de `Components.md`):**

- `TranscriptionBadge` = `Badge` + ícone + texto, quatro variantes (G2).
- `TranscriptionCell` = badge + linha de apoio (motivo, "a anterior continua valendo", data) + ações (G3).
- `TranscriptReader` = lista somente leitura de `TranscriptLine` (tempo + texto) dentro do `Dialog` de TV3.
- `TranscriptPanel` = `Tabs` + `TranscriptSearch` + lista de `TranscriptSegment` com rolagem própria.
- `TranscriptSegment` = botão de linha inteira: tempo (`font-mono`) + texto; estados repouso, atual, achado, achado atual.
- `CaptionToggle` = botão da barra do player com `aria-pressed`.
- `CaptionLayer` = camada de legenda do contêiner do player (`aria-hidden`).

**Legenda dos desenhos:** `[ Botão ]` · `_link_` · `( ! )` Alert · `[____]` Input · `(Estado)` Badge ·
`✎` editar título · `🏷` editar tags · `[CC]` botão de legenda · `▓` Progress · `⟨e-mail⟩` marca d'água ·
`▶` trecho atual · `░` Skeleton.

---

## 4. Wireframes — tela Vídeos (`admin-spa`)

### TV1 · Estado da transcrição na linha

```text
TV1.a  Tabela com a coluna Transcrição (desktop 1440; AppShell, filtros e paginação como em V1.a)
┌────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ Título                  Estado          Transcrição                      Autor      Enviado                │
│────────────────────────────────────────────────────────────────────────────────────────────────────────────│
│ Aula 4 — Testes de      (◌ Em           — Começa sozinha quando o        Rafael     26/09   ✎ 🏷           │
│ integração              preparação)       vídeo ficar pronto.            Silva      11:02                  │
│                                                                          (você)                            │
│────────────────────────────────────────────────────────────────────────────────────────────────────────────│
│ Aula 3 — Injeção de     (✓ Pronto)      (✓ Pronta)                       Júlia      25/09   ✎ 🏷           │
│ dependência             42:15           Pronta em 12/10 14:30            Lima       18:40                  │
│ [Docker]                                [ Ler ] [ Refazer ]                                                │
│────────────────────────────────────────────────────────────────────────────────────────────────────────────│
│ Aula 5 — Docker         (✓ Pronto)      (– Sem transcrição)              Rafael     12/10   ✎ 🏷           │
│ Compose                 18:02           [ Transcrever ]                  Silva      09:15                  │
│────────────────────────────────────────────────────────────────────────────────────────────────────────────│
│ Aula 6 — Banco de       (✓ Pronto)      (◌ Transcrevendo)                Júlia      12/10   ✎ 🏷           │
│ dados                   31:40           Pode levar um tempo. Você não    Lima       10:40                  │
│                                         precisa ficar nesta tela.                                          │
│────────────────────────────────────────────────────────────────────────────────────────────────────────────│
│ Aula 7 — Refatoração    (✓ Pronto)      (◌ Transcrevendo)                Rafael     11/10   ✎ 🏷           │
│                         55:20           A anterior continua valendo      Silva      16:20                  │
│                                         para os alunos.                                                    │
│                                         [ Ler ]                                                            │
│────────────────────────────────────────────────────────────────────────────────────────────────────────────│
│ Aula 8 — Deploy         (✓ Pronto)      (⚠ Falhou)                       Júlia      10/10   ✎ 🏷           │
│                         27:09           Serviço de transcrição           Lima       08:05                  │
│                                         indisponível no momento.                                           │
│                                         [ Refazer ]                                                        │
│────────────────────────────────────────────────────────────────────────────────────────────────────────────│
│ Aula 9 — Workshop       (✓ Pronto)      (⚠ Falhou)                       Rafael     09/10   ✎ 🏷           │
│ gravado                 2:30:00         Vídeo mais longo que o limite    Silva      15:32                  │
│                                         de 2 horas. Refazer não resolve:                                   │
│                                         envie um vídeo mais curto.                                         │
│────────────────────────────────────────────────────────────────────────────────────────────────────────────│
│ aula-2-rascunho         (⚠ Falhou)      —                                Júlia      24/09   ✎ 🏷           │
│                         Duração acima                                    Lima       09:30                  │
│                         de 3 horas.                                                                        │
└────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
Colunas (1112 px de conteúdo): Título 260 · Estado 140 · Transcrição 280 · Autor 120 · Enviado 90 · ações 80 (✎ 🏷, como hoje).
A coluna Transcrição nunca corta texto: quebra para a linha seguinte e cresce a linha.
Cada célula já diz o nome do campo ao leitor de tela: "Transcrição: pronta, em 12/10 às 14:30".
```

```text
TV1.b  Motivos de falha e o que cada um oferece (texto exato; sem código técnico na tela)

  Código do contrato     Texto na célula                                                  Ação
  ─────────────────────  ───────────────────────────────────────────────────────────────  ────────────
  duration-limit         Vídeo mais longo que o limite de 2 horas. Refazer não resolve:   nenhuma
                         envie um vídeo mais curto.
  size-limit             Vídeo maior que o tamanho aceito para transcrição. Refazer não   nenhuma
                         resolve: envie um vídeo menor.
  service-unavailable    Serviço de transcrição indisponível no momento.                  [ Refazer ]
  processing-failed      Não foi possível transcrever este vídeo.                         [ Refazer ]
  timed-out              A transcrição demorou mais que o limite.                         [ Refazer ]
  (valor novo no futuro) Não foi possível transcrever este vídeo.  (cai em processing-failed) [ Refazer ]

Os dois motivos de limite vêm com `canStart = false` no contrato: a ausência de Refazer vem do servidor,
a tela não decide por conta própria.
```

```text
TV1.c  Casos combinados com a transcrição anterior (RN-TL07)

  Pronta, ninguém refez:      (✓ Pronta)  Pronta em 12/10 14:30       [ Ler ] [ Refazer ]
  Refazendo (vigente existe): (◌ Transcrevendo)  A anterior continua valendo para os alunos.   [ Ler ]
  Refeita que falhou:         (⚠ Falhou)  Serviço de transcrição indisponível no momento.
                              A anterior continua valendo para os alunos.         [ Ler ] [ Refazer ]
  Refeita que falhou por limite (ex.: vídeo trocado/edição futura):
                              (⚠ Falhou)  Vídeo mais longo que o limite de 2 horas. Refazer não
                              resolve: envie um vídeo mais curto. A anterior continua valendo
                              para os alunos.                                      [ Ler ]
  Falhou, nunca houve vigente: só o motivo e [ Refazer ] (ou nada, se for limite)
  Em andamento, sem vigente:   sem [ Ler ] (nada a ler ainda)
```

```text
TV1.d  Vídeo que ainda não está pronto, ou recurso indisponível
  Recebido / Em preparação:   —  Começa sozinha quando o vídeo ficar pronto.
  Falhou na preparação:       —
                              (texto oculto para leitor de tela: "Sem transcrição: o vídeo não ficou pronto")
  Pronto, mas `canStart = false`, nunca pedida (recurso desligado no ambiente, D-06):
                              (– Sem transcrição)  Transcrição indisponível neste ambiente.
  Nenhuma ação aparece em nenhum desses casos.
```

```text
TV1.e  Atualização automática (G7)
  ● Atualizando automaticamente                     ← acima da tabela, enquanto houver vídeo Recebido,
                                                      Em preparação ou com transcrição Transcrevendo
  Quando a transcrição termina, a célula troca de conteúdo no mesmo lugar: sem piscar, sem rolar a
  página, sem mover o foco. Anúncio único, em região aria-live="polite":
     "Aula 6 — Banco de dados: transcrição pronta"
     "Aula 8 — Deploy: a transcrição falhou. Serviço de transcrição indisponível no momento."
  Se o foco estava num botão que continua existindo (ex.: Ler), ele continua nele. Se estava no botão
  que acabou de desaparecer (Transcrever depois do clique), o foco vai para o texto de estado da célula.
```

```text
TV1.f  Mobile 390: VideoCard ganha um bloco "Transcrição" (largura total do cartão)
┌──────────────────────────────────────┐
│ Aula 3 — Injeção de dependência ✎ 🏷 │
│ (✓ Pronto) 42:15                     │
│ [Docker]                             │
│ Júlia Lima · 25/09 18:40             │
│ ──────────────────────────────────── │
│ Transcrição                          │
│ (✓ Pronta)  Pronta em 12/10 14:30    │
│ [ Ler ]  [ Refazer ]                 │
└──────────────────────────────────────┘
┌──────────────────────────────────────┐
│ Aula 7 — Refatoração            ✎ 🏷 │
│ (✓ Pronto) 55:20                     │
│ Rafael Silva · 11/10 16:20           │
│ ──────────────────────────────────── │
│ Transcrição                          │
│ (◌ Transcrevendo)                    │
│ A anterior continua valendo para os  │
│ alunos.                              │
│ [ Ler ]                              │
└──────────────────────────────────────┘
Botões em fila, quebrando para a linha seguinte quando não cabem; alvo de toque de 44 px.
```

```text
TV1.g  Carregando e erro da lista: como V1.d e V1.e (a coluna Transcrição entra no Skeleton da linha).
       Filtros por estado da preparação, busca e tags não mudam; não há filtro por estado da transcrição neste corte.
```

### TV2 · Refazer (AlertDialog) — só com transcrição vigente (G6, D-02)

```text
TV2.a  Confirmar
┌─ AlertDialog ─────────────────────────── ✕ ┐
│ Refazer a transcrição?                (H4) │
│ Aula 3 — Injeção de dependência            │
│                                            │
│ Os alunos continuam vendo a transcrição    │
│ atual até a nova ficar pronta. Quando       │
│ ficar, ela substitui a atual na aula, sem   │
│ precisar publicar o curso de novo.          │
│                                            │
│              [ Cancelar ]  [ Refazer ]     │
└────────────────────────────────────────────┘
TV2.b  Pedindo: [ Refazer ] com spinner e "Pedindo…"; Cancelar e ✕ desabilitados contra clique duplo.
       Sucesso: o diálogo fecha, a célula vira (◌ Transcrevendo), toast "Transcrição pedida".
```

### TV3 · Ler a transcrição (Dialog somente leitura) — RF-08, RN-TL08

```text
TV3.a  Lida (desktop; Dialog 720 × 80% da altura, rolagem dentro do corpo)
┌─ Dialog ─────────────────────────────────────────────────────────── ✕ ┐
│ Transcrição                                                      (H4) │
│ Aula 3 — Injeção de dependência                                       │
│ Pronta em 12/10 14:30 · somente leitura                               │
│ ┌───────────────────────────────────────────────────────────────────┐ │
│ │ 00:00   Olá pessoal, bem-vindos à aula de injeção de dependência. │ │
│ │ 00:07   Hoje vamos ver como desacoplar as classes do projeto.      │ │
│ │ 00:15   Primeiro, o que é uma dependência e por que ela atrapalha. │ │
│ │ 02:10   Introdução ao Docker, só para subir o banco de testes.     │ │
│ │ 14:30   Agora o docker compose com os três serviços.               │ │
│ │ 1:02:15 Fechamos com um resumo do que vimos.                       │ │
│ │ …                                                                  │ │
│ └───────────────────────────────────────────────────────────────────┘ │
│                                                         [ Fechar ]    │
└───────────────────────────────────────────────────────────────────────┘
Tempo: mm:ss abaixo de 1 h, h:mm:ss a partir de 1 h (mesma regra da duração na lista).
Somente leitura: sem campo de busca, sem clique que leve a vídeo, sem editar, excluir, copiar tudo ou baixar.
O texto se seleciona e copia como em qualquer página. A lista é uma região rolável, focável (`tabindex="0"`),
com nome acessível "Texto da transcrição"; a rolagem se faz com as setas e Page Up/Down.
Nada do texto vai a log, URL ou telemetria.

TV3.b  Lida, com uma nova em andamento (RN-TL07)
│ ( i ) Há uma transcrição nova em andamento. Você está lendo a atual, que os alunos continuam vendo.
│       (Alert info, acima da lista)
Quando a nova fica pronta, o diálogo aberto não muda sozinho; ao reabrir, o professor lê a nova.

TV3.c  Carregando: Skeleton de 8 linhas (tempo + texto) no corpo; título e Fechar já visíveis.

TV3.d  Ainda não há transcrição pronta (a leitura foi pedida, mas não há vigente; ex.: tela desatualizada)
│ Ainda não há transcrição pronta para este vídeo.
│ (EmptyState simples, sem ação; Fechar)

TV3.e  Erro de carga
│ ( ! ) Não foi possível abrir a transcrição agora.   [ Tentar de novo ]    (Alert destructive)

TV3.f  Mobile 390: o Dialog ocupa a tela inteira; cabeçalho fixo com ✕; tempo acima do texto de cada linha
       quando a linha não cabe (tempo 12 px muted, texto 16 px).
```

### TV4 · Recusas e falhas do pedido (Transcrever / Refazer)

Todas aparecem como `Alert` destructive na própria célula (mensagem curta) **e** em região `role="alert"`; a
linha é recarregada para mostrar o estado real.

```text
TV4.a  409 VIDEO_NOT_READY             "Este vídeo ainda não está pronto para ser transcrito."
TV4.b  409 TRANSCRIPTION_NOT_RETRYABLE "Refazer não resolve este caso. Envie um vídeo mais curto ou menor."
TV4.c  403 PERMISSION_DENIED           "Você não tem permissão para pedir a transcrição."
       403 CSRF_INVALID                "Sua sessão mudou. Recarregue a página e tente de novo."
TV4.d  404 VIDEO_NOT_FOUND             "Este vídeo não existe mais." (a linha sai da lista na recarga)
TV4.e  401 SESSION_REQUIRED            fluxo de sessão do backoffice (B1 com "Sua sessão foi encerrada.")
TV4.f  502 / 504 MEDIA_UNAVAILABLE     "Não foi possível pedir a transcrição agora. Tente de novo."  [ Tentar de novo ]
                                       (repete o pedido; o pedido repetido nunca cria uma segunda transcrição)
Clique duplo, ou outro professor pedindo ao mesmo tempo: a tela mostra o estado real (Transcrevendo), sem erro.
```

---

## 5. Wireframes — página da aula (`student-spa`)

### TA1 · Painel de transcrição (abas na coluna direita; G9, D-03, D-04)

```text
TA1.a  Pronta, desktop 1440 (reproduzindo; trecho atual acompanhado)
┌──────────────────────────────────────────────────────────────────────────────────────────┐
│ [</>] Code4Coders        Cursos                       [◐ tema]  ( AS ) Ana Souza ▾        │
├────────────────────────────────────────────────────────────────────┬─────────────────────┤
│  _← Voltar para o curso_                                           │ [Transcrição]│ Aulas │
│  Injeção de dependência na prática                            (H1) │ ─────────────────── │
│  ┌─ LessonPlayer (16:9) ────────────────────────────────────┐     │ [🔍 Buscar na aula_] │
│  │                                                           │     │ ─────────────────── │
│  │                        imagem do vídeo                    │     │  11:58  Para usar o  │
│  │                                                           │     │         contêiner…   │
│  │ [❚❚] ━━━━●━━━━━━ 12:04 / 48:20  [🔊] [1x ▾] [CC] [⛶]     │     │ ▶12:04  Agora vamos  │
│  └───────────────────────────────────────────────────────────┘     │ ▌       criar o…     │
│  ( i ) Este conteúdo é de uso pessoal. O seu e-mail aparece        │         ( atual )    │
│        sobre o vídeo durante a aula.                               │  12:19  Repare que o │
│                                                                    │         módulo…      │
│                                                                    │  12:35  Dentro dele…  │
│                                                                    │  …                   │
└────────────────────────────────────────────────────────────────────┴─────────────────────┘
Abas: *Transcrição* e *Aulas do curso* (Tabs do DS; a aba ativa tem texto, sublinhado e `aria-selected`).
O corpo da aba *Transcrição* tem a altura do bloco player + aviso e rola por dentro.
Cada trecho: tempo à esquerda (mono, mm:ss ou h:mm:ss), texto à direita; linha inteira clicável.
Trecho atual: fundo bg-secondary + barra lateral (▌) + ▶ + `aria-current="true"` + texto "atual" para leitor de tela.
```

```text
TA1.b  Mobile 390 (as abas ficam abaixo do player e do aviso)
┌──────────────────────────────────┐
│ [</>] Code4Coders           ☰     │
│ _← Voltar para o curso_          │
│ Injeção de dependência           │
│ na prática                  (H1) │
│ ┌─ LessonPlayer ───────────────┐ │
│ │      imagem do vídeo         │ │
│ │ [❚❚] ━━●━━ 12:04 / 48:20    │ │
│ │ [🔊] [1x ▾] [CC] [⛶]        │ │
│ └──────────────────────────────┘ │
│ ( i ) Este conteúdo é de uso     │
│ pessoal. O seu e-mail aparece    │
│ sobre o vídeo durante a aula.    │
│ [Transcrição]│ Aulas do curso    │
│ [🔍 Buscar na aula__________]    │
│ ┌──────────────────────────────┐ │
│ │ 11:58 Para usar o contêiner… │ │
│ │▶12:04 Agora vamos criar o…   │ │
│ │▌      ( atual )              │ │
│ │ 12:19 Repare que o módulo…   │ │
│ └──────────────────────────────┘ │
│ (altura do painel: 360 px, rola  │
│  por dentro; página rola fora)   │
└──────────────────────────────────┘
```

```text
TA1.c  Acompanhamento pausado pela rolagem manual (G11)
│ [🔍 Buscar na aula__________]    │
│  05:10  O primeiro passo é…      │
│  05:24  Em seguida, instale…     │
│  …(aluno rolou para cima)        │
│                                  │
│        [ ↓ Ir para o trecho atual ]   ← botão flutuante na base do painel, aparece só
└──────────────────────────────────┘      com acompanhamento pausado; foco só vai nele por Tab
Clicar nele: a lista rola até o trecho atual e volta a acompanhar. Anúncio: "Acompanhando o trecho atual".
Rolar por teclado dentro da lista também pausa o acompanhamento.
```

```text
TA1.d  Carregando (aba Transcrição)
│ [🔍 Buscar na aula__________]    │   (campo desabilitado)
│ ░░░░  ░░░░░░░░░░░░░░░░░░░░░░     │
│ ░░░░  ░░░░░░░░░░░░░░             │
│ ░░░░  ░░░░░░░░░░░░░░░░░░         │
│ ░░░░  ░░░░░░░░░░░░░░░░░░░░       │   Região com "Carregando a transcrição…" (aria-live polite)

TA1.e  Indisponível: em andamento, falhou, não pedida ou recurso desligado (RN-TL05, G15)
│ A transcrição desta aula ainda não está disponível.                    │
│ O vídeo toca normalmente.                                              │
Sem ícone de alerta, sem cor de erro, sem campo de busca, sem botão de legenda, sem falar em acesso.

TA1.f  403 (direito negado) — mesma frase de A1.f, dentro do painel
│ Você não tem acesso a este curso.                                      │
TA1.g  503 (decisão indisponível) — mesma frase de A1.h
│ Não foi possível confirmar seu acesso agora.                           │
│ Tente de novo em instantes.     [ Tentar de novo ]                     │
TA1.h  Outro erro (G20)
│ Não foi possível carregar a transcrição agora.   [ Tentar de novo ]    │
O vídeo continua tocando normalmente em TA1.e a TA1.h. 401 segue o fluxo de sessão da aula (entrar e voltar).
```

```text
TA1.i  Direito encerrado na renovação (G21): o painel e a legenda somem junto com o vídeo (A1.m)
│ [Aulas do curso]  ← única aba                         │
Nenhum trecho fica na tela; o texto não é atualizado nem carregado de novo.
```

### TA2 · Busca

```text
TA2.a  Vazia (estado inicial)                  TA2.b  Digitando "docker" — 2 resultados
│ [🔍 Buscar na aula__________]    │            │ [🔍 docker_______________ ✕]   │
│ (lista inteira)                  │            │ 2 resultados   [ ‹ ] [ › ]     │
                                                │  02:10  Introdução ao [Docker], │
                                                │         só para subir o banco…  │
                                                │  14:30  Agora o [docker]        │
                                                │         compose com os três…    │
[Docker] = `mark`: fundo + sublinhado (nunca só cor). Só entram na lista os trechos que contêm o texto.

TA2.c  Depois de "Próxima" — ocorrência atual                TA2.d  "introducao" acha "Introdução" (RN-TL01)
│ 1 de 2 resultados   [ ‹ ] [ › ]                            │ 1 resultado   [ ‹ ] [ › ]
│  02:10  Introdução ao [[Docker]], …                        │  02:10  [Introdução] ao Docker, …
│         ocorrência atual: contorno + negrito               │  (ignora maiúsculas, acentos e pontuação)
Anterior/Próxima são circulares (depois da última, volta à primeira). Enter no campo = Próxima;
Shift+Enter = Anterior; Esc limpa a busca. A lista rola até a ocorrência atual sem mover o foco do campo.
A busca não move o vídeo: só o clique num trecho faz isso.

TA2.e  Nenhum resultado                                      TA2.f  Só espaços = busca vazia
│ [🔍 xyz________________ ✕]                                 │ [🔍 ·····_____________ ✕]
│ Nenhum trecho encontrado para essa busca.                  │ (lista inteira, sem contagem)
│ [ Limpar busca ]
```

```text
TA2.g  Mobile 390
┌──────────────────────────────────┐
│ [🔍 docker__________________ ✕]  │
│ 2 resultados        [ ‹ ] [ › ]  │
│ 02:10 Introdução ao [Docker],…   │
│ 14:30 Agora o [docker] compose…  │
└──────────────────────────────────┘
Teclado do celular: o campo usa `type="search"` e tecla "Buscar"; Anterior/Próxima com alvo de 44 px.
Contagem e mudança de resultado anunciadas em região aria-live="polite" ("2 resultados"; "1 de 2 resultados").
Com busca ativa o acompanhamento automático fica desligado (G12); ao limpar, volta a acompanhar o trecho atual.
```

### TA3 · Clique no trecho (RN-TL02)

```text
TA3.a  Sessão de reprodução ativa
  clique (ou Enter) no trecho "14:30" ──▶ o vídeo vai a 14:30 e toca; o trecho vira o atual; a busca,
  se havia, continua aplicada; o foco permanece no trecho clicado.

TA3.b  Sem sessão ainda (o aluno não tocou o vídeo), ou sessão que venceu
  clique ──▶ a reprodução abre pelo caminho normal e começa naquele ponto
  ┌─ LessonPlayer ───────────────────┐
  │ ▒▒▒▒▒▒▒▒▒▒ (Skeleton do vídeo)   │   enquanto abre: A1.d "Carregando a aula…"
  │ Carregando a aula…               │   (nenhum aviso extra: o aluno pediu para tocar e toca)
  └──────────────────────────────────┘
  Se a abertura falha, o player mostra o estado de sempre (A1.f, A1.g, A1.h, A1.k) e o painel o
  acompanha (TA1.f, TA1.g, TA1.i). Em A1.h/A1.n, [ Tentar de novo ] no player e o clique no
  trecho fazem o mesmo, e este último começa no ponto escolhido.

TA3.c  Vídeo em tela cheia: o painel não aparece (a lista some em tela cheia, G6 de wireframes-aula.md);
       o clique é, portanto, uma ação da página, não do modo tela cheia.
```

### TA4 · Legenda no player

```text
TA4.a  Barra de controles com o botão de legenda (só existe com transcrição pronta)
Ordem de foco: [❚❚] → linha do tempo → [🔊] → [1x ▾] → [CC Legenda] → [⛶]

 Desligada (padrão):   [ CC ]   aria-pressed="false"   nome: "Legenda"   dica: "Ligar a legenda"
 Ligada:               [ CC ]   aria-pressed="true"    realce + sublinhado do ícone (não só cor)
                       dica: "Desligar a legenda"
 Sem transcrição pronta (em andamento, falhou, não pedida): o botão **não existe**; o player é o de hoje (RN-TL05).
 A preferência (ligada/desligada) fica no dispositivo; mudar de aula no mesmo dispositivo mantém a escolha;
 outro dispositivo começa desligada (RN-TL04).
```

```text
TA4.b  Legenda ligada, desktop (camada própria, G16, G17)
┌─ LessonPlayer (16:9) ────────────────────────────────────────────┐
│ ⟨ marina.alves@example.com ⟩          (zona superior: exemplo)    │
│                                                                   │
│                        imagem do vídeo                            │
│                                                                   │
│        ┌───────────────────────────────────────────────┐          │  ← faixa da LEGENDA
│        │  Agora vamos criar o contêiner e                │          │     (centrada, até 2 linhas,
│        │  conferir se a rede está respondendo.           │          │     largura máx. 70% do vídeo)
│        └───────────────────────────────────────────────┘          │
│ ┌───────────────────────────────────────────────────────────────┐ │  ← faixa INFERIOR DA MARCA D'ÁGUA
│ │ ⟨ marina.alves@example.com ⟩                (zonas 3 e 4 aqui)  │ │     (reservada; a marca aparece
│ └───────────────────────────────────────────────────────────────┘ │     nela só quando está em 3 ou 4)
│ [❚❚] ━━━━●━━━━━━ 12:04 / 48:20  [🔊] [1x ▾] [CC] [⛶]              │  ← barra de controles
└───────────────────────────────────────────────────────────────────┘
De baixo para cima: barra de controles → faixa inferior da marca (zonas 3 e 4) → legenda. A legenda ocupa
sempre a mesma faixa, quer a marca esteja na zona 1, 2, 3 ou 4: nunca se sobrepõem, a legenda não pula
quando a marca troca de zona, e nenhuma cobre a barra de controles.
Visual da legenda: texto branco 18 px (20 px em tela cheia) com fundo preto a 75% de opacidade (contraste
mínimo AA sobre qualquer imagem), máximo 2 linhas, quebra por palavra, centralizada; nenhuma sombra
ou animação. Com vídeo pausado a legenda fica na tela com o texto do ponto atual.
```

```text
TA4.c  Tela cheia (desktop e celular em paisagem) — legenda e marca d'água juntas
┌──────────────────────────────────────────────────────────────────────────────────────────┐
│                                                                                          │
│                                   imagem do vídeo                                        │
│                                                                                          │
│             ┌──────────────────────────────────────────────────────┐                     │
│             │  Agora vamos criar o contêiner e conferir se a rede    │                     │
│             │  está respondendo.                                     │                     │
│             └──────────────────────────────────────────────────────┘                     │
│   ⟨ marina.alves@example.com ⟩     (zona inferior esquerda: exemplo após a troca)         │
│   [❚❚] ━━━━━━━━●━━━━━━━━━━ 12:34 / 48:20   [🔊] [1,5x ▾] [CC] [⛶ Sair]                     │
└──────────────────────────────────────────────────────────────────────────────────────────┘
O pedido de tela cheia continua sendo ao contêiner; a camada de legenda mora dentro dele e, portanto, aparece.
A legenda acompanha pausa, avanço, retrocesso e mudança de velocidade (o texto é o do ponto atual do vídeo).
```

```text
TA4.d  Celular 390, fora de tela cheia: a legenda vai ABAIXO da imagem (G17), sem cobrir imagem, marca nem controles
┌─ LessonPlayer ───────────────────┐
│ ⟨ marina.alves@example.com ⟩      │
│        imagem do vídeo           │
│ [❚❚] ━━●━━ 12:04 / 48:20        │
│ [🔊] [1x ▾] [CC] [⛶]            │
├──────────────────────────────────┤
│ Agora vamos criar o contêiner e   │   ← faixa de legenda sob a imagem (fundo bg-inverse,
│ conferir se a rede responde.      │     até 2 linhas de 14 px); só existe com a legenda ligada
└──────────────────────────────────┘
Em tela cheia no celular (paisagem) vale o desenho de TA4.c.
Ligar ou desligar a legenda não recarrega o vídeo nem muda o ponto da aula.
```

```text
TA4.e  Falha ao carregar a legenda (ex.: 404 CAPTIONS_NOT_AVAILABLE, 410 sessão vencida, rede)
  O botão [CC] some da barra e a região de status (aria-live polite) diz, uma vez:
  "A legenda não está disponível agora."
  O player continua tocando; sem alerta vermelho. Se a causa foi sessão vencida, a renovação normal
  da sessão devolve o botão na próxima carga. Direito encerrado: a legenda para e nenhum texto novo aparece (G21).

TA4.f  Renovação de sessão: a legenda continua ligada, sem piscar nem reiniciar (RF-03). Nada na tela muda.
```

### 5.4 Q-G · faixa nativa × camada própria (D-01)

| Critério | (a) Faixa nativa do navegador | (b) Camada própria — **recomendada** |
|---|---|---|
| Marca d'água em tela cheia | A posição da faixa é do navegador (rodapé do vídeo); pode sobrepor a zona inferior da marca ou ficar sob a barra própria | Faixa reservada acima da faixa da marca: nunca se sobrepõem |
| Barra de controles própria | O navegador só reposiciona a faixa quando seus controles nativos estão ativos; aqui não estão | A faixa nasce já acima da barra |
| Contraste e tamanho (RF-03) | Estilo limitado e diferente por navegador | Tokens do DS: AA garantido |
| Celular | Cobre a imagem de 219 px de altura | Vai abaixo da imagem (TA4.d) |
| Leitor de tela | Anuncia cada cue (ruído) ou não, conforme o navegador | `aria-hidden`: o texto completo está no painel (TA1) |
| Esforço | Menor | Maior: camada, mapeamento do trecho atual, testes de posição |

Ponto de partida da TechSpec era a faixa nativa; este desenho recomenda trocar para (b) porque nenhuma
das garantias de RF-03 ("nunca cobre os controles nem a marca d'água") é do navegador. A fonte do texto é
a mesma nas duas: o WebVTT da sessão.

---

## 6. Acessibilidade e teclado

- **Tela Vídeos.** As ações são botões com nome que inclui o vídeo: "Transcrever Aula 5 — Docker Compose", "Refazer Aula 3 — Injeção de dependência", "Ler transcrição de Aula 3 — Injeção de dependência". Estado em texto, nunca só cor. Mudança de estado anunciada uma vez por vídeo (G7). Diálogos devolvem o foco ao botão que os abriu (Ler, Refazer).
- **Leitura do professor.** Região rolável com nome "Texto da transcrição", operável por teclado. Foco inicial no título do diálogo; `Esc` fecha.
- **Painel do aluno.** Abas navegáveis por setas; busca com rótulo visível "Buscar na aula" (não só placeholder); cada trecho é um botão, acionável por **Enter** e **Espaço**, com foco visível; trecho atual com `aria-current="true"`; resultado de busca anunciado; a rolagem automática nunca move o foco.
- **Legenda.** Botão com `aria-pressed` e nome "Legenda"; estado por ícone e sublinhado, além da cor; camada `aria-hidden` (G16); contraste AA.
- **Movimento.** Ícone `loader` parado com `prefers-reduced-motion`; rolagem até o trecho atual sem animação suave nesse caso.
- **Celular.** Alvos de toque de 44 px; sem rolagem horizontal; campo de busca não perde o foco ao filtrar.

---

## 7. Verificação de linguagem

- **Aluno:** nenhuma tela contém "Whisper", "serviço", "fila", "áudio", "IA" ou "automática"; as frases de ausência (G15) são as mesmas para em andamento, falhou e não pedida; nenhuma frase afirma que o conteúdo é protegido contra cópia (RN-M15).
- **Professor:** os cinco motivos usam as frases do contrato (TV1.b); a palavra "serviço" aparece nelas porque o professor precisa do motivo; nenhum código técnico (`duration-limit` etc.) aparece em tela.
- **Texto de aula:** nunca em log, métrica, span, URL nem armazenamento persistente do navegador; a única persistência é a preferência da legenda (ligada ou desligada), sem texto.

---

## 8. Handoff do Figma — aprovado

O desenho foi materializado no arquivo **Code4Coders — Design System** em 2026-10-12, na página `Screens — Transcrição` ([337:2](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=337-2)), sobre o ASCII aprovado (seções 1 a 7), sem reabrir fluxo nem textos. Os frames aprovados de Vídeos (V1.g, V1.h) e de Aula (A1.a, A1.b) foram **clonados**, não alterados. O [índice de revisão](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=347-922).

> **Aprovação do Figma:** aprovado pelo responsável em 2026-10-12, com a divergência F-02 ajustada (seção 8.10). Os frames abaixo são a referência de tela das tasks seguintes.

Tamanhos: desktop em 1440 de largura (altura do conteúdo quando passa de 900); mobile em 390; painéis de estado em 360 × 640; diálogos conforme D-05 (720) e 520. Claro em todas as telas; Dark em TA1.a e TA4.b. Os dados são ilustrativos e o desenho não executa transcrição, busca nem reprodução.

### 8.1 TV1 · Vídeos — coluna e estados da transcrição (`admin-spa`) — [seção 338:9](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=338-9)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| TV1.a · Tabela com a coluna Transcrição (oito linhas de estado) | 1440 × 1144 | [338:17](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=338-17) |
| TV1.b–e · Motivos de falha, casos com a anterior vigente, sem ação e atualização automática | 1440 × 2461 | [339:157](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=339-157) |
| TV1.f · VideoCard com bloco Transcrição — Mobile | 390 × 1404 | [339:41463](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=339-41463) |

### 8.2 TV2 · Refazer — AlertDialog — [seção 338:10](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=338-10)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| TV2.a · Confirmar Refazer | 520 × 246 | [341:353](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=341-353) |
| TV2.b · Pedindo… | 520 × 294 | [341:368](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=341-368) |

### 8.3 TV3 · Ler a transcrição — Dialog somente leitura — [seção 338:11](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=338-11)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| TV3.a · Lida | 720 × 640 | [342:475](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=342-475) |
| TV3.b · Lida, com nova em andamento | 720 × 640 | [342:518](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=342-518) |
| TV3.c · Carregando | 720 × 640 | [342:567](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=342-567) |
| TV3.d · Ainda não há transcrição pronta | 720 × 640 | [342:606](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=342-606) |
| TV3.e · Erro de carga | 720 × 640 | [342:626](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=342-626) |
| TV3.f · Lida — Mobile (tela cheia) | 390 × 844 | [342:649](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=342-649) |

### 8.4 TV4 · Recusas e falhas do pedido — [seção 338:12](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=338-12)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| TV4 · Alertas 409, 403, 404 e 502/504 | 520 × 718 | [341:385](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=341-385) |

### 8.5 TA1 · Painel de transcrição na página da aula (`student-spa`) — [seção 338:13](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=338-13)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| TA1.a · Pronta, trecho atual acompanhado — Desktop Light | 1440 × 900 | [343:368](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=343-368) |
| TA1.b · Pronta — Mobile Light | 390 × 1101 | [343:683](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=343-683) |
| TA1.c · Acompanhamento pausado pela rolagem manual | 360 × 640 | [344:495](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=344-495) |
| TA1.d · Carregando | 360 × 640 | [344:544](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=344-544) |
| TA1.e · Indisponível (em andamento, falhou ou não pedida) | 360 × 640 | [344:577](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=344-577) |
| TA1.f · 403 | 360 × 640 | [344:587](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=344-587) |
| TA1.g · 503 | 360 × 640 | [344:596](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=344-596) |
| TA1.h · Outro erro de carga | 360 × 640 | [344:609](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=344-609) |
| TA1.i · Direito encerrado (só a lista de aulas) | 360 × 640 | [344:621](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=344-621) |

### 8.6 TA2 · Busca — [seção 338:14](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=338-14)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| TA2.a · Vazia | 360 × 640 | [344:686](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=344-686) |
| TA2.b · “docker” — 2 resultados | 360 × 640 | [344:730](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=344-730) |
| TA2.c · Depois de Próxima — 1 de 2, ocorrência atual | 360 × 640 | [344:785](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=344-785) |
| TA2.d · “introducao” acha “Introdução” | 360 × 640 | [344:842](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=344-842) |
| TA2.e · Nenhum resultado | 360 × 640 | [344:885](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=344-885) |
| TA2.f · Só espaços = busca vazia | 360 × 640 | [344:907](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=344-907) |
| TA2.g · Busca — Mobile | 390 × 844 | [344:955](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=344-955) |

### 8.7 TA4 · Legenda no player — [seção 338:15](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=338-15)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| TA4.a · Botão de legenda: desligado, ligado, com foco | 998 × 188 | [345:608](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=345-608) |
| TA4.b · Legenda ligada · marca na zona 1 (superior esquerda) | 1440 × 810 | [345:636](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=345-636) |
| TA4.b · Legenda ligada · marca na zona 2 (superior direita) | 1440 × 810 | [345:710](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=345-710) |
| TA4.b · Legenda ligada · marca na zona 3 (inferior esquerda) | 1440 × 810 | [345:774](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=345-774) |
| TA4.b · Legenda ligada · marca na zona 4 (inferior direita) | 1440 × 810 | [345:838](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=345-838) |
| TA4.c · Tela cheia — legenda e marca d’água juntas (zona 3) | 1440 × 810 | [345:902](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=345-902) |
| TA4.d · Legenda abaixo da imagem — Mobile | 390 × 1091 | [346:773](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=346-773) |
| TA4.e · Falha ao carregar a legenda | 888 × 600 | [345:978](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=345-978) |

### 8.8 Dark mode — validação de tokens — [seção 338:16](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=338-16)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| TA1.a · Painel de transcrição — Dark | 1440 × 900 | [346:967](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=346-967) |
| TA4.b · Legenda ligada, zona 2 — Dark | 1440 × 810 | [346:1043](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=346-1043) |

### 8.9 🧩 Composições propostas — [seção 346:770](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=346-770)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| TranscriptionBadge — status: none · processing · ready · failed | — | [337:23](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=337-23) |
| TranscriptSegment — state: repouso · atual · achado · achado-atual | — | [337:64](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=337-64) |
| CaptionToggle — state: off · on · focus | — | [337:39](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=337-39) |
| CaptionLayer — layout: desktop · tela-cheia · mobile | — | [346:769](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=346-769) |

### 8.10 Divergências em relação ao ASCII — decididas pelo responsável em 2026-10-12

Nenhuma muda fluxo, regra, contrato nem texto fixado em 1.0. Decisão do responsável: F-01, F-03, F-04, F-05, F-06 e F-07 **mantidas como estão**; F-02 **ajustada**.

| # | Ponto | O que o Figma mostra | Decisão |
|---|---|---|---|
| F-01 | Aba ativa do painel | `Tab` do DS (pílula clara), não texto com sublinhado como no ASCII; a aba ativa continua distinta por fundo e texto, e leva `aria-selected` | **Mantida:** `Tab` do DS; vale o mesmo padrão dos filtros de Vídeos |
| F-02 | Selo *Sem transcrição* | `secondary` do DS é lavanda e, ao lado de *Recebido* (também lavanda), os dois se parecem em Vídeos | **Ajustada:** o selo passa a `outline` neutro do DS (fundo `card`, borda `border`, texto e ícone `muted-foreground`), distinto de *Recebido*. Componente `TranscriptionBadge`, variante `status=none` ([337:3](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=337-3)); vale em todos os frames que o usam (TV1.a, TV1.b–e e TV1.f) |
| F-03 | Tela cheia do desktop (TA4.b) | O frame mostra o player em 16:9 (1440 × 810) com as quatro zonas da marca, sem a página ao redor; o ASCII desenhava o player dentro da página | **Mantida:** a página já está em TA1.a e o que importa aqui é a posição da legenda × marca × controles |
| F-04 | Mark da busca | O destaque é fundo `warning-soft` + sublinhado por palavra (o Figma não tem fundo de texto contínuo); a ocorrência atual ganha contorno `ring` e negrito | **Mantida;** no código o `mark` cobre só o trecho achado |
| F-05 | Rótulo do campo de busca | "Buscar na aula" aparece como rótulo acima do campo e o placeholder é "Palavra ou trecho"; o ASCII só mostrava o rótulo dentro do campo | **Mantida:** cumpre o rótulo visível exigido na seção 6 |
| F-06 | Botão de legenda ligado | Fundo `primary` + sublinhado do ícone (o ASCII dizia "realce + sublinhado") | **Mantida** |
| F-07 | Altura de TV1.a | A tabela com oito linhas passa de 900; o frame cresce com o conteúdo (1144) | **Mantida** |

---

## 9. Registro de aprovação

**ASCII aprovado pelo responsável em 2026-10-12**, com as decisões D-01 a D-06 da seção 1.4 nas opções
recomendadas: D-01 (b) camada própria de legenda acima da faixa da marca d'água; D-02 (b) Refazer confirma só com
transcrição vigente; D-03 (b) aba *Transcrição* abre primeiro quando há transcrição pronta; D-04 (a) abas na coluna
direita; D-05 (a) Dialog 720; D-06 (a) "Transcrição indisponível neste ambiente.". A task 2.0 (Figma) está liberada.

**Figma aprovado pelo responsável em 2026-10-12** (task 2.0). Decisões sobre as divergências da seção 8.10: F-01, F-03, F-04, F-05, F-06 e F-07 aceitas como estão; F-02 ajustada, e o selo *Sem transcrição* passou a `outline` neutro do DS (`TranscriptionBadge`, variante `status=none`, [337:3](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=337-3)), distinto de *Recebido* em Vídeos. A aprovação do Figma foi decisão separada da do ASCII. O código de tela das tasks seguintes está liberado.
