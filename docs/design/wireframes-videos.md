# Wireframes ASCII — Vídeos do backoffice (CAP-006)

> **Status:** aprovado (ASCII e Figma) em 2026-09-26 · implementação não iniciada
> **Objetivo:** desenhar no Figma, sobre o Design System Code4Coders e o AppShell do backoffice
> aprovado em CAP-002, as telas da área Vídeos antes da implementação no `admin-spa` (tasks 3.0–9.0 de
> `tasks/prd-ingestao-midia`). Aprovado este ASCII → desenho no Figma (task 2.0) → aprovação → código.
> **Fontes:** `tasks/prd-ingestao-midia/prd.md` (RF-01…RF-07, Experiência do Usuário, DP-01…DP-06),
> `techspec.md` (Bloco Frontend, V-01…V-06), `api-contract.yaml` 1.1.1,
> `docs/design/wireframes-acesso-interno.md` (AppShell, B12, B13 e componentes aprovados),
> `DESIGN.md`, `docs/design/Components.md`.

---

## 1. Inventário

### 1.1 O que o PRD entrega em tela

| Entrega | RF | Tem tela? | Observação |
|---|---|---|---|
| Permissão de envio | RF-01 | Indireta | Item "Vídeos" no menu e card no Início só com `midia.enviar` |
| Enviar vídeo | RF-02 | **Sim** | Dialog de envio + painel de transferência sobre a lista |
| Retomar envio | RF-03 | **Sim** | Aviso de envio incompleto no topo da lista |
| Preparar o vídeo | RF-04 | Indireta | Só o estado na linha; nenhuma ação do professor |
| Estado e motivo da falha | RF-05 | **Sim** | Badge de estado + motivo na própria linha |
| Lista da escola | RF-06 | **Sim** | Tabela com filtro, busca e atualização automática |
| Editar título | RF-07 | **Sim** | Dialog pela linha |
| Consulta para vínculo | RF-08 | Não | Consumida por CAP-005 |
| Fatos pronto/falhou | RF-09 | Não | Sem aviso por e-mail (DP-04) |
| Nada público | RF-10 | Texto | Linguagem "não fica público" (RN-M15) |
| Volume guardado | RF-11 | Não | Telemetria |

### 1.2 Telas (`admin-spa`, base `/admin/`)

| # | Tela | Rota | RF | Permissão | Estados |
|---|---|---|---|---|---|
| V1 | Vídeos (lista) | `/admin/videos` | RF-05, RF-06 | `midia.enviar` | com vídeos · atualizando · vazio · filtro sem resultado · carregando · erro · indisponível |
| V2 | Enviar vídeo (Dialog) | sobre V1 | RF-02, RF-03 | `midia.enviar` | escolher arquivo · arquivo recusado · conferir título · título em branco · iniciando · arquivo diferente do pendente |
| V3 | Transferência (painel) | topo de V1 | RF-02, RF-03 | `midia.enviar` | enviando · reconectando · concluindo · pausado por falha · concluído |
| V4 | Envio incompleto (aviso) | topo de V1 | RF-03 | `midia.enviar` | um pendente · vários pendentes |
| V5 | Sair durante a transferência (AlertDialog) | qualquer navegação interna | RF-02 | — | confirmação |
| V6 | Editar título (Dialog) | sobre V1 | RF-07 | `midia.enviar` | formulário · título em branco · salvando |
| V7 | Início do professor | `/admin/` | RF-01 | sessão | card Vídeos |
| B12 | Sem permissão | `/admin/videos` por link direto | RF-01 | — | reusa B12 de CAP-002 |
| B13 | Erro | qualquer | — | — | reusa B13 de CAP-002 |

### 1.3 Lacunas e decisões de desenho (PRD × TechSpec × contrato × DS)

