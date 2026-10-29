// Stripe falso para a suíte e2e (TASK-42.3).
//
// Por que existe: o billing fala com o Stripe real (Checkout Sessions + webhook assinado). No CI não há
// chave de teste, o checkout hospedado do Stripe não é automatizável de forma estável e o webhook real
// não alcança uma stack efêmera. O billing aceita `Stripe__BaseUrl`, então este servidor responde no
// lugar da API e, quando o "aluno paga", entrega ao billing um `checkout.session.completed` assinado
// com o mesmo segredo (`Stripe__WebhookSigningSecret`) e o mesmo algoritmo do Stripe (HMAC-SHA256 de
// `t.body`). Billing, commerce, BFF e SPA rodam de verdade; só a fronteira externa é simulada.
//
// Configuração por ambiente:
//   PORT                       porta HTTP (API usada pelo billing e healthcheck; padrão 8080)
//   TLS_PORT / TLS_CERT / TLS_KEY  porta HTTPS (padrão 8443) e certificado autoassinado; a página de pagamento
//                              precisa de https, porque o commerce recusa `paymentUrl` que não seja https
//   PUBLIC_URL                 URL (https) da página de pagamento vista pelo navegador (padrão https://localhost:5199)
//   BILLING_WEBHOOK_URL        destino do webhook (padrão http://billing:8080/webhooks/v1/stripe/events)
//   STRIPE_WEBHOOK_SECRET      segredo de assinatura (o mesmo do billing)
import { createHmac, randomUUID } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { createServer } from 'node:http';
import { createServer as createSecureServer } from 'node:https';

const port = Number(process.env.PORT ?? 8080);
const tlsPort = Number(process.env.TLS_PORT ?? 8443);
const publicUrl = (process.env.PUBLIC_URL ?? 'https://localhost:5199').replace(/\/$/, '');
const webhookUrl = process.env.BILLING_WEBHOOK_URL ?? 'http://billing:8080/webhooks/v1/stripe/events';
const webhookSecret = process.env.STRIPE_WEBHOOK_SECRET ?? '';

/** @type {Map<string, any>} */
const sessions = new Map();
/** @type {Map<string, string>} */
const sessionByIdempotencyKey = new Map();

const json = (response, status, body) => {
  response.writeHead(status, { 'content-type': 'application/json' });
  response.end(JSON.stringify(body));
};

const html = (response, status, body) => {
  response.writeHead(status, { 'content-type': 'text/html; charset=utf-8' });
  response.end(`<!doctype html><html lang="pt-BR"><head><meta charset="utf-8"><title>Pagamento de teste</title></head><body>${body}</body></html>`);
};

const readBody = async (request) => {
  const chunks = [];
  for await (const chunk of request) chunks.push(chunk);
  return Buffer.concat(chunks).toString('utf8');
};

const escapeHtml = (value) => String(value).replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);

const view = (session) => ({
  id: session.id,
  object: 'checkout.session',
  url: `${publicUrl}/pay/${session.id}`,
  expires_at: session.expiresAt,
  status: session.status,
  payment_status: session.paymentStatus,
  payment_intent: session.paymentIntent,
});

const deliverWebhook = async (session) => {
  const event = {
    id: `evt_test_${randomUUID().replaceAll('-', '')}`,
    object: 'event',
    type: 'checkout.session.completed',
    created: Math.floor(Date.now() / 1000),
    data: {
      object: {
        id: session.id,
        object: 'checkout.session',
        client_reference_id: session.clientReferenceId,
        metadata: session.metadata,
        payment_intent: session.paymentIntent,
        payment_status: 'paid',
        amount_total: session.amountTotal,
        currency: session.currency,
      },
    },
  };
  const body = JSON.stringify(event);
  const timestamp = Math.floor(Date.now() / 1000);
  const signature = createHmac('sha256', webhookSecret).update(`${timestamp}.${body}`).digest('hex');
  const response = await fetch(webhookUrl, {
    method: 'POST',
    headers: { 'content-type': 'application/json', 'stripe-signature': `t=${timestamp},v1=${signature}` },
    body,
  });
  if (!response.ok) throw new Error(`billing recusou o webhook: ${response.status} ${await response.text()}`);
};

