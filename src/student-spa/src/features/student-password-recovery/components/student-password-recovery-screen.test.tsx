import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { env } from '@/config/env';
import { paths } from '@/config/paths';
import { StudentPasswordRecoveryScreen } from '@/features/student-password-recovery/components/student-password-recovery-screen';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const LocationProbe = () => {
  const location = useLocation();
  return <p data-testid="current-url">{`${location.pathname}${location.search}`}</p>;
};

const renderRecoveryRoute = (entry: string, mode: 'request' | 'reset') =>
  renderWithProviders(
    <MemoryRouter initialEntries={[entry]}>
      <Routes>
        <Route
          element={
            <>
              <StudentPasswordRecoveryScreen mode={mode} />
              <LocationProbe />
            </>
          }
          path="*"
        />
      </Routes>
    </MemoryRouter>,
  );

describe('StudentPasswordRecovery', () => {
  it('StudentPasswordRecovery returns the same neutral request message and normalizes the email', async () => {
    const user = userEvent.setup();
    let requestBody: unknown;
    let idempotencyKey: string | null = null;
    server.use(
      http.post(`${env.API_URL}/api/v1/password-reset-requests`, async ({ request }) => {
        requestBody = await request.json();
        idempotencyKey = request.headers.get('Idempotency-Key');
        return new HttpResponse(null, { status: 202 });
      }),
    );

    renderRecoveryRoute(paths.studentPasswordRecovery.getHref(), 'request');

    await user.type(screen.getByRole('textbox', { name: 'E-mail' }), 'ANA@example.com');
    await user.click(screen.getByRole('button', { name: 'Receber link de recuperação' }));

    expect(await screen.findByRole('heading', { name: 'Verifique seu e-mail' })).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Se houver uma conta de aluno com ana@example.com');
    expect(requestBody).toEqual({ email: 'ana@example.com' });
    expect(idempotencyKey).toBeTruthy();
  });

  it('StudentPasswordRecovery removes the reset token from the URL and submits it with the new password', async () => {
    const user = userEvent.setup();
    let requestBody: unknown;
    let idempotencyKey: string | null = null;
    server.use(
      http.post(`${env.API_URL}/api/v1/password-resets`, async ({ request }) => {
        requestBody = await request.json();
        idempotencyKey = request.headers.get('Idempotency-Key');
        return new HttpResponse(null, { status: 204 });
      }),
    );

    renderRecoveryRoute(`${paths.studentPasswordReset.getHref()}?token=one-time-secret`, 'reset');

    expect(await screen.findByRole('heading', { name: 'Crie uma nova senha' })).toBeInTheDocument();
    await waitFor(() => expect(screen.getByTestId('current-url')).toHaveTextContent(paths.studentPasswordReset.getHref()));
    expect(screen.getByTestId('current-url')).not.toHaveTextContent('one-time-secret');
    await user.type(screen.getByLabelText('Nova senha'), 'SenhaNova2!');
    await user.click(screen.getByRole('button', { name: 'Redefinir senha' }));

    expect(await screen.findByRole('heading', { name: 'Senha redefinida' })).toBeInTheDocument();
    expect(requestBody).toEqual({ token: 'one-time-secret', newPassword: 'SenhaNova2!' });
    expect(idempotencyKey).toBeTruthy();
  });

  it('StudentPasswordRecovery keeps a rejected link out of the URL and offers another request', async () => {
    const user = userEvent.setup();
    server.use(
      http.post(`${env.API_URL}/api/v1/password-resets`, () => HttpResponse.json({
        code: 'PASSWORD_RESET_REJECTED',
      }, { status: 422 })),
    );

    renderRecoveryRoute(`${paths.studentPasswordReset.getHref()}?token=expired-token`, 'reset');

    await waitFor(() => expect(screen.getByTestId('current-url')).not.toHaveTextContent('expired-token'));
    await user.type(screen.getByLabelText('Nova senha'), 'SenhaNova2!');
    await user.click(screen.getByRole('button', { name: 'Redefinir senha' }));

    expect(await screen.findByRole('heading', { name: 'Este link não vale mais' })).toBeInTheDocument();
    expect(screen.getByTestId('current-url')).not.toHaveTextContent('expired-token');
    expect(screen.getByRole('link', { name: 'Pedir novo link' })).toHaveAttribute(
      'href',
      paths.studentPasswordRecovery.getHref(),
    );
  });
});
