import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { toast } from 'sonner';
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
  const dialog = await screen.findByRole('alertdialog', { name: 'Desistir deste pedido?' });
  expect(within(dialog).getByText(/O pedido #000123 será cancelado e o pagamento pendente, também/u)).toBeInTheDocument();
  expect(within(dialog).getByRole('button', { name: 'Manter pedido' })).toHaveFocus();
  await userEvent.click(within(dialog).getByRole('button', { name: 'Desistir do pedido' }));
  await waitFor(() => expect(wasCancelled()).toBe(true));
  expect(await screen.findByRole('heading', { name: 'Pedido cancelado' })).toBeInTheDocument();
  expect(toast.success).toHaveBeenCalledWith('Pedido cancelado.');
};

describe('order-cancel', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.spyOn(toast, 'success');
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
    expect(screen.getByText(/Você desistiu deste pedido em 06\/10\/2026\. Nenhum acesso foi liberado\./u)).toBeInTheDocument();

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

    expect(await screen.findByRole('heading', { name: 'Aguardando pagamento' })).toBeInTheDocument();
    await confirmCancellation('Desistir', () => cancelCalled);
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

    const cancelBtn = await screen.findByRole('button', { name: 'Desistir' });
    await userEvent.click(cancelBtn);

    await screen.findByRole('alertdialog', { name: 'Desistir deste pedido?' });
    await userEvent.click(screen.getByRole('button', { name: 'Manter pedido' }));
    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());

    await userEvent.click(cancelBtn);
    await screen.findByRole('alertdialog', { name: 'Desistir deste pedido?' });
    await userEvent.click(screen.getByRole('button', { name: 'Fechar' }));
    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
    expect(cancelCalled).toBe(false);
    expect(screen.getByRole('heading', { name: 'Aguardando pagamento' })).toBeInTheDocument();
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
    expect(screen.getByText(/O prazo para pagar este pedido venceu em 06\/10\/2026 e o acesso não foi liberado/u)).toBeInTheDocument();
    expect(screen.getByText(/Se você pagou este pedido e o pagamento ainda não foi compensado/u)).toBeInTheDocument();

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
    expect(screen.getByText(/O prazo para pagar este pedido venceu e o acesso não foi liberado/u)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Comprar de novo' })).toBeInTheDocument();
  });

  it('keeps the dialog open with Nada mudou when cancellation fails', async () => {
    server.use(
      http.get(`${env.API_URL}/api/v1/orders/:orderId`, () => HttpResponse.json({ ...purchaseOrder, status: 'awaiting-payment' })),
      http.post(`${env.API_URL}/api/v1/orders/:orderId/cancellation`, () => HttpResponse.json({ code: 'COMMERCE_UNAVAILABLE' }, { status: 502 })),
    );

    renderStudentOrderRoute(paths.studentOrder.getHref(purchaseOrderId));

    await userEvent.click(await screen.findByRole('button', { name: 'Desistir' }));
    const dialog = await screen.findByRole('alertdialog', { name: 'Desistir deste pedido?' });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Desistir do pedido' }));
    expect(await within(dialog).findByText('Nada mudou. Tente de novo.')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Aguardando pagamento' })).toBeInTheDocument();
  });

  it('closes the dialog and shows the current situation when the order is no longer cancellable', async () => {
    let reads = 0;
    server.use(
      http.get(`${env.API_URL}/api/v1/orders/:orderId`, () => { reads++; return HttpResponse.json(reads === 1 ? { ...purchaseOrder, status: 'awaiting-payment' } : { ...purchaseOrder, status: 'expired', expiredAt: '2026-10-06T12:00:00Z' }); }),
      http.post(`${env.API_URL}/api/v1/orders/:orderId/cancellation`, () => HttpResponse.json(
        { code: 'ORDER_NOT_CANCELLABLE' },
        { status: 422 },
      )),
    );

    renderStudentOrderRoute(paths.studentOrder.getHref(purchaseOrderId));

    await userEvent.click(await screen.findByRole('button', { name: 'Desistir' }));
    const dialog = await screen.findByRole('alertdialog', { name: 'Desistir deste pedido?' });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Desistir do pedido' }));

    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
    expect(await screen.findByRole('heading', { name: 'Pedido expirado' })).toBeInTheDocument();
  });
});
