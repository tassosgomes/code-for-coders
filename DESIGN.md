# Code4Coders — DESIGN.md

> Fonte da verdade visual da plataforma Code4Coders.
> **Figma:** [Code4Coders — Design System](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn) · **Stack de referência:** Next.js + Tailwind CSS v4 + shadcn/ui
> **Implementação SPA:** `src/student-spa/src/assets/globals.css` (tokens em código) · `docs/design/Components.md` (guia de componentes)

---

## 1. Princípios

1. **Código é a estrela.** Somos uma plataforma para programadores: no lugar de fotos genéricas de banco de imagem, a ilustração principal é código real (`<CodeWindow>`, glifos de tecnologia, terminal). Fotos só de pessoas reais: instrutores e alunos.
2. **Claro, arejado, uma cor de marca.** A base é neutra com leve toque violeta (`ink`). O violeta (`primary`) é reservado para ação e destaque. Se tudo é roxo, nada é importante.
3. **Verde = funcionou.** O verde (`success` / `code-accent`) comunica teste passando, aula concluída e deploy ok. Não use verde como decoração.
4. **Tokens, nunca valores.** Nenhum hex, px arbitrário ou `bg-violet-600` em componente. Sempre token semântico (`bg-primary`, `text-muted-foreground`, `rounded-xl`).
5. **shadcn primeiro.** Antes de criar qualquer componente, verifique se ele existe em [ui.shadcn.com](https://ui.shadcn.com). Componente custom só para composições recorrentes da plataforma (ver `docs/design/Components.md`).

---

## 2. Setup

```bash
npx shadcn@latest init            # style: new-york · baseColor: neutral · cssVariables: true
# depois substitua app/globals.css pelo globals.css deste pacote
npm i next-themes tw-animate-css lucide-react
```

**Fontes** (`app/layout.tsx`):

```tsx
import { Inter, Plus_Jakarta_Sans, JetBrains_Mono } from "next/font/google"

const inter = Inter({ subsets: ["latin"], variable: "--font-inter" })
const jakarta = Plus_Jakarta_Sans({ subsets: ["latin"], variable: "--font-jakarta", weight: ["600", "700", "800"] })
const jetbrains = JetBrains_Mono({ subsets: ["latin"], variable: "--font-jetbrains" })

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="pt-BR" className={`${inter.variable} ${jakarta.variable} ${jetbrains.variable}`} suppressHydrationWarning>
      <body>{children}</body>
    </html>
  )
}
```

**Dark mode:** `next-themes` com `attribute="class"` e `defaultTheme="system"`. O CSS já traz o bloco `.dark`.

**React + Vite existente:** mantenha o Vite, instale `@tailwindcss/vite` e mantenha os tokens CSS dentro do projeto consumidor, importados pela folha principal da aplicação. No SPA de aluno, o arquivo está em `src/student-spa/src/assets/globals.css` e é importado por `app.css`. Carregue as mesmas fontes por `<link>` no HTML; `next/font` se aplica apenas a projetos Next.js. Não edite os valores gerados em `globals.css`.

---

## 3. Cores

### 3.1 Arquitetura

```
Primitives (Figma) ──alias──▶ Theme · Light/Dark (Figma) ══ :root / .dark (globals.css) ══▶ classes Tailwind
violet/600                     primary                        --primary                       bg-primary
```

- **Primitivas** (`violet`, `ink`, `emerald`, `amber`, `red`, `blue`, `pink`) ficam **ocultas** no Figma e **não** são usadas direto em código. Exceção: syntax highlight do `<CodeWindow>`.
- **Semânticas** seguem exatamente os nomes do shadcn, com extensões marcadas com ★.

### 3.2 Tokens semânticos

| Token | Light | Dark |
|---|---|---|
| `--background` | `ink/0` `#FFFFFF` | `ink/950` `#0D0B14` |
| `--foreground` | `ink/900` `#16131F` | `ink/50` `#F8F7FC` |
| `--card` | `ink/0` `#FFFFFF` | `ink/900` `#16131F` |
| `--card-foreground` | `ink/900` `#16131F` | `ink/50` `#F8F7FC` |
| `--popover` | `ink/0` `#FFFFFF` | `ink/900` `#16131F` |
| `--popover-foreground` | `ink/900` `#16131F` | `ink/50` `#F8F7FC` |
| `--primary` | `violet/600` `#7C3AED` | `violet/400` `#A78BFA` |
| `--primary-foreground` | `ink/0` `#FFFFFF` | `ink/950` `#0D0B14` |
| `--secondary` | `violet/50` `#F5F3FF` | `ink/800` `#242033` |
| `--secondary-foreground` | `violet/700` `#6D28D9` | `violet/200` `#DDD6FE` |
| `--muted` | `ink/50` `#F8F7FC` | `ink/800` `#242033` |
| `--muted-foreground` | `ink/500` `#6E6883` | `ink/400` `#9A94AE` |
| `--accent` | `violet/100` `#EDE9FE` | `ink/800` `#242033` |
| `--accent-foreground` | `violet/800` `#5B21B6` | `ink/50` `#F8F7FC` |
| `--destructive` | `red/600` `#DC2626` | `red/600` `#DC2626` |
| `--destructive-foreground` | `ink/0` `#FFFFFF` | `ink/0` `#FFFFFF` |
| `--success` ★ | `emerald/600` `#059669` | `emerald/400` `#34D399` |
| `--success-foreground` ★ | `ink/0` `#FFFFFF` | `ink/950` `#0D0B14` |
| `--success-soft` ★ | `emerald/50` `#ECFDF5` | `ink/800` `#242033` |
| `--success-soft-foreground` ★ | `emerald/700` `#047857` | `emerald/400` `#34D399` |
| `--warning` ★ | `amber/500` `#F59E0B` | `amber/400` `#FBBF24` |
| `--warning-foreground` ★ | `ink/950` `#0D0B14` | `ink/950` `#0D0B14` |
| `--warning-soft` ★ | `amber/50` `#FFFBEB` | `ink/800` `#242033` |
| `--warning-soft-foreground` ★ | `amber/700` `#B45309` | `amber/400` `#FBBF24` |
| `--info` ★ | `blue/600` `#2563EB` | `blue/400` `#60A5FA` |
| `--border` | `ink/200` `#E4E1EF` | `ink/800` `#242033` |
| `--input` | `ink/400` `#9A94AE` | `ink/500` `#6E6883` |
| `--ring` | `violet/500` `#8B5CF6` | `violet/400` `#A78BFA` |
| `--inverse` ★ | `ink/950` `#0D0B14` | `violet/950` `#2E1065` |
| `--inverse-foreground` ★ | `ink/50` `#F8F7FC` | `ink/50` `#F8F7FC` |
| `--code` ★ | `ink/950` `#0D0B14` | `ink/950` `#0D0B14` |
| `--code-foreground` ★ | `ink/100` `#F1EFF8` | `ink/100` `#F1EFF8` |
| `--code-accent` ★ | `emerald/400` `#34D399` | `emerald/400` `#34D399` |
| `--chart-1` | `violet/600` `#7C3AED` | `violet/400` `#A78BFA` |
| `--chart-2` | `emerald/500` `#10B981` | `emerald/400` `#34D399` |
| `--chart-3` | `amber/500` `#F59E0B` | `amber/400` `#FBBF24` |
| `--chart-4` | `blue/500` `#3B82F6` | `blue/400` `#60A5FA` |
| `--chart-5` | `pink/500` `#EC4899` | `pink/400` `#F472B6` |
| `--sidebar` | `ink/50` `#F8F7FC` | `ink/900` `#16131F` |
| `--sidebar-foreground` | `ink/700` `#3A354C` | `ink/100` `#F1EFF8` |
| `--sidebar-primary` | `violet/600` `#7C3AED` | `violet/400` `#A78BFA` |
| `--sidebar-primary-foreground` | `ink/0` `#FFFFFF` | `ink/950` `#0D0B14` |
| `--sidebar-accent` | `violet/100` `#EDE9FE` | `ink/800` `#242033` |
| `--sidebar-accent-foreground` | `violet/800` `#5B21B6` | `ink/50` `#F8F7FC` |
| `--sidebar-border` | `ink/200` `#E4E1EF` | `ink/800` `#242033` |
| `--sidebar-ring` | `violet/500` `#8B5CF6` | `violet/400` `#A78BFA` |

★ Extensões ao shadcn: `success*`, `warning*`, `info`, `inverse*`, `code*`. Todas já estão registradas em `@theme inline`, então `bg-success-soft`, `text-code-accent` etc. funcionam direto.

Exceção consciente em `--input` (2026-10-13): a borda do campo subiu de `ink/200` para `ink/400` no claro e de `ink/700` para `ink/500` no escuro, para atender ao critério de contraste não textual da WCAG 1.4.11. `ink/400` dá 2,91:1 sobre branco, abaixo dos 3:1 exigidos; o dono do produto aceitou essa exceção. `ink/500` dá 3,69:1 sobre o fundo escuro (`ink/950`). A variável `input` no Figma ainda precisa receber o mesmo valor.

### 3.3 Regras de uso

| Situação | Use | Não use |
|---|---|---|
| Fundo da página | `bg-background` | `bg-white` |
| Área de app logado (atrás dos cards) | `bg-muted` | `bg-gray-50` |
| Texto secundário, metadados | `text-muted-foreground` | `text-gray-500`, `opacity-60` |
| CTA principal | `bg-primary text-primary-foreground` | violeta hardcoded |
| Tag de nível / chip suave | `bg-secondary text-secondary-foreground` | `bg-primary/10` |
| Status "concluído" | `bg-success-soft text-success-soft-foreground` | `text-success` sobre `success-soft` (3,6:1, reprova AA) |
| Status "atenção" | `bg-warning-soft text-warning-soft-foreground` | `text-warning` sobre fundo claro (2,1:1) |
| Faixa de métricas, footer, seções de impacto | `bg-inverse` + container `dark` | `bg-black` |
| Blocos de código, terminal, thumbnails de fallback | `bg-code text-code-foreground`, destaque `text-code-accent` | `bg-slate-900` |
| Gráficos | `chart-1` … `chart-5`, nessa ordem | cores aleatórias |

**Proporção sugerida por tela:** cerca de 80% neutros, 15% `secondary`/`accent` e até 5% `primary`.

### 3.4 Tokens do backoffice (`admin-spa`)

O backoffice não usa Tailwind: é CSS puro, com nomes de token próprios. A única fonte de cor é `src/admin-spa/src/assets/theme.css`, e nenhuma tela define cor fora desses tokens. O next-themes aplica o tema como classe no `<html>`: sem classe (ou com `.light`) vale `:root`; com `.dark`, vale `:root.dark`.

O guardrail `src/admin-spa/src/assets/theme-tokens.test.ts` falha quando:
- `app.css` ou `authoring.css` têm cor literal (hex, `rgb`, `rgba`, `hsl` ou `hsla`);
- um `var(--…)` usado nesses arquivos não existe em `theme.css`;
- o tema escuro deixa de redefinir um token de interface definido no claro.

**Superfícies**

| Token | Claro | Escuro |
|---|---|---|
| `--background` | `#F8F7FC` | `#242033` |
| `--sidebar` | `#F8F7FC` | `#16131F` |
| `--topbar` | `#FFFFFF` | `#0D0B14` |
| `--surface` | `#FFFFFF` | `#16131F` |
| `--surface-subtle` | `#FAF9FC` | `#1C1829` |
| `--surface-muted` | `#F5F3F8` | `#1F1B2D` |
| `--input` | `#FFFFFF` | `#0D0B14` |
| `--input-border` | `#9A94AE` | `#6E6883` |
| `--border` | `#E4E1EF` | `#3A354C` |
| `--border-subtle` | `#F0EEF5` | `#1F1B2C` |
| `--border-strong` | `#D8D3E5` | `#4B4560` |
| `--overlay` | `rgb(13 11 20 / 0.5)` | `rgb(0 0 0 / 0.6)` |

**Texto**

| Token | Claro | Escuro |
|---|---|---|
| `--foreground` | `#16131F` | `#F8F7FC` |
| `--foreground-secondary` | `#3A354C` | `#D4CFE3` |
| `--muted` | `#6E6883` | `#9A94AE` |

**Marca**

| Token | Claro | Escuro |
|---|---|---|
| `--primary` | `#7C3AED` | `#A78BFA` |
| `--primary-hover` | `#6D28D9` | `#C4B5FD` |
| `--primary-foreground` | `#FFFFFF` | `#0D0B14` |
| `--accent` | `#EDE9FE` | `#242033` |
| `--accent-foreground` | `#5B21B6` | `#F8F7FC` |
| `--accent-subtle` | `#F5F3FF` | `#2A2445` |
| `--accent-subtle-foreground` | `#6D28D9` | `#C4B5FD` |
| `--accent-border` | `#DDD6FE` | `#4C3F7A` |

**Estados**

| Token | Claro | Escuro |
|---|---|---|
| `--danger` | `#DC2626` | `#F87171` |
| `--danger-hover` | `#B91C1C` | `#EF4444` |
| `--danger-foreground` | `#FFFFFF` | `#0D0B14` |
| `--danger-text` | `#991B1B` | `#FCA5A5` |
| `--danger-soft` | `#FEF2F2` | `#3B1219` |
| `--danger-border` | `#FECACA` | `#6B1D22` |
| `--warning-soft` | `#FFFBEB` | `#242033` |
| `--warning-soft-foreground` | `#B45309` | `#FBBF24` |
| `--warning-text` | `#92400E` | `#FBBF24` |
| `--warning-border` | `#FDE68A` | `#5B4516` |
| `--success-soft` | `#ECFDF5` | `#062B20` |
| `--success-soft-foreground` | `#047857` | `#34D399` |
| `--success-border` | `#BBF7D0` | `#0F4D35` |
| `--info` | `#2563EB` | `#60A5FA` |
| `--info-soft` | `#EFF6FF` | `#0F1D3D` |
| `--info-text` | `#1E3A8A` | `#BFDBFE` |
| `--info-border` | `#BFDBFE` | `#1E3A8A` |
| `--status-success` | `#059669` | `#34D399` |
| `--status-danger` | `#DC2626` | `#F87171` |
| `--status-neutral` | `#6E6883` | `#9A94AE` |

**Painel de código** (`--code-*`, usado na tela de acesso): decorativo e igual nos dois temas, definido só em `:root`. Valores: `--code-surface` `#0D0B14`, `--code-border` `#242033`, `--code-text` `#F8F7FC`, `--code-muted` `#9A94AE`, `--code-faint` `#6E6883`, `--code-accent` `#A78BFA`, `--code-highlight` `#FBBF24`, `--code-success` `#34D399`, `--code-error` `#F87171`, `--code-dot-red` `#FB7185`.

**Tipografia** (só em `:root`): `--font-sans` (Inter), `--font-heading` (Plus Jakarta Sans) e `--font-mono` (JetBrains Mono). As famílias são carregadas em `index.html`.

**Sombras** (só em `:root`, mas os canais vêm de `--shadow-rgb`, que muda por tema: `22 19 31` no claro e `0 0 0` no escuro): `--shadow-xs`, `--shadow-sm`, `--shadow-md`, `--shadow-overlay`, `--shadow-dialog` e `--shadow-sheet`.

**Equivalentes no aluno (shadcn)**

| Admin | Aluno | Observação |
|---|---|---|
| `--background` | `--background` | Claro igual; escuro difere (ver abaixo) |
| `--sidebar` | `--sidebar` | |
| `--surface` | `--card` | |
| `--foreground` | `--foreground` | |
| `--muted` | `--muted-foreground` | Texto secundário. No aluno, `--muted` é fundo de área |
| `--input-border` | `--input` | Borda do campo |
| `--input` | (sem equivalente) | Fundo do campo. No shadcn, `--input` é a borda |
| `--border` | `--border` | |
| `--primary` | `--primary` | |
| `--primary-foreground` | `--primary-foreground` | |
| `--accent` | `--accent` | |
| `--accent-foreground` | `--accent-foreground` | |
| `--danger` | `--destructive` | |
| `--danger-foreground` | `--destructive-foreground` | |
| `--success-soft-foreground` | `--success-soft-foreground` | |
| `--warning-soft-foreground` | `--warning-soft-foreground` | |
| `--info` | `--info` | |

Diferenças para o aluno:
- O fundo escuro do admin é `ink/800` (`#242033`). No aluno é `ink/950` (`#0D0B14`).
- O `--border` escuro do admin é `ink/700` (`#3A354C`). No aluno é `ink/800`.
- O `--input-border` do admin já usa `ink/400` no claro e `ink/500` no escuro, os mesmos valores que a borda de input do aluno passa a usar (ver nota da §3.2).

**Regra:** controles interativos do admin (botões, campos, paginação, ações de tabela) têm altura mínima de 44px em telas de até 767px. Isso é mais restrito que os 40px da §10.

---

## 4. Tipografia

| Papel | Fonte | Uso |
|---|---|---|
| Headings / Display | **Plus Jakarta Sans** 600–800 | títulos, números de métricas |
| UI / corpo | **Inter** 400–600 | todo o resto |
| Código | **JetBrains Mono** 400–700 | snippets, nomes de tecnologia, durações, atalhos (`⌘K`) |

| Text Style (Figma) | Classe | Tamanho/linha | Uso |
|---|---|---|---|
| Display/2XL | `typo-display-2xl` | 64/68 | hero da landing (1 por página) |
| Display/XL | `typo-display-xl` | 48/54 | hero de páginas internas, glifos |
| Heading/H1 | `typo-h1` | 36/42 | título de seção da landing |
| Heading/H2 | `typo-h2` | 30/36 | título de página no app |
| Heading/H3 | `typo-h3` | 24/30 | valor de Stat, título de bloco |
| Heading/H4 | `typo-h4` | 20/28 | título de card grande, preço |
| Heading/H5 | `typo-h5` | 16/24 | título de card, item de roadmap |
| Body/LG | `text-lg` | 18/28 | lead do hero |
| Body/Base | `text-base` | 16/24 | parágrafos |
| Body/SM | `text-sm` | 14/20 | **padrão de UI** (botões, inputs, menus) |
| Body/XS | `text-xs` | 12/16 | metadados, legendas |
| Label/Overline | `typo-overline` | 12/16, caps, +1.2 | eyebrow acima de títulos, sempre `text-primary` |
| Code/Base · SM · Label | `font-mono text-sm` · `text-xs` · `text-xs font-medium` | 14/22 · 12/18 · 12/16 | código |

Regras:
- Hierarquia por **tamanho e peso**, nunca por cor. Títulos em `text-foreground`.
- Destaque de palavra no display com `<span className="text-primary">`. No máximo uma por título.
- Largura de leitura: `max-w-prose` (cerca de 65ch) em textos corridos.

---

## 5. Espaçamento e layout

- Escala Tailwind padrão (base 4px). No Figma: coleção **Spacing** (`space/1` = 4px … `space/24` = 96px).
- **Container:** `mx-auto max-w-7xl px-6 xl:px-0`. A landing no Figma tem 1440 de largura com 120 de margem, o que dá 1200 de conteúdo.
- **Ritmo vertical da landing:** `py-24` entre seções. No app: `gap-7` entre blocos e `p-8` no conteúdo.
- **Cards:** `p-5` (compacto) ou `p-6` (padrão). Gap interno de `gap-3` a `gap-4`.
- **Grids:** catálogo `grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6`. Dashboard: conteúdo `flex-1` + coluna lateral `w-[380px]` (empilha abaixo de `lg`).
- **App shell:** Sidebar de 264px (`--sidebar-width: 16.5rem`) + topbar de 68px com borda inferior.

---

## 6. Raios, bordas e sombras

| Token | Valor | Uso |
|---|---|---|
| `rounded-sm` | 8px | checkbox, kbd |
| `rounded-md` | 10px | **botões, inputs, tabs** |
| `rounded-lg` | 12px | tiles de ícone, popovers |
| `rounded-xl` | 16px | **cards**, code window |
| `rounded-2xl` | 20px | faixas (stats band) |
| `rounded-3xl` | 24px | CTA box da landing |
| `rounded-full` | — | badges, avatares, progress |

- Borda padrão: `border` (1px, `border-border`). Inputs usam `border-input`.
- **Sombras são discretas.** `shadow-sm` em cards, `shadow-lg` só em elementos flutuantes (popover, cards do hero) e `shadow-brand` só no CTA principal. No dark, sem sombra: a separação vem da borda.
- Foco: `focus-visible:ring-[3px] ring-ring/50` (padrão shadcn). Nunca remova o outline sem substituto.

---

## 7. Ícones

- **lucide-react** exclusivamente, com `strokeWidth={2}`.
- Tamanhos: `size-4` em botões e inputs, `size-5` na sidebar e em tiles de Stat, `size-6` em destaques.
- Ícone em tile: `size-11 rounded-lg bg-secondary text-primary grid place-items-center`.
- Ícone sem texto exige `aria-label` ou `<span className="sr-only">`.
- No Figma, os ícones estão representados por glifos de texto como placeholder. Na implementação, sempre lucide.

---

## 8. Motion

- Duração: 150ms (hover/estado) e 200–250ms (entrada de overlays). Easing `ease-out`.
- Hover de card clicável: `transition hover:-translate-y-0.5 hover:shadow-md`.
- Sempre respeite `motion-reduce:transition-none motion-reduce:hover:translate-y-0`.
- Celebrações (aula concluída, streak) podem usar confetti ou pulse, com no máximo 1 animação por evento.

---

## 9. Dark mode

- Todos os tokens semânticos têm valor Dark (no Figma, troque o modo da coleção **Theme** no frame).
- No dark, `primary` fica **mais claro** (violet-400) com `primary-foreground` **escuro**. É isso que garante contraste tanto como fundo de botão quanto como cor de link.
- Faixas `inverse` e o footer ficam sempre escuros: aplique `className="dark"` no container para que os filhos (Stat, Card) usem os tokens dark.
- `code` é escuro nos dois temas.

---

## 10. Acessibilidade (validado)

Contraste WCAG calculado sobre os tokens (mínimo AA 4.5:1 para texto normal):

| Par | Light | Dark |
|---|---|---|
| foreground / background | 18.3 | 18.3 |
| muted-foreground / background | 5.3 | 6.7 |
| muted-foreground / muted | 5.0 | 5.4 |
| primary-foreground / primary | 5.7 | 7.2 |
| primary (como texto) / background | 5.7 | 7.2 |
| secondary-foreground / secondary | 6.5 | 11.4 |
| destructive-foreground / destructive | 4.8 | 4.8 |
| success-soft-foreground / success-soft | 5.2 | 8.2 |
| warning-soft-foreground / warning-soft | 4.8 | 9.5 |
| code-accent / code | 10.2 | 10.2 |

Outras regras: alvo mínimo de toque de 40px (`size="default"`); status nunca só por cor (sempre ícone ou texto: "✓ Concluído"); `<Progress>` sempre acompanhado do percentual em texto.

---

## 11. Voz e conteúdo

- **pt-BR, de dev para dev.** Direto, sem jargão corporativo. Pode usar termos técnicos em inglês quando é assim que se fala: deploy, commit, hook, pull request.
- Botões no **infinitivo com verbo de ação**: "Começar agora", "Retomar aula", "Resolver". Evite "Clique aqui" e "Enviar".
- Números em formato BR: `15 mil+`, `R$ 197`, `2.340 XP`, `6h 40m`.
- Mensagens de erro dizem o que fazer: "Senha precisa de 8+ caracteres", e não "Senha inválida".

---

## 12. Figma ↔ código

| Figma | Código |
|---|---|
| Variável `Theme/primary` (code syntax `var(--primary)`) | `--primary` → `bg-primary` |
| Variável `Radius/radius/xl` | `rounded-xl` |
| Variável `Spacing/space/6` | `p-6` / `gap-6` |
| Text style `Heading/H1` | `typo-h1` |
| Effect style `Shadow/SM` | `shadow-sm` |
| Componente `Button` com `variant=outline, size=lg` | `<Button variant="outline" size="lg">` |

Estrutura do arquivo Figma: **Cover · Getting Started · Foundations · Components** (Atoms + Blocks) **· Screens** (Landing, Dashboard do aluno).

**Processo de mudança:** altere o token no Figma, regenere o `globals.css`, abra PR com screenshot Light/Dark e marque o design owner.

---

## 13. Do / Don't

| ✅ Faça | ❌ Não faça |
|---|---|
| Um único `Button variant="default"` por área visível | Três botões roxos lado a lado |
| `<CodeWindow>` ou glifo de tecnologia como ilustração | Foto de banco de "pessoa sorrindo no laptop" |
| `text-muted-foreground` para texto secundário | `opacity-50` em texto |
| `Badge variant="code"` para nomes de tecnologia | Badge colorido por tecnologia (azul p/ React, amarelo p/ JS…) |
| Tokens semânticos | `bg-[#7C3AED]`, `text-violet-600` |
| Estados vazios com ilustração de código e CTA | Tela em branco com "Nenhum item" |
