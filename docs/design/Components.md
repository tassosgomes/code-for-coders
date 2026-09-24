# Code4Coders — Guia de Componentes

> Qual componente usar, quando usar e como. Complementa o `DESIGN.md` (tokens e regras visuais).
> **Figma:** página *Components* do arquivo [Code4Coders — Design System](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn). Os nomes de componentes e de variants no Figma são **idênticos** às props em código.

---

## 1. Estrutura e regras

```
components/
├── ui/        # shadcn/ui — gerado pelo CLI. Só edite para ajustar variants (seção 3).
├── blocks/    # composições da plataforma (CourseCard, CodeWindow, Stat, LessonItem…)
└── layout/    # AppShell, SiteHeader, SiteFooter
```

1. **shadcn primeiro.** Se existe em `ui/`, use. Se falta, rode `npx shadcn@latest add <nome>` antes de criar qualquer coisa.
2. **Block só com 3+ usos.** Uma composição vira `blocks/` quando aparece em três ou mais telas. Antes disso, componha inline.
3. **Nada de estilo de marca fora de `ui/` e `blocks/`.** Páginas só fazem layout (`grid`, `flex`, `gap`, `max-w`).
4. **Customize por variant, não por `className`.** Se você está passando o mesmo `className` em três lugares, isso é um variant novo: adicione no `cva`, no Figma e neste guia.
5. **Ícones:** `lucide-react`, `size-4` dentro de componentes.

### Instalação (base v1)

```bash
npx shadcn@latest add button badge input label textarea select checkbox switch \
  avatar progress tabs card separator skeleton tooltip dropdown-menu dialog sheet \
  accordion sidebar command form sonner table breadcrumb pagination
```

---

## 2. "Preciso de…" → use

| Preciso de… | Use | Evite |
|---|---|---|
| Ação principal da tela | `Button variant="default"` (1 por área) | vários `default` competindo |
| Ação secundária | `Button variant="outline"` ou `"secondary"` | `ghost` fora de toolbars |
| Ação em toolbar, topbar, ícone | `Button variant="ghost" size="icon"` + `Tooltip` | ícone sem label acessível |
| Link dentro de texto ou "Ver todos" | `Button variant="link"` ou `<Link>` | `<a>` estilizado à mão |
| Ação destrutiva | `Button variant="destructive"` **dentro de** `AlertDialog` | excluir sem confirmação |
| Nível, status, contagem | `Badge` (`secondary` · `success` · `warning` · `outline`) | texto colorido solto |
| Nome de tecnologia (React, Python…) | `Badge variant="code"` | cor por tecnologia |
| Campo de texto | `Form` + `FormField` + `Input` + `Label` | `Input` sem label |
| Escolha única, até 5 opções | `RadioGroup` | `Select` |
| Escolha única, mais de 5 opções | `Select` (ou `Combobox` com busca) | lista de radios longa |
| Liga/desliga com efeito imediato | `Switch` | `Checkbox` |
| Busca global | `CommandDialog` (⌘K) | página de busca separada |
| Confirmação / formulário curto | `Dialog` / `AlertDialog` | nova página |
| Painel de detalhes, filtros, menu mobile | `Sheet` | `Dialog` gigante |
| Menu de ações de um item | `DropdownMenu` | botões enfileirados |
| Navegação entre vistas do mesmo contexto | `Tabs` | `Select` como navegação |
| Módulos do currículo | `Accordion` + `LessonItem` | lista plana de 80 aulas |
| Feedback de ação ("Aula concluída!") | `toast()` do **Sonner** | `alert()`, banner fixo |
| Aviso persistente na página | `Alert` | toast |
| Carregando | `Skeleton` no formato do conteúdo | spinner em tela cheia |
| Progresso de curso | `Progress` + texto com % | barra sem número |
| Pessoa (aluno, instrutor) | `Avatar` + `AvatarFallback` com iniciais | `<img>` sem fallback |
| Dados tabulares (admin, ranking) | `Table` (+ TanStack Table se precisar de ordenação/filtro) | grid de cards |
| Navegação do app logado | `Sidebar` (shadcn) | sidebar custom |
| Código / terminal | `CodeWindow` (block) | `<pre>` cru |