| # | Ponto | Origem | Proposta no wireframe |
|---|---|---|---|
| G1 | **QP-02 — nome e posição da área** | PRD, Questões em Aberto | Área própria **"Vídeos"**, logo abaixo de Início. Não entra em Autoria: a biblioteca é da escola e independe de curso ou aula (PRD, Abordagem Escolhida; RN-M02). Quando Autoria ganhar tela (CAP-005), as duas passam a um grupo "Conteúdo" na sidebar |
| G2 | Vários envios ao mesmo tempo | TechSpec: a fila de partes vive no componente de envio | **Uma transferência por vez.** Durante a transferência, *Enviar vídeo* fica desabilitado com a dica "Aguarde o envio atual terminar"; a preparação não bloqueia nada |
| G3 | "O título começa preenchido com o nome do arquivo" | RF-02 | Nome do arquivo **sem a extensão** (`aula-3.mp4` → `aula-3`), editável; limite de 200 caracteres com contador |
| G4 | Estado com cor e texto, nunca só cor | PRD, Experiência do Usuário | `VideoStatusBadge`: *Recebido* (secondary, ícone relógio), *Em preparação* (info, ícone girando — parado com `prefers-reduced-motion`), *Pronto* (success, ícone check), *Falhou* (destructive, ícone alerta) |
| G5 | O contrato aceita vários estados no filtro | `listVideos?status=` repetível | Filtro de uma escolha só: **Todos · Em andamento** (recebido + em preparação) **· Prontos · Falharam** |
| G6 | Busca pelo título | RF-06; `q` até 120 caracteres | Campo de busca com espera de 300 ms após digitar; acento indiferente ("injecao" acha "Injeção") |
| G7 | A lista se atualiza sozinha só com vídeo em andamento | TechSpec (10 s, aba visível) | Linha discreta acima da tabela: "● Atualizando automaticamente" só enquanto há vídeo recebido ou em preparação |
| G8 | Formato da duração | `durationSeconds` | `m:ss` abaixo de 1 h, `h:mm:ss` a partir de 1 h: "pronto · 0:20", "pronto · 1:02:15" |
| G9 | Ação por linha | RF-07; excluir está fora do escopo | Um único botão-ícone ✎ "Editar título" (ghost) na linha — sem menu `⋯`, porque não há outra ação |
| G10 | Falhou exige reenviar como vídeo novo | RF-05 | Motivo na segunda linha da célula de estado + link _Enviar de novo_, que abre V2 vazio (não reaproveita o vídeo que falhou) |
| G11 | Sair durante a transferência interrompe o envio | PRD, Experiência do Usuário | Navegação dentro do backoffice → V5 (AlertDialog do DS). Fechar a aba ou recarregar → aviso nativo do navegador (não desenhável; texto fixado pelo navegador) |
| G12 | Linguagem de proteção | RN-M15 | Ajuda fixa em V2: "O vídeo não fica público: ele só é entregue a quem tem acesso à aula." Nenhum "protegido contra cópia" |
| G13 | Retomada escolhendo outro arquivo | RF-03 | V2 mostra Alert info: "Este não é o arquivo do envio incompleto. Ele será enviado como um vídeo novo." O pendente continua no aviso V4 até vencer |
| G14 | Serviço indisponível | Contrato 1.1.1: 502/504 `MEDIA_UNAVAILABLE` | Na lista: Alert destructive + *Tentar de novo*. No envio: V3 "pausado" com *Tentar de novo*, que continua das partes já recebidas |
| G15 | Paginação | `_size` até 50 | 20 por página, `Pagination` do DS |
| G16 | Autor | `uploadedBy` | Nome retratado no envio (C-13); linha do próprio ator ganha "(você)" |
| G17 | Acessibilidade do progresso | PRD, Acessibilidade | `aria-live="polite"` anuncia a cada 10% e na conclusão; mudança de estado na lista anunciada uma vez por vídeo ("Aula 3 — pronto") |

---

## 2. Fluxo do usuário

