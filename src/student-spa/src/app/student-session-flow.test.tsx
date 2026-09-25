import { cleanup, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, RouterProvider, type RouteObject } from 'react-router';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { http, HttpResponse } from 'msw';

import { AppProviders } from '@/app/providers';
import { DashboardRoute, requireStudentSession } from '@/app/routes/dashboard-route';
import { RootRoute } from '@/app/routes/root-route';
import { RouteError } from '@/app/routes/route-error';
import { StudentLoginRoute } from '@/app/routes/student-login-route';
import { StudentAppLayoutRoute } from '@/app/routes/student-app-layout-route';
import { env } from '@/config/env';
import { queryClient } from '@/lib/query-client';
import { server } from '@/testing/server';

const routes: RouteObject[] = [
  {
    path: '/',
    element: <RootRoute />,
    errorElement: <RouteError />,
    children: [
      { path: 'entrar', element: <StudentLoginRoute /> },
      {
        loader: requireStudentSession,
        element: <StudentAppLayoutRoute />,
        children: [{ index: true, element: <DashboardRoute /> }],
      },
    ],
  },
];

const renderStudentApp = (initialEntry: string | { pathname: string; state?: unknown }) => {
  const router = createMemoryRouter(routes, { initialEntries: [initialEntry] });
  return render(
    <AppProviders>
      <RouterProvider router={router} />
    </AppProviders>,
  );
};

describe('StudentSession flow', () => {
  beforeEach(() => {
    queryClient.clear();
    window.localStorage.clear();
    window.sessionStorage.clear();
  });

  afterEach(() => {
    cleanup();
    queryClient.clear();
    window.localStorage.clear();
    window.sessionStorage.clear();
  });

  it('redirects a protected route to sign in when the session is expired', async () => {
    server.use(http.get(`${env.API_URL}/api/v1/student-sessions/current`, () =>
      HttpResponse.json({ code: 'SESSION_REQUIRED' }, { status: 401 }),
    ));
    renderStudentApp('/');

    expect(await screen.findByRole('heading', { name: 'Entrar' })).toBeInTheDocument();
  });

  it('keeps the session-expired alert until the student signs in', async () => {
    const user = userEvent.setup();
    renderStudentApp({ pathname: '/entrar', state: { sessionExpired: true } });

    expect(screen.getByRole('status')).toHaveTextContent('Sua sessão expirou');

    await user.click(screen.getByRole('button', { name: 'Entrar' }));

    expect(screen.getByRole('status')).toHaveTextContent('Sua sessão expirou');
  });

  it('carries a protected session-expired event to the login screen', async () => {
    server.use(http.get(`${env.API_URL}/api/v1/student-sessions/current`, () => HttpResponse.json({
      accountId: '0199f23d-4a00-7000-8000-000000000001',
      name: 'Ana Souza',
      csrfToken: 'session-csrf-proof',
    })));
    renderStudentApp('/');

    await screen.findByRole('heading', { name: 'Olá, Ana Souza 👋' });
    window.dispatchEvent(new Event('app:session-expired'));

    expect(await screen.findByRole('status')).toHaveTextContent('Sua sessão expirou');
  });

  it('creates a session, displays the student identity and ends the session', async () => {
    const user = userEvent.setup();
    renderStudentApp({ pathname: '/entrar', state: { sessionExpired: true } });

    expect(screen.getByRole('status')).toHaveTextContent('Sua sessão expirou');

    await user.type(screen.getByRole('textbox', { name: 'E-mail' }), 'ana@example.com');
    await user.type(screen.getByLabelText('Senha'), 'SenhaForte1!');
    await user.click(screen.getByRole('button', { name: 'Entrar' }));

    expect(await screen.findByRole('heading', { name: 'Olá, Ana Souza 👋' })).toBeInTheDocument();
    expect(screen.queryByText('Sua sessão expirou')).not.toBeInTheDocument();
    expect(window.sessionStorage.getItem('student-session-expired')).toBeNull();
    expect(window.localStorage.getItem('student-session-active')).toBe('true');
    expect(screen.getByRole('region', { name: 'Sua conta' })).toHaveTextContent('Ana Souza');
    await user.click(screen.getByRole('button', { name: 'Abrir o menu da conta de Ana Souza' }));
    await user.click(screen.getByRole('menuitem', { name: 'Sair' }));
    expect(await screen.findByRole('heading', { name: 'Entrar' })).toBeInTheDocument();
    expect(await screen.findByText('Você saiu da sua conta.')).toBeInTheDocument();
    expect(window.localStorage.getItem('student-session-active')).toBeNull();
    expect(window.sessionStorage.getItem('student-session-expired')).toBeNull();
  });

  it('refreshes CSRF proof after 403 without repeating the logout request', async () => {
    const user = userEvent.setup();
    let deleteCalls = 0;
    let currentSessionCalls = 0;
    server.use(
      http.get(`${env.API_URL}/api/v1/student-sessions/current`, () => {
        currentSessionCalls++;
        return HttpResponse.json({
          accountId: '00000000-0000-4000-8000-000000000001',
          name: 'Ana Souza',
          csrfToken: currentSessionCalls > 1 ? 'refreshed-csrf-proof' : 'student-session-csrf',
        });
      }),
      http.delete(`${env.API_URL}/api/v1/student-sessions/current`, () => {
        deleteCalls++;
        return HttpResponse.json({ code: 'CSRF_INVALID' }, { status: 403 });
      }),
    );
    renderStudentApp('/');

    await screen.findByRole('heading', { name: 'Olá, Ana Souza 👋' });
    await user.click(screen.getByRole('button', { name: 'Abrir o menu da conta de Ana Souza' }));
    await user.click(screen.getByRole('menuitem', { name: 'Sair' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível encerrar sua sessão.');
    expect(deleteCalls).toBe(1);

    await user.click(screen.getByRole('menuitem', { name: 'Sair' }));
    await waitFor(() => expect(deleteCalls).toBe(2));
    await user.keyboard('{Escape}');
    expect(screen.getByRole('heading', { name: 'Olá, Ana Souza 👋' })).toBeInTheDocument();
  });
});
