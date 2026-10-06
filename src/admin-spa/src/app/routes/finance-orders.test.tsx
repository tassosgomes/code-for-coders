import { cleanup, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';

import { routes } from '@/app/app-routes';
import { env } from '@/config/env';
import { financeCourseId, financeStudentId, financeOrderId, financeOrdersHandlers, financePage, financeDetailFixture } from '@/testing/finance-orders-handlers';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const setup = (entry = '/financeiro', permissions = ['financeiro.ler', 'oferta.editar', 'cortesia.conceder']) => {
  server.use(...financeOrdersHandlers, http.get(`${env.API_URL}/api/v1/staff-sessions/current`, () => HttpResponse.json({ accountId: financeStudentId, name: 'Marina Alves', roles: ['financeiro'], permissions, csrfToken: 'csrf-finance' })));
  const router = createMemoryRouter(routes, { initialEntries: [entry] });
  renderWithProviders(<RouterProvider router={router} />);
  return { router, user: userEvent.setup() };
};
describe('Finance orders', () => {
  afterEach(cleanup);
  it('shows students and read-only orders and paginates', async () => {
    const { user, router } = setup();
    expect(await screen.findByText('Ana Souza')).toBeInTheDocument();
    expect(screen.getByText('ana.souza@example.com')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Pedido #000123' })).toBeInTheDocument();
    expect(screen.getByText('Cartão de crédito')).toBeInTheDocument();
    expect(screen.getByText('Pago', { selector: '.finance-status' })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Próxima' }));
    expect(await screen.findByText('Página 2 de 2')).toBeInTheDocument();
    expect(router.state.location.search).toContain('_page=2');
  });
  it('applies combined filters only on submit', async () => {
    const { user, router } = setup(); const calls: URL[] = [];
    await screen.findByText('Ana Souza');
    server.use(http.get(`${env.API_URL}/api/v1/finance/orders`, ({ request }) => { calls.push(new URL(request.url)); return HttpResponse.json(financePage()); }));
    await user.selectOptions(screen.getByLabelText('Situação'), 'awaiting-payment');
    await user.selectOptions(screen.getByLabelText('Curso'), financeCourseId);
    expect(calls).toHaveLength(0);
    await user.click(screen.getByRole('button', { name: 'Filtrar' }));
    await waitFor(() => expect(calls).toHaveLength(1));
    expect(calls[0]?.searchParams.get('status')).toBe('awaiting-payment');
    expect(calls[0]?.searchParams.get('courseId')).toBe(financeCourseId);
    expect(router.state.location.search).toContain('status=awaiting-payment');
  });
  it('rejects inverted dates without changing the list', async () => {
    const { user } = setup(); await screen.findByText('Ana Souza'); let calls = 0;
    server.use(http.get(`${env.API_URL}/api/v1/finance/orders`, () => { calls += 1; return HttpResponse.json(financePage()); }));
    await user.type(screen.getByLabelText('De'), '2026-10-10'); await user.type(screen.getByLabelText('Até'), '2026-10-01');
    await user.click(screen.getByRole('button', { name: 'Filtrar' }));
    expect(await screen.findByText('A data final não pode ser anterior à inicial.')).toBeInTheDocument(); expect(calls).toBe(0);
  });
  it('locates normalized email with POST and applies only studentId to the URL', async () => {
    const { user, router } = setup(); await screen.findByText('Ana Souza'); let email: unknown;
    server.use(http.post(`${env.API_URL}/api/v1/student-account-lookups`, async ({ request }) => { email = await request.json(); return HttpResponse.json({ ...financeDetailFixture.student, emailConfirmed: true, status: 'active' }); }));
    await user.type(screen.getByLabelText('E-mail do aluno'), ' ANA.SOUZA@EXAMPLE.COM ');
    await user.click(screen.getByRole('button', { name: 'Localizar' }));
    expect(await screen.findByText('Ana Souza · ana.souza@example.com')).toBeInTheDocument(); expect(email).toEqual({ email: 'ana.souza@example.com' });
    await user.click(screen.getByRole('button', { name: 'Filtrar' }));
    await waitFor(() => expect(router.state.location.search).toContain(`studentId=${financeStudentId}`));
    expect(router.state.location.search).not.toMatch(/email|souza|example/i);
  });
  it('unknown student shows the indistinguishable message and does not apply a filter', async () => {
    const { user, router } = setup(); await screen.findByText('Ana Souza');
    server.use(http.post(`${env.API_URL}/api/v1/student-account-lookups`, () => HttpResponse.json({ code: 'STUDENT_ACCOUNT_NOT_FOUND' }, { status: 404 })));
    await user.type(screen.getByLabelText('E-mail do aluno'), 'unknown@example.com'); await user.click(screen.getByRole('button', { name: 'Localizar' }));
    expect(await screen.findByText('Nenhuma conta de aluno com este e-mail.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Filtrar' })).toBeDisabled(); expect(router.state.location.search).toBe('');
    expect(screen.getByText('Ana Souza')).toBeInTheDocument();
  });
  it('opens full detail and returns with filters and page preserved', async () => {
    const entry = `/financeiro?status=paid&courseId=${financeCourseId}&_page=2`;
    const { user, router } = setup(entry); await screen.findByText('Ana Souza');
    await user.click(screen.getByRole('link', { name: 'Pedido #000123' }));
    expect(await screen.findByRole('heading', { name: 'Pedido #000123' })).toBeInTheDocument();
    expect(screen.getByText('pi_3Q2w3E4r5T6y7U8i0')).toBeInTheDocument(); expect(screen.getByText('R$ 397,00')).toBeInTheDocument();
    expect(screen.getByText(financeDetailFixture.grantId)).toBeInTheDocument(); expect(screen.getByText(/contados a partir da liberação/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Cancelar|Estornar|Reenviar|Conceder/ })).not.toBeInTheDocument();
    await user.click(screen.getByRole('link', { name: 'Voltar para Pedidos' })); await screen.findByRole('heading', { name: 'Pedidos' });
    expect(router.state.location.pathname + router.state.location.search).toBe(entry);
  });
  it('blocks direct list and detail routes without finance permission', async () => {
    const { router } = setup('/financeiro', ['autoria.ler']);
    expect(await screen.findByRole('heading', { name: 'Esta área não é do seu papel' })).toBeInTheDocument();
    await router.navigate(`/financeiro/pedidos/${financeOrderId}`);
    await waitFor(() => expect(screen.getByRole('heading', { name: 'Esta área não é do seu papel' })).toBeInTheDocument());
  });
  it('shows unavailable and allows retry without a partial list', async () => {
    const { user } = setup();
    server.use(http.get(`${env.API_URL}/api/v1/finance/orders`, () => HttpResponse.json({ code: 'IDENTITY_UNAVAILABLE' }, { status: 502 })));
    expect(await screen.findByText('Não foi possível carregar os pedidos agora.')).toBeInTheDocument(); expect(screen.queryByText('Ana Souza')).not.toBeInTheDocument();
    server.use(http.get(`${env.API_URL}/api/v1/finance/orders`, () => HttpResponse.json(financePage())));
    await user.click(screen.getByRole('button', { name: 'Tentar de novo' })); expect(await screen.findByText('Ana Souza')).toBeInTheDocument();
  });
  it('shows empty filtered result and clears filters', async () => {
    const { user, router } = setup('/financeiro?status=cancelled');
    server.use(http.get(`${env.API_URL}/api/v1/finance/orders`, () => HttpResponse.json({ data: [], pagination: { page: 1, size: 20, total: 0, totalPages: 0 } })));
    expect(await screen.findByText('Nenhum pedido com estes filtros.')).toBeInTheDocument();
    await user.click(screen.getAllByRole('button', { name: 'Limpar filtros' }).at(-1)!);
    expect(await screen.findByText(/Ainda não há pedidos/)).toBeInTheDocument(); expect(router.state.location.search).toBe('');
  });
  it('shows missing detail and paid order awaiting its grant', async () => {
    const { router } = setup(`/financeiro/pedidos/${financeOrderId}`);
    server.use(http.get(`${env.API_URL}/api/v1/finance/orders/:orderId`, () => HttpResponse.json({ code: 'ORDER_NOT_FOUND' }, { status: 404 })));
    expect(await screen.findByText('Este pedido não existe ou não pertence a esta escola.')).toBeInTheDocument();
    server.use(http.get(`${env.API_URL}/api/v1/finance/orders/:orderId`, () => HttpResponse.json({ ...financeDetailFixture, grantId: null, accessGrantedAt: null, accessPeriod: { type: 'lifetime' } })));
    await router.navigate('/financeiro'); await screen.findByText('Ana Souza'); await router.navigate(`/financeiro/pedidos/${financeOrderId}`);
    expect(await screen.findByText('O acesso é liberado sozinho; atualize em alguns instantes.')).toBeInTheDocument(); expect(screen.getByText('Acesso vitalício')).toBeInTheDocument();
  });
});