```
  ┌──────────────┐  midia.enviar   ┌───────────────────────────────────────────────────────────┐
  │ V7 Início    │────────────────▶│ V1 Vídeos  /admin/videos                                  │
  │ card Vídeos  │  item "Vídeos"  │  lista da escola · filtro · busca · atualiza sozinha      │
  └──────────────┘                 └──┬───────────────┬───────────────┬────────────────────┬───┘
                                      │ [Enviar vídeo]│ V4 aviso       │ ✎ na linha         │ link direto
                                      ▼               │ "envio         ▼                    │ sem permissão
                              ┌───────────────┐       │ incompleto"  ┌──────────────┐       ▼
                              │ V2 Enviar     │◀──────┘ [Selecionar  │ V6 Editar    │   B12 Sem
                              │ escolher      │          o arquivo]  │ título       │   permissão
                              │ arquivo       │                      └──────┬───────┘
                              └──┬─────────┬──┘                             │ salvo → toast
                 formato/tamanho │         │ ok                             ▼
                 fora do limite  ▼         ▼                                V1
                        ┌──────────────┐ ┌───────────────┐  mesmo arquivo do pendente:
                        │ V2·recusado  │ │ V2 conferir   │  pula para V3 já com as partes
                        │ (nada é      │ │ título        │  recebidas (ex.: 10/48)
                        │ transferido) │ └──────┬────────┘
                        └──────────────┘        │ [Enviar]
                                                ▼
     ┌──────────────────────────────────────────────────────────────────────────────┐
     │ V3 Transferência (painel no topo de V1; a lista continua usável)             │
     │   enviando ──falha de rede numa parte──▶ reconectando (até 3 tentativas)      │
     │      │                                       │ esgotou / serviço fora         │
     │      │                                       ▼                                │
     │      │                                  pausado [Tentar de novo] ─────────────┤
     │      ▼ todas as partes                                                       │
     │   concluindo ──▶ concluído: toast "aula-3 recebido" + linha nova "Recebido"   │
     │                                                                              │
     │   navegar para fora ──▶ V5 "Sair interrompe o envio"                          │
     │                          [Continuar enviando]   [Sair mesmo assim] → V4 depois │
     └──────────────────────────────────────────────────────────────────────────────┘

  Linha na lista (sem ação do professor):
  Recebido ──▶ Em preparação ──▶ Pronto · 0:20
                     └──────────▶ Falhou · motivo  + _Enviar de novo_ → V2
```

---

## 3. Layout base e componentes

### 3.1 AppShell do backoffice

Reusa o AppShell aprovado em CAP-002 (`Sidebar` 264 + `Topbar` 68 + conteúdo `bg-muted` com `p-8`).
O item **Vídeos** entra logo abaixo de Início (G1), ícone `clapperboard` (lucide), só com
`midia.enviar`. Nada desabilitado ou "trancado" para quem não tem a permissão.

```
┌──────────────────────┬─────────────────────────────────────────────────────────────────────┐
│ [</>] Code4Coders    │                                              ( RS ) Rafael Silva ▾  │
│       Backoffice     ├─────────────────────────────────────────────────────────────────────┤
│ OPERAÇÃO             │                                                                     │
│ ▣ Início             │                                                                     │
│ 🎬 Vídeos  ◀ ativo   │  ← só com midia.enviar (professor)                                  │
│                      │     << conteúdo da página >>                                        │
└──────────────────────┴─────────────────────────────────────────────────────────────────────┘
```

### 3.2 Componentes do DS usados

`Card` · `Form`/`FormField`/`Label`/`Input` · `Button` (default, outline, ghost icon, link) · `Alert`
(default, info, warning, destructive) · `Badge` · `Table` · `ToggleGroup` (filtro) · `Dialog` /
`AlertDialog` · `Progress` · `Skeleton` · `Pagination` · `Sonner` · `Sidebar` · `EmptyState`,
`StatusTile` e `CodeWindow` (aprovados em CAP-002 / conta do aluno).

**Componentes novos propostos:**

- `VideoStatusBadge` = `Badge` + ícone + texto, quatro variantes (G4).
- `FileDropzone` = área de soltar arquivo + botão *Escolher arquivo* + linha de regras (formatos e
  limite); estados normal, arrastando, recusado.
- `UploadProgressPanel` = `Card` com nome do arquivo, `Progress`, volume transferido/total, restante
  estimado e ação; estados enviando, reconectando, concluindo, pausado, concluído.
- `VideoRow` (desktop) / `VideoCard` (mobile) = linha da biblioteca com título, estado, autor, data,
  duração e ✎.

---

## 4. Wireframes

Legenda: `[ Botão primário ]` · `[ Botão outline ]` · `_link_` · `( ! )` Alert · `[____]` Input ·
`(Estado)` VideoStatusBadge · `✎` botão-ícone editar · `▓▓▓░░` Progress · `( • )` ToggleGroup ativo.

### V1 · Vídeos — `/admin/videos` (só `midia.enviar`)

