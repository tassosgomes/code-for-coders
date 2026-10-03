import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';

import { routes } from '@/app/app-routes';
import { env } from '@/config/env';
import type { CourtesyGrant } from '@/features/courtesies/types/courtesy-grant';
import { courtesyGrantFixture } from '@/testing/courtesy-grant-handlers';
import { courtesyCourseHandlers } from '@/testing/courtesy-course-handlers';
import { courtesyStudentFixture, courtesyStudentLookupHandlers } from '@/testing/courtesy-student-lookup-handlers';
import { server } from '@/testing/server';

const renderCourtesy = (grants: CourtesyGrant[] = []) => {
  server.use(...courtesyCourseHandlers, ...courtesyStudentLookupHandlers, http.get(`${env.API_URL}/api/v1/staff-sessions/current`, () => HttpResponse.json({ accountId: crypto.randomUUID(), name: 'Financeiro', roles: ['financeiro'], permissions: ['financeiro.ler', 'cortesia.conceder'], csrfToken: 'courtesy-csrf' })),
    http.get(`${env.API_URL}/api/v1/students/:studentId/access-grants`, () => HttpResponse.json({ data: grants, pagination: { page: 1, size: 50, total: grants.length, totalPages: grants.length ? 1 : 0 } })));
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(<QueryClientProvider client={client}><RouterProvider router={createMemoryRouter(routes, { initialEntries: ['/cortesias'] })} /></QueryClientProvider>);
};
const grant = (overrides: Partial<CourtesyGrant> = {}): CourtesyGrant => ({ ...courtesyGrantFixture, accessPeriod: { type: 'months', months: 6 }, ...overrides });
const lookup = async () => {
  const user = userEvent.setup(); await user.type(await screen.findByRole('textbox', { name: 'E-mail do aluno' }), courtesyStudentFixture.email);
  await user.click(screen.getByRole('button', { name: 'Localizar' })); await screen.findByRole('heading', { name: courtesyStudentFixture.name }); return user;
};
const review = async (lifetime = false) => {
  const user = await lookup(); await user.click(screen.getByRole('button', { name: 'Continuar' }));
  await user.click(await screen.findByRole('button', { name: 'Escolher Fundamentos de C#' })); await user.click(screen.getByRole('button', { name: 'Continuar' }));
  if (lifetime) await user.click(screen.getByRole('radio', { name: 'Vitalícia' }));
  await waitFor(() => expect(screen.getByRole('button', { name: 'Continuar' })).toBeEnabled()); await user.click(screen.getByRole('button', { name: 'Continuar' }));
  await user.type(screen.getByRole('textbox', { name: 'Motivo da cortesia' }), 'Bolsa de mentoria'); await user.click(screen.getByRole('button', { name: 'Revisar' }));
  await screen.findByRole('heading', { name: 'Revisão' }); return user;
};
describe('courtesy student grants', () => {
  afterEach(cleanup);
  it('shows current course titles origins absolute dates expired status and lifetime in the student step', async () => {
    renderCourtesy([grant(), grant({ grantId: crypto.randomUUID(), courseTitle: 'Expired purchase', origin: 'purchase', status: 'expired', reason: null, endsOn: '2026-01-02' }), grant({ grantId: crypto.randomUUID(), courseTitle: 'Lifetime course', accessPeriod: { type: 'lifetime' }, endsOn: null, expiresAt: null })]);
    await lookup(); const region = await screen.findByRole('region', { name: 'Concessões do aluno' });
    await waitFor(() => expect(within(region).getAllByRole('listitem')).toHaveLength(3));
    expect(region).toHaveTextContent('Fundamentos de C#'); expect(region).toHaveTextContent('Cortesia'); expect(region).toHaveTextContent('6 meses · até 02/04/2027'); expect(region).toHaveTextContent('Compra'); expect(region).toHaveTextContent('Vencida'); expect(region).toHaveTextContent('Ativa'); expect(region).toHaveTextContent('Acesso vitalício');
  });
  it('shows an empty state for a student without grants', async () => {
    renderCourtesy(); await lookup(); expect(await screen.findByText('Este aluno ainda não tem concessões.')).toBeInTheDocument();
  });
  it('warns about the latest active end date without blocking a second courtesy', async () => {
    renderCourtesy([grant(), grant({ grantId: crypto.randomUUID(), endsOn: '2027-08-02', expiresAt: '2027-08-03T03:00:00Z' }), grant({ grantId: crypto.randomUUID(), status: 'expired', endsOn: '2028-01-02' })]);
    const user = await review(); expect(await screen.findByText('Este aluno já tem acesso até 02/08/2027.')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole('button', { name: 'Confirmar cortesia' })).toBeEnabled()); await user.click(screen.getByRole('button', { name: 'Confirmar cortesia' }));
    expect(await screen.findByRole('heading', { name: 'Cortesia concedida' })).toBeInTheDocument();
  });
  it('does not warn about expired grants or active grants for another course', async () => {
    renderCourtesy([grant({ status: 'expired' }), grant({ grantId: crypto.randomUUID(), courseId: crypto.randomUUID() })]); await review();
    await waitFor(() => expect(screen.queryByText('Consultando concessões do aluno…')).not.toBeInTheDocument()); expect(screen.queryByText(/Este aluno já tem acesso/)).not.toBeInTheDocument(); expect(screen.queryByRole('checkbox')).not.toBeInTheDocument();
  });
  it('requires the focused lifetime confirmation before any post and clears it when returning to review', async () => {
    renderCourtesy(); let calls = 0;
    server.use(http.post(`${env.API_URL}/api/v1/courtesy-grants`, async ({ request }) => { calls++; expect(await request.json()).toHaveProperty('accessPeriod', { type: 'lifetime' }); return HttpResponse.json(grant({ accessPeriod: { type: 'lifetime' }, endsOn: null, expiresAt: null }), { status: 201 }); }));
    const user = await review(true); const checkbox = screen.getByRole('checkbox', { name: /não há como desfazer pela tela/ });
    expect(checkbox).toHaveFocus(); expect(checkbox).not.toBeChecked(); expect(screen.getByRole('button', { name: 'Confirmar cortesia' })).toBeDisabled();
    await user.click(screen.getByRole('button', { name: 'Confirmar cortesia' })); expect(calls).toBe(0);
    await user.click(checkbox); await user.click(screen.getByRole('button', { name: 'Voltar' })); await user.click(screen.getByRole('button', { name: 'Revisar' }));
    await screen.findByRole('heading', { name: 'Revisão' }); expect(screen.getByRole('checkbox')).not.toBeChecked(); expect(screen.getByRole('button', { name: 'Confirmar cortesia' })).toBeDisabled();
    await user.click(screen.getByRole('checkbox')); await user.click(screen.getByRole('button', { name: 'Confirmar cortesia' })); await screen.findByRole('heading', { name: 'Cortesia concedida' }); expect(calls).toBe(1);
  });
  it('warns about lifetime access and still permits a period courtesy without reinforcement', async () => {
    renderCourtesy([grant({ accessPeriod: { type: 'lifetime' }, endsOn: null, expiresAt: null })]); await review();
    expect(await screen.findByText('Este aluno já tem acesso vitalício a este curso.')).toBeInTheDocument(); expect(screen.queryByRole('checkbox')).not.toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole('button', { name: 'Confirmar cortesia' })).toBeEnabled());
  });
  it('checks grants beyond the first server page and refreshes them when entering review', async () => {
    renderCourtesy(); let firstPageCalls = 0;
    server.use(http.get(`${env.API_URL}/api/v1/students/:studentId/access-grants`, ({ request }) => {
      const page = Number(new URL(request.url).searchParams.get('_page')); if (page === 1) firstPageCalls++;
      const data = page === 1 ? [grant({ courseId: crypto.randomUUID() })] : [grant()];
      return HttpResponse.json({ data, pagination: { page, size: 50, total: 51, totalPages: 2 } });
    }));
    await review(); expect(await screen.findByText('Este aluno já tem acesso até 02/04/2027.')).toBeInTheDocument();
    await waitFor(() => expect(firstPageCalls).toBeGreaterThanOrEqual(2));
  });
  it('shows a retry for unavailable grants and does not retain the previous student when email changes', async () => {
    renderCourtesy(); server.use(http.get(`${env.API_URL}/api/v1/students/:studentId/access-grants`, () => HttpResponse.json({ code: 'COMMERCE_UNAVAILABLE' }, { status: 502 })));
    const user = await lookup(); expect(await screen.findByText('Não foi possível carregar as concessões do aluno.')).toBeInTheDocument();
    server.use(http.get(`${env.API_URL}/api/v1/students/:studentId/access-grants`, () => HttpResponse.json({ data: [grant()], pagination: { page: 1, size: 50, total: 1, totalPages: 1 } })));
    await user.click(screen.getByRole('button', { name: 'Tentar de novo' })); await screen.findByText('6 meses ·', { selector: '.courtesy-grant-months' });
    await user.type(screen.getByRole('textbox', { name: 'E-mail do aluno' }), 'x'); expect(screen.queryByRole('region', { name: 'Concessões do aluno' })).not.toBeInTheDocument();
  });
});