---

## 3. Atoms (shadcn com ajustes de variant)

### Button — `components/ui/button.tsx`

Figma: `Button` · `variant` × `size` (18 variantes) · props `Label`, `Icon`.

Ajustes em relação ao shadcn padrão: alturas 32/40/48 (em vez de 32/36/40), raio `rounded-md` (10px) e sombra `xs` só no `default`.

```tsx
const buttonVariants = cva(
  "inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-md text-sm font-semibold transition-all disabled:pointer-events-none disabled:opacity-50 [&_svg]:pointer-events-none [&_svg:not([class*='size-'])]:size-4 shrink-0 [&_svg]:shrink-0 outline-none focus-visible:border-ring focus-visible:ring-ring/50 focus-visible:ring-[3px] aria-invalid:ring-destructive/20 aria-invalid:border-destructive",
  {
    variants: {
      variant: {
        default: "bg-primary text-primary-foreground shadow-xs hover:bg-primary/90",
        secondary: "bg-secondary text-secondary-foreground hover:bg-secondary/80",
        outline: "border border-input bg-background hover:bg-accent hover:text-accent-foreground",
        ghost: "hover:bg-accent hover:text-accent-foreground",
        destructive: "bg-destructive text-destructive-foreground hover:bg-destructive/90 focus-visible:ring-destructive/20",
        link: "text-primary underline-offset-4 hover:underline px-0 h-auto",
      },
      size: {
        sm: "h-8 px-3 text-xs font-medium",
        default: "h-10 px-4",
        lg: "h-12 px-6 text-base font-medium",
        icon: "size-10",
      },
    },
    defaultVariants: { variant: "default", size: "default" },
  }
)
```

```tsx
<Button size="lg">Explorar trilhas <ArrowRight /></Button>
<Button variant="outline" size="lg">Ver como funciona</Button>
<Button variant="ghost" size="icon" aria-label="Notificações"><Bell /></Button>
```

- Ícone **à direita** para avançar (→) e **à esquerda** para ações de objeto (＋ Nova aula).
- Estado de loading: `disabled` + `<Loader2 className="animate-spin" />` no lugar do ícone, mantendo o label.

### Badge — `components/ui/badge.tsx`

Figma: `Badge` · `variant` = default | secondary | outline | success | warning | destructive | **code**.

```tsx
const badgeVariants = cva(
  "inline-flex items-center gap-1 rounded-full border border-transparent px-2.5 py-0.5 text-xs font-medium w-fit whitespace-nowrap shrink-0 [&>svg]:size-3",
  {
    variants: {
      variant: {
        default: "bg-primary text-primary-foreground",
        secondary: "bg-secondary text-secondary-foreground",
        outline: "border-border text-foreground",
        success: "bg-success-soft text-success-soft-foreground",
        warning: "bg-warning-soft text-warning-soft-foreground",
        destructive: "bg-destructive text-destructive-foreground",
        code: "bg-code text-code-accent font-mono",
      },
    },
    defaultVariants: { variant: "default" },
  }
)
```

| Variant | Quando |
|---|---|
| `secondary` | nível (Iniciante/Intermediário/Avançado), categoria |
| `code` | nome de tecnologia, sempre em mono |
| `success` | Concluído, Aprovado, Testes ok |
| `warning` | Em revisão, Prazo próximo |
| `outline` | contagens neutras ("12 aulas", "8 semanas") |
| `default` | "Novo", "Popular". No máximo 1 por card |
| `destructive` | Expirado, Reprovado |

### Input — `components/ui/input.tsx`

Figma: `Input` · `state` = default | focus | error | disabled · props `Value`, `Leading icon`.

Ajustes: `h-10`, `rounded-md` e `px-3`. Para ícone à esquerda, envolva num `relative` e posicione o ícone com `absolute left-3 size-4 text-muted-foreground` + `pl-9` no input.