```
V1.a  Com vídeos (desktop 1440)
┌──────────────────────┬─────────────────────────────────────────────────────────────────────┐
│ [</>] Code4Coders    │                                              ( RS ) Rafael Silva ▾  │
│       Backoffice     ├─────────────────────────────────────────────────────────────────────┤
│ ▣ Início             │  VÍDEOS                                                             │
│ 🎬 Vídeos  ◀ ativo   │  Vídeos da escola                               [ ⤒ Enviar vídeo ]  │
│                      │  Envie as gravações das aulas e acompanhe até ficarem prontas.      │
│                      │                                                                     │
│                      │  [ Todos ]( Em andamento )( Prontos )( Falharam )  [🔍 Buscar título_]│
│                      │  ● Atualizando automaticamente                     ← G7, só c/ andamento│
│                      │  ┌───────────────────────────────────────────────────────────────┐  │
│                      │  │ Título                     Estado            Autor     Enviado  │  │
│                      │  │───────────────────────────────────────────────────────────────│  │
│                      │  │ Aula 4 — Testes de         (◌ Em preparação) Rafael    26/09 ✎ │  │
│                      │  │ integração                                   Silva     11:02   │  │
│                      │  │                                              (você)            │  │
│                      │  │ Aula 3 — Injeção de        (✓ Pronto) 42:15  Júlia     25/09 ✎ │  │
│                      │  │ dependência                                  Lima      18:40   │  │
│                      │  │ live-coding-parte-2        (⏱ Recebido)      Rafael    25/09 ✎ │  │
│                      │  │                                              Silva     17:05   │  │
│                      │  │ aula-2-rascunho            (⚠ Falhou)        Júlia     24/09 ✎ │  │
│                      │  │                            Duração acima de  Lima      09:30   │  │
│                      │  │                            3 horas.                            │  │
│                      │  │                            _Enviar de novo_                    │  │
│                      │  └───────────────────────────────────────────────────────────────┘  │
│                      │                                       ‹ 1 2 3 › (Pagination, 20/pág.)│
└──────────────────────┴─────────────────────────────────────────────────────────────────────┘
Ordem: do envio mais recente ao mais antigo. Nenhuma miniatura nem player (reprodução é CAP-007).

Motivos de falha (RF-05, texto exato, sem código técnico):
  unreadable-file      → "Arquivo de vídeo ilegível."
  unsupported-format   → "Formato de vídeo não suportado."
  duration-exceeded    → "Duração acima de 3 horas."
  preparation-failed   → "Não foi possível preparar este vídeo — envie novamente."
  Todos seguidos de _Enviar de novo_ (G10).
```

```
V1.b  Vazio (escola sem vídeo)                  V1.c  Filtro/busca sem resultado
┌─────────────────────────────────────────┐    ┌─────────────────────────────────────────┐
│ VÍDEOS                                  │    │ ( Todos )( Em andamento )[ Falharam ]    │
│ Vídeos da escola      [ ⤒ Enviar vídeo ]│    │ [🔍 injecao______________________ ✕ ]    │
│                                         │    │                                         │
│ ┌─ EmptyState ────────────────────────┐ │    │ Nenhum vídeo com esse título falhou.    │
│ │ ┌─ CodeWindow ─────────── ● ● ● ┐   │ │    │ _Limpar filtros_                        │
│ │ │ $ ls videos/                  │   │ │    └─────────────────────────────────────────┘
│ │ │ (vazio)                       │   │ │
│ │ └───────────────────────────────┘   │ │    V1.d  Carregando: Skeleton de 5 linhas da Table,
│ │ Nenhum vídeo ainda            (H4)  │ │          filtro e busca já visíveis.
│ │ Envie a primeira gravação. Ela fica │ │
│ │ pronta para a aula sozinha.         │ │    V1.e  Erro / serviço indisponível (G14)
│ │ [ ⤒ Enviar vídeo ]                  │ │    ( ! ) Não conseguimos carregar os vídeos agora.
│ └─────────────────────────────────────┘ │          [ Tentar de novo ]    (Alert destructive)
└─────────────────────────────────────────┘

V1.f  Mobile 390: Table vira lista de VideoCard (título, badge, duração, autor · data, ✎);
      filtro vira ToggleGroup com rolagem horizontal; [ ⤒ Enviar vídeo ] em largura total no topo.
┌──────────────────────────────────┐
│ ☰  Vídeos                        │
│ [        ⤒ Enviar vídeo        ] │
│ ( Todos )( Em andamento )( Pro…→ │
│ [🔍 Buscar título______________] │
│ ┌──────────────────────────────┐ │
│ │ Aula 3 — Injeção de       ✎  │ │
│ │ dependência                  │ │
│ │ (✓ Pronto) 42:15             │ │
│ │ Júlia Lima · 25/09 18:40     │ │
│ └──────────────────────────────┘ │
└──────────────────────────────────┘
```

