# Plano de refatoração — Conta do aluno

> **Status:** T1–T8 e E1/E2 implementados; gates da SPA e Notification verificados em 2026-09-24. Revisão de telas feita em cenários representativos desktop/mobile e Light/Dark.
> **Data:** 2026-09-24  
> **Referências:** [Figma — Screens · Conta do aluno](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-Design-System?node-id=33-2&m=dev), [`wireframes-conta-aluno.md`](wireframes-conta-aluno.md), [`DESIGN.md`](../../DESIGN.md), [`Components.md`](Components.md).

## Objetivo

Atualizar a apresentação e a composição das telas existentes do `student-spa` para o Design System Code4Coders, completar os estados de UX definidos no wireframe e preservar os contratos atuais de autenticação, sessão e formulários.

O escopo principal é CAP-001 (T1–T8). Os e-mails E1/E2 de CAP-026 aparecem no wireframe e devem ser tratados como uma frente relacionada no serviço de notificações. Admin, CAP-030 sem tela e catálogo/cursos de CAP-008 ficam fora desta entrega.

## Análise das referências e do código atual

### Figma

A página [Screens — Conta do aluno](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-Design-System?node-id=33-2&m=dev) agora contém todas as telas do escopo, estados desktop principais, variantes mobile representativas, e-mails e validação do tema escuro. O wireframe registra a aprovação e os nodes na seção 8. O fluxo está na página [Fluxo — Conta do aluno](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-Design-System?node-id=56-3&m=dev).

- T1–T8 têm seções próprias: cadastro (`35:97`), confirmação (`46:368`), login (`51:1168`), recuperação (`51:1355`), redefinição (`51:1757`), início (`53:1617`), troca de senha (`53:2134`) e erros (`54:1808`).
- O fluxo de usuário mapeia as rotas, retornos e e-mails (`56:3`). Os e-mails E1/E2 (`54:1856`) usam corpo de 600 px, link textual, validade dinâmica e tema Light.
- A validação Dark (`54:1957`) tem T1.a, T3.a, T6.a e T7.a. As telas públicas mantêm duas colunas de 720 px em desktop; o painel de marca some no mobile de 390 px. A área logada usa sidebar de 264 px e topbar de 68 px.
- Além dos componentes já mapeados, a proposta do Figma inclui `Toast`, `Menu Item` e `Account Menu`. A página de componentes continua identificada como proposta; esses componentes devem ser implementados a partir dos nodes aprovados e documentados no DS durante a entrega.
- Os estados desktop estão detalhados. Mobile cobre os caminhos principais, não cada variação de erro/resultado. A implementação deve manter todos os estados acessíveis e responsivos; revisão nos breakpoints faz parte do aceite, sem exigir um frame separado para cada estado repetido.

### Implementação

- `src/student-spa` já implementa os fluxos e as rotas, com features isoladas, React Router, React Hook Form, Zod, React Query e Vite. A refatoração deve preservar APIs, schemas, hooks de formulário, tokens de idempotência e semântica dos tokens de confirmação/redefinição.
- `RootRoute` atualmente envolve todas as rotas em `AppShell`; `AppShell` exibe a marca técnica `student-spa`, menu em inglês e navegação horizontal. Isso não separa os layouts público e autenticado.
- `src/student-spa/src/assets/globals.css` contém os tokens do DS e é importado por `app.css`; os estilos específicos da aplicação ficam no SPA, junto de quem os consome.
- `student-spa` usa CSS convencional e não tem Tailwind, shadcn/ui ou lucide-react nas dependências. `DESIGN.md` e `Components.md` descrevem Tailwind v4 + shadcn e usam instruções específicas do Next.js. Recomenda-se manter Vite/React e adaptar essa configuração ao Vite; não migrar o SPA para Next.js. A adoção de Tailwind v4 + componentes shadcn deve ser a escolha de implementação, pois é a convenção expressa nos documentos de design.
- O contrato atual de sessão fornece `accountId`, `name` e `csrfToken`, mas não e-mail nem endpoint de perfil. Para preservar os contratos, a identidade no dashboard e no menu usa o nome; e-mail/perfil dependem de uma evolução futura da API.
- `DashboardScreen` ainda apresenta o health-check técnico do workspace; `RouteError` contém texto em inglês; a tela de troca de senha ainda usa uma página de sucesso em vez do retorno ao início com toast.
- A renderização de e-mail em `src/notification/.../MessageTemplateRenderer.cs` hoje gera somente texto em inglês. O modelo `TransactionalEmail` já aceita `HtmlBody`, e os adapters SMTP/HTTP já transmitem texto e HTML.

