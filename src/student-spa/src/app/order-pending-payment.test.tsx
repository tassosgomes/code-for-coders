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
import { purchaseCourseId, purchaseOrder, purchaseOrderId } from '@/testing/student-purchase-data';
import { renderWithProviders } from '@/testing/test-utils';

const session = { accountId: '00000000-0000-7000-8000-000000000001', name: 'Ana', csrfToken: 'csrf' };
const lessonId = '00000000-0000-7000-8000-000000000099';

const renderRoute = (entry = paths.studentOrder.getHref(purchaseOrderId)) => {
  const router = createMemoryRouter([
    { path: paths.studentOrder.path, loader: requireStudentSession, element: <StudentOrderRoute /> },
    { path: paths.studentLesson.path, element: <h1>Aula aberta</h1> },
    { path: '/', element: <h1>Início</h1> },
  ], { initialEntries: [entry] });
  renderWithProviders(<RouterProvider router={router} />);
  return router;
};

describe('order-pending-payment', () => {
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

  it('renders Aguardando pagamento do PIX with notice and Ver código PIX button', async () => {
    let paymentCalled = false;
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
      http.post(`${env.API_URL}/api/v1/orders/:orderId/payment-session`, () => {
        paymentCalled = true;
        return HttpResponse.json({
          orderId: purchaseOrderId,
          kind: 'pix-instructions',
          paymentUrl: 'https://pay.stripe.com/receipts/pix_instructions_test_1',
          expiresAt: '2026-10-06T14:20:00Z',
        });
      }),
    );

    const assignMock = vi.fn();
    Object.defineProperty(window, 'location', {
      configurable: true,
      writable: true,
      value: { ...window.location, assign: assignMock },
    });

    renderRoute(paths.studentOrder.getHref(purchaseOrderId));

    expect(await screen.findByText('Aguardando pagamento do PIX')).toBeInTheDocument();
    expect(screen.getByText(/Pague o PIX até/)).toBeInTheDocument();
    expect(screen.getByText(/O acesso será liberado sozinho assim que o pagamento for confirmado/)).toBeInTheDocument();
    expect(screen.getByText(/O curso ainda não está liberado/)).toBeInTheDocument();

    const pixButton = screen.getByRole('button', { name: 'Ver código PIX' });
    expect(pixButton).toBeInTheDocument();

    await userEvent.click(pixButton);
    await waitFor(() => expect(paymentCalled).toBe(true));
    expect(assignMock).toHaveBeenCalledWith('https://pay.stripe.com/receipts/pix_instructions_test_1');
  });

  it('renders Aguardando pagamento do boleto with notice and Ver boleto button', async () => {
    let paymentCalled = false;
    server.use(
      http.get(`${env.API_URL}/api/v1/orders/:orderId`, () => HttpResponse.json({
        ...purchaseOrder,
        status: 'awaiting-payment',
        paymentMethod: 'boleto',
        pendingPayment: {
          method: 'boleto',
          expiresAt: '2026-10-09T02:59:59Z',
        },
        paymentPageExpiresAt: null,
      })),
      http.post(`${env.API_URL}/api/v1/orders/:orderId/payment-session`, () => {
        paymentCalled = true;
        return HttpResponse.json({
          orderId: purchaseOrderId,
          kind: 'boleto-instructions',
          paymentUrl: 'https://pay.stripe.com/receipts/boleto_voucher_test_1',
          expiresAt: '2026-10-09T02:59:59Z',
        });
      }),
    );

    const assignMock = vi.fn();
    Object.defineProperty(window, 'location', {
      configurable: true,
      writable: true,
      value: { ...window.location, assign: assignMock },
    });

    renderRoute(paths.studentOrder.getHref(purchaseOrderId));

    expect(await screen.findByText('Aguardando pagamento do boleto')).toBeInTheDocument();
    expect(screen.getByText(/Pague o boleto até/)).toBeInTheDocument();
    expect(screen.getByText(/quando o pagamento for compensado, o que pode levar até 3 dias úteis/)).toBeInTheDocument();
    expect(screen.getByText(/O curso ainda não está liberado/)).toBeInTheDocument();

    const boletoButton = screen.getByRole('button', { name: 'Ver boleto' });
    expect(boletoButton).toBeInTheDocument();

    await userEvent.click(boletoButton);
    await waitFor(() => expect(paymentCalled).toBe(true));
    expect(assignMock).toHaveBeenCalledWith('https://pay.stripe.com/receipts/boleto_voucher_test_1');
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

    renderRoute(paths.studentOrder.getHref(purchaseOrderId));

    const pixButton = await screen.findByRole('button', { name: 'Ver código PIX' });
    await userEvent.click(pixButton);

    expect(await screen.findByText(
      'Não foi possível abrir o pagamento agora. Seu pedido continua aguardando.',
    )).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Tentar de novo' })).toBeInTheDocument();
  });
});
