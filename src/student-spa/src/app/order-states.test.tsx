import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';

import { installStudentOrderRoute, renderStudentOrderRoute } from './student-order-route.test-utils';
import { env } from '@/config/env';
import { paths } from '@/config/paths';
import { server } from '@/testing/server';
import { purchaseOrder, purchaseOrderId } from '@/testing/student-purchase-data';

const orderEndpoint = `${env.API_URL}/api/v1/orders/:orderId`;

describe('order-states', () => {
  beforeEach(() => installStudentOrderRoute());

  it('shows the order header and the frozen summary with the status badge', async () => {
    server.use(http.get(orderEndpoint, () => HttpResponse.json({ ...purchaseOrder, paymentMethod: 'card' })));
    renderStudentOrderRoute();
    expect(await screen.findByRole('heading', { level: 1, name: '.NET do zero à API' })).toBeInTheDocument();
    expect(screen.getByText('Pedido #000123')).toBeInTheDocument();
    expect(screen.getByText('Acesso por 12 meses · R$ 497,00 · criado em 05/10/2026')).toBeInTheDocument();
    const summary = screen.getByRole('region', { name: 'Resumo do pedido' });
    expect(within(summary).getByText('Acesso por 12 meses, contados a partir da liberação do acesso')).toBeInTheDocument();
    expect(within(summary).getByText('Cartão de crédito')).toBeInTheDocument();
    expect(within(summary).getByText('Aguardando pagamento')).toBeInTheDocument();
  });

  it('awaiting without method asks to go to the payment and shows the page deadline after leaving it', async () => {
    server.use(http.get(orderEndpoint, () => HttpResponse.json({ ...purchaseOrder, paymentPageExpiresAt: '2026-10-06T17:20:00Z' })));
    renderStudentOrderRoute();
    expect(await screen.findByText(/Você saiu do pagamento antes de escolher como pagar\. Seu pedido continua aguardando e vale até 06\/10\/2026 às 14:20\./u)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Continuar pagamento' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Desistir' })).toBeEnabled();
  });

  it('missing order shows the empty state with a link to my orders', async () => {
    server.use(http.get(orderEndpoint, () => HttpResponse.json({ code: 'ORDER_NOT_FOUND' }, { status: 404 })));
    renderStudentOrderRoute();
    expect(await screen.findByRole('heading', { name: 'Pedido não encontrado' })).toBeInTheDocument();
    expect(screen.getByText('Este pedido não existe ou não é seu. Confira o endereço ou veja a lista dos seus pedidos.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ver meus pedidos' })).toHaveAttribute('href', paths.studentMyOrders.getHref());
  });

  it('load failure offers Tentar de novo and recovers', async () => {
    let reads = 0;
    server.use(http.get(orderEndpoint, () => { reads++; return reads === 1 ? HttpResponse.json({ code: 'COMMERCE_UNAVAILABLE' }, { status: 502 }) : HttpResponse.json(purchaseOrder); }));
    renderStudentOrderRoute();
    expect(await screen.findByText('Não foi possível carregar o pedido agora.')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Tentar de novo' }));
    expect(await screen.findByRole('heading', { level: 1, name: '.NET do zero à API' })).toBeInTheDocument();
  });

  it('keeps the current panel and shows a discreet notice when following fails', async () => {
    let reads = 0;
    server.use(http.get(orderEndpoint, () => { reads++; return reads === 1 ? HttpResponse.json(purchaseOrder) : HttpResponse.error(); }));
    renderStudentOrderRoute(`${paths.studentOrder.getHref(purchaseOrderId)}?resultado=concluido`);
    expect(await screen.findByRole('heading', { name: 'Confirmando pagamento' })).toBeInTheDocument();
    expect(await screen.findByText('Não conseguimos atualizar agora. Tentaremos de novo.', {}, { timeout: 5000 })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Confirmando pagamento' })).toBeInTheDocument();
  });

  it('paid order without the grant keeps following even without returning from the gateway', async () => {
    let reads = 0;
    server.use(http.get(orderEndpoint, () => {
      reads++;
      return HttpResponse.json({ ...purchaseOrder, status: 'paid', paymentMethod: 'card', paidAt: '2026-10-05T14:21:00Z', accessGrantedAt: reads > 1 ? '2026-10-05T14:21:30Z' : null });
    }));
    renderStudentOrderRoute();
    expect(await screen.findByRole('heading', { name: 'Liberando seu acesso' })).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole('heading', { name: 'Compra confirmada' })).toBeInTheDocument(), { timeout: 5000 });
    expect(screen.queryByText('Você receberá o comprovante por e-mail.')).not.toBeInTheDocument();
  });
});