## Telas, estados e código envolvidos

| Item | Rota / estado | Código atual principal | Refatoração prevista |
|---|---|---|---|
| T1 — Criar conta | `/cadastro`: formulário, envio, conta existente, erro genérico, sucesso | `app/routes/student-registration-route.tsx`; `features/student-registration/components/student-registration-screen.tsx` e hooks da feature | AuthLayout; campos/PasswordField; alerta de conta existente com ações Entrar, Recuperar senha e Reenviar confirmação; sucesso neutro de envio; responsividade. |
| T2 — Confirmar conta | `/confirm-account?token=`: validando, confirmado, expirado/usado; pedido de reenvio e resposta neutra | `app/routes/student-confirmation-route.tsx`; `features/student-confirmation/components/student-confirmation-screen.tsx` | Skeleton acessível durante validação; CTA de entrada após confirmação; link indisponível com novo pedido; estado explícito de reenvio vindo do login sem token; mensagem neutra. |
| T3 — Entrar | `/entrar`: padrão, envio, credenciais inválidas, conta pendente, sessão expirada | `app/routes/student-login-route.tsx`; `features/student-session/components/student-login-screen.tsx`; `hooks/use-student-session-events.ts` | AuthLayout e PasswordField; manter mensagem genérica para credenciais; ação de reenviar confirmação; alerta transitório após expiração, limpo ao entrar; toast ao sair manualmente. |
| T4 — Recuperar senha | `/recuperar-senha`: formulário, resposta neutra, falha ao enviar | `app/routes/student-password-recovery-route.tsx`; `features/student-password-recovery/components/student-password-recovery-screen.tsx` | AuthLayout; resposta independente de a conta existir; Alert de rede preservando o e-mail digitado. |
| T5 — Redefinir senha | `/redefinir-senha?token=`: formulário, senha fora da política, redefinida, link indisponível, falha ao salvar | Mesmo route/component de T4, modo `reset` | PasswordField e checklist; erro de política inline mantém token válido; falha recuperável mantém formulário; sucesso informa encerramento de outras sessões; token sai da URL ao abrir e não aparece na telemetria. |
| T6 — Início logado | `/`: carregando, vazio, erro de carregamento | `app/routes/dashboard-route.tsx`; `features/student-dashboard/components/dashboard-screen.tsx`; `features/student-session/components/student-session-panel.tsx` | AppShell; boas-vindas, estado vazio sem CTA de catálogo, card da conta; substituir health-check visível por conteúdo honesto; Skeleton; cards empilhados no mobile. |
| T7 — Trocar senha | `/trocar-senha`: formulário, envio, rejeição, sucesso | `app/routes/student-password-change-route.tsx`; `features/student-password-change/components/student-password-change-screen.tsx` | Dentro do AppShell; campos atual/nova senha com visibilidade; checklist e aviso sobre encerramento das outras sessões; rejeição em Alert; sucesso volta a T6 com toast. |
| T8 — Erro de rota | 404 e falha inesperada | `app/routes/route-error.tsx`; `app/router.tsx` | Mensagens e ações em pt-BR; aparência integrada ao layout público/logado e estados distintos de 404 e falha inesperada. |
| Global — Sessão | Expiração em qualquer rota logada; saída voluntária | `features/student-session/hooks/use-student-session-events.ts`; `lib/api-client.ts`; `features/student-session/components/student-session-panel.tsx` | Levar o motivo de expiração até o login para mostrar o Alert especificado; saída manual recebe toast, sem alerta de expiração. |
| E1/E2 — E-mail | Confirmação de conta e recuperação de senha | `src/notification/src/CodeForCoders.Notification.Application/Services/MessageTemplateRenderer.cs`; testes do serviço de notificações | Em frente separada de CAP-026: assunto/conteúdo pt-BR, HTML compatível com e-mail e alternativa texto, largura de 600 px, link copiável, validade configurada e apenas o nome como dado pessoal. |

