# Suíte e2e (Playwright)

Fluxos críticos validados pelo navegador contra a stack completa (SPAs, BFFs e APIs reais):

| Spec | Fluxo |
| --- | --- |
| `tests/student-account.spec.ts` | cadastro, confirmação por e-mail e login do aluno; senha incorreta |
| `tests/student-purchase.spec.ts` | vitrine, compra avulsa, pagamento no gateway e liberação do acesso |
| `tests/student-lesson.spec.ts` | abrir a aula comprada e reproduzir o vídeo protegido (HLS cifrado) |
| `tests/admin-login.spec.ts` | login, saída e senha incorreta no backoffice |
| `tests/admin-invite.spec.ts` | convite da equipe e aceite do convite |

## Cenário e dados

`global-setup.ts` monta o cenário uma vez por execução (`support/seed.ts`), só por API/e-mail, sem tocar o banco:

1. Provisiona o primeiro administrador (`provision-first-admin`, comando oficial do Identity) e define a senha
   pelo link recebido no smtp4dev.
2. O administrador só tem `acesso.gerir`; ele **convida** uma conta de equipe com os papéis professor e financeiro
   (o mesmo fluxo que o spec de convite valida) para montar o curso.
3. Envia `fixtures/aula-e2e.mp4` (854x480, 6 s) pelo upload multipart, espera o ffmpeg/HLS do media-worker, cria e
   publica o curso e a oferta.

Cada teste cria o próprio aluno (`e2e-<fluxo>-<timestamp>@example.test`); nada depende de dados de outra execução.
O estado do cenário vai para `.state/seed.json` (ignorado pelo Git).

### Por que há um Stripe falso

O billing fala com o Stripe real (Checkout + webhook assinado). No CI não há chave de teste, o checkout hospedado
não é automatizável de forma estável e o webhook real não alcança uma stack efêmera. O billing aceita
`Stripe__BaseUrl`, então `docker-compose.e2e.yml` aponta para `fake-stripe/server.mjs`, que implementa só as rotas
usadas (`checkout/sessions`, `payment_intents`) e, quando o teste "paga", entrega ao billing um
`checkout.session.completed` **assinado** com o mesmo segredo e algoritmo do Stripe. Billing, commerce, BFF e SPA
rodam de verdade; só a fronteira externa é simulada. A página de pagamento é https com certificado autoassinado
(`fake-stripe/make-cert.sh`), porque o commerce recusa `paymentUrl` que não seja https; o Playwright usa
`ignoreHTTPSErrors`.

### Turnstile

O `docker-compose.e2e.yml` fixa as chaves de teste oficiais do Cloudflare (site key `1x00000000000000000000AA`,
secret `1x0000000000000000000000000000000AA`), que sempre aprovam, mesmo que o `.env` tenha chaves reais. As telas
carregam o widget real; os atalhos de API usam o token `XXXX.DUMMY.TOKEN.XXXX`. O widget é remontado quando o tema
resolve e descarta um clique dado nessa janela; `support/captcha-form.ts` repete o clique até o resultado aparecer.

## Rodar

CI e local com infraestrutura própria (precisa de Docker, .NET SDK e Node 24):

```bash
scripts/e2e-stack.sh up        # .env, infraestrutura, migrations, APIs, SPAs e Stripe falso
cd e2e && npm ci && npx playwright test
scripts/e2e-stack.sh down      # apaga a stack e os volumes
```

O vídeo é H.264, então a suíte usa o Google Chrome (`channel: 'chrome'`); o Chromium do Playwright não decodifica.
`E2E_BROWSER_CHANNEL` troca o navegador.

Contra o servidor compartilhado (sem subir infraestrutura local):

```bash
e2e/fake-stripe/make-cert.sh
APPS_EXTRA_COMPOSE_FILES=docker-compose.e2e.yml scripts/apps.sh start --remote
E2E_SMTP4DEV_URL=https://smtp.tasso.dev.br E2E_SMTP4DEV_USERNAME=... E2E_SMTP4DEV_PASSWORD=... \
E2E_ADMIN_EMAIL=... E2E_ADMIN_PASSWORD=... npx playwright test   # o tenant já tem administrador
```

Variáveis (todas opcionais no modo local): `E2E_STUDENT_ORIGIN`, `E2E_ADMIN_ORIGIN`, `E2E_*_API_ORIGIN` (chamadas do
Node, quando o `localhost` do navegador é remapeado via `E2E_HOST_RULES`), `E2E_SMTP4DEV_URL`,
`E2E_ADMIN_EMAIL`/`E2E_ADMIN_PASSWORD`, `E2E_TENANT_ID`, `E2E_REUSE_SEED=1` (reaproveita `.state/seed.json`) e
`E2E_WORKERS`.

Estabilidade: `npx playwright test --repeat-each=10` (no CI, `workflow_dispatch` com `repeat=10`). Não há retry
configurado; um teste instável deve ser corrigido, não repetido.
