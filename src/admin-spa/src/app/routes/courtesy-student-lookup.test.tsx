import { cleanup, screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it } from 'vitest';

import { env } from '@/config/env';
import { courtesyStudentFixture } from '@/testing/courtesy-student-lookup-handlers';
import { lookupCourtesyStudent, renderCourtesy } from '@/testing/courtesy-route-test-helpers';
import { server } from '@/testing/server';

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
    await lookupCourtesyStudent('  JOANA@STUDENT.TEST  ');
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
    await lookupCourtesyStudent();
    expect(await screen.findByText('Esta conta está desativada. Não é possível conceder cortesia.')).toBeInTheDocument();
    state = 'missing'; await lookupCourtesyStudent('missing@student.test');
    expect(await screen.findByText('Não há conta de aluno com este e-mail')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Joana Ribeiro' })).not.toBeInTheDocument();
    state = 'unavailable'; await lookupCourtesyStudent();
    expect(await screen.findByText('Não foi possível localizar o aluno agora. Tente de novo.')).toBeInTheDocument();
    state = 'active'; await lookupCourtesyStudent();
    expect(await screen.findByRole('heading', { name: 'Joana Ribeiro' })).toBeInTheDocument();
  });

  it('keeps email out of URL query keys and browser storage and discards form and mutation data on exit', async () => {
    const { router, queryClient } = renderCourtesy(); const user = await lookupCourtesyStudent();
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
