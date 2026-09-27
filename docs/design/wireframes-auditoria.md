# Wireframes ASCII — Auditoria do backoffice (CAP-030, 2º PRD)

> **Status:** aprovado (ASCII e Figma) em 2026-09-27 · implementação não iniciada
> **Objetivo:** desenhar no Figma, sobre o Design System Code4Coders e o AppShell do backoffice
> aprovado em CAP-002, as telas da área Auditoria antes da implementação no `admin-spa` (fatias
> V-01…V-03 de `tasks/prd-consulta-trilha-auditoria`). Aprovado este ASCII → desenho no Figma →
> aprovação → código.
> **Fontes:** `tasks/prd-consulta-trilha-auditoria/prd.md` v1.0 (RF-01…RF-05, US-01…US-04,
> Experiência do Usuário, DP-01…DP-05), `techspec.md` v1.0 (Frontend, Jornada, V-01…V-03),
> `api-contract.yaml` 1.1.0, `tasks/prd-trilha-auditoria/asyncapi-contract.yaml` (tipos de ato e
> atributos), `docs/design/wireframes-acesso-interno.md` (AppShell, B5, B12, B13, `RoleBadge`,
> `ReasonField`, `EmptyState`), `docs/design/wireframes-videos.md` (padrões de lista),
> `DESIGN.md`, `docs/design/Components.md`.

---

## 1. Inventário

### 1.1 O que o PRD entrega em tela

| Entrega | RF | Tem tela? | Observação |
|---|---|---|---|
| Trilha só do administrador do tenant | RF-01 | Indireta | Item "Auditoria" no menu e card no Início só com papel `administrador`; B12 por link direto |
| Localizar registros | RF-02 | **Sim** | Lista com filtros, paginação estável e estado vazio |
| Examinar registro e referências | RF-03 | **Sim** | Detalhe com os dois momentos, faltas e referências |
| Acrescentar complemento | RF-04 | **Sim** | Formulário no detalhe + estado "aguardando registro" |
| Sequência original + complementos | RF-05 | **Sim** | Linha do tempo no detalhe; indicador na lista |

### 1.2 Telas (`admin-spa`, base `/admin/`)

| # | Tela | Rota | RF | Acesso | Estados |
|---|---|---|---|---|---|
| A1 | Auditoria (lista) | `/admin/auditoria` | RF-01, RF-02, RF-05 | papel `administrador` | com registros · filtrada · filtro de pessoa · vazio · filtro sem resultado · período inválido · busca expirada · busca reiniciada · carregando · erro |
| A2 | Registro (detalhe) | `/admin/auditoria/{recordId}` | RF-01, RF-03, RF-05 | papel `administrador` | conforme · não conforme · referência não identificada · com complementos · não encontrado · carregando · erro |
| A3 | Acrescentar complemento | dentro de A2 | RF-04 | papel `administrador` | fechado · escrevendo · explicação em branco · confirmando · falha de envio |
| A4 | Aguardando registro | dentro de A2 | RF-04, RF-05 | papel `administrador` | aguardando · registrado · ainda não apareceu |
| A5 | Início do administrador | `/admin/` | RF-01 | sessão | card Auditoria ao lado de Acessos |
| B12 | Sem permissão | `/admin/auditoria…` por link direto | RF-01 | — | reusa B12 de CAP-002 |
| B13 | Erro | qualquer | — | — | reusa B13 de CAP-002 |

### 1.3 Lacunas e decisões de desenho (PRD × TechSpec × contrato × DS)

