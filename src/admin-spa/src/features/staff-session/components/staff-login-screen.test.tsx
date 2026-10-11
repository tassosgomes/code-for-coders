import { http, HttpResponse } from 'msw';
import { RouterProvider, createMemoryRouter } from 'react-router';
import { cleanup, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it } from 'vitest';

import { StaffLoginScreen } from '@/features/staff-session/components/staff-login-screen';
import { env } from '@/config/env';
import { expiredStaffSessionMarker, loggedOutStaffSessionMarker } from '@/config/session-markers';
import { server } from '@/testing/server';
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
});