## Componentes envolvidos

### Reaproveitar ou adaptar

- Tokens semânticos de `src/student-spa/src/assets/globals.css` e regras de cor, tipografia, espaçamento, raio, dark mode e acessibilidade de `DESIGN.md`.
- `FormTextField` em `src/student-spa/src/components/ui/form.tsx`, integrado a React Hook Form. Adaptar a apresentação mantendo os schemas Zod como fonte de validação.
- `passwordPolicySchema` e `studentPasswordSchema` existentes para os requisitos ao vivo; não criar uma segunda política no componente visual.
- Rotas e APIs/hook existentes nas features de cadastro, sessão, confirmação, recuperação, dashboard e troca de senha.

### Criar ou completar na camada compartilhada

- `AuthLayout` e `AuthBrandPanel`, `BrandLogo` e `CodeWindow` para páginas públicas.
- Primitivos DS: `Button`, `Card`, `Alert`, `Input`, `Label`, `Avatar`, `Skeleton`, `DropdownMenu`, `Sidebar` e `Sheet`; `Sonner` para feedback breve.
- `PasswordField` configurável (mostrar/ocultar, link opcional e lista de requisitos) e `PasswordRequirement` com ícone e texto para os estados atendido/pendente.
- `StatusTile` para feedback de sucesso, aviso e estado principal.
- `AppShell` visual compartilhado, responsivo, com Sidebar de 264 px, Topbar de 68 px e navegação mobile em Sheet; a navegação da conta do aluno deve ser composta em `app/`, sem fazer `components/` importar uma feature.
- Seletor de tema no Topbar para Light/Dark conforme o fluxo/DS aprovado.
- `Toast`, `Menu Item` e `Account Menu`, conforme a proposta de componentes do Figma; o menu expõe identidade, Trocar senha e Sair.

O componente de UI compartilhado deve permanecer sem conhecimento de domínio. As páginas e decisões de negócio continuam nas features; `app/routes/` faz a composição entre layout, dashboard e componentes de sessão.

## Sequência proposta

### Etapa 0 — Preparar execução e fechar decisões técnicas

1. Usar as decisões de `wireframes-conta-aluno.md` §7 e os nodes aprovados na §8 como baseline. Atualizar o fluxo e as cópias somente se a implementação revelar conflito com os contratos atuais.
2. Conferir o mapeamento dos componentes da proposta Figma para `src/components/ui` e blocos/layouts compartilhados; registrar no `Components.md` as variantes implementadas.
3. Fechar a adaptação do DS ao SPA Vite: Tailwind v4 pelo plugin oficial do Vite e componentes shadcn gerados/adaptados para o código existente. Definir carregamento de fontes sem `next/font`, inclusão dos tokens via fluxo gerador e configuração do dark mode.
4. Corrigir a referência documental de `COMPONENTS.md` para o caminho real `docs/design/Components.md` numa revisão dos documentos.

**Saída:** configuração técnica definida, mapa Figma→componentes/código e decisões de produto existentes registradas para implementação. Não há dependência de desenhar novas telas para iniciar.

### Etapa 1 — Base visual e layouts

