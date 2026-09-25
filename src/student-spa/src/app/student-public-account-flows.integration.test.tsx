import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { env } from '@/config/env';
import { paths } from '@/config/paths';
import { StudentConfirmationRoute } from '@/app/routes/student-confirmation-route';
import { StudentPasswordRecoveryRoute, StudentPasswordResetRoute } from '@/app/routes/student-password-recovery-route';
import { StudentRegistrationRoute } from '@/app/routes/student-registration-route';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const LocationProbe = () => {
  const location = useLocation();
  return <p data-testid="current-url">{`${location.pathname}${location.search}`}</p>;
};

const renderPublicAccountRoutes = (entry: string) =>
  renderWithProviders(
    <MemoryRouter initialEntries={[entry]}>
      <Routes>
        <Route
          element={
            <>
              <StudentRegistrationRoute />
              <LocationProbe />
            </>
          }
          path={paths.studentRegistration.path}
        />
        <Route
          element={
            <>
              <StudentConfirmationRoute />
              <LocationProbe />
            </>
          }
          path={paths.studentAccountConfirmation.path}
        />
        <Route
          element={
            <>
              <StudentPasswordRecoveryRoute />
              <LocationProbe />
            </>
          }
          path={paths.studentPasswordRecovery.path}
        />
        <Route
          element={
            <>
              <StudentPasswordResetRoute />
              <LocationProbe />
            </>
          }
          path={paths.studentPasswordReset.path}
        />
      </Routes>
    </MemoryRouter>,
  );