| # | Ponto | Origem | Proposta no wireframe |
|---|---|---|---|
| G1 | Nome e posição da área | PRD, RF-01 | Item **"Auditoria"** logo abaixo de Acessos, ícone `scroll-text` (lucide). Aparece pelo **papel** `administrador`, não por permissão (TechSpec, `get-staff-areas.ts`). Nada desabilitado para os demais papéis |
| G2 | **Nomes de autor e alvo na lista** | TechSpec: rótulos são resolvidos só no detalhe; o schema `IdentityReference` da lista já aceita `label` opcional | **Proposta: nomes também na lista** (a página tem no máximo 2 × 20 referências, um lote de Identity). Exige ajuste pequeno na TechSpec (BFF resolve rótulos da página). Sem o ajuste, a célula mostra a referência curta `Conta interna · 337f…6be7` (A1.g) — ver decisão 2 |
| G3 | Filtro por autor ou alvo exige UUID | Contrato: `authorId`/`targetId` UUID; TechSpec: vem do detalhe por estado em memória | Autor/alvo **não** têm campo de digitar. Entram como `Chip` removível ("Autor: Rafael Silva ✕") a partir de _Ver atos desta pessoa_ no detalhe (A2). Nenhum UUID na URL nem na tela |
| G4 | Recarregar a lista aberta pelo filtro de pessoa | TechSpec | Filtro em memória some; Alert info "A busca foi reiniciada sem o filtro de pessoa." (A1.f) |
| G5 | Período inclusivo, data e hora | RF-02; contrato `from`/`to` date-time | Dois campos **De** e **Até** (data + hora, horário local do navegador). "Até" sem hora vale até 23:59:59. Intervalo invertido é barrado no cliente: "A data final vem antes da inicial." (A1.e). Sem período = toda a trilha |
| G6 | Tipo | Contrato: `type` livre; asyncapi: 4 tipos | `Select` de uma escolha: **Todos · Convite emitido · Convite aceito · Papel concedido · Papel revogado** |
| G7 | Conformidade | `compliant` boolean | `Tabs` (como Vídeos): **Todos · Conformes · Não conformes**. Selo na linha: `ComplianceBadge` *Conforme* (success, ✓) / *Não conforme* (warning, ⚠) — cor + ícone + texto |
| G8 | Nomes dos tipos em pt-BR | asyncapi CAP-030 | `convite-interno-emitido` → "Convite emitido"; `convite-interno-aceito` → "Convite aceito"; `papel-concedido` → "Papel concedido"; `papel-revogado` → "Papel revogado"; outro valor → valor recebido em `mono` + selo "Tipo desconhecido" |
| G9 | Razões de não conformidade | enum do contrato | `tipo-desconhecido` "Tipo de ato desconhecido" · `autor-ausente` "Autor não informado pela origem" · `alvo-ausente` "Alvo não informado pela origem" · `motivo-ausente` "Motivo obrigatório não informado" · `momento-ausente` "Momento do ato não informado" · `complemento-invalido` "Conteúdo complementar inválido" · `motivo-excede-limite` "Motivo acima do limite de tamanho" |
| G10 | Dois momentos | RF-03, RN-A04 | Rótulos fixos **"Momento do ato"** e **"Recebido pela Auditoria"**, com segundos (`27/09/2026 10:00:04`) no detalhe; lista só com o do ato (`27/09 10:00`). Sem momento do ato: "— ausente" em itálico e selo de falta; a posição na lista vem do recebimento (contrato) |
| G11 | Paginação estável (snapshot 30 min) | TechSpec, ADR-0007 | 20 por página (`_size` até 50), `Pagination` do DS. Linha discreta: "Resultado fixado às 14:02 · atos novos entram ao buscar de novo  _Atualizar_". Mudar filtro volta à página 1 sem aviso |
| G12 | Snapshot expirado (`422 AUDIT_FILTER_INVALID`) | TechSpec | A UI refaz a busca na página 1 e mostra Alert warning "A busca expirou e foi refeita. Você voltou à primeira página." (A1.e). Nunca mistura páginas de buscas diferentes |
| G13 | Indicador de complemento na lista | RF-05, `hasComplements` | Ícone `message-square-plus` + texto "Complementado" na coluna de conformidade; complementos nunca são linhas |
| G14 | Separar fato recebido de contexto acrescentado | RF-05; Riscos do PRD | Detalhe em dois blocos: **"Recebido da origem"** (Card com overline `ORIGINAL · NÃO EDITÁVEL`) e **"Complementos"** (linha do tempo abaixo, cada item com overline `ACRESCENTADO DEPOIS`, autor e momento). O texto do complemento nunca aparece dentro do Card original |
| G15 | Campo ausente vs. não se aplica | RF-03 | Obrigatório ausente: "— ausente" + marcação warning. Não exigido (motivo em "Convite aceito"): "Não se aplica a este tipo", texto `muted`, sem aviso |
| G16 | Referência sem nome | RF-03; contrato: `label` ausente | "Nome não disponível" + referência curta `Conta interna · 550e…0000` com botão-ícone copiar. O contrato **não distingue** Identity indisponível de conta não encontrada, então a UI usa um texto só para os dois casos |
| G17 | Registro inexistente, de outro tenant ou ID de complemento (`404`) | TechSpec | Estado dentro da área: "Registro não encontrado" — mesma mensagem para os três casos (A2.e) |
| G18 | Complemento é irreversível | RF-04; DP-03, DP-04 | Formulário **no próprio detalhe** (o original fica à vista enquanto se escreve), aberto por _Acrescentar complemento_. Texto fixo: "Complementos não podem ser editados nem excluídos. O registro original não muda." Um clique em *Confirmar complemento*, sem AlertDialog extra — ver decisão 6 |
| G19 | Explicação | Contrato: 1–1000, não branca | Reusa `ReasonField` de CAP-002: `Textarea` + contador `0/1000` + ajuda "Não cite dados pessoais de outras pessoas." Em branco: "Escreva a explicação do que foi apurado." |
| G20 | Aceite ≠ registro (`202`) | TechSpec: consulta a cada 2 s por até 30 s | Após o `202`, item "Aguardando registro…" com spinner na linha do tempo (A4.a). Encontrado o `confirmationId` → vira o complemento + toast "Complemento registrado". Após 30 s → Alert info "A confirmação foi aceita e ainda está sendo registrada." + _Atualizar_ (A4.c). Nunca "falhou" |
| G21 | Falha de rede ao confirmar | TechSpec: mesma `Idempotency-Key` no retry | Formulário mantém o texto; Alert destructive "Não conseguimos confirmar agora." + *Tentar de novo* (reusa a mesma chave; não cria duplicado) |
| G22 | Atributos recebidos | contrato `attributes` | `papel` vira `RoleBadge` ("Professor"); outros atributos em lista chave/valor `mono`, sem tradução |
| G23 | Origem | contrato `origin` | `identidade` → "Identidade e Acesso"; outro valor em `mono` |
| G24 | Sessão/papel revogado com a área aberta | RF-01 | Próxima ação `403` → B12; `401` → B1 com "Sua sessão foi encerrada. Entre de novo." (G2 de CAP-002). Logout limpa cache de rótulos e detalhes |
| G25 | Motivo e explicação fora da URL | PRD, G10/G23 | Rotas só com `recordId`; filtros não vão para a query string (busca é POST); nenhum texto livre em título de aba |
| G26 | Acessibilidade | PRD, Experiência do Usuário | Tabela com `caption` oculta; linha inteira é link para o detalhe (Enter); filtros com `Label`; `aria-live="polite"` em "Aguardando registro" e no resultado ("12 registros"); foco volta ao botão ao fechar o formulário |

