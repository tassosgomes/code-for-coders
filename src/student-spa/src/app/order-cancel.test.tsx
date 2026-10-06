import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { installStudentOrderRoute, renderStudentOrderRoute } from './student-order-route.test-utils';
import { env } from '@/config/env';
import { paths } from '@/config/paths';
import { server } from '@/testing/server';
import { purchaseCourseId, purchaseOfferId, purchaseOrder, purchaseOrderId } from '@/testing/student-purchase-data';

const cancelledOrder = {
  ...purchaseOrder,
  status: 'cancelled',
  cancelledAt: '2026-10-06T10:00:00Z',
  pendingPayment: null,
};

const stubCancellation = (onCancel: () => void) =>
  http.post(`${env.API_URL}/api/v1/orders/:orderId/cancellation`, () => {
    onCancel();
    return HttpResponse.json(cancelledOrder);
  });

const confirmCancellation = async (buttonName: string, wasCancelled: () => boolean) => {
  await userEvent.click(await screen.findByRole('button', { name: buttonName }));
  expect(await screen.findByText('Desistir do pedido?')).toBeInTheDocument();
  await userEvent.click(screen.getByRole('button', { name: 'Sim, desistir' }));
  await waitFor(() => expect(wasCancelled()).toBe(true));
  expect(await screen.findByText('Pedido cancelado')).toBeInTheDocument();
};

describe('order-cancel', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    installStudentOrderRoute();
  });

  it('renders Desistir e pagar de outra forma when order is awaiting boleto, and cancelling transitions to Cancelado', async () => {
    let cancelCalled = false;
    server.use(
      http.get(`${env.API_URL}/api/v1/orders/:orderId`, () => HttpResponse.json({
        ...purchaseOrder,
        status: 'awaiting-payment',
        paymentMethod: 'boleto',
        pendingPayment: {
          method: 'boleto',
          expiresAt: '2026-10-09T02:59:59Z',
        },
      })),
      stubCancellation(() => {
        cancelCalled = true;
      }),
    );

    renderStudentOrderRoute(paths.studentOrder.getHref(purchaseOrderId));

    expect(await screen.findByText('Aguardando pagamento do boleto')).toBeInTheDocument();
    await confirmCancellation('Desistir e pagar de outra forma', () => cancelCalled);
    expect(screen.getByText(/Este pedido foi cancelado/)).toBeInTheDocument();

    const buyAgain = screen.getByRole('link', { name: 'Comprar de novo' });
    expect(buyAgain).toHaveAttribute('href', paths.studentPurchase.getHref(purchaseOfferId, purchaseCourseId));
  });

  it('allows cancelling an awaiting order without pending payment method', async () => {
    let cancelCalled = false;
    server.use(
      http.get(`${env.API_URL}/api/v1/orders/:orderId`, () => HttpResponse.json({
        ...purchaseOrder,
        status: 'awaiting-payment',
        pendingPayment: null,
      })),
      stubCancellation(() => {
        cancelCalled = true;
      }),
    );

    renderStudentOrderRoute(paths.studentOrder.getHref(purchaseOrderId));

    expect(await screen.findByText('Aguardando pagamento')).toBeInTheDocument();
    await confirmCancellation('Desistir do pedido', () => cancelCalled);
  });

  it('modal can be dismissed without cancelling the order', async () => {
    let cancelCalled = false;
    server.use(
      http.get(`${env.API_URL}/api/v1/orders/:orderId`, () => HttpResponse.json({
        ...purchaseOrder,
        status: 'awaiting-payment',
      })),
      http.post(`${env.API_URL}/api/v1/orders/:orderId/cancellation`, () => {
        cancelCalled = true;
        return HttpResponse.json(purchaseOrder);
      }),
    );

    renderStudentOrderRoute(paths.studentOrder.getHref(purchaseOrderId));

    const cancelBtn = await screen.findByRole('button', { name: 'Desistir do pedido' });
    await userEvent.click(cancelBtn);

    expect(await screen.findByText('Desistir do pedido?')).toBeInTheDocument();
    const dismissBtn = screen.getByRole('button', { name: 'Voltar' });
    await userEvent.click(dismissBtn);

    await waitFor(() => {
      expect(screen.queryByText('Desistir do pedido?')).not.toBeInTheDocument();
    });
    expect(cancelCalled).toBe(false);
    expect(screen.getByText('Aguardando pagamento')).toBeInTheDocument();
  });

  it('renders Pedido expirado with Comprar de novo button when order status is expired', async () => {
    server.use(
      http.get(`${env.API_URL}/api/v1/orders/:orderId`, () => HttpResponse.json({
        ...purchaseOrder,
        status: 'expired',
        expiredAt: '2026-10-06T12:00:00Z',
      })),
    );

    renderStudentOrderRoute(paths.studentOrder.getHref(purchaseOrderId));

    expect(await screen.findByText('Pedido expirado')).toBeInTheDocument();
    expect(screen.getByText(/O prazo de pagamento deste pedido venceu/)).toBeInTheDocument();

    const buyAgain = screen.getByRole('link', { name: 'Comprar de novo' });
    expect(buyAgain).toHaveAttribute('href', paths.studentPurchase.getHref(purchaseOfferId, purchaseCourseId));
  });

  it('shows expired notice and Comprar de novo when payment resume returns 422 ORDER_NOT_PAYABLE', async () => {
    server.use(
      http.get(`${env.API_URL}/api/v1/orders/:orderId`, () => HttpResponse.json({
        ...purchaseOrder,
        status: 'awaiting-payment',
      })),
      http.post(`${env.API_URL}/api/v1/orders/:orderId/payment-session`, () => HttpResponse.json(
        { code: 'ORDER_NOT_PAYABLE', detail: 'O prazo de pagamento venceu; o pedido não pode ser pago.' },
        { status: 422 },
      )),
    );

    renderStudentOrderRoute(paths.studentOrder.getHref(purchaseOrderId));

    const payBtn = await screen.findByRole('button', { name: 'Ir para o pagamento' });
    await userEvent.click(payBtn);

    expect(await screen.findByText('Pedido expirado')).toBeInTheDocument();
    expect(screen.getByText(/O prazo de pagamento deste pedido venceu/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Comprar de novo' })).toBeInTheDocument();
  });

  it('shows error notice when cancellation fails', async () => {
    server.use(
      http.get(`${env.API_URL}/api/v1/orders/:orderId`, () => HttpResponse.json({
        ...purchaseOrder,
        status: 'awaiting-payment',
      })),
      http.post(`${env.API_URL}/api/v1/orders/:orderId/cancellation`, () => HttpResponse.json(
        { code: 'ORDER_NOT_CANCELLABLE' },
        { status: 422 },
      )),
    );

    renderStudentOrderRoute(paths.studentOrder.getHref(purchaseOrderId));

    const cancelBtn = await screen.findByRole('button', { name: 'Desistir do pedido' });
    await userEvent.click(cancelBtn);

    const confirmBtn = await screen.findByRole('button', { name: 'Sim, desistir' });
    await userEvent.click(confirmBtn);

    expect(await screen.findByText('Não foi possível cancelar o pedido agora.')).toBeInTheDocument();
  });
});