describe('public account flows', () => {
  it('registration explains an existing account and allows a neutral confirmation resend', async () => {
    const user = userEvent.setup();
    let requestBody: unknown;
    let idempotencyKey: string | null = null;

    server.use(
      http.post(`${env.API_URL}/api/v1/student-accounts`, () => HttpResponse.json({
        code: 'ACCOUNT_ALREADY_EXISTS',
      }, { status: 409 })),
      http.post(`${env.API_URL}/api/v1/account-confirmation-requests`, async ({ request }) => {
        requestBody = await request.json();
        idempotencyKey = request.headers.get('Idempotency-Key');
        return new HttpResponse(null, { status: 202 });
      }),
    );

    renderPublicAccountRoutes(paths.studentRegistration.getHref());

    await user.type(screen.getByRole('textbox', { name: 'Nome' }), 'Ana Souza');
    await user.type(screen.getByRole('textbox', { name: 'E-mail' }), 'ANA@example.com');
    await user.type(screen.getByLabelText('Senha'), 'SenhaForte1!');
    await user.click(screen.getByRole('button', { name: 'Criar conta' }));

    expect(await screen.findByText('Já existe uma conta com esse e-mail.')).toBeInTheDocument();
    await user.click(screen.getByRole('link', { name: 'Reenviar confirmação' }));
    expect(await screen.findByRole('heading', { name: 'Reenviar confirmação' })).toBeInTheDocument();
    expect(screen.getByText(/Se houver uma conta pendente/)).toBeInTheDocument();

    await user.type(screen.getByRole('textbox', { name: 'E-mail' }), 'ANA@example.com');
    await user.click(screen.getByRole('button', { name: 'Receber novo link' }));

    expect(await screen.findByRole('heading', { name: 'Verifique seu e-mail' })).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Se houver uma conta pendente');
    expect(requestBody).toEqual({ email: 'ana@example.com' });
    expect(idempotencyKey).toBeTruthy();
  });

  it('confirmation removes a valid token from the route and offers the login action', async () => {
    let requestBody: unknown;
    let idempotencyKey: string | null = null;

    server.use(
      http.post(`${env.API_URL}/api/v1/account-confirmations`, async ({ request }) => {
        requestBody = await request.json();
        idempotencyKey = request.headers.get('Idempotency-Key');
        return new HttpResponse(null, { status: 204 });
      }),
    );

    renderPublicAccountRoutes(`${paths.studentAccountConfirmation.getHref()}?token=confirmation-secret`);

    expect(await screen.findByRole('heading', { name: 'E-mail confirmado' })).toBeInTheDocument();
    await waitFor(() => expect(screen.getByTestId('current-url')).toHaveTextContent(
      paths.studentAccountConfirmation.getHref(),
    ));
    expect(screen.getByTestId('current-url')).not.toHaveTextContent('confirmation-secret');
    expect(requestBody).toEqual({ token: 'confirmation-secret' });
    expect(idempotencyKey).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Entrar na plataforma' })).toHaveAttribute(
      'href',
      paths.studentLogin.getHref(),
    );
  });

  it('password recovery keeps the email after a network failure and returns a neutral result', async () => {
    const user = userEvent.setup();
    const idempotencyKeys: Array<string | null> = [];
    let requestCount = 0;

    server.use(
      http.post(`${env.API_URL}/api/v1/password-reset-requests`, async ({ request }) => {
        idempotencyKeys.push(request.headers.get('Idempotency-Key'));
        requestCount += 1;
        if (requestCount === 1) {
          return HttpResponse.json({ title: 'Temporary failure' }, { status: 503 });
        }
        return new HttpResponse(null, { status: 202 });
      }),
    );

    renderPublicAccountRoutes(paths.studentPasswordRecovery.getHref());

    const emailField = screen.getByRole('textbox', { name: 'E-mail' });
    await user.type(emailField, 'ana@example.com');
    await user.click(screen.getByRole('button', { name: 'Receber link de recuperação' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Não conseguimos solicitar a recuperação agora.');
    expect(emailField).toHaveValue('ana@example.com');
    await user.click(screen.getByRole('button', { name: 'Receber link de recuperação' }));

    expect(await screen.findByRole('heading', { name: 'Verifique seu e-mail' })).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Se houver uma conta de aluno');
    expect(screen.getByRole('status')).toHaveTextContent('ana@example.com');
    expect(requestCount).toBe(2);
    expect(idempotencyKeys[0]).toBeTruthy();
    expect(idempotencyKeys[1]).toBe(idempotencyKeys[0]);
  });

  it('an invalid password leaves the reset token available for a valid retry', async () => {
    const user = userEvent.setup();
    let requestBody: unknown;
    let idempotencyKey: string | null = null;
    let requestCount = 0;

    server.use(
      http.post(`${env.API_URL}/api/v1/password-resets`, async ({ request }) => {
        requestCount += 1;
        requestBody = await request.json();
        idempotencyKey = request.headers.get('Idempotency-Key');
        return new HttpResponse(null, { status: 204 });
      }),
    );

    renderPublicAccountRoutes(`${paths.studentPasswordReset.getHref()}?token=reset-secret`);

    expect(await screen.findByRole('heading', { name: 'Crie uma nova senha' })).toBeInTheDocument();
    await waitFor(() => expect(screen.getByTestId('current-url')).toHaveTextContent(
      paths.studentPasswordReset.getHref(),
    ));
    expect(screen.getByTestId('current-url')).not.toHaveTextContent('reset-secret');

    const passwordField = screen.getByLabelText('Nova senha');
    await user.type(passwordField, 'curta');
    await user.click(screen.getByRole('button', { name: 'Redefinir senha' }));

    expect(await screen.findByText('Use oito ou mais caracteres, com maiúscula, minúscula, número e símbolo.')).toBeInTheDocument();
    expect(requestCount).toBe(0);
    expect(passwordField).toHaveValue('curta');

    await user.clear(passwordField);
    await user.type(passwordField, 'SenhaNova2!');
    await user.click(screen.getByRole('button', { name: 'Redefinir senha' }));

    expect(await screen.findByRole('heading', { name: 'Senha redefinida' })).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('encerramos as outras sessões');
    expect(requestBody).toEqual({ token: 'reset-secret', newPassword: 'SenhaNova2!' });
    expect(idempotencyKey).toBeTruthy();
    expect(requestCount).toBe(1);
    expect(screen.getByTestId('current-url')).not.toHaveTextContent('reset-secret');
  });
});
