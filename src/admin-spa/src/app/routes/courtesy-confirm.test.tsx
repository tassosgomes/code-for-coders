import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';

import { routes } from '@/app/app-routes';
import { env } from '@/config/env';
import { courtesyCourseHandlers } from '@/testing/courtesy-course-handlers';
import { courtesyStudentFixture, courtesyStudentLookupHandlers } from '@/testing/courtesy-student-lookup-handlers';
import { server } from '@/testing/server';

const renderCourtesy = (permissions = ['financeiro.ler', 'cortesia.conceder'], role = 'financeiro') => {
  server.use(...courtesyCourseHandlers, ...courtesyStudentLookupHandlers, http.get(`${env.API_URL}/api/v1/staff-sessions/current`, () => HttpResponse.json({
    accountId: '0198dfac-674a-7000-8000-000000000002', name: 'Financeiro', roles: [role], permissions, csrfToken: 'courtesy-csrf',
  })));
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const router = createMemoryRouter(routes, { initialEntries: ['/cortesias'] });
  render(<QueryClientProvider client={queryClient}><RouterProvider router={router} /></QueryClientProvider>);
  return { router, queryClient };
};
const lookup = async (email = courtesyStudentFixture.email) => {
  const user = userEvent.setup();
  const input = await screen.findByRole('textbox', { name: 'E-mail do aluno' });
  await user.clear(input); await user.type(input, email); await user.click(screen.getByRole('button', { name: 'Localizar' }));
  return user;
};

const openCourseStep = async () => {
  const user = await lookup();
  await screen.findByRole('heading', { name: 'Joana Ribeiro' });
  await user.click(screen.getByRole('button', { name: 'Continuar' }));
  return user;
};