```tsx
<FormField control={form.control} name="email" render={({ field }) => (
  <FormItem>
    <FormLabel>E-mail</FormLabel>
    <FormControl><Input type="email" placeholder="voce@exemplo.com" {...field} /></FormControl>
    <FormMessage />
  </FormItem>
)} />
```

- Erro = `aria-invalid` (o shadcn já pinta a borda de `destructive`) + `FormMessage` explicando **como corrigir**.
- Formulários: **react-hook-form + zod**, sempre via `Form` do shadcn.

### Avatar

Figma: `Avatar` · `size` = sm (24) | md (36) | lg (56). Em código: `className="size-6 | size-9 | size-14"`.

```tsx
<div className="flex -space-x-2 *:ring-2 *:ring-background">
  {users.map(u => <Avatar key={u.id} className="size-9"><AvatarImage src={u.photo} /><AvatarFallback>{initials(u.name)}</AvatarFallback></Avatar>)}
</div>
```

Fallback: `bg-accent text-accent-foreground`, com 2 iniciais.

### Progress

Figma: `Progress` · `value` = 0 | 25 | 50 | 75 | 100. Em 100%, o indicador fica `bg-success`.

```tsx
<div className="space-y-1.5">
  <div className="flex justify-between text-xs"><span className="text-muted-foreground">Aula 18 de 48</span><span className="font-medium text-primary">38%</span></div>
  <Progress value={38} className="h-2 [&>[data-slot=progress-indicator]]:data-[complete=true]:bg-success" />
</div>
```

> Adicione `data-complete={value === 100}` ao Indicator em `ui/progress.tsx`.

### Tabs

Figma: `Tab` · `state` = active | inactive. Use o `TabsList` padrão (`bg-muted p-1 rounded-lg`). Para 2 a 4 abas use `w-full grid grid-cols-N`.

### Card

Base: `rounded-xl border bg-card shadow-sm`. Em código, use `Card`, `CardHeader`, `CardContent` e `CardFooter`. Não aninhe card dentro de card: use `Separator` ou `bg-muted` para agrupar.

### Sidebar (app logado)

Figma: `Sidebar Item` · `state` = default | active. Use o bloco **shadcn Sidebar** (`SidebarProvider`, `SidebarMenuButton isActive`). Largura via `--sidebar-width: 16.5rem`. Grupos com `SidebarGroupLabel` (visual `typo-overline`). O card "Plano Pro" fica no `SidebarFooter` com `className="dark bg-inverse"`.

---

## 4. Blocks (composições da plataforma)

### CodeWindow — `components/blocks/code-window.tsx`

Figma: `Code Window` · prop `Filename`. É a **assinatura visual da marca**: use no hero, em páginas de curso, em desafios e em estados vazios.

```tsx
type CodeWindowProps = { filename: string; lang?: string; code: string; className?: string }

export async function CodeWindow({ filename, lang = "ts", code, className }: CodeWindowProps) {
  const html = await codeToHtml(code, { lang, theme: c4cTheme }) // shiki, tema c4c (abaixo)
  return (
    <div className={cn("overflow-hidden rounded-xl bg-code text-code-foreground shadow-lg", className)}>
      <div className="flex items-center gap-2 border-b border-white/10 px-4 py-3">
        <span className="size-2.5 rounded-full bg-red-400" /><span className="size-2.5 rounded-full bg-amber-400" /><span className="size-2.5 rounded-full bg-emerald-400" />
        <span className="ml-1 font-mono text-xs text-code-foreground/60">{filename}</span>
        <span className="ml-auto font-mono text-xs uppercase text-code-accent">{lang}</span>
      </div>
      <div className="p-5 font-mono text-sm leading-[22px] [&_pre]:!bg-transparent" dangerouslySetInnerHTML={{ __html: html }} />
    </div>
  )
}
```