### V2 · Enviar vídeo — Dialog sobre V1

```
V2.a  Escolher arquivo                          V2.b  Arquivo recusado (antes de transferir)
┌─ Dialog ─────────────────────────────── ✕ ┐  ┌─ Dialog ─────────────────────────────── ✕ ┐
│ Enviar vídeo                         (H4) │  │ Enviar vídeo                              │
│                                           │  │                                           │
│ ┌─ FileDropzone ────────────────────────┐ │  │ ┌─ FileDropzone (recusado) ─────────────┐ │
│ │             ⤒                          │ │  │ │ ⚠ aula-completa.mp4 · 6,2 GB          │ │
│ │  Arraste o vídeo para cá               │ │  │ │ O limite é 5 GB. Divida a gravação ou │ │
│ │  [ Escolher arquivo ]                  │ │  │ │ exporte em qualidade menor.           │ │
│ │  MP4, MOV ou MKV · até 5 GB e 3 horas  │ │  │ │ [ Escolher outro arquivo ]            │ │
│ └───────────────────────────────────────┘ │  │ └───────────────────────────────────────┘ │
│ O vídeo não fica público: ele só é        │  │                                           │
│ entregue a quem tem acesso à aula. (G12)  │  │ Formato: "aula.avi não é um formato       │
│                                           │  │ aceito. Envie MP4, MOV ou MKV."           │
│                           [ Cancelar ]    │  │                           [ Cancelar ]    │
└───────────────────────────────────────────┘  └───────────────────────────────────────────┘

V2.c  Conferir título                           V2.d  Título em branco
┌─ Dialog ─────────────────────────────── ✕ ┐  ┌─ Dialog ─────────────────────────────── ✕ ┐
│ Enviar vídeo                              │  │ Título                                    │
│                                           │  │ [_________________________________]       │
│ 🎞 aula-3.mp4 · 3,1 GB    _Trocar arquivo_│  │ ⚠ Dê um título para reconhecer o vídeo.   │
│                                           │  │                        (FormField erro)   │
│ Título                                    │  │ [ Cancelar ]  [ ⤒ Enviar ] (desabilitado) │
│ [aula-3___________________________] 6/200 │  └───────────────────────────────────────────┘
│ Só para reconhecer o vídeo na escola.     │
│ Você pode mudar depois.                   │  V2.e  Iniciando: [ ⤒ Enviar ] com spinner
│                                           │        "Preparando o envio…" (poucos segundos)
│ A duração só é conferida depois do envio: │
│ vídeos acima de 3 horas falham na         │  V2.f  Arquivo diferente do pendente (G13)
│ preparação.                               │  ( i ) Este não é o arquivo do envio incompleto
│                                           │        (aula-3.mp4). Ele será enviado como um
│              [ Cancelar ]  [ ⤒ Enviar ]   │        vídeo novo.            (Alert info)
└───────────────────────────────────────────┘
Ao clicar em Enviar, o Dialog fecha e V3 aparece no topo de V1.
Mesmo arquivo do envio incompleto: não mostra V2.c; vai direto para V3 continuando das partes já recebidas.
```

### V3 · Transferência — painel no topo de V1

Não é modal: a lista continua usável. Uma transferência por vez (G2).