const handleApi = async (request, response, url) => {
  const { pathname } = url;

  if (request.method === 'POST' && pathname === '/v1/checkout/sessions') {
    const form = new URLSearchParams(await readBody(request));
    const idempotencyKey = request.headers['idempotency-key'];
    const known = idempotencyKey ? sessionByIdempotencyKey.get(String(idempotencyKey)) : undefined;
    if (known) return json(response, 200, view(sessions.get(known)));

    const id = `cs_test_${randomUUID().replaceAll('-', '')}`;
    const session = {
      id,
      status: 'open',
      paymentStatus: 'unpaid',
      paymentIntent: `pi_test_${randomUUID().replaceAll('-', '')}`,
      clientReferenceId: form.get('client_reference_id'),
      metadata: { tenantId: form.get('metadata[tenantId]'), orderId: form.get('metadata[orderId]') },
      amountTotal: Number(form.get('line_items[0][price_data][unit_amount]') ?? 0),
      currency: form.get('line_items[0][price_data][currency]') ?? 'brl',
      description: form.get('line_items[0][price_data][product_data][name]') ?? '',
      successUrl: form.get('success_url'),
      cancelUrl: form.get('cancel_url'),
      expiresAt: Math.floor(Date.now() / 1000) + 24 * 60 * 60,
    };
    sessions.set(id, session);
    if (idempotencyKey) sessionByIdempotencyKey.set(String(idempotencyKey), id);
    return json(response, 200, view(session));
  }

  const sessionMatch = pathname.match(/^\/v1\/checkout\/sessions\/([^/]+)(\/expire)?$/);
  if (sessionMatch) {
    const session = sessions.get(decodeURIComponent(sessionMatch[1]));
    if (!session) return json(response, 404, { error: { message: 'No such checkout session' } });
    if (request.method === 'POST' && sessionMatch[2]) {
      if (session.status !== 'open') return json(response, 400, { error: { message: 'Session not open' } });
      session.status = 'expired';
    }
    return json(response, 200, view(session));
  }

  const intentMatch = pathname.match(/^\/v1\/payment_intents\/([^/]+)(\/cancel)?$/);
  if (intentMatch) {
    return json(response, 200, { id: decodeURIComponent(intentMatch[1]), object: 'payment_intent', status: 'succeeded', payment_method: { id: 'pm_test', type: 'card' } });
  }

  return json(response, 404, { error: { message: `Unknown route ${pathname}` } });
};

const handlePayPage = async (request, response, url) => {
  const match = url.pathname.match(/^\/pay\/([^/]+)(\/confirm)?$/);
  const session = match ? sessions.get(decodeURIComponent(match[1])) : undefined;
  if (!session) return html(response, 404, '<h1>Sessão de pagamento não encontrada</h1>');

  if (request.method === 'POST' && match[2]) {
    if (session.status === 'open') {
      try {
        await deliverWebhook(session);
      } catch (error) {
        console.error('[fake-stripe]', error);
        return html(response, 502, `<h1>Falha ao entregar o webhook</h1><pre>${escapeHtml(error.message)}</pre>`);
      }
      session.status = 'complete';
      session.paymentStatus = 'paid';
    }
    response.writeHead(303, { location: session.successUrl });
    return response.end();
  }

  return html(response, 200, `
    <main>
      <h1>Pagamento de teste</h1>
      <p data-testid="payment-description">${escapeHtml(session.description)}</p>
      <p data-testid="payment-amount">${(session.amountTotal / 100).toFixed(2)} ${escapeHtml(session.currency.toUpperCase())}</p>
      <form method="post" action="/pay/${encodeURIComponent(session.id)}/confirm">
        <button type="submit">Pagar com cartão de teste</button>
      </form>
      <p><a href="${escapeHtml(session.cancelUrl ?? '#')}">Voltar</a></p>
    </main>`);
};

const handle = async (request, response) => {
  const url = new URL(request.url ?? '/', 'http://fake-stripe');
  try {
    if (url.pathname === '/healthz') return json(response, 200, { status: 'ok' });
    if (url.pathname.startsWith('/v1/')) return await handleApi(request, response, url);
    if (url.pathname.startsWith('/pay/')) return await handlePayPage(request, response, url);
    return json(response, 404, { error: { message: 'Not found' } });
  } catch (error) {
    console.error('[fake-stripe]', error);
    return json(response, 500, { error: { message: String(error) } });
  }
};

createServer(handle).listen(port, () => console.log(`[fake-stripe] HTTP em :${port}`));
if (process.env.TLS_CERT && process.env.TLS_KEY) {
  createSecureServer({ cert: readFileSync(process.env.TLS_CERT), key: readFileSync(process.env.TLS_KEY) }, handle).listen(tlsPort, () =>
    console.log(`[fake-stripe] HTTPS em :${tlsPort} (${publicUrl})`),
  );
} else {
  console.warn('[fake-stripe] TLS_CERT/TLS_KEY ausentes: a página de pagamento só responde em HTTP e o commerce vai recusá-la.');
}
