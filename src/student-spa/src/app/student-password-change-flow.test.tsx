import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, MemoryRouter, RouterProvider } from 'react-router';
import { http, HttpResponse } from 'msw';
import { Toaster } from 'sonner';
import { describe, expect, it } from 'vitest';

import { env } from '@/config/env';
import { StudentPasswordChangeRoute } from '@/app/routes/student-password-change-route';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

describe('StudentPasswordChange', () => {
  it('StudentPasswordChange submits both passwords with the current session CSRF proof and an idempotency key', async () => {
    const user = userEvent.setup();
    let body: unknown;
    let csrfProof: string | null = null;
    let idempotencyKey: string | null = null;
    let changeCalls = 0;
    server.use(
      http.get(`${env.API_URL}/api/v1/student-sessions/current`, () => HttpResponse.json({
        accountId: '0199f23d-4a00-7000-8000-000000000001',
        name: 'Ana Souza',
        csrfToken: 'session-csrf-proof',
      })),
      http.post(`${env.API_URL}/api/v1/password-changes`, async ({ request }) => {
        changeCalls++;
        body = await request.json();
        csrfProof = request.headers.get('X-CSRF-Token');
        idempotencyKey = request.headers.get('Idempotency-Key');
        return new HttpResponse(null, { status: 204 });
      }),
    );

    const router = createMemoryRouter([
      { path: '/trocar-senha', element: <StudentPasswordChangeRoute /> },
      { path: '/', element: <h1>Início</h1> },
    ], { initialEntries: ['/trocar-senha'] });
    renderWithProviders(
      <>
        <RouterProvider router={router} />
        <Toaster />
      </>,
    );

    await user.type(await screen.findByLabelText('Senha atual'), 'legacy');
    await user.type(screen.getByLabelText('Nova senha'), 'NovaSenha2!');
    await user.click(screen.getByRole('button', { name: 'Trocar senha' }));

    expect(await screen.findByText('Senha trocada. Encerramos as outras sessões.')).toBeInTheDocument();
    expect(await screen.findByRole('heading', { name: 'Início' })).toBeInTheDocument();
    expect(body).toEqual({ currentPassword: 'legacy', newPassword: 'NovaSenha2!' });
    expect(csrfProof).toBe('session-csrf-proof');
    expect(idempotencyKey).toBeTruthy();
    expect(changeCalls).toBe(1);
  });

  it('StudentPasswordChange refreshes CSRF proof after a rejection without retrying the password change', async () => {
    const user = userEvent.setup();
    let sessionReads = 0;
    let changeCalls = 0;
    let proofWasRefreshed = false;
    const handleCsrfRefresh = () => {
      proofWasRefreshed = true;
    };
    window.addEventListener('app:csrf-refreshed', handleCsrfRefresh);
    server.use(
      http.get(`${env.API_URL}/api/v1/student-sessions/current`, () => {
        sessionReads++;
        return HttpResponse.json({
          accountId: '0199f23d-4a00-7000-8000-000000000001',
          name: 'Ana Souza',
          csrfToken: sessionReads === 1 ? 'stale-csrf-proof' : 'renewed-csrf-proof',
        });
      }),
      http.post(`${env.API_URL}/api/v1/password-changes`, () => {
        changeCalls++;
        return HttpResponse.json({ code: 'CSRF_INVALID' }, { status: 403 });
      }),
    );

    renderWithProviders(
      <MemoryRouter>
        <StudentPasswordChangeRoute />
      </MemoryRouter>,
    );

    await user.type(await screen.findByLabelText('Senha atual'), 'legacy');
    await user.type(screen.getByLabelText('Nova senha'), 'NovaSenha2!');
    await user.click(screen.getByRole('button', { name: 'Trocar senha' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível trocar sua senha agora.');
    await waitFor(() => expect(proofWasRefreshed).toBe(true));
    expect(sessionReads).toBe(2);
    expect(changeCalls).toBe(1);
    window.removeEventListener('app:csrf-refreshed', handleCsrfRefresh);
  });

  it('shows the password rejection and keeps both values so the student can correct them', async () => {
    const user = userEvent.setup();
    server.use(
      http.get(`${env.API_URL}/api/v1/student-sessions/current`, () => HttpResponse.json({
        accountId: '0199f23d-4a00-7000-8000-000000000001',
        name: 'Ana Souza',
        csrfToken: 'session-csrf-proof',
      })),
      http.post(`${env.API_URL}/api/v1/password-changes`, () =>
        HttpResponse.json({ code: 'PASSWORD_CHANGE_REJECTED' }, { status: 400 })),
    );

    renderWithProviders(
      <MemoryRouter>
        <StudentPasswordChangeRoute />
      </MemoryRouter>,
    );

    const currentPassword = await screen.findByLabelText('Senha atual');
    const newPassword = screen.getByLabelText('Nova senha');
    await user.type(currentPassword, 'legacy');
    await user.type(newPassword, 'NovaSenha2!');
    await user.click(screen.getByRole('button', { name: 'Trocar senha' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'A senha atual não confere ou a nova senha não cumpre os requisitos. Confira e tente de novo.',
    );
    expect(currentPassword).toHaveValue('legacy');
    expect(newPassword).toHaveValue('NovaSenha2!');
  });
});