```
V3.a  Enviando
┌─ UploadProgressPanel ──────────────────────────────────────────────────────────────┐
│ ⤒ Enviando aula-3.mp4                                                               │
│ ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓░░░░░░░░░░░░░░░░░░░░  48%                                     │
│ 1,5 GB de 3,1 GB · cerca de 12 min restantes                                        │
│ Não feche esta aba até o envio terminar. Depois, a preparação continua sozinha.     │
└────────────────────────────────────────────────────────────────────────────────────┘

V3.b  Retomado (RF-03)                            V3.c  Reconectando
│ ⤒ Continuando aula-3.mp4                    │  │ ⟳ Conexão instável — tentando de novo… │
│ ▓▓▓▓▓▓░░░░░░░░░░░░░░░░ 21%                  │  │ ▓▓▓▓▓▓▓▓▓▓▓░░░░░░░░░░ 52%               │
│ Retomado de onde parou: 640 MB já estavam   │  │ (Alert warning dentro do painel)        │
│ na escola.                                  │
                                                  V3.d  Concluindo: Progress 100% indeterminado,
V3.e  Pausado (rede caiu de vez ou serviço fora)        "Conferindo o envio…"
│ ⏸ Envio pausado em 67%                                                              │
│ ( ! ) Não conseguimos continuar o envio. As partes já enviadas ficam guardadas até   │
│       27/09 às 14:30.                              [ Tentar de novo ]  (Alert destr.) │

V3.f  Concluído → o painel some; toast "aula-3 recebido. A preparação começou." e a linha nova
      aparece no topo da lista como (⏱ Recebido).
```

### V4 · Envio incompleto — aviso no topo de V1 (RF-03)

Só o próprio professor vê os seus envios incompletos. Não aparece como linha da tabela.

```
V4.a  Um envio incompleto
┌─ Alert warning ─────────────────────────────────────────────────────────────────────┐
│ ⚠ Envio incompleto de aula-3.mp4 — selecione o mesmo arquivo para continuar.        │
│   640 MB de 3,1 GB já estão na escola. Você pode continuar até 27/09 às 14:30.      │
│                                                        [ Selecionar o arquivo ]     │
└─────────────────────────────────────────────────────────────────────────────────────┘

V4.b  Vários: mesmo Alert, uma linha por arquivo (nome · volume · prazo · [ Selecionar ]).
Depois do prazo, o aviso some sozinho; escolher o arquivo recomeça do zero.
```

### V5 · Sair durante a transferência — AlertDialog

```
┌─ AlertDialog ─────────────────────────────────────┐
│ Sair interrompe o envio                      (H4) │
│ aula-3.mp4 está em 48%. O que já foi enviado fica │
│ guardado até 27/09 às 14:30 — volte e selecione o │
│ mesmo arquivo para continuar.                     │
│                                                   │
│         [ Sair mesmo assim ]  [ Continuar enviando ]│
└───────────────────────────────────────────────────┘
Fechar a aba ou recarregar usa o aviso nativo do navegador (G11).
```

### V6 · Editar título — Dialog

```
┌─ Dialog ─────────────────────────────── ✕ ┐
│ Editar título                        (H4) │
│                                           │
│ Título                                    │
│ [Aula 3 — Injeção de dependência__] 32/200│
│ Mudar o título não altera o vídeo.        │
│                                           │
│              [ Cancelar ]  [ Salvar ]     │
└───────────────────────────────────────────┘
Em branco → "Dê um título para reconhecer o vídeo." e Salvar desabilitado.
Salvo → toast "Título atualizado"; a linha mostra o título novo e mantém o estado.
Qualquer vídeo da escola pode ser renomeado, inclusive de colega (DP-03).
```

### V7 · Início do professor — `/admin/`

Substitui o estado B5.b ("Suas ferramentas chegam em breve") para quem tem `midia.enviar`.

```
┌──────────────────────┬─────────────────────────────────────────────────────────────────────┐
│ ▣ Início  ◀ ativo    │  INÍCIO                                                             │
│ 🎬 Vídeos            │  Olá, Rafael                                                  (H2)  │
│                      │  Seus papéis: (Professor)                                           │
│                      │                                                                     │
│                      │  ┌─ Card ──────────────────────────┐                                │
│                      │  │ 🎬  Vídeos                  (H5)│                                │
│                      │  │ Envie as gravações das aulas e  │                                │
│                      │  │ acompanhe a preparação.         │                                │
│                      │  │ [ Abrir vídeos → ]              │                                │
│                      │  └─────────────────────────────────┘                                │
└──────────────────────┴─────────────────────────────────────────────────────────────────────┘
```

