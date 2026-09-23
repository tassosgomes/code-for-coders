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
import { env } from '@/config/env';
import { queryClient } from '@/lib/query-client';
import { server } from '@/testing/server';

const routes: RouteObject[] = [
  {
    path: '/',
    element: <RootRoute />,
    errorElement: <RouteError />,
    children: [
      { index: true, loader: requireStudentSession, element: <DashboardRoute /> },
      { path: 'entrar', element: <StudentLoginRoute /> },
    ],
  },
];

const renderStudentApp = (initialEntry: string) => {
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
  });

  afterEach(() => {
    cleanup();
    queryClient.clear();
  });

  it('redirects a protected route to sign in when the session is expired', async () => {
    server.use(http.get(`${env.API_URL}/api/v1/student-sessions/current`, () =>
      HttpResponse.json({ code: 'SESSION_REQUIRED' }, { status: 401 }),
    ));
    renderStudentApp('/');

    expect(await screen.findByRole('heading', { name: 'Entrar' })).toBeInTheDocument();
  });

  it('creates a session, displays the student identity and ends the session', async () => {
    const user = userEvent.setup();
    renderStudentApp('/entrar');

    await user.type(screen.getByRole('textbox', { name: 'E-mail' }), 'ana@example.com');
    await user.type(screen.getByLabelText('Senha'), 'SenhaForte1!');
    await user.click(screen.getByRole('button', { name: 'Entrar' }));

    expect(await screen.findByRole('heading', { name: 'Learning overview' })).toBeInTheDocument();
    expect(await screen.findByText('Ana Souza')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Sair' }));
    expect(await screen.findByRole('heading', { name: 'Entrar' })).toBeInTheDocument();
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

    await screen.findByRole('heading', { name: 'Learning overview' });
    await user.click(await screen.findByRole('button', { name: 'Sair' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível encerrar sua sessão.');
    expect(deleteCalls).toBe(1);

    await user.click(screen.getByRole('button', { name: 'Sair' }));
    await waitFor(() => expect(deleteCalls).toBe(2));
    expect(screen.getByRole('heading', { name: 'Learning overview' })).toBeInTheDocument();
  });
});