const openTerm = async () => {
  const user = await openCourseStep();
  await user.click(await screen.findByRole('button', { name: 'Escolher Fundamentos de C#' }));
  await user.click(screen.getByRole('button', { name: 'Continuar' }));
  await screen.findByText('Acesso até 02/04/2027'); return user;
};
const openReview = async () => {
  const user = await openTerm(); await waitFor(() => expect(screen.getByRole('button', { name: 'Continuar' })).toBeEnabled());
  await user.click(screen.getByRole('button', { name: 'Continuar' }));
  await user.type(screen.getByRole('textbox', { name: 'Motivo da cortesia' }), 'Bolsa de mentoria');
  await user.click(screen.getByRole('button', { name: 'Revisar' }));
  await screen.findByRole('heading', { name: 'Revisão' }); return user;
};
describe('courtesy confirm', () => {
  afterEach(cleanup);
  it('refreshes the server preview on month change and review and shows the granted final date', async () => {
    renderCourtesy(); let previews = 0;
    server.use(http.get(`${env.API_URL}/api/v1/courtesy-term-preview`, () => { previews++; return HttpResponse.json({ months: 6, computedAt: '2026-10-02T12:00:00Z', endsOn: '2027-04-02', expiresAt: '2027-04-03T03:00:00Z' }); }),
      http.post(`${env.API_URL}/api/v1/courtesy-grants`, async ({ request }) => {
        expect(request.headers.get('Idempotency-Key')).toBeTruthy(); expect(request.headers.get('X-CSRF-Token')).toBe('courtesy-csrf');
        const body = await request.json(); expect(body).not.toHaveProperty('endsOn');
        return HttpResponse.json({ grantId: crypto.randomUUID(), courseTitle: 'Fundamentos de C#', endsOn: '2027-04-03', reason: 'Bolsa de mentoria' }, { status: 201 });
      }));
    const user = await openTerm(); await user.clear(screen.getByRole('spinbutton', { name: 'Meses' })); await user.type(screen.getByRole('spinbutton', { name: 'Meses' }), '12');
    await waitFor(() => expect(previews).toBeGreaterThanOrEqual(2));
    await waitFor(() => expect(screen.getByRole('button', { name: 'Continuar' })).toBeEnabled()); await user.click(screen.getByRole('button', { name: 'Continuar' }));
    await user.type(screen.getByRole('textbox', { name: 'Motivo da cortesia' }), 'Bolsa de mentoria'); const beforeReview = previews;
    await user.click(screen.getByRole('button', { name: 'Revisar' })); await screen.findByRole('heading', { name: 'Revisão' }); expect(previews).toBeGreaterThan(beforeReview);
    expect(screen.getByRole('region', { name: 'Passo Revisão' })).toHaveTextContent('até 02/04/2027');
    await waitFor(() => expect(screen.getByRole('button', { name: 'Confirmar cortesia' })).toBeEnabled()); await user.click(screen.getByRole('button', { name: 'Confirmar cortesia' }));
    expect(await screen.findByRole('heading', { name: 'Cortesia concedida' })).toBeInTheDocument(); expect(screen.getByRole('region', { name: 'Resultado da cortesia' })).toHaveTextContent('Acesso até 03/04/2027');
  });
  it('keeps one key and the typed reason when retrying an uncertain grant', async () => {
    renderCourtesy(); const keys: (string | null)[] = []; let calls = 0;
    server.use(http.post(`${env.API_URL}/api/v1/courtesy-grants`, async ({ request }) => {
      keys.push(request.headers.get('Idempotency-Key')); expect(await request.json()).toHaveProperty('reason', 'Bolsa de mentoria'); calls++;
      return calls === 1 ? HttpResponse.json({ code: 'UPSTREAM_TIMEOUT' }, { status: 504 }) : HttpResponse.json({ grantId: crypto.randomUUID(), courseTitle: 'Fundamentos de C#', endsOn: '2027-04-02', reason: 'Bolsa de mentoria' });
    }));
    const user = await openReview(); await waitFor(() => expect(screen.getByRole('button', { name: 'Confirmar cortesia' })).toBeEnabled()); await user.click(screen.getByRole('button', { name: 'Confirmar cortesia' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('mesmos dados'); expect(screen.getByRole('region', { name: 'Passo Revisão' })).toHaveTextContent('Bolsa de mentoria');
    await user.click(screen.getByRole('button', { name: 'Confirmar cortesia' })); await screen.findByRole('heading', { name: 'Cortesia concedida' }); expect(keys).toHaveLength(2); expect(keys[0]).toBe(keys[1]);
  });
  it('rejects blank and oversized reasons and presents field errors', async () => {
    renderCourtesy(); const user = await openTerm(); await waitFor(() => expect(screen.getByRole('button', { name: 'Continuar' })).toBeEnabled()); await user.click(screen.getByRole('button', { name: 'Continuar' }));
    await user.click(screen.getByRole('button', { name: 'Revisar' })); expect(await screen.findByRole('alert')).toHaveTextContent('Informe o motivo');
    await user.type(screen.getByRole('textbox', { name: 'Motivo da cortesia' }), 'a'.repeat(501)); await user.click(screen.getByRole('button', { name: 'Revisar' })); expect(await screen.findByRole('alert')).toHaveTextContent('500');
  });
  it('concedes lifetime without sending months or requesting another term preview', async () => {
    renderCourtesy(); let body: unknown;
    server.use(http.post(`${env.API_URL}/api/v1/courtesy-grants`, async ({ request }) => { body = await request.json(); return HttpResponse.json({ grantId: crypto.randomUUID(), courseTitle: 'Fundamentos de C#', endsOn: null, reason: 'Lifetime scholarship' }, { status: 201 }); }));
    const user = await openTerm(); await user.click(screen.getByRole('radio', { name: 'Vitalícia' })); await user.click(screen.getByRole('button', { name: 'Continuar' }));
    await user.type(screen.getByRole('textbox', { name: 'Motivo da cortesia' }), 'Lifetime scholarship'); await user.click(screen.getByRole('button', { name: 'Revisar' })); await screen.findByRole('heading', { name: 'Revisão' });
    await user.click(screen.getByRole('button', { name: 'Confirmar cortesia' })); await screen.findByRole('heading', { name: 'Cortesia concedida' }); expect(body).toHaveProperty('accessPeriod', { type: 'lifetime' }); expect(screen.getByRole('region', { name: 'Resultado da cortesia' })).toHaveTextContent('Acesso vitalício');
  });
});