---

## 2. Fluxo do usuário

```
  ┌──────────────┐  papel administrador ┌──────────────────────────────────────────────────────┐
  │ A5 Início    │─────────────────────▶│ A1 Auditoria  /admin/auditoria                        │
  │ card         │  item "Auditoria"    │  período · tipo · conformidade · paginação estável    │
  │ Auditoria    │                      └──┬──────────────┬──────────────┬──────────────┬───────┘
  └──────────────┘                         │ clique/Enter │ nenhum        │ busca > 30min │ link direto
                                           │ na linha     │ resultado     │ (422)         │ sem papel
                                           ▼              ▼               ▼               ▼
                        ┌─────────────────────────────┐ A1.c/A1.d     A1.e refeita     B12 Sem
                        │ A2 Registro                 │ _Limpar       na página 1      permissão
                        │ /admin/auditoria/{recordId} │  filtros_
                        │  Recebido da origem         │
                        │  Complementos (linha tempo) │──── _Ver atos desta pessoa_ ───▶ A1.b
                        └──┬───────────────┬──────────┘     (chip "Autor: …" em memória;
                           │               │ 404              recarregar → A1.f reiniciada)
       _Acrescentar        │               ▼
        complemento_       │         A2.e Registro não encontrado  [ ← Voltar para a trilha ]
                           ▼
              ┌──────────────────────────────┐   em branco   ┌──────────────────────────┐
              │ A3 Formulário no detalhe     │──────────────▶│ A3.c "Escreva a          │
              │ ReasonField 0/1000           │               │ explicação…"             │
              └──┬────────────────────┬──────┘               └──────────────────────────┘
                 │ [Confirmar]        │ rede falhou
                 ▼                    ▼
        202 aceito               A3.e texto mantido · [Tentar de novo] (mesma chave)
                 │
                 ▼
     ┌────────────────────────────────────────────────────────────────────────────┐
     │ A4 Aguardando registro (item na linha do tempo; consulta a cada 2 s)       │
     │   apareceu o confirmationId ──▶ A4.b complemento visível + toast           │
     │   30 s sem aparecer ─────────▶ A4.c "ainda está sendo registrada" _Atualizar_│
     └────────────────────────────────────────────────────────────────────────────┘

  Qualquer ação com papel revogado ──▶ 403 ──▶ B12      Sessão encerrada ──▶ 401 ──▶ B1
```

---

## 3. Layout base e componentes

### 3.1 AppShell do backoffice

Reusa o AppShell aprovado em CAP-002 (`Sidebar` 264 + `Topbar` 68 + conteúdo `bg-muted` com `p-8`).
O item **Auditoria** entra logo abaixo de Acessos (G1), ícone `scroll-text`, só com papel
`administrador`.

```
┌──────────────────────┬─────────────────────────────────────────────────────────────────────┐
│ [</>] Code4Coders    │                                              ( MC ) Marina Costa ▾  │
│       Backoffice     ├─────────────────────────────────────────────────────────────────────┤
│ ▣ Início             │                                                                     │
│ ⚿ Acessos            │                                                                     │
│ 📜 Auditoria ◀ ativo │  ← só com papel administrador                                       │
│                      │     << conteúdo da página >>                                        │
└──────────────────────┴─────────────────────────────────────────────────────────────────────┘
```

### 3.2 Componentes do DS usados

`Card` · `Form`/`FormField`/`Label`/`Input`/`Textarea` · `Select` · `Tabs` (conformidade) · `Button`
(default, outline, ghost icon, link) · `Alert` (default, info, warning, destructive) · `Badge` ·
`Table` · `Pagination` · `Skeleton` · `Sonner` · `Tooltip` · `Sheet` (filtros no mobile) · `Sidebar` ·
`EmptyState`, `CodeWindow`, `RoleBadge`, `ReasonField`, `Area Card` (aprovados em CAP-002).

**Componentes novos propostos:**

- `ComplianceBadge` = `Badge` + ícone + texto: *Conforme* (success, `circle-check`), *Não conforme*
  (warning, `triangle-alert`) (G7).
- `DateTimeRangeField` = dois `Input` data + hora ("De", "Até") com um erro compartilhado (G5).
- `FilterChip` = `Badge` outline removível com ✕ e rótulo "Autor: Nome" / "Alvo: Nome" (G3).
- `IdentityRef` = nome resolvido **ou** "Nome não disponível" + referência curta `mono` com
  botão-ícone copiar; variante com _Ver atos desta pessoa_ (G2, G16).
