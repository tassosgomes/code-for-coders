import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';

import { routes } from '@/app/app-routes';
import { env } from '@/config/env';
import { courtesyStudentFixture, courtesyStudentLookupHandlers } from '@/testing/courtesy-student-lookup-handlers';
import { server } from '@/testing/server';

const renderCourtesy = (permissions = ['financeiro.ler', 'cortesia.conceder'], role = 'financeiro') => {
  server.use(...courtesyStudentLookupHandlers, http.get(`${env.API_URL}/api/v1/staff-sessions/current`, () => HttpResponse.json({
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

describe('courtesy student lookup', () => {
  afterEach(cleanup);

  it('finance opens the menu and finds an unconfirmed student using a normalized POST body and CSRF', async () => {
    renderCourtesy();
    let requestBody: unknown; let csrf: string | null = null; let url = '';
    server.use(http.post(`${env.API_URL}/api/v1/student-account-lookups`, async ({ request }) => {
      requestBody = await request.json(); csrf = request.headers.get('X-CSRF-Token'); url = request.url;
      return HttpResponse.json(courtesyStudentFixture);
    }));
    expect(await screen.findByRole('heading', { name: 'Conceder cortesia' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Cortesias' })).toHaveAttribute('href', '/cortesias');
    expect(screen.getByRole('list', { name: 'Passos da cortesia' }).children).toHaveLength(6);
    await lookup('  JOANA@STUDENT.TEST  ');
    expect(await screen.findByRole('heading', { name: 'Joana Ribeiro' })).toBeInTheDocument();
    expect(screen.getByText('joana@student.test')).toBeInTheDocument();
    expect(screen.getByText('E-mail ainda não confirmado. Isso não impede a cortesia.')).toBeInTheDocument();
    expect(screen.getByText('Conta ativa')).toBeInTheDocument();
    expect(requestBody).toEqual({ email: 'joana@student.test' }); expect(csrf).toBe('courtesy-csrf');
    expect(url).toBe(`${env.API_URL}/api/v1/student-account-lookups`);
  });

  it('teacher support administrator and student cannot see the menu or lookup form via direct route', async () => {
    for (const role of ['professor', 'suporte', 'administrador', 'aluno']) {
      let calls = 0; renderCourtesy(role === 'administrador' ? ['acesso.gerir'] : [], role);
      server.use(http.post(`${env.API_URL}/api/v1/student-account-lookups`, () => { calls++; return HttpResponse.json(courtesyStudentFixture); }));
      expect(await screen.findByRole('heading', { name: 'Você não tem acesso a esta área.' })).toBeInTheDocument();
      expect(screen.queryByRole('link', { name: 'Cortesias' })).not.toBeInTheDocument();
      expect(screen.queryByRole('textbox', { name: 'E-mail do aluno' })).not.toBeInTheDocument(); expect(calls).toBe(0);
      cleanup();
    }
  });

  it('shows the same not-found message, blocks a disabled account, clears a previous result and retries errors', async () => {
    renderCourtesy(); let state: 'disabled' | 'missing' | 'unavailable' | 'active' = 'disabled';
    server.use(http.post(`${env.API_URL}/api/v1/student-account-lookups`, () => state === 'missing' ? HttpResponse.json({ code: 'STUDENT_ACCOUNT_NOT_FOUND' }, { status: 404 })
      : state === 'unavailable' ? HttpResponse.json({ code: 'IDENTITY_UNAVAILABLE' }, { status: 502 })
        : HttpResponse.json({ ...courtesyStudentFixture, status: state, emailConfirmed: true })));
    await lookup();
    expect(await screen.findByText('Esta conta está desativada. Não é possível conceder cortesia.')).toBeInTheDocument();
    state = 'missing'; await lookup('missing@student.test');
    expect(await screen.findByText('Não há conta de aluno com este e-mail')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Joana Ribeiro' })).not.toBeInTheDocument();
    state = 'unavailable'; await lookup();
    expect(await screen.findByText('Não foi possível localizar o aluno agora. Tente de novo.')).toBeInTheDocument();
    state = 'active'; await lookup();
    expect(await screen.findByRole('heading', { name: 'Joana Ribeiro' })).toBeInTheDocument();
  });

  it('keeps email out of URL query keys and browser storage and discards form and mutation data on exit', async () => {
    const { router, queryClient } = renderCourtesy(); const user = await lookup();
    expect(await screen.findByRole('heading', { name: 'Joana Ribeiro' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/cortesias'); expect(router.state.location.search).toBe('');
    expect(JSON.stringify(queryClient.getQueryCache().getAll().map((query) => query.queryKey))).not.toContain(courtesyStudentFixture.email);
    expect(JSON.stringify(queryClient.getMutationCache().getAll().map((mutation) => mutation.options.mutationKey))).not.toContain(courtesyStudentFixture.email);
    expect(JSON.stringify(localStorage)).not.toContain(courtesyStudentFixture.email); expect(JSON.stringify(sessionStorage)).not.toContain(courtesyStudentFixture.email);
    expect(screen.getByRole('textbox', { name: 'E-mail do aluno' })).toHaveAttribute('autocomplete', 'off');
    await user.click(screen.getByRole('link', { name: 'Início' }));
    await waitFor(() => expect(queryClient.getMutationCache().getAll()).toHaveLength(0));
    await user.click(screen.getByRole('link', { name: 'Cortesias' }));
    expect(await screen.findByRole('textbox', { name: 'E-mail do aluno' })).toHaveValue('');
    expect(screen.queryByRole('heading', { name: 'Joana Ribeiro' })).not.toBeInTheDocument();
  });
});