### B12 · Sem permissão e B13 · Erro

Reusam os frames aprovados em CAP-002. Em B12, o `CodeWindow` mostra `$ GET /admin/videos` → `403
Forbidden`. Nenhum desenho novo.

---

## 5. Plano para o Figma (após aprovação deste ASCII)

Páginas novas no arquivo `Code4Coders — Design System`, sem mexer nas existentes:

1. **🧭 Fluxo — Vídeos**: o fluxo da seção 2, com os frames das telas como nós.
2. **📱 Screens — Vídeos**: V1–V7, todos os estados, em desktop 1440; mobile 390 para V1.a, V1.b,
   V2.c e V3.a. Tema Light; Dark em V1.a e V3.a.
3. **Components (proposta)**: `VideoStatusBadge`, `FileDropzone`, `UploadProgressPanel`, `VideoRow`,
   `VideoCard`, item de sidebar "Vídeos" e ícone `clapperboard`; `Progress` e `ToggleGroup` do DS se
   ainda não existirem no arquivo.

Tudo reusando o AppShell, `EmptyState`, `Area Card`, `Dialog Header` e demais componentes aprovados em
CAP-002, e as variáveis e text styles do DS, sem valor fixo.

---

## 6. Decisões para você aprovar

1. **QP-02: área própria "Vídeos", abaixo de Início** (G1), fora de Autoria.
2. **Uma transferência por vez**, com painel não modal no topo da lista (G2, V3).
3. **Título inicial = nome do arquivo sem extensão** (G3).
4. **Estados com badge de ícone + texto** e cores secondary / info / success / destructive (G4).
5. **Filtro de uma escolha: Todos · Em andamento · Prontos · Falharam** (G5).
6. **Indicador "Atualizando automaticamente"** só com vídeo em andamento (G7).
7. **Só ✎ na linha**, sem menu `⋯` (G9); **_Enviar de novo_** no vídeo que falhou (G10).
8. **AlertDialog ao sair durante a transferência** + aviso nativo ao fechar a aba (G11).
9. **Envio incompleto como aviso no topo**, nunca como linha da tabela (V4).
10. **Início do professor com card Vídeos** no lugar do "chegam em breve" (V7).
11. **Componentes novos:** `VideoStatusBadge`, `FileDropzone`, `UploadProgressPanel`, `VideoRow`/`VideoCard`.

---

## 7. Figma (aprovado em 2026-09-26)

Base: `https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System`

| Entrega | Página | node-id |
|---|---|---|
| Fluxo do usuário | Fluxo — Vídeos | `112-2` |
| Componentes novos (proposta) | Screens — Vídeos | `109-5072` |
| V1 · Vídeos (a–f, desktop + mobile) | Screens — Vídeos | `109-5677` |
| V2 · Enviar vídeo (a–f + mobile) | Screens — Vídeos | `109-6849` |
| V3 · Transferência (a–f + mobile) | Screens — Vídeos | `109-7395` |
| V4 · Envio incompleto (a, b) | Screens — Vídeos | `109-7867` |
| V5 · Sair interrompe o envio | Screens — Vídeos | `109-8274` |
| V6 · Editar título (a–c) | Screens — Vídeos | `109-8367` |
| V7 · Início do professor | Screens — Vídeos | `109-8972` |
| Dark mode — validação | Screens — Vídeos | `109-9381` |

B12 (Sem permissão) e B13 (Erro) não têm frame novo: reusam `90-2841` e T8.

Componentes propostos (migram para a página Components após aprovação): Icon (9 lucide novos:
clapperboard, upload, pencil, search, rotate-cw, pause, film, loader-circle, triangle-alert),
Video Status Badge, Video Row, Video Card (mobile), File Dropzone, Upload Progress Panel.

Ajustes de desenho em relação ao ASCII, sem mudar comportamento:

- *Em preparação* e *Falhou* usam contorno colorido sobre `card`, como o `Alert` do DS, porque o
  tema não tem tokens "soft" de info e destructive; *Recebido* e *Pronto* usam os tokens soft.
- O filtro por estado usa o `Tab` do DS (não há `ToggleGroup` no arquivo).
- O Início do professor (V7) mostra o card sem contagem, como no ASCII.
