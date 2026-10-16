import type { ReactElement } from 'react';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { ThemeProvider } from 'next-themes';
import { cleanup, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it } from 'vitest';

import { env } from '@/config/env';
import { paths } from '@/config/paths';
import { StaffPasswordRecoveryScreen } from '@/features/staff-password-recovery/components/staff-password-recovery-screen';
import { server } from '@/testing/server';
import { expireTurnstile, turnstileMock, turnstileTestToken } from '@/testing/turnstile-mock';
import { renderWithProviders } from '@/testing/test-utils';

describe('StaffPasswordRecovery', () => {
  afterEach(() => cleanup());

  it('submits the email and shows the same confirmation message', async () => {
    const user = userEvent.setup();
    let requestBody: unknown;
    let idempotencyKey: string | null = null;
    server.use(
      http.post(`${env.API_URL}/api/v1/staff-password-reset-requests`, async ({ request }) => {
        requestBody = await request.json();
        idempotencyKey = request.headers.get('Idempotency-Key');
        return new HttpResponse(null, { status: 202 });
      }),
    );

    const router = createMemoryRouter(
      [{ path: paths.staffPasswordRecovery.path, element: <StaffPasswordRecoveryScreen /> }],
      { initialEntries: [paths.staffPasswordRecovery.getHref()] },
    );
    renderWithProviders(<RouterProvider router={router} />);

    await user.type(screen.getByLabelText('E-mail'), 'marina@example.com');
    await user.click(screen.getByRole('button', { name: 'Receber link de recuperação' }));

    expect(await screen.findByRole('heading', { name: 'Confira seu e-mail' })).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent(
      'Se esse e-mail for de uma conta da equipe, enviamos um link para criar uma nova senha. Ele vale por tempo limitado.',
    );
    expect(requestBody).toEqual({ email: 'marina@example.com' });
    expect(idempotencyKey).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Voltar para a entrada' })).toHaveAttribute('href', paths.staffLogin.getHref());
  });

  it('shows the error box when the request fails', async () => {
    const user = userEvent.setup();
    server.use(
      http.post(`${env.API_URL}/api/v1/staff-password-reset-requests`, () => new HttpResponse(null, { status: 500 })),
    );
    const router = createMemoryRouter(
      [{ path: paths.staffPasswordRecovery.path, element: <StaffPasswordRecoveryScreen /> }],
      { initialEntries: [paths.staffPasswordRecovery.getHref()] },
    );
    renderWithProviders(<RouterProvider router={router} />);

    expect(screen.getByPlaceholderText('voce@escola.com')).toBeInTheDocument();
    await user.type(screen.getByLabelText('E-mail'), 'marina@example.com');
    await user.click(screen.getByRole('button', { name: 'Receber link de recuperação' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível processar o pedido agora. Tente novamente.');
    expect(screen.getByRole('link', { name: 'Voltar para a entrada' })).toBeInTheDocument();
  });

  const renderRecovery = (wrap: (ui: ReactElement) => ReactElement = (ui) => ui) => {
    const router = createMemoryRouter(
      [{ path: paths.staffPasswordRecovery.path, element: <StaffPasswordRecoveryScreen /> }],
      { initialEntries: [paths.staffPasswordRecovery.getHref()] },
    );
    renderWithProviders(wrap(<RouterProvider router={router} />));
  };

  it('disables the submit button while the captcha token is missing', () => {
    renderRecovery();
    expireTurnstile();

    expect(screen.getByRole('button', { name: 'Receber link de recuperação' })).toBeDisabled();
    expect(turnstileMock.resetCount).toBe(1);
  });

  it('sends the captcha token in the X-Captcha-Token header', async () => {
    let header: string | null = null;
    server.use(http.post(`${env.API_URL}/api/v1/staff-password-reset-requests`, ({ request }) => {
      header = request.headers.get('X-Captcha-Token');
      return new HttpResponse(null, { status: 202 });
    }));
    renderRecovery();
    const user = userEvent.setup();
    await user.type(screen.getByLabelText('E-mail'), 'marina@example.com');
    await user.click(screen.getByRole('button', { name: 'Receber link de recuperação' }));

    await screen.findByRole('heading', { name: 'Confira seu e-mail' });
    expect(header).toBe(turnstileTestToken);
  });

  it.each([
    [403, 'CAPTCHA_INVALID', {}, 'Não conseguimos confirmar que você não é um robô. Tente de novo.'],
    [503, 'CAPTCHA_UNAVAILABLE', {}, 'A verificação de segurança está indisponível no momento.'],
    [429, 'RATE_LIMITED', { 'Retry-After': '30' }, 'Tente de novo em 30 segundos.'],
  ])('shows a pt-BR message and restarts the widget on %i %s', async (status, code, headers, message) => {
    server.use(http.post(`${env.API_URL}/api/v1/staff-password-reset-requests`, () =>
      HttpResponse.json({ code }, { status, headers }),
    ));
    renderRecovery();
    const user = userEvent.setup();
    await user.type(screen.getByLabelText('E-mail'), 'marina@example.com');
    await user.click(screen.getByRole('button', { name: 'Receber link de recuperação' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(message);
    expect(turnstileMock.resetCount).toBe(1);
    expect(screen.getByRole('button', { name: 'Receber link de recuperação' })).toBeDisabled();
  });

  it('passes the active dark theme to the widget', () => {
    renderRecovery((ui) => (
      <ThemeProvider attribute="class" defaultTheme="dark" enableSystem={false}>{ui}</ThemeProvider>
    ));

    expect(turnstileMock.props?.options).toMatchObject({ theme: 'dark' });
  });
});