- `EvidenceField` = linha rótulo/valor do Card original com três formas: valor, "— ausente"
  (warning) e "Não se aplica a este tipo" (muted) (G15).
- `ComplementItem` = item da linha do tempo: overline `ACRESCENTADO DEPOIS`, autor, momento,
  explicação; variantes registrado e aguardando (G14, G20).
- `AuditRow` (desktop) / `AuditCard` (mobile) = linha da trilha.

---

## 4. Wireframes

Legenda: `[ Botão primário ]` · `[ Botão outline ]` · `_link_` · `( ! )` Alert · `[____]` Input ·
`[▾ ]` Select · `(✓ Conforme)` `(⚠ Não conforme)` ComplianceBadge · `‹Autor: … ✕›` FilterChip ·
`⧉` copiar · `( • )` Tab ativo.

### A1 · Auditoria — `/admin/auditoria` (só papel `administrador`)

```
A1.a  Com registros (desktop 1440)
┌──────────────────────┬─────────────────────────────────────────────────────────────────────┐
│ [</>] Code4Coders    │                                              ( MC ) Marina Costa ▾  │
│       Backoffice     ├─────────────────────────────────────────────────────────────────────┤
│ ▣ Início             │  AUDITORIA                                                          │
│ ⚿ Acessos            │  Trilha de atos administrativos                                     │
│ 📜 Auditoria ◀ ativo │  Quem fez o quê, com quem e quando. Nada aqui pode ser alterado.    │
│                      │                                                                     │
│                      │  ┌─ Card filtros ────────────────────────────────────────────────┐  │
│                      │  │ De                     Até                    Tipo             │  │
│                      │  │ [dd/mm/aaaa --:--]     [dd/mm/aaaa --:--]     [▾ Todos       ] │  │
│                      │  │                                        [ Limpar ] [ Buscar ]   │  │
│                      │  └───────────────────────────────────────────────────────────────┘  │
│                      │  ( Todos )[ Conformes ][ Não conformes ]                 (Tabs)     │
│                      │  128 registros · fixado às 14:02  _Atualizar_            ← G11      │
│                      │  ┌───────────────────────────────────────────────────────────────┐  │
│                      │  │ Momento     Tipo            Autor        Alvo     Situação    │  │
│                      │  │───────────────────────────────────────────────────────────────│  │
│                      │  │ 27/09 10:00 Papel concedido Marina Costa Rafael   (✓ Conforme)│  │
│                      │  │             (Professor)                  Silva                │  │
│                      │  │ 26/09 18:22 Papel concedido — ausente    Júlia    (⚠ Não conf)│  │
│                      │  │             (Suporte)                    Lima     + Complem.  │  │
│                      │  │ 25/09 16:40 Convite emitido Marina Costa Convite  (✓ Conforme)│  │
│                      │  │             (Suporte)                    de Bruno             │  │
│                      │  │ 25/09 11:03 Convite aceito  Rafael Silva Rafael   (✓ Conforme)│  │
│                      │  │                                          Silva                │  │
│                      │  │ 24/09 09:12 Papel revogado  Marina Costa Carla    (✓ Conforme)│  │
│                      │  │             (Financeiro)                 Nunes                │  │
│                      │  └───────────────────────────────────────────────────────────────┘  │
│                      │                               ‹ 1 2 3 … 7 › (Pagination, 20/pág.)   │
└──────────────────────┴─────────────────────────────────────────────────────────────────────┘
Ordem: do momento do ato mais recente ao mais antigo; o recebimento não reordena (RF-02).
Linha inteira abre A2 (clique ou Enter). Sem motivo nem texto livre na lista (RF-02).
Papel do ato (atributo `papel`) aparece como RoleBadge abaixo do tipo.
Alvo "Convite de Bruno": convite ainda não aceito, identificado pelo rótulo que Identity devolver.
"(⚠ Não conf)" e "+ Complem." abreviam só no ASCII; no Figma: selo "Não conforme" e "Complementado" (G13).
```

