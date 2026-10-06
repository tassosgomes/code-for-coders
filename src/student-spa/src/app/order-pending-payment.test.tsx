import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { installStudentOrderRoute, renderStudentOrderRoute, showPendingPaymentInstructions } from './student-order-route.test-utils';
import { env } from '@/config/env';
import { paths } from '@/config/paths';
import { server } from '@/testing/server';
import { purchaseOrder, purchaseOrderId } from '@/testing/student-purchase-data';

describe('order-pending-payment', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    installStudentOrderRoute();
  });

  it('renders Aguardando pagamento do PIX with notice and Ver código PIX button', async () => {
    await showPendingPaymentInstructions({
      method: 'pix',
      expiresAt: '2026-10-06T14:20:00Z',
      kind: 'pix-instructions',
      paymentUrl: 'https://pay.stripe.com/receipts/pix_instructions_test_1',
      statusTitle: 'Aguardando pagamento do PIX',
      deadlinePattern: /Pague o PIX até/,
      compensationPattern: /O acesso será liberado sozinho assim que o pagamento for confirmado/,
      buttonName: 'Ver código PIX',
    });
  });

  it('renders Aguardando pagamento do boleto with notice and Ver boleto button', async () => {
    await showPendingPaymentInstructions({
      method: 'boleto',
      expiresAt: '2026-10-09T02:59:59Z',
      kind: 'boleto-instructions',
      paymentUrl: 'https://pay.stripe.com/receipts/boleto_voucher_test_1',
      statusTitle: 'Aguardando pagamento do boleto',
      deadlinePattern: /Pague o boleto até/,
      compensationPattern: /quando o pagamento for compensado, o que pode levar até 3 dias úteis/,
      buttonName: 'Ver boleto',
    });
  });

  it('shows error notice and Tentar de novo when opening payment instructions fails', async () => {
    server.use(
      http.get(`${env.API_URL}/api/v1/orders/:orderId`, () => HttpResponse.json({
        ...purchaseOrder,
        status: 'awaiting-payment',
        paymentMethod: 'pix',
        pendingPayment: {
          method: 'pix',
          expiresAt: '2026-10-06T14:20:00Z',
        },
        paymentPageExpiresAt: null,
      })),
      http.post(`${env.API_URL}/api/v1/orders/:orderId/payment-session`, () => HttpResponse.json(
        { code: 'PAYMENT_PROVIDER_UNAVAILABLE' },
        { status: 503 },
      )),
    );

    renderStudentOrderRoute(paths.studentOrder.getHref(purchaseOrderId));

    const pixButton = await screen.findByRole('button', { name: 'Ver código PIX' });
    await userEvent.click(pixButton);

    expect(await screen.findByText(
      'Não foi possível abrir o pagamento agora. Seu pedido continua aguardando.',
    )).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Tentar de novo' })).toBeInTheDocument();
  });
});