1. Integrar os tokens do Figma ao `student-spa` pelo mecanismo de geração definido; remover aos poucos o CSS legado de `assets/app.css`, sem editar valores gerados à mão.
2. Implementar os primitivos compartilhados e validar suas variantes de foco, erro, disabled, sucesso e tema.
3. Separar a árvore de rotas em composição pública e autenticada: AuthLayout para T1–T5; AppShell para T6–T7; manter no root apenas providers e efeitos globais de sessão. Configurar boundaries de erro adequadas às áreas.
4. Garantir marca Code4Coders, pt-BR, fontes, foco visível, ícones Lucide com nomes acessíveis e layout desktop/mobile.

**Saída:** base visual usada por ao menos uma rota pública e uma rota autenticada, com rotas antigas e contratos ainda preservados.

### Etapa 2 — Componentes de formulário e fluxos públicos

1. Entregar PasswordField/PasswordRequirement e integrar com formulários existentes de cadastro, login, redefinição e troca de senha.
2. Implementar T1–T3 conforme os frames: estados de cadastro duplicado, confirmação/reenvio, login inválido, conta não confirmada, sessão expirada e toast após sair. Para reenvio a partir do login, usar o estado T2.d sem token, sem alterar o endpoint.
3. Implementar T4/T5 com os estados visuais novos de falha de rede e senha fora da política; manter a resposta neutra, retirar o token da URL ao abrir o reset e preservar a validade do token quando a senha digitada ainda não atende à política.
4. Cobrir envio, erros conhecidos e genéricos, links vencidos e feedback acessível com testes de integração a partir das rotas.

**Saída:** fluxos públicos completos e coerentes em desktop/mobile, sem alteração dos contratos de conta existentes.

### Etapa 3 — Área logada e estados globais

1. Implementar Sidebar/Topbar e composição do menu de conta (nome, Trocar senha, Sair); usar Sheet em viewport mobile. E-mail/perfil ficam fora da UI enquanto não houver fonte de dados no contrato atual.
2. Refatorar T6 para boas-vindas, empty state e card de conta; remover o health-check da UI do aluno, sem remover a API operacional se ainda for usada em outro contexto.
3. Refatorar T7, incluindo política de senha, aviso de sessões e retorno ao início com toast após sucesso.
4. Preservar a diferença entre expiração e saída voluntária e implementar T8 para 404 deslogado, 404 logado e falha inesperada em pt-BR.
5. Cobrir loader e guardas de sessão, menu móvel, expiração, saída, troca de senha e renderização de erro em testes de integração.

**Saída:** experiência logada completa, sem placeholder técnico ou cópia em inglês.

### Etapa 4 — E-mails CAP-026 (frente relacionada)

1. Evoluir `MessageTemplateRenderer` para compor versões HTML e texto em pt-BR para confirmação e recuperação.
2. Usar layout de e-mail simples, largura máxima de 600 px, estilos inline compatíveis, marca, CTA e URL textual. Manter texto alternativo legível sem imagens.
3. Usar a validade já configurada por propósito; não inventar duração nem expor e-mail/token em logs. Validar o modelo renderizado e a entrega pelos adapters já compatíveis com HTML.

**Saída:** mensagens E1/E2 aderentes ao Figma e às restrições do PRD, com testes do renderer e do payload entregue.

### Etapa 5 — Revisão visual e gate de entrega

- Conferir as telas representativas contra os frames aprovados em 1440 px e 390 px; rever Light/Dark nos frames definidos. Os testes de integração cobrem os estados de fluxo sem frame mobile próprio.
- Fazer revisão de teclado, foco, leitor de tela, mensagens de status, alvos de toque e contraste conforme `DESIGN.md`.
- Rodar no `student-spa`: lint, build/typecheck e testes Vitest afetados; rodar os testes do serviço Notification alterado. Revisar screenshots e registrar exceções antes do merge.
- Atualizar `docs/design/Components.md` para `PasswordField`, layouts e demais blocos realmente adicionados; manter Figma e documentação alinhados.

## Registro da execução