```
A1.b  Filtrada pela pessoa (vinda de A2)          A1.c  Vazio (tenant sem nenhum ato)
┌─────────────────────────────────────────────┐  ┌─────────────────────────────────────────┐
│ De [01/09/2026 00:00]  Até [27/09/2026 --:--]│  │ AUDITORIA                               │
│ Tipo [▾ Papel concedido ]                    │  │ Trilha de atos administrativos          │
│ ‹Autor: Marina Costa ✕›           (FilterChip)│  │                                         │
│ ( Todos )[ Conformes ][ Não conformes ]      │  │ ┌─ EmptyState ────────────────────────┐ │
│ 4 registros · fixado às 14:05  _Atualizar_   │  │ │ ┌─ CodeWindow ─────────── ● ● ● ┐   │ │
│ ┌──────────────────────────────────────────┐ │  │ │ │ $ tail audit.log              │   │ │
│ │ … só atos com autor Marina Costa, tipo   │ │  │ │ │ (vazio)                       │   │ │
│ │ Papel concedido, de 01/09 00:00 até      │ │  │ │ └───────────────────────────────┘   │ │
│ │ 27/09 23:59:59 (bordas incluídas)        │ │  │ │ Nenhum ato registrado ainda   (H4)  │ │
│ └──────────────────────────────────────────┘ │  │ │ Convites e mudanças de papel feitos │ │
│ Não conformes que ainda trazem a referência  │  │ │ em Acessos aparecem aqui.           │ │
│ também aparecem (RF-02).                     │  │ │ [ Abrir acessos → ]  (outline)      │ │
└─────────────────────────────────────────────┘  │ └─────────────────────────────────────┘ │
                                                 └─────────────────────────────────────────┘

A1.d  Filtro sem resultado                        A1.e  Avisos de filtro
┌─────────────────────────────────────────────┐  ┌─────────────────────────────────────────┐
│ ‹Alvo: Carla Nunes ✕›  Tipo [▾ Convite aceito]│  │ Período invertido (antes de buscar):   │
│ ( Todos )[ Conformes ][ Não conformes ]      │  │ De [27/09 10:00]  Até [26/09 10:00]     │
│                                              │  │ ⚠ A data final vem antes da inicial.   │
│ Nenhum registro com esses filtros.           │  │   [ Buscar ] (desabilitado)             │
│ Altere o período, o tipo ou a situação.      │  │                                         │
│ _Limpar filtros_                             │  │ Busca expirada (422, G12):              │
└─────────────────────────────────────────────┘  │ ( ! ) A busca expirou e foi refeita.    │
                                                 │       Você voltou à primeira página.    │
                                                 │                       (Alert warning)   │
                                                 └─────────────────────────────────────────┘

A1.f  Busca reiniciada (recarregou com filtro de pessoa, G4)
( i ) A busca foi reiniciada sem o filtro de pessoa.  _Voltar ao registro_   (Alert info)
      — _Voltar ao registro_ só se o navegador ainda tiver o registro de origem; senão, não aparece.

A1.g  Sem nomes na lista (se a decisão 2 não for aprovada)
│ 27/09 10:00   Papel concedido   Conta interna    Conta interna    (✓ Conforme) │
│               (Professor)       337f…6be7 ⧉      550e…0000 ⧉                    │

A1.h  Carregando: Skeleton de 5 linhas da Table; filtros já visíveis e desabilitados.
A1.i  Erro / serviço indisponível
      ( ! ) Não conseguimos carregar a trilha agora.  [ Tentar de novo ]   (Alert destructive)

A1.j  Mobile 390: filtros num Sheet; Table vira lista de AuditCard.
┌──────────────────────────────────┐   ┌─ Sheet "Filtros" ────────────── ✕ ┐
│ ☰  Auditoria                     │   │ De   [dd/mm/aaaa --:--]           │
│ [ ⚙ Filtros (2) ]  128 registros │   │ Até  [dd/mm/aaaa --:--]           │
│ ( Todos )[ Conformes ][ Não c…→  │   │ Tipo [▾ Todos               ]     │
│ ┌──────────────────────────────┐ │   │ ‹Autor: Marina Costa ✕›           │
│ │ Papel concedido  (✓ Conforme)│ │   │                                   │
│ │ (Professor)                  │ │   │ [ Limpar ]      [ Aplicar ]       │
│ │ Marina Costa → Rafael Silva  │ │   └───────────────────────────────────┘
│ │ 27/09 10:00                ›  │ │
│ └──────────────────────────────┘ │
│ ┌──────────────────────────────┐ │
│ │ Papel concedido (⚠ Não conf.)│ │
│ │ (Suporte)       + Complem.   │ │
│ │ — ausente → Júlia Lima       │ │
│ │ 26/09 18:22                ›  │ │
│ └──────────────────────────────┘ │
│            ‹ 1 2 3 … 7 ›         │
└──────────────────────────────────┘
```

### A2 · Registro — `/admin/auditoria/{recordId}`

```
A2.a  Conforme, sem complemento (desktop 1440)
┌──────────────────────┬─────────────────────────────────────────────────────────────────────┐
│ [</>] Code4Coders    │                                              ( MC ) Marina Costa ▾  │
│       Backoffice     ├─────────────────────────────────────────────────────────────────────┤
│ ▣ Início             │  _← Trilha de auditoria_          (volta à lista com filtros/página) │
│ ⚿ Acessos            │  REGISTRO DE AUDITORIA                                              │
│ 📜 Auditoria ◀ ativo │  Papel concedido                                (✓ Conforme)  (H2)  │
│                      │                                                                     │
│                      │  ┌─ Card ───────────────────────────────────────────────────────┐   │
│                      │  │ ORIGINAL · NÃO EDITÁVEL                 Recebido da origem    │   │
│                      │  │──────────────────────────────────────────────────────────────│   │
│                      │  │ Momento do ato           27/09/2026 10:00:00                 │   │
│                      │  │ Recebido pela Auditoria  27/09/2026 10:00:04                 │   │
│                      │  │ Origem                   Identidade e Acesso                 │   │
│                      │  │ Tipo                     Papel concedido                     │   │
│                      │  │ Autor                    Marina Costa                        │   │
│                      │  │                          _Ver atos desta pessoa_             │   │
│                      │  │ Alvo                     Rafael Silva                        │   │
│                      │  │                          _Ver atos desta pessoa_             │   │
│                      │  │ Papel                    (Professor)            (RoleBadge)  │   │
│                      │  │ Motivo                   ┃ Assumiu a turma de .NET após a    │   │
│                      │  │                          ┃ saída do professor anterior.      │   │
│                      │  └──────────────────────────────────────────────────────────────┘   │
│                      │                                                                     │
│                      │  COMPLEMENTOS                                                 (H4)  │
│                      │  Nenhum complemento. Complementos acrescentam o que foi apurado     │
│                      │  depois, sem mudar o registro acima.                                │
│                      │  [ + Acrescentar complemento ]  (outline)                  → A3     │
└──────────────────────┴─────────────────────────────────────────────────────────────────────┘
Horários no fuso do navegador, com segundos. O nome do autor/alvo vem de Identity na hora da
leitura (não é guardado na Auditoria).
```