Tema shiki `c4c` (única exceção autorizada ao uso de primitivas): keywords `violet-300`, strings e sucesso `emerald-400`, funções `amber-400`, comentários e números de linha `ink-600`, texto `ink-100`.

### CourseCard — `components/blocks/course-card.tsx`

Figma: `Course Card` · `status` = default (catálogo) | in-progress (Continuar aprendendo).

Anatomia: thumbnail 16:9 (imagem do curso **ou** fallback `bg-inverse` com glifo em `text-code-accent`) → `Badge code` (tecnologia) + `Badge secondary` (nível) → título `typo-h5` (máx. 2 linhas, `line-clamp-2`) → instrutor (`Avatar` sm + nome) → meta (★ nota · aulas · horas) → footer (preço + `Button secondary sm` **ou** `Progress`).

```tsx
<CourseCard course={course} />                                  // catálogo
<CourseCard course={course} progress={enrollment.progress} />   // em andamento
```

- O card inteiro é clicável (`<Link>` envolvendo), com `hover:-translate-y-0.5 hover:shadow-md`. O botão interno é só visual (`tabIndex={-1}`, `aria-hidden`).
- Preço riscado: `text-sm text-muted-foreground line-through`.

### Stat — `components/blocks/stat.tsx`

Figma: `Stat` · props `Value`, `Label`.

```tsx
<Stat icon={Code2} value="15 mil+" label="Devs formados" />
```

`Card` horizontal: tile de ícone (`size-11 rounded-lg bg-secondary text-primary`) + valor `typo-h3` + label `text-sm text-muted-foreground`. Na landing ele fica dentro da faixa `dark bg-inverse rounded-2xl p-7`.

### LessonItem — `components/blocks/lesson-item.tsx`

Figma: `Lesson Item` · `state` = completed | current | locked.

| state | Indicador | Título | Container |
|---|---|---|---|
| completed | `bg-success-soft` + ✓ `text-success-soft-foreground` | `text-foreground` | transparente |
| current | `bg-primary` + ▶ `text-primary-foreground` | `font-medium` | `bg-secondary rounded-lg` |
| locked | `bg-muted` + `Lock` `text-muted-foreground` | `text-muted-foreground` | `aria-disabled` |

Duração à direita em `font-mono text-xs text-muted-foreground`. Sempre renderize como `<button>` ou `<Link>`, nunca como `<div onClick>`.

---

## 5. Padrões recorrentes

**Cabeçalho de seção (landing):**
```tsx
<div className="flex items-end justify-between">
  <div className="space-y-2"><p className="typo-overline text-primary">Cursos em alta</p><h2 className="typo-h1">Os mais escolhidos pelos devs</h2></div>
  <Button variant="link">Ver todos os cursos <ArrowRight /></Button>
</div>
```

**Estados vazios:** um `CodeWindow` pequeno com um comentário bem-humorado + título `typo-h4` + 1 `Button`. Exemplo: `// nenhum curso por aqui ainda` e o botão "Explorar trilhas".

**Loading:** `Skeleton` com a mesma geometria do componente real (ex.: `CourseCardSkeleton`). Nunca bloqueie a tela inteira.

**Feedback:** `toast.success("Aula concluída! +50 XP")`. Erros de rede: `toast.error` com ação "Tentar de novo".

**Overlays:** até 3 campos → `Dialog`; lista ou filtros → `Sheet` lateral; no mobile, `Sheet side="bottom"`.

---

## 6. Checklist de PR (UI)

- [ ] Usa apenas componentes de `ui/` e `blocks/`, ou justifica o novo na descrição do PR
- [ ] Zero cores, raios ou sombras hardcoded (`grep -E "#[0-9a-fA-F]{3,6}|\[.*px\]"` limpo)
- [ ] Testado em Light **e** Dark (screenshots no PR)
- [ ] Navegável por teclado, foco visível, ícones com `aria-label`
- [ ] Estados cobertos: loading, vazio, erro
- [ ] Textos em pt-BR seguindo a seção 11 do `DESIGN.md`
- [ ] Variant novo? Adicionado também no Figma e neste guia