import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { requireStudentSession } from '@/app/routes/dashboard-route';
import { StudentOrderRoute } from '@/app/routes/student-order-route';
import { env } from '@/config/env';
import { paths } from '@/config/paths';
import { server } from '@/testing/server';
import { purchaseCourseId, purchaseOfferId, purchaseOrder, purchaseOrderId } from '@/testing/student-purchase-data';
import { renderWithProviders } from '@/testing/test-utils';

const session = { accountId: '00000000-0000-7000-8000-000000000001', name: 'Ana', csrfToken: 'csrf' };
const lessonId = '00000000-0000-7000-8000-000000000099';

const renderRoute = (entry = paths.studentOrder.getHref(purchaseOrderId)) => {
  const router = createMemoryRouter([
    { path: paths.studentOrder.path, loader: requireStudentSession, element: <StudentOrderRoute /> },
    { path: paths.studentLesson.path, element: <h1>Aula aberta</h1> },
    { path: paths.studentPurchase.path, element: <h1>Página de compra</h1> },
    { path: '/', element: <h1>Início</h1> },
  ], { initialEntries: [entry] });
  renderWithProviders(<RouterProvider router={router} />);
  return router;
};

describe('order-cancel', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    server.use(
      http.get(`${env.API_URL}/api/v1/student-sessions/current`, () => HttpResponse.json(session)),
      http.get(`${env.API_URL}/api/v1/orders/:orderId`, () => HttpResponse.json(purchaseOrder)),
      http.get(`${env.API_URL}/api/v1/my-courses`, () => HttpResponse.json({
        progressAvailable: true,
        active: [{
          courseId: purchaseCourseId,
          title: '.NET do zero à API',
          started: false,
          lastActivityAt: null,
          continueLessonId: lessonId,
          progress: null,
        }],
        ended: [],
      })),
    );
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
      http.post(`${env.API_URL}/api/v1/orders/:orderId/cancellation`, () => {
        cancelCalled = true;
        return HttpResponse.json({
          ...purchaseOrder,
          status: 'cancelled',
          cancelledAt: '2026-10-06T10:00:00Z',
          pendingPayment: null,
        });
      }),
    );

    renderRoute(paths.studentOrder.getHref(purchaseOrderId));

    expect(await screen.findByText('Aguardando pagamento do boleto')).toBeInTheDocument();
    const cancelBtn = screen.getByRole('button', { name: 'Desistir e pagar de outra forma' });
    expect(cancelBtn).toBeInTheDocument();

    await userEvent.click(cancelBtn);

    expect(await screen.findByText('Desistir do pedido?')).toBeInTheDocument();
    const confirmBtn = screen.getByRole('button', { name: 'Sim, desistir' });
    await userEvent.click(confirmBtn);

    await waitFor(() => expect(cancelCalled).toBe(true));
    expect(await screen.findByText('Pedido cancelado')).toBeInTheDocument();
    expect(screen.getByText(/Este pedido foi cancelado/)).toBeInTheDocument();

    const buyAgainBtn = screen.getByRole('link', { name: 'Comprar de novo' });
    expect(buyAgainBtn).toBeInTheDocument();
    expect(buyAgainBtn).toHaveAttribute('href', paths.studentPurchase.getHref(purchaseOfferId, purchaseCourseId));
  });

  it('allows cancelling an awaiting order without pending payment method', async () => {
    let cancelCalled = false;
    server.use(
      http.get(`${env.API_URL}/api/v1/orders/:orderId`, () => HttpResponse.json({
        ...purchaseOrder,
        status: 'awaiting-payment',
        pendingPayment: null,
      })),
      http.post(`${env.API_URL}/api/v1/orders/:orderId/cancellation`, () => {
        cancelCalled = true;
        return HttpResponse.json({
          ...purchaseOrder,
          status: 'cancelled',
          cancelledAt: '2026-10-06T10:00:00Z',
        });
      }),
    );

    renderRoute(paths.studentOrder.getHref(purchaseOrderId));

    expect(await screen.findByText('Aguardando pagamento')).toBeInTheDocument();
    const cancelBtn = screen.getByRole('button', { name: 'Desistir do pedido' });
    expect(cancelBtn).toBeInTheDocument();

    await userEvent.click(cancelBtn);

    expect(await screen.findByText('Desistir do pedido?')).toBeInTheDocument();
    const confirmBtn = screen.getByRole('button', { name: 'Sim, desistir' });
    await userEvent.click(confirmBtn);

    await waitFor(() => expect(cancelCalled).toBe(true));
    expect(await screen.findByText('Pedido cancelado')).toBeInTheDocument();
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

    renderRoute(paths.studentOrder.getHref(purchaseOrderId));

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

    renderRoute(paths.studentOrder.getHref(purchaseOrderId));

    expect(await screen.findByText('Pedido expirado')).toBeInTheDocument();
    expect(screen.getByText(/O prazo de pagamento deste pedido venceu/)).toBeInTheDocument();

    const buyAgainBtn = screen.getByRole('link', { name: 'Comprar de novo' });
    expect(buyAgainBtn).toBeInTheDocument();
    expect(buyAgainBtn).toHaveAttribute('href', paths.studentPurchase.getHref(purchaseOfferId, purchaseCourseId));
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

    renderRoute(paths.studentOrder.getHref(purchaseOrderId));

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

    renderRoute(paths.studentOrder.getHref(purchaseOrderId));

    const cancelBtn = await screen.findByRole('button', { name: 'Desistir do pedido' });
    await userEvent.click(cancelBtn);

    const confirmBtn = await screen.findByRole('button', { name: 'Sim, desistir' });
    await userEvent.click(confirmBtn);

    expect(await screen.findByText('Não foi possível cancelar o pedido agora.')).toBeInTheDocument();
  });
});