```
A2.b  Não conforme (autor e motivo ausentes) com dois complementos
│  REGISTRO DE AUDITORIA                                                                   │
│  Papel concedido                                                   (⚠ Não conforme) (H2) │
│                                                                                          │
│  ( ⚠ ) A origem enviou este ato incompleto                              (Alert warning)  │
│        • Autor não informado pela origem                                                 │
│        • Motivo obrigatório não informado                                                │
│        Complementos não tornam o registro conforme.                                      │
│                                                                                          │
│  ┌─ Card ORIGINAL · NÃO EDITÁVEL ──────────────────────────────────────────────────────┐ │
│  │ Momento do ato           26/09/2026 18:22:10                                        │ │
│  │ Recebido pela Auditoria  26/09/2026 18:31:47                                        │ │
│  │ Origem                   Identidade e Acesso                                        │ │
│  │ Tipo                     Papel concedido                                            │ │
│  │ Autor                    — ausente                               (EvidenceField ⚠)  │ │
│  │ Alvo                     Júlia Lima  _Ver atos desta pessoa_                        │ │
│  │ Papel                    (Suporte)                                                  │ │
│  │ Motivo                   — ausente                               (EvidenceField ⚠)  │ │
│  └─────────────────────────────────────────────────────────────────────────────────────┘ │
│                                                                                          │
│  COMPLEMENTOS  2                                                                    (H4) │
│  │                                                                                       │
│  ●─ ACRESCENTADO DEPOIS · Marina Costa · 27/09/2026 09:15:02          (ComplementItem)   │
│  │  A concessão foi feita pelo script de migração de 26/09. Autorização                  │
│  │  confirmada no chamado interno de suporte.                                            │
│  │                                                                                       │
│  ●─ ACRESCENTADO DEPOIS · Paulo Alves · 27/09/2026 11:40:31                              │
│  │  Script corrigido para enviar o autor a partir de 27/09.                              │
│                                                                                          │
│  [ + Acrescentar complemento ]                                                           │
Complementos em ordem de criação (mais antigo primeiro), cada um com autor e momento próprios.
Nenhum complemento tem editar ou excluir (RF-04).
```

```
A2.c  Convite aceito (motivo não exigido)        A2.d  Referência sem nome (G16)
┌─────────────────────────────────────────────┐  ┌─────────────────────────────────────────┐
│ Convite aceito                (✓ Conforme)  │  │ Autor  Nome não disponível              │
│ …                                           │  │        Conta interna · 337f…6be7  ⧉     │
│ Autor   Rafael Silva                        │  │        _Ver atos desta referência_      │
│ Alvo    Convite de Rafael Silva             │  │                                         │
│ Motivo  Não se aplica a este tipo  (muted)  │  │ Tooltip ⧉: "Copiar referência"          │
│ Sem Alert, sem marcação de falta (RF-03).   │  │ Vale para conta desativada, convite     │
└─────────────────────────────────────────────┘  │ inexistente e Identity fora do ar — o   │
                                                 │ contrato não diferencia (G16).          │
                                                 └─────────────────────────────────────────┘

A2.e  Não encontrado (404 — inexistente, outro tenant ou ID de complemento; G17)
┌─ EmptyState ──────────────────────────────────────┐
│ ┌─ CodeWindow ─────────────────────────── ● ● ● ┐ │
│ │ $ GET /admin/auditoria/5137eb89-…             │ │
│ │ 404 Not Found                                 │ │
│ └───────────────────────────────────────────────┘ │
│ Registro não encontrado                     (H3)  │
│ Ele pode não existir ou não estar disponível      │
│ para você.                                        │
│ [ ← Voltar para a trilha ]                        │
└───────────────────────────────────────────────────┘

A2.f  Tipo desconhecido: Tipo = `acesso-suspenso` (mono) + selo "Tipo desconhecido";
      razão "Tipo de ato desconhecido" no Alert; atributos recebidos em lista chave/valor mono.
A2.g  Carregando: Skeleton do Card original (8 linhas) e de 1 ComplementItem.
A2.h  Erro: Alert destructive "Não conseguimos abrir o registro agora." + [ Tentar de novo ].

A2.i  Mobile 390: rótulo acima do valor, Card em largura total, linha do tempo igual.
┌──────────────────────────────────┐
│ ← Trilha de auditoria            │
│ Papel concedido                  │
│ (⚠ Não conforme)                 │
│ ┌─ ORIGINAL · NÃO EDITÁVEL ────┐ │
│ │ Momento do ato               │ │
│ │ 26/09/2026 18:22:10          │ │
│ │ Recebido pela Auditoria      │ │
│ │ 26/09/2026 18:31:47          │ │
│ │ Autor                        │ │
│ │ — ausente  ⚠                 │ │
│ │ …                            │ │
│ └──────────────────────────────┘ │
│ COMPLEMENTOS 2                   │
│ ●─ Marina Costa · 27/09 09:15    │
│ [ + Acrescentar complemento ]    │
└──────────────────────────────────┘
```

