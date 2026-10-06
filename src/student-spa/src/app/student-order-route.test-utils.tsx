import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { expect, vi } from 'vitest';

import { requireStudentSession } from '@/app/routes/dashboard-route';
import { StudentOrderRoute } from '@/app/routes/student-order-route';
import { env } from '@/config/env';
import { paths } from '@/config/paths';
import { server } from '@/testing/server';
import { purchaseCourseId, purchaseOrder, purchaseOrderId } from '@/testing/student-purchase-data';
import { renderWithProviders } from '@/testing/test-utils';

const session = { accountId: '00000000-0000-7000-8000-000000000001', name: 'Ana', csrfToken: 'csrf' };

export const studentOrderLessonId = '00000000-0000-7000-8000-000000000099';

export const installStudentOrderRoute = () => {
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
        continueLessonId: studentOrderLessonId,
        progress: null,
      }],
      ended: [],
    })),
  );
};

export const renderStudentOrderRoute = (entry = paths.studentOrder.getHref(purchaseOrderId)) => {
  const router = createMemoryRouter([
    { path: paths.studentOrder.path, loader: requireStudentSession, element: <StudentOrderRoute /> },
    { path: paths.studentLesson.path, element: <h1>Aula aberta</h1> },
    { path: paths.studentPurchase.path, element: <h1>Página de compra</h1> },
    { path: '/', element: <h1>Início</h1> },
  ], { initialEntries: [entry] });
  renderWithProviders(<RouterProvider router={router} />);
  return router;
};

type PendingInstructions = {
  method: 'pix' | 'boleto';
  expiresAt: string;
  kind: string;
  paymentUrl: string;
  statusTitle: string;
  deadlinePattern: RegExp;
  compensationPattern: RegExp;
  buttonName: string;
};

export const showPendingPaymentInstructions = async (options: PendingInstructions) => {
  let paymentCalled = false;
  server.use(
    http.get(`${env.API_URL}/api/v1/orders/:orderId`, () => HttpResponse.json({
      ...purchaseOrder,
      status: 'awaiting-payment',
      paymentMethod: options.method,
      pendingPayment: {
        method: options.method,
        expiresAt: options.expiresAt,
      },
      paymentPageExpiresAt: null,
    })),
    http.post(`${env.API_URL}/api/v1/orders/:orderId/payment-session`, () => {
      paymentCalled = true;
      return HttpResponse.json({
        orderId: purchaseOrderId,
        kind: options.kind,
        paymentUrl: options.paymentUrl,
        expiresAt: options.expiresAt,
      });
    }),
  );

  const assign = vi.fn();
  Object.defineProperty(window, 'location', {
    configurable: true,
    writable: true,
    value: { ...window.location, assign },
  });

  renderStudentOrderRoute(paths.studentOrder.getHref(purchaseOrderId));

  expect(await screen.findByText(options.statusTitle)).toBeInTheDocument();
  expect(screen.getByText(options.deadlinePattern)).toBeInTheDocument();
  expect(screen.getByText(options.compensationPattern)).toBeInTheDocument();
  expect(screen.getByText(/O curso ainda não está liberado/)).toBeInTheDocument();

  await userEvent.click(screen.getByRole('button', { name: options.buttonName }));
  await waitFor(() => expect(paymentCalled).toBe(true));
  expect(assign).toHaveBeenCalledWith(options.paymentUrl);
};
