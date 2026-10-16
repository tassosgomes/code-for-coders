import { http, HttpResponse } from 'msw';
import { RouterProvider, createMemoryRouter } from 'react-router';
import { ThemeProvider } from 'next-themes';
import { cleanup, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it } from 'vitest';

import { StaffLoginScreen } from '@/features/staff-session/components/staff-login-screen';
import { env } from '@/config/env';
import { expiredStaffSessionMarker, loggedOutStaffSessionMarker } from '@/config/session-markers';
import { server } from '@/testing/server';
import { expireTurnstile, solveTurnstile, turnstileMock, turnstileTestToken } from '@/testing/turnstile-mock';
import { renderWithProviders } from '@/testing/test-utils';

describe('StaffSession login', () => {
  afterEach(() => {
    cleanup();
    window.sessionStorage.clear();
  });

  const renderLogin = () => {
    const router = createMemoryRouter([
      { path: '/entrar', element: <StaffLoginScreen /> },
    ], { initialEntries: ['/entrar'] });
    renderWithProviders(<RouterProvider router={router} />);
  };

  it('shows placeholders and no notices by default', () => {
    renderLogin();

    expect(screen.getByPlaceholderText('voce@escola.com')).toBeInTheDocument();
    expect(screen.getByPlaceholderText('Sua senha')).toBeInTheDocument();
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  it('announces an ended session', () => {
    window.sessionStorage.setItem(expiredStaffSessionMarker, 'true');
    renderLogin();

    expect(screen.getByRole('status')).toHaveTextContent('Sua sessão foi encerrada');
    expect(screen.getByRole('status')).toHaveTextContent('Entre de novo para continuar.');
  });

  it('shows the logout toast once and clears the marker', () => {
    window.sessionStorage.setItem(loggedOutStaffSessionMarker, 'true');
    renderLogin();

    expect(screen.getByRole('status')).toHaveTextContent('Você saiu do backoffice.');
    expect(window.sessionStorage.getItem(loggedOutStaffSessionMarker)).toBeNull();
  });

  it('shows one generic error when Identity rejects the credentials', async () => {
    server.use(http.post(`${env.API_URL}/api/v1/staff-sessions`, () =>
      HttpResponse.json({ code: 'INVALID_CREDENTIALS' }, { status: 401 }),
    ));
    const router = createMemoryRouter([
      { path: '/entrar', element: <StaffLoginScreen /> },
    ], { initialEntries: ['/entrar'] });

    renderWithProviders(<RouterProvider router={router} />);
    const user = userEvent.setup();
    await user.type(screen.getByLabelText('E-mail'), 'student@example.com');
    await user.type(screen.getByLabelText('Senha'), 'SenhaForte1!');
    await user.click(screen.getByRole('button', { name: 'Entrar' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('E-mail ou senha incorretos');
    expect(alert).toHaveTextContent('Confira os dados e tente de novo.');
  });

  const fillAndSubmit = async (user: ReturnType<typeof userEvent.setup>) => {
    await user.type(screen.getByLabelText('E-mail'), 'marina@example.com');
    await user.type(screen.getByLabelText('Senha'), 'SenhaForte1!');
    await user.click(screen.getByRole('button', { name: 'Entrar' }));
  };

  it('disables the submit button without a captcha token and re-enables it after solving', async () => {
    renderLogin();
    expect(screen.getByRole('button', { name: 'Entrar' })).toBeEnabled();

    expireTurnstile();
    expect(screen.getByRole('button', { name: 'Entrar' })).toBeDisabled();
    expect(turnstileMock.resetCount).toBe(1);

    solveTurnstile();
    await waitFor(() => expect(screen.getByRole('button', { name: 'Entrar' })).toBeEnabled());
  });

  it('sends the captcha token in the X-Captcha-Token header', async () => {
    let header: string | null = null;
    server.use(http.post(`${env.API_URL}/api/v1/staff-sessions`, ({ request }) => {
      header = request.headers.get('X-Captcha-Token');
      return HttpResponse.json({
        accountId: '3e4f5a6b-7c8d-4e9f-8a0b-1c2d3e4f5a6b',
        name: 'Marina Alves',
        roles: ['administrador'],
        permissions: ['acesso.gerir'],
        csrfToken: 'staff-session-csrf',
      });
    }));
    renderLogin();
    await fillAndSubmit(userEvent.setup());

    await waitFor(() => expect(header).toBe(turnstileTestToken));
  });

  it.each([
    [403, 'CAPTCHA_INVALID', {}, 'Não conseguimos confirmar que você não é um robô. Tente de novo.'],
    [400, 'CAPTCHA_REQUIRED', {}, 'Confirme que você não é um robô para continuar.'],
    [503, 'CAPTCHA_UNAVAILABLE', {}, 'A verificação de segurança está indisponível no momento.'],
    [429, 'RATE_LIMITED', { 'Retry-After': '90' }, 'Tente de novo em 2 minutos.'],
  ])('shows a pt-BR message and restarts the widget on %i %s', async (status, code, headers, message) => {
    server.use(http.post(`${env.API_URL}/api/v1/staff-sessions`, () =>
      HttpResponse.json({ code }, { status, headers }),
    ));
    renderLogin();
    await fillAndSubmit(userEvent.setup());

    expect(await screen.findByRole('alert')).toHaveTextContent(message);
    expect(turnstileMock.resetCount).toBe(1);
    expect(screen.getByRole('button', { name: 'Entrar' })).toBeDisabled();
  });

  it('restarts the widget after a credentials error', async () => {
    server.use(http.post(`${env.API_URL}/api/v1/staff-sessions`, () =>
      HttpResponse.json({ code: 'INVALID_CREDENTIALS' }, { status: 401 }),
    ));
    renderLogin();
    await fillAndSubmit(userEvent.setup());

    await screen.findByRole('alert');
    expect(turnstileMock.resetCount).toBe(1);
  });

  it('passes the active dark theme to the widget', () => {
    const router = createMemoryRouter([
      { path: '/entrar', element: <StaffLoginScreen /> },
    ], { initialEntries: ['/entrar'] });
    renderWithProviders(
      <ThemeProvider attribute="class" defaultTheme="dark" enableSystem={false}>
        <RouterProvider router={router} />
      </ThemeProvider>,
    );

    expect(turnstileMock.props?.options).toMatchObject({ theme: 'dark', language: 'pt-BR' });
  });
});