### A3 · Acrescentar complemento — formulário no fim de A2

```
A3.a  Fechado: [ + Acrescentar complemento ] (outline) abaixo da linha do tempo.

A3.b  Escrevendo                                 A3.c  Explicação em branco
┌─ Card ─────────────────────────────────────┐  ┌─ Card ─────────────────────────────────────┐
│ Acrescentar complemento              (H5)  │  │ Explicação                                 │
│                                            │  │ [                                        ] │
│ Explicação                                 │  │ [                                        ] │
│ [A autorização foi confirmada no chamado ] │  │ ⚠ Escreva a explicação do que foi         │
│ [interno de suporte.                     ] │  │   apurado.               (FormField erro)  │
│ [                                        ] │  │ 0/1000                                     │
│ Não cite dados pessoais de outras  58/1000 │  └────────────────────────────────────────────┘
│ pessoas.                     (ReasonField) │  Só espaços também conta como em branco.
│                                            │
│ ( i ) Complementos não podem ser editados  │  A3.d  Confirmando: [ Confirmar complemento ]
│       nem excluídos. O registro original   │        com spinner; Textarea e Cancelar
│       não muda.               (Alert info) │        desabilitados.
│                                            │
│      [ Cancelar ]  [ Confirmar complemento ]│  A3.e  Falha de envio (rede/5xx, G21)
└────────────────────────────────────────────┘  ( ! ) Não conseguimos confirmar agora. Seu
Cancelar com texto digitado descarta sem              texto foi mantido.  [ Tentar de novo ]
perguntar (nada foi enviado).                         (Alert destructive; mesma chave)
```

### A4 · Aguardando registro — item na linha do tempo de A2

```
A4.a  Aguardando (logo após o 202)
│  ●─ Paulo Alves · 27/09/2026 11:40:31                                                    │
│  │  Script corrigido para enviar o autor a partir de 27/09.                              │
│  │                                                                                       │
│  ◌─ AGUARDANDO REGISTRO · Marina Costa                        (ComplementItem pending)   │
│  │  A autorização foi confirmada no chamado interno de suporte.                          │
│  │  A Auditoria está registrando este complemento…     (aria-live polite; spinner parado │
│                                                          com prefers-reduced-motion)     │
Formulário fecha; [ + Acrescentar complemento ] volta desabilitado até registrar ou 30 s.

A4.b  Registrado: o item vira ACRESCENTADO DEPOIS com o momento vindo da Auditoria;
      toast "Complemento registrado". O original continua idêntico.

A4.c  Ainda não apareceu após 30 s
│  ◌─ AGUARDANDO REGISTRO · Marina Costa                                                   │
│  │  A autorização foi confirmada no chamado interno de suporte.                          │
│  ( i ) A confirmação foi aceita e ainda está sendo registrada. Não envie de novo.        │
│        _Atualizar_                                                       (Alert info)    │
Sem "falhou" e sem reenvio automático (TechSpec). Sair da página e voltar mostra o
complemento quando a Auditoria o registrar.
```

### A5 · Início do administrador — `/admin/`

Evolui B5.a de CAP-002: card **Auditoria** ao lado de Acessos.

```
┌──────────────────────┬─────────────────────────────────────────────────────────────────────┐
│ ▣ Início  ◀ ativo    │  INÍCIO                                                             │
│ ⚿ Acessos            │  Olá, Marina                                                  (H2)  │
│ 📜 Auditoria         │  Seus papéis: (Administrador)                                       │
│                      │                                                                     │
│                      │  ┌─ Card ──────────────────────┐  ┌─ Card ──────────────────────┐   │
│                      │  │ ⚿  Acessos              (H5)│  │ 📜  Auditoria           (H5)│   │
│                      │  │ Convide pessoas e ajuste os │  │ Consulte quem concedeu,     │   │
│                      │  │ papéis da equipe.           │  │ revogou ou convidou, e      │   │
│                      │  │ 2 convites pendentes        │  │ quando.                     │   │
│                      │  │ [ Abrir acessos → ]         │  │ [ Abrir auditoria → ]       │   │
│                      │  └─────────────────────────────┘  └─────────────────────────────┘   │
└──────────────────────┴─────────────────────────────────────────────────────────────────────┘
Card Auditoria sem contagem (não há consulta barata de total sem criar snapshot).
```

### B12 · Sem permissão e B13 · Erro

Reusam os frames aprovados em CAP-002. Em B12, o `CodeWindow` mostra `$ GET /admin/auditoria` →
`403 Forbidden`; vale também para `/admin/auditoria/{recordId}` e para o papel revogado com a área
aberta (G24). Nenhum desenho novo.

