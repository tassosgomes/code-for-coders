# Code4Coders — DESIGN.md

> Fonte da verdade visual da plataforma Code4Coders.
> **Figma:** [Code4Coders — Design System](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn) · **Stack:** Next.js + Tailwind CSS v4 + shadcn/ui
> **Arquivos irmãos:** `globals.css` (tokens em código) · `COMPONENTS.md` (guia de componentes)

---

## 1. Princípios

1. **Código é a estrela.** Somos uma plataforma para programadores: no lugar de fotos genéricas de banco de imagem, a ilustração principal é código real (`<CodeWindow>`, glifos de tecnologia, terminal). Fotos só de pessoas reais: instrutores e alunos.
2. **Claro, arejado, uma cor de marca.** A base é neutra com leve toque violeta (`ink`). O violeta (`primary`) é reservado para ação e destaque. Se tudo é roxo, nada é importante.
3. **Verde = funcionou.** O verde (`success` / `code-accent`) comunica teste passando, aula concluída e deploy ok. Não use verde como decoração.
4. **Tokens, nunca valores.** Nenhum hex, px arbitrário ou `bg-violet-600` em componente. Sempre token semântico (`bg-primary`, `text-muted-foreground`, `rounded-xl`).
5. **shadcn primeiro.** Antes de criar qualquer componente, verifique se ele existe em [ui.shadcn.com](https://ui.shadcn.com). Componente custom só para composições recorrentes da plataforma (ver `COMPONENTS.md`).

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
| `--input` | `ink/200` `#E4E1EF` | `ink/700` `#3A354C` |
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
