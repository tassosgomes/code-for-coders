import { act, renderHook, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { requireStudentSession } from '@/app/routes/dashboard-route';
import { StudentOrderRoute } from '@/app/routes/student-order-route';
import { env } from '@/config/env';
import { paths } from '@/config/paths';
import { useOrderReturnDelay } from '@/features/student-purchase/hooks/use-order-return-delay';
import { server } from '@/testing/server';
import { purchaseCourseId, purchaseOrder, purchaseOrderId } from '@/testing/student-purchase-data';
import { renderWithProviders } from '@/testing/test-utils';

const session = { accountId: '00000000-0000-7000-8000-000000000001', name: 'Ana', csrfToken: 'csrf' };
const lessonId = '00000000-0000-7000-8000-000000000099';

const renderRoute = (entry = `${paths.studentOrder.getHref(purchaseOrderId)}?resultado=concluido`) => {
  const router = createMemoryRouter([
    { path: paths.studentOrder.path, loader: requireStudentSession, element: <StudentOrderRoute /> },
    { path: paths.studentLesson.path, element: <h1>Aula aberta</h1> },
    { path: '/', element: <h1>Início</h1> },
  ], { initialEntries: [entry] });
  renderWithProviders(<RouterProvider router={router} />);
  return router;
};

describe('order-return', () => {
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

  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows Confirmando pagamento when returning with resultado=concluido and awaiting payment', async () => {
    server.use(
      http.get(`${env.API_URL}/api/v1/orders/:orderId`, () => HttpResponse.json({
        ...purchaseOrder,
        status: 'awaiting-payment',
      })),
    );

    renderRoute(`${paths.studentOrder.getHref(purchaseOrderId)}?resultado=concluido`);
    expect(await screen.findByText('Confirmando pagamento')).toBeInTheDocument();
  });

  it('shows Liberando seu acesso when paid but access is not granted yet', async () => {
    server.use(
      http.get(`${env.API_URL}/api/v1/orders/:orderId`, () => HttpResponse.json({
        ...purchaseOrder,
        status: 'paid',
        accessGrantedAt: null,
      })),
    );

    renderRoute(`${paths.studentOrder.getHref(purchaseOrderId)}?resultado=concluido`);
    expect(await screen.findByText('Liberando seu acesso')).toBeInTheDocument();
  });

  it('shows Compra confirmada, receipt notice and links to the lesson when access is granted', async () => {
    server.use(
      http.get(`${env.API_URL}/api/v1/orders/:orderId`, () => HttpResponse.json({
        ...purchaseOrder,
        status: 'paid',
        accessGrantedAt: '2026-10-05T15:00:00Z',
        paymentMethod: 'card',
      })),
    );

    const router = renderRoute(`${paths.studentOrder.getHref(purchaseOrderId)}?resultado=concluido`);

    expect(await screen.findByText('Compra confirmada')).toBeInTheDocument();
    expect(screen.getByText('Você receberá o comprovante por e-mail.')).toBeInTheDocument();

    const courseLink = screen.getByRole('link', { name: 'Ir para o curso' });
    expect(courseLink).toBeInTheDocument();
    await userEvent.click(courseLink);
    expect(router.state.location.pathname).toBe(paths.studentLesson.getHref(lessonId));
    expect(await screen.findByText('Aula aberta')).toBeInTheDocument();
  });

  it('triggers delay notice hook after 2 minutes of following', () => {
    vi.useFakeTimers();
    const { result } = renderHook(() => useOrderReturnDelay(true));
    expect(result.current).toBe(false);

    act(() => {
      vi.advanceTimersByTime(120_000);
    });
    expect(result.current).toBe(true);
  });

  it('returning with resultado=saiu shows Continuar pagamento button', async () => {
    let paymentCalled = false;
    server.use(
      http.post(`${env.API_URL}/api/v1/orders/:orderId/payment-session`, () => {
        paymentCalled = true;
        return HttpResponse.json({
          orderId: purchaseOrderId,
          kind: 'checkout',
          paymentUrl: 'https://checkout.stripe.com/c/pay/cs_test_return_1',
          expiresAt: '2026-10-06T15:00:00Z',
        });
      }),
    );

    const assignMock = vi.fn();
    Object.defineProperty(window, 'location', {
      configurable: true,
      writable: true,
      value: {
        ...window.location,
        assign: assignMock,
      },
    });

    renderRoute(`${paths.studentOrder.getHref(purchaseOrderId)}?resultado=saiu`);

    const continueButton = await screen.findByRole('button', { name: 'Continuar pagamento' });
    expect(continueButton).toBeInTheDocument();

    await userEvent.click(continueButton);
    await waitFor(() => expect(paymentCalled).toBe(true));
  });

  it('503 on start payment shows error and changes button to Tentar de novo', async () => {
    server.use(
      http.post(`${env.API_URL}/api/v1/orders/:orderId/payment-session`, () => HttpResponse.json(
        { code: 'PAYMENT_PROVIDER_UNAVAILABLE' },
        { status: 503 },
      )),
    );

    renderRoute(paths.studentOrder.getHref(purchaseOrderId));

    const payButton = await screen.findByRole('button', { name: 'Ir para o pagamento' });
    await userEvent.click(payButton);

    expect(await screen.findByText('Não foi possível abrir o pagamento agora. Seu pedido continua aguardando.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Tentar de novo' })).toBeInTheDocument();
  });
});
