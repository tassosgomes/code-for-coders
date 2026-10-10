import { http, HttpResponse } from 'msw';
import { RouterProvider, createMemoryRouter } from 'react-router';
import { cleanup, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it } from 'vitest';

import { AdminLayoutRoute } from '@/app/routes/admin-layout-route';
import { DashboardRoute } from '@/app/routes/dashboard-route';
import { loadStaffSession } from '@/app/routes/staff-session-loader';
import { env } from '@/config/env';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const renderAdminHome = (permissions: string[], initialPath = '/') => {
  server.use(http.get(`${env.API_URL}/api/v1/staff-sessions/current`, () =>
    HttpResponse.json({
      accountId: '3e4f5a6b-7c8d-4e9f-8a0b-1c2d3e4f5a6b',
      name: 'Marina Alves',
      roles: permissions.length === 0 ? [] : ['financeiro'],
      permissions,
      csrfToken: 'staff-session-csrf',
    }),
  ));
  const router = createMemoryRouter([
    {
      path: '/',
      loader: loadStaffSession,
      element: <AdminLayoutRoute serviceName="admin-spa" title="Admin Workspace" />,
      children: [
        { index: true, element: <DashboardRoute /> },
        { path: 'autoria', element: <p>Cursos da escola</p> },
      ],
    },
  ], { initialEntries: [initialPath] });

  renderWithProviders(<RouterProvider router={router} />);
};

describe('StaffSession areas', () => {
  afterEach(cleanup);

  it('shows only the areas granted by the session permissions', async () => {
    renderAdminHome(['financeiro.ler', 'suporte.atender']);

    const navigation = within(await screen.findByRole('navigation', { name: 'admin-spa navigation' }));
    expect(navigation.getByRole('link', { name: 'Financeiro' })).toBeInTheDocument();
    expect(navigation.queryByRole('link', { name: 'Acessos' })).not.toBeInTheDocument();
    expect(navigation.queryByRole('link', { name: 'Suporte' })).not.toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Olá, Marina' })).toBeInTheDocument();
  });

  it.each(['/', '/autoria'])('keeps Vídeos before Autoria in the sidebar on %s', async (path) => {
    renderAdminHome(['midia.enviar', 'autoria.ler'], path);

    const navigation = within(await screen.findByRole('navigation', { name: 'admin-spa navigation' }));
    const labels = navigation.getAllByRole('link').map((link) => link.textContent);
    expect(labels.indexOf('Vídeos')).toBeGreaterThan(-1);
    expect(labels.indexOf('Vídeos')).toBeLessThan(labels.indexOf('Autoria'));
  });

  it('shows the no-access guidance without any area when the account has no role', async () => {
    renderAdminHome([]);

    expect(await screen.findByRole('heading', { name: 'Você ainda não tem acesso a uma área' })).toBeInTheDocument();
    const navigation = within(screen.getByRole('navigation', { name: 'admin-spa navigation' }));
    expect(navigation.queryByRole('link', { name: 'Acessos' })).not.toBeInTheDocument();
    expect(navigation.queryByRole('link', { name: 'Financeiro' })).not.toBeInTheDocument();
  });

  it('clears the logout error when navigating to another admin route', async () => {
    server.use(
      http.get(`${env.API_URL}/api/v1/staff-sessions/current`, () => HttpResponse.json({
        accountId: '3e4f5a6b-7c8d-4e9f-8a0b-1c2d3e4f5a6b',
        name: 'Marina Alves',
        roles: ['administrador'],
        permissions: ['acesso.gerir'],
        csrfToken: 'staff-session-csrf',
      })),
      http.delete(`${env.API_URL}/api/v1/staff-sessions/current`, () =>
        HttpResponse.json({ code: 'CSRF_INVALID' }, { status: 403 }),
      ),
    );
    const router = createMemoryRouter([
      {
        path: '/',
        loader: loadStaffSession,
        element: <AdminLayoutRoute serviceName="admin-spa" title="Admin Workspace" />,
        children: [
          { index: true, element: <h1>Início</h1> },
          { path: 'acessos', element: <h1>Gerenciar acessos</h1> },
        ],
      },
    ], { initialEntries: ['/'] });
    const user = userEvent.setup();

    renderWithProviders(<RouterProvider router={router} />);

    await user.click(await screen.findByRole('button', { name: /Marina Alves/ }));
    await user.click(screen.getByRole('menuitem', { name: 'Sair' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível sair agora. Tente novamente.');

    await user.click(screen.getByRole('link', { name: 'Acessos' }));

    expect(await screen.findByRole('heading', { name: 'Gerenciar acessos' })).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
