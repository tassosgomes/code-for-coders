---
id: TASK-15
title: 'Tema universal no SPA admin (claro, escuro ou sistema em todas as telas)'
status: Done
assignee: []
created_date: '2026-10-10 18:49'
updated_date: '2026-10-10 19:03'
labels: []
dependencies: []
references:
  - admin-spa-src-components-app-shell
modified_files:
  - src/admin-spa/package.json
  - src/admin-spa/package-lock.json
  - src/admin-spa/index.html
  - src/admin-spa/src/app/providers.tsx
  - src/admin-spa/src/components/app-shell.tsx
  - src/admin-spa/src/components/auth-layout.tsx
  - src/admin-spa/src/components/theme-menu.tsx
  - src/admin-spa/src/components/theme-menu.test.tsx
  - src/admin-spa/src/stores/use-shell-store.ts
  - src/admin-spa/src/assets/theme.css
  - src/admin-spa/src/assets/theme-tokens.test.ts
  - src/admin-spa/src/assets/app.css
  - src/admin-spa/src/assets/authoring.css
priority: high
type: bug
ordinal: 13000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
O tema do admin-spa é decidido por tela. O escuro automático existe só no início e na Autoria (authoring.css, via :has(.authoring-page)), e Vídeos tem tokens próprios sempre claros. Com a conta de professor, abrir Vídeos a partir do início troca o tema. O tema também não é global: o botão só aparece em duas telas, a escolha não persiste e telas públicas (login, recuperação, convite) não têm tema. Referência: student-spa usa next-themes com classe no html, padrão sistema e menu Claro/Escuro/Sistema.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Existe um único estado de tema (claro, escuro ou sistema), aplicado ao html em todas as rotas, inclusive login, recuperação de senha, redefinição e convite
- [x] #2 Um menu Claro/Escuro/Sistema fica disponível em todas as telas autenticadas e nas telas públicas, no mesmo modelo do student-spa
- [x] #3 A escolha persiste no navegador e o padrão é seguir o sistema
- [x] #4 Navegar entre quaisquer telas não altera o tema escolhido; Vídeos e Auditoria acessadas a partir do início não trocam o tema
- [x] #5 Nenhuma cor literal em app.css fora do arquivo de tokens; tokens claros preservam a aparência atual
- [x] #6 Lint, build e testes do admin-spa passam, com teste de tema e validação visual das telas nos dois temas
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Adicionar next-themes@^0.4.6 (mesma versão do student-spa) ao admin-spa e configurar ThemeProvider em src/app/providers.tsx com attribute=class, defaultTheme=system, enableSystem e disableTransitionOnChange, aplicando html.light/html.dark.
2. Criar src/components/theme-menu.tsx com o menu Claro/Escuro/Sistema no mesmo modelo do student-spa, adaptado ao menu de conta existente (role menu, menuitemradio, aria-checked).
3. Colocar o menu no header do AppShell (todas as telas autenticadas) e no AuthLayout (login, recuperação, redefinição e convite).
4. Remover o estado de tema do use-shell-store, o data-theme do AppShell, as regras escopadas por página de authoring.css (incluindo o prefers-color-scheme) e os tokens --video-* de app.css.
5. Definir tokens semânticos globais em :root (claro, valores atuais) e :root.dark (escuro, a partir da paleta de authoring.css) e substituir as cores literais de app.css por var(--token), mantendo o tema claro visualmente igual.
6. Guardrail: teste que falha se app.css tiver cor literal fora do bloco de tokens; teste do menu de tema e da persistência.
7. Validar com lint, typecheck/build, testes do admin-spa e conferência visual no navegador nos dois temas, incluindo Vídeos e Auditoria a partir do início.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Decisões: next-themes 0.4.6 (mesma versão do student-spa), attribute=class, defaultTheme=system, enableSystem, chave localStorage 'theme'. Tokens em src/assets/theme.css: valores claros = literais atuais; escuros = paleta da Autoria aprovada no Figma, com derivados para status e superfícies que não tinham desenho escuro. app.css e authoring.css perderam o escopo por página (:has(.authoring-page) e :has(.videos-page)) e o prefers-color-scheme; só sobrou layout. Script inline em index.html aplica a classe antes da pintura (o script do next-themes não executa no React 19 no cliente).

Validação: lint OK; build OK (avisos de runtime-env e tamanho de bundle já existentes); 47 arquivos e 254 testes passam, incluindo theme-tokens.test.ts (guardrail: sem cor literal em app.css/authoring.css, toda var() definida, claro e escuro com as mesmas chaves) e theme-menu.test.tsx (Claro/Escuro/Sistema, persistência, matchMedia, Escape). Playwright em 1440px: capturas claro e escuro de Início, Vídeos, Autoria, Auditoria, Acessos, Catálogo, Financeiro, Cortesias e Entrar. Início->Vídeos e Início->Auditoria mantêm a classe e as cores nos dois temas. Recuperar senha, redefinir senha e convite têm o menu e o tema escuro. Claro comparado pixel a pixel com o HEAD: diferenças apenas no botão de tema novo no cabeçalho, na cor do texto da navegação da Autoria (agora --foreground-secondary, antes #16131f) e 2 px no formulário de login.

Exceções de aparência no claro: tons quase iguais foram unificados (#fbfaff e #f9f7ff em #faf9fc; #f2f0f6 e #f4f2f8 em #f5f3f8; #027a48 e #166534 em #047857; #ecfdf3 e #f0fdf4 em tokens de sucesso; #fff7ed e #fffaeb em aviso; #0006 do backdrop do financeiro passou a 0.5, igual aos demais overlays). Limites: o RouteError do nível raiz renderiza fora do shell e não tem o botão de tema (o tema é aplicado, mas não há menu nessa tela); escuro das telas que não tinham desenho aprovado (Auditoria, Financeiro, Catálogo, Acessos, Cortesias) é derivado e precisa de revisão de design; contraste WCAG não medido formalmente; 390px não capturado. student-spa não foi alterado.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Tema agora é global no admin-spa: um estado (claro, escuro ou sistema) com next-themes, aplicado ao html em todas as rotas, incluindo as públicas. O menu Claro/Escuro/Sistema fica no cabeçalho e no AuthLayout. As cores vêm de tokens em theme.css; app.css e authoring.css não têm mais escopo por página. Verificado com lint, build, 254 testes, guardrail de tokens e capturas nos dois temas, incluindo o fluxo que motivou o bug (professor: início escuro -> Vídeos mantém o escuro).
<!-- SECTION:FINAL_SUMMARY:END -->