- Base SPA: Tailwind v4 pelo plugin Vite, tokens canônicos em `src/student-spa/src/assets/globals.css`, componentes shadcn gerados pelo CLI e layouts `AuthLayout`/`AppShell`.
- Fluxos: cadastro, confirmação e reenvio, login, recuperação e redefinição, dashboard, troca de senha, limites de rota, sessão expirada e logout.
- Blocos e documentação: `BrandLogo`, `CodeWindow`, `StatusTile`, `PasswordField`, seletor de tema, Sidebar/Sheet, Sonner e correção do caminho de `Components.md`.
- Conta: o menu e o card mostram o nome disponível na sessão; e-mail e perfil não foram inventados nem adicionados ao contrato.
- Revisão visual: login em 1440 px e 390 px, dashboard autenticado simulado em 1440 px e 390 px, Light e Dark. A validação foi visual/manual e não adicionou screenshots ao repositório.

## Gates de aceite

1. Todas as rotas T1–T8 têm os estados de aceite definidos e não apresentam shell/navegação incorretos.
2. Os fluxos de reenvio e recuperação não revelam se um e-mail existe; links expirados mantêm ação recuperável.
3. Os tokens e componentes do DS são usados sem cores decorativas hardcoded; a política de senha continua derivada dos schemas existentes.
4. App autenticado funciona em desktop e mobile; páginas públicas ocultam o painel de marca no mobile, conforme Figma.
5. Expiração de sessão comunica a causa no login; logout manual confirma a saída com toast.
6. Dashboard não apresenta health-check nem CTA para catálogo inexistente.
7. E-mails têm versão HTML e texto, link visível e validade configurada; testes confirmam o conteúdo necessário sem revelar segredos.
8. Lint, build/typecheck e testes afetados passam antes de integrar.

## Riscos e dependências

- **Cobertura mobile por estado:** alguns erros/toasts têm frame só em desktop. Implementar os mesmos estados nos componentes responsivos e validar em 390 px; pedir extensão do Figma apenas se aparecer uma decisão visual/comportamental que não possa ser inferida dos padrões aprovados.
- **Migração de estilos:** trazer Tailwind/shadcn ao SPA altera build e estrutura de CSS. Fazer isso antes das telas e validar a pipeline Vite, mantendo a aplicação em React/Vite.
- **Texto do login não confirmado:** o wireframe pede T2.d sem token, enquanto hoje `/confirm-account` decide o estado pela query string; definir o estado de UI sem fabricar token nem mudar o contrato do endpoint.
- **Layouts e rotas:** retirar `AppShell` global afeta todas as rotas e o fallback de erro; separar a composição com cobertura de navegação antes de migrar as telas.
- **Email rendering:** a capacidade HTML já existe na fronteira do sender, mas `MessageTemplateRenderer` ainda só preenche texto; mudanças de HTML requerem testes do conteúdo e compatibilidade dos dois providers configurados.

## Arquivos prioritários

- SPA: `src/student-spa/src/app/router.tsx`, `app/routes/*`, `components/app-shell.tsx`, `components/ui/form.tsx`, `assets/app.css`.
- Features: `features/student-registration/components/student-registration-screen.tsx`, `features/student-confirmation/components/student-confirmation-screen.tsx`, `features/student-session/components/student-login-screen.tsx`, `features/student-session/components/student-session-panel.tsx`, `features/student-password-recovery/components/student-password-recovery-screen.tsx`, `features/student-dashboard/components/dashboard-screen.tsx` e `features/student-password-change/components/student-password-change-screen.tsx`.
- Notificações: `src/notification/src/CodeForCoders.Notification.Application/Services/MessageTemplateRenderer.cs` e testes relacionados.
- Design docs: `DESIGN.md`, `docs/design/Components.md` e `docs/design/wireframes-conta-aluno.md`. Os tokens CSS ficam em `src/student-spa/src/assets/globals.css`, dentro do SPA consumidor.
