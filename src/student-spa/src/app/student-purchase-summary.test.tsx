import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { beforeEach, describe, expect, it } from 'vitest';

import { requireStudentSession } from '@/app/routes/dashboard-route';
import { StudentLoginRoute } from '@/app/routes/student-login-route';
import { StudentOrderRoute } from '@/app/routes/student-order-route';
import { StudentPurchaseRoute } from '@/app/routes/student-purchase-route';
import { StudentShowcaseCourseRoute } from '@/app/routes/student-showcase-course-route';
import { env } from '@/config/env';
import { paths } from '@/config/paths';
import { getPendingPurchase, savePendingPurchase } from '@/features/student-purchase/utils/pending-purchase';
import { server } from '@/testing/server';
import { purchaseCourseId, purchaseOfferId, purchaseOrder, purchaseOrderId, purchaseShowcaseCourse, purchaseSummary } from '@/testing/student-purchase-data';
import { renderWithProviders } from '@/testing/test-utils';

const session = { accountId: '00000000-0000-7000-8000-000000000001', name: 'Ana', csrfToken: 'csrf' };
const summaryEndpoint = `${env.API_URL}/api/v1/offers/:offerId/purchase-summary`;
const renderRoute = (entry = paths.studentPurchase.getHref(purchaseOfferId, purchaseCourseId)) => {
  const router = createMemoryRouter([
    { path: paths.studentShowcaseCourse.path, element: <StudentShowcaseCourseRoute /> },
    { path: paths.studentPurchase.path, loader: requireStudentSession, element: <StudentPurchaseRoute /> },
    { path: paths.studentOrder.path, loader: requireStudentSession, element: <StudentOrderRoute /> },
    { path: paths.studentLogin.path, element: <StudentLoginRoute /> },
    { path: '/', element: <h1>Início</h1> },
  ], { initialEntries: [entry] });
  renderWithProviders(<RouterProvider router={router} />); return router;
};
const enter = async () => {
  await userEvent.type(await screen.findByLabelText('E-mail'), 'ana@example.com');
  await userEvent.type(screen.getByLabelText('Senha'), 'SecurePassword1!');
  await userEvent.click(screen.getByRole('button', { name: 'Entrar' }));
};
describe('student-purchase-summary', () => {
  beforeEach(() => {
    window.localStorage.clear(); window.sessionStorage.clear();
    server.use(http.get(`${env.API_URL}/api/v1/student-sessions/current`, () => HttpResponse.json(session)),
      http.get(summaryEndpoint, () => HttpResponse.json(purchaseSummary)),
      http.get(`${env.API_URL}/api/v1/orders/:orderId`, () => HttpResponse.json(purchaseOrder)),
      http.get(`${env.API_URL}/api/v1/showcase/courses/:courseId`, () => HttpResponse.json(purchaseShowcaseCourse)));
  });
  it('visitor buys an option, enters and returns to the same summary with only a temporary purchase stored', async () => {
    let loggedIn = false;
    server.use(http.get(`${env.API_URL}/api/v1/student-sessions/current`, () => loggedIn ? HttpResponse.json(session) : HttpResponse.json({ code: 'SESSION_REQUIRED' }, { status: 401 })),
      http.post(`${env.API_URL}/api/v1/student-sessions`, () => { loggedIn = true; return HttpResponse.json(session); }));
    const router = renderRoute(paths.studentShowcaseCourse.getHref(purchaseCourseId));
    await userEvent.click(await screen.findByRole('link', { name: 'Comprar Acesso por 12 meses' }));
    await screen.findByLabelText('E-mail');
    expect(getPendingPurchase()).toMatchObject({ offerId: purchaseOfferId, courseId: purchaseCourseId });
    expect(Object.keys(getPendingPurchase() ?? {}).sort()).toEqual(['courseId', 'offerId', 'savedAt']);
    expect(new URLSearchParams(router.state.location.search).get('returnTo')).toContain(`/comprar/${purchaseOfferId}`);
    await enter(); expect(await screen.findByRole('heading', { name: 'Resumo da compra' })).toBeInTheDocument();
    expect(await screen.findByText('R$ 497,00')).toBeInTheDocument(); expect(getPendingPurchase()).toBeUndefined();
  });
  it('login without returnTo resumes purchase after registration and email confirmation in another tab', async () => {
    savePendingPurchase(purchaseOfferId, purchaseCourseId);
    server.use(http.post(`${env.API_URL}/api/v1/student-sessions`, () => HttpResponse.json(session)));
    const router = renderRoute(paths.studentLogin.getHref());
    expect(await screen.findByText('Entre para continuar sua compra')).toBeInTheDocument();
    await enter(); expect(await screen.findByRole('heading', { name: 'Resumo da compra' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe(paths.studentPurchase.getHref(purchaseOfferId)); expect(getPendingPurchase()).toBeUndefined();
  });
  it('shows courtesy as information without preventing confirmation', async () => {
    server.use(http.get(summaryEndpoint, () => HttpResponse.json({ ...purchaseSummary, existingAccess: { origin: 'courtesy', validity: { type: 'until', endsOn: '2026-11-30' } } })));
    renderRoute(); expect(await screen.findByText('Você já tem acesso a este curso até 30/11/2026 (cortesia).')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Confirmar compra' })).toBeEnabled();
    expect(screen.getByText('Acesso por 12 meses, contados a partir da liberação')).toBeInTheDocument();
  });
  it('confirmation sends only offerId with csrf and one key then displays frozen awaiting order', async () => {
    let received: Request | undefined; let input: unknown;
    server.use(http.post(`${env.API_URL}/api/v1/orders`, async ({ request }) => { received = request; input = await request.json(); return HttpResponse.json(purchaseOrder, { status: 201 }); }));
    const router = renderRoute(); await userEvent.click(await screen.findByRole('button', { name: 'Confirmar compra' }));
    expect(await screen.findByRole('heading', { name: 'Pedido nº 000123' })).toBeInTheDocument(); expect(screen.getByText('Aguardando pagamento')).toBeInTheDocument();
    expect(input).toEqual({ offerId: purchaseOfferId }); expect(received?.headers.get('X-CSRF-Token')).toBe('csrf'); expect(received?.headers.get('Idempotency-Key')).toMatch(/^[0-9a-f-]{36}$/u);
    expect(router.state.location.pathname).toBe(paths.studentOrder.getHref(purchaseOrderId)); expect(screen.queryByRole('button', { name: 'Ir para o pagamento' })).not.toBeInTheDocument();
  });
  it('pending offer links to the existing order without showing current conditions or creating another', async () => {
    let creates = 0;
    server.use(http.get(summaryEndpoint, () => HttpResponse.json({ ...purchaseSummary, pendingOrderId: purchaseOrderId })),
      http.post(`${env.API_URL}/api/v1/orders`, () => { creates++; return HttpResponse.json(purchaseOrder); }));
    renderRoute(); await screen.findByRole('link', { name: 'Ver pedido pendente' }); expect(screen.queryByText('R$ 497,00')).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('link', { name: 'Ver pedido pendente' })); expect(await screen.findByRole('heading', { name: 'Pedido nº 000123' })).toBeInTheDocument(); expect(creates).toBe(0);
  });
  it('retry after an uncertain creation reuses the confirmation key', async () => {
    const keys: string[] = [];
    server.use(http.post(`${env.API_URL}/api/v1/orders`, ({ request }) => { keys.push(request.headers.get('Idempotency-Key') ?? ''); return keys.length === 1 ? HttpResponse.json({ code: 'COMMERCE_UNAVAILABLE' }, { status: 502 }) : HttpResponse.json(purchaseOrder, { status: 201 }); }));
    renderRoute(); await userEvent.click(await screen.findByRole('button', { name: 'Confirmar compra' }));
    await screen.findByText('Não foi possível confirmar a compra agora. Tente de novo.'); await userEvent.click(screen.getByRole('button', { name: 'Confirmar compra' }));
    await screen.findByRole('heading', { name: 'Pedido nº 000123' }); expect(keys).toHaveLength(2); expect(keys[0]).toBe(keys[1]);
  });
  it('unavailable offer explains no charge and no order', async () => {
    server.use(http.get(summaryEndpoint, () => HttpResponse.json({ code: 'OFFER_NOT_AVAILABLE' }, { status: 404 })));
    renderRoute(); expect(await screen.findByText('Esta opção não está mais disponível')).toBeInTheDocument(); expect(screen.getByText('Nenhum pedido foi criado e você não foi cobrado.')).toBeInTheDocument();
  });
  it('expired stored purchase is cleared and does not divert login', async () => {
    window.localStorage.setItem('student-pending-purchase', JSON.stringify({ offerId: purchaseOfferId, courseId: purchaseCourseId, savedAt: Date.now() - 24 * 60 * 60 * 1000 }));
    server.use(http.post(`${env.API_URL}/api/v1/student-sessions`, () => HttpResponse.json(session)));
    const router = renderRoute(paths.studentLogin.getHref()); await enter(); await waitFor(() => expect(router.state.location.pathname).toBe('/')); expect(getPendingPurchase()).toBeUndefined();
  });
});
