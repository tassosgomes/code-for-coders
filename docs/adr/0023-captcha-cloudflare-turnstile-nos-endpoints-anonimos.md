# ADR-0023: Captcha Cloudflare Turnstile nos endpoints anônimos dos BFFs

## Status

Accepted

## Identidade e escopo

- Caminho: `docs/adr/0023-captcha-cloudflare-turnstile-nos-endpoints-anonimos.md`
- Domínios/componentes afetados: `bff-student`, `bff-admin`, `student-spa`, `admin-spa`, configuração de deploy (`.env` de cada host)
- Origem histórica: TASK-37 (Backlog)
- Substitui: Nenhuma. Complementa a [ADR-0019](0019-runtime-e-deploy-atual.md) na configuração por ambiente

## Data

2026-10-16

## Contexto

Login, cadastro e pedido de redefinição de senha nos BFFs são anônimos e hoje só têm proteção de senha. Robôs podem fazer força bruta de login, criar contas falsas e disparar e-mails em massa por `notification`. A escola já usa Cloudflare para tunnel, DNS e proxy.

## Decisão

1. **Captcha é o Cloudflare Turnstile**, um widget por SPA (admin e aluno), em modo Managed. Hostnames dos widgets: `dev-code4coders.tasso.dev.br`, `code4coders.tasso.dev.br` e `localhost` (desenvolvimento).
2. **Os BFFs validam** o token com `POST https://challenges.cloudflare.com/turnstile/v0/siteverify`, enviando `secret`, `response` e `remoteip` (vindo de `CF-Connecting-IP`). O token é de uso único e vale 300 segundos.
3. **O token chega no header `X-Captcha-Token`**, não no corpo. O SPA obtém um token novo do widget a cada tentativa.
4. **Endpoints cobertos:**
   - `bff-student`: `POST /api/v1/student-sessions`, `POST /api/v1/student-accounts`, `POST /api/v1/password-reset-requests`.
   - `bff-admin`: `POST /api/v1/staff-sessions`, `POST /api/v1/staff-password-reset-requests`.
5. **Endpoints excluídos:** `password-resets`, `staff-password-resets`, `staff-invitation-lookups` e `staff-invitation-acceptances` (já protegidos por token de e-mail de uso único) e todas as rotas autenticadas.
6. **Respostas** em `application/problem+json` com `code` e `traceId`, no formato já usado pelo limite de requisições de intenção de compra do `bff-student`:
   - 400 `CAPTCHA_REQUIRED`: header ausente ou vazio.
   - 403 `CAPTCHA_INVALID`: a siteverify reprovou o token (inválido, expirado ou já usado).
   - 503 `CAPTCHA_UNAVAILABLE`: siteverify fora do ar ou com timeout. Só nos endpoints fail-closed.
   - 429 `RATE_LIMITED` com `Retry-After`: limite por IP nos mesmos endpoints.
7. **Política de falha da siteverify:**
   - **Fail-open nos logins** (`student-sessions`, `staff-sessions`): a requisição passa e o BFF registra a falha. Uma queda da Cloudflare não pode trancar todos os alunos e a equipe fora do sistema; a senha e o limite por IP continuam valendo.
   - **Fail-closed no cadastro e nos pedidos de redefinição** (`student-accounts`, `password-reset-requests`, `staff-password-reset-requests`): respondem 503. Esses endpoints criam contas e disparam e-mails, que é o abuso que o captcha existe para impedir.
8. **O login do admin exige captcha sempre**, não só depois de N falhas.
9. **O captcha não substitui o rate limit.** O limite por IP nos BFFs permanece e vale nos mesmos endpoints.
10. **Configuração.** Nos BFFs: `Turnstile__Enabled` e `Turnstile__SecretKey`. Nos SPAs: `TURNSTILE_SITE_KEY` no runtime-env. Os valores reais ficam no `.env` de cada host (ver ADR-0019), fora do repositório. Dev, testes e Cloud Agents usam as chaves de teste oficiais da Cloudflare: sitekey `1x00000000000000000000AA` e secret `1x0000000000000000000000000000000AA` sempre aprovam; o secret `2x0000000000000000000000000000000AA` sempre reprova.

## Alternativas Consideradas

### Alternativa 1: Google reCAPTCHA

- **Descrição:** reCAPTCHA v2 ou v3 nos mesmos endpoints.
- **Prós:** muito difundido.
- **Contras:** a cota gratuita é limitada e exige projeto no Google Cloud; carrega as preocupações de privacidade e LGPD de enviar dados de navegação ao Google.
- **Por que rejeitada:** Turnstile é gratuito, sem limite de verificações, e a escola já está na Cloudflare.

### Alternativa 2: Só rate limit por IP

- **Descrição:** sem captcha, apenas limitar por IP.
- **Prós:** nenhuma dependência externa.
- **Contras:** robôs distribuídos contornam o limite por IP; o limite pune redes compartilhadas (NAT de escola).
- **Por que rejeitada:** o limite continua, mas sozinho não barra criação de contas em massa nem e-mails disparados.

### Alternativa 3: Captcha só após N falhas de login

- **Descrição:** desafio condicional ao histórico de falhas.
- **Prós:** menos atrito para quem acerta a senha.
- **Contras:** exige estado por conta ou IP e deixa cadastro e redefinição sem proteção até o gatilho.
- **Por que rejeitada:** decisão do usuário: o login do admin pede captcha sempre; o modo Managed já reduz o atrito.

## Consequências

### Positivas

- Cadastro, redefinição e login deixam de ser alvo barato de automação.
- Sem custo e sem cota.
- O contrato nomeia header e códigos de erro, então BFFs e SPAs implementam o mesmo acordo.

### Negativas

- Dependência externa (Cloudflare) nas telas anônimas; o fail-closed pode bloquear cadastro e redefinição durante uma queda.
- Mais configuração por ambiente (sitekey e secret por SPA).
- O IP do cliente depende de `CF-Connecting-IP`, então o tráfego precisa passar pela Cloudflare.

### Riscos

- **Fail-open no login** permite tentativas sem captcha durante a queda. Mitigação: a falha é registrada e o limite por IP continua.
- **Fail-open vale só para a siteverify.** Se o script do widget não carrega no navegador, o SPA não obtém token e o login também fica bloqueado. Aceitar login sem token anularia o captcha, então essa queda não tem fail-open.
- **`CF-Connecting-IP` forjado** por quem alcança o BFF sem passar pela Cloudflare. O valor é enviado à siteverify como `remoteip` e é a chave do limite por IP, então quem o forja escapa do limite por IP (o captcha continua valendo). Mitigação: o BFF não é exposto direto à internet.
- **Secret vazado.** Mitigação: fica só no `.env` do host, nunca no repositório nem no SPA.

## Notas de Implementação

- A implementação dos BFFs é a TASK-37.2; os widgets, as TASK-37.3 e 37.4; a configuração dos ambientes e o smoke, a TASK-37.5.
- `Turnstile__Enabled=false` desliga a verificação (uso local sem widget). Em ambientes compartilhados fica `true`.