---

## 5. Plano para o Figma (após aprovação deste ASCII)

Páginas novas no arquivo `Code4Coders — Design System`, sem mexer nas existentes:

1. **🧭 Fluxo — Auditoria**: o fluxo da seção 2, com os frames das telas como nós.
2. **📱 Screens — Auditoria**: A1–A5, todos os estados, em desktop 1440; mobile 390 para A1.a,
   A1.j (Sheet), A2.b e A3.b. Tema Light; Dark em A1.a e A2.b.
3. **Components (proposta)**, na página de Screens: `ComplianceBadge`, `DateTimeRangeField`,
   `FilterChip`, `IdentityRef`, `EvidenceField`, `ComplementItem`, `AuditRow`, `AuditCard`, item de
   sidebar "Auditoria" e ícones lucide novos (`scroll-text`, `message-square-plus`, `copy`,
   `circle-check`, `filter`) se ainda não existirem; `Select`, `Textarea` e `Sheet` do DS se ainda
   não existirem no arquivo.

Tudo reusando o AppShell, `EmptyState`, `CodeWindow`, `Area Card`, `RoleBadge`, `ReasonField`,
`Tab` e demais componentes aprovados em CAP-002 e CAP-006, e as variáveis e text styles do DS, sem
valor fixo.

---

## 6. Decisões para você aprovar

1. **Área "Auditoria" abaixo de Acessos**, pelo papel `administrador`, com card no Início (G1, A5).
2. **Nomes de autor e alvo na lista** (G2) — pede ajuste na TechSpec para o BFF resolver os rótulos
   da página. Alternativa: manter a TechSpec e mostrar referências curtas na lista (A1.g).
3. **Autor/alvo só como chip vindo do detalhe**, sem campo para digitar ID (G3).
4. **Filtros:** período De/Até com data e hora, `Select` de tipo e `Tabs` de conformidade (G5–G7);
   20 por página com "fixado às hh:mm · _Atualizar_" (G11).
5. **Detalhe em dois blocos** — Card "ORIGINAL · NÃO EDITÁVEL" e linha do tempo "ACRESCENTADO
   DEPOIS" — com Alert listando as faltas no não conforme (G14, A2.b).
6. **Complemento no próprio detalhe**, sem AlertDialog de confirmação extra; aviso fixo de que não
   pode ser editado nem excluído (G18, A3).
7. **"Aguardando registro"** na linha do tempo e, após 30 s, aviso de que ainda está sendo
   registrada — nunca "falhou" (G20, A4).
8. **"Nome não disponível" único** para referência sem nome, já que o contrato não diferencia a
   causa (G16).
9. **Componentes novos:** `ComplianceBadge`, `DateTimeRangeField`, `FilterChip`, `IdentityRef`,
   `EvidenceField`, `ComplementItem`, `AuditRow`/`AuditCard`.

---

## 7. Figma (aprovado em 2026-09-27)

Base: `https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System`

| Entrega | Página | node-id |
|---|---|---|
| Fluxo do usuário | Fluxo — Auditoria | `123-2` |
| Componentes novos (proposta) | Screens — Auditoria | `118-7461` |
| A1 · Auditoria (a–f, h–j; desktop + mobile + Sheet) | Screens — Auditoria | `121-7828` |
| A2 · Registro (a–i, desktop + mobile) | Screens — Auditoria | `122-1305` |
| A3 · Acrescentar complemento (b–e) | Screens — Auditoria | `122-10351` |
| A4 · Aguardando registro (a–c) | Screens — Auditoria | `122-10894` |
| A5 · Início do administrador | Screens — Auditoria | `122-11395` |
| Dark mode — validação | Screens — Auditoria | `122-11478` |

B12 (Sem permissão), B13 (Erro) e B1 (sessão encerrada) não têm frame novo: reusam os de CAP-002.

Componentes propostos (migram para a página Components após aprovação): Icon (6 lucide novos:
scroll-text, message-square-plus, copy, filter, calendar, chevron-right), Compliance Badge, Filter
Chip, Identity Ref, Evidence Field, Complement Item, Audit Row, Audit Card (mobile), Date Time Field e
Select.

Ajustes de desenho em relação ao ASCII, sem mudar comportamento:

- A decisão 2 (nomes de autor e alvo na lista) foi aprovada, então A1.g (lista só com referências)
  não foi desenhado. A [TechSpec 1.1](../../tasks/prd-consulta-trilha-auditoria/techspec.md)
  incorporou o ajuste: o BFF resolve os rótulos de Identity também para a página da lista.
- A1.a (estado fechado do formulário) é o próprio A2.a: o botão *+ Acrescentar complemento*.
- O filtro de situação usa o `Tab` do DS, como em Vídeos (não há `ToggleGroup` no arquivo).
- A1.e foi dividido em dois frames: A1.e (período invertido) e A1.e2 (busca expirada e refeita).
- *Não conforme* usa os tokens `warning-soft` / `warning-soft-foreground`; *Conforme* usa
  `success-soft`.
- Não havia `Select` nem campo de data no arquivo: `Select` (gatilho fechado) e `Date Time Field`
  entram como componentes propostos.
