import { http, HttpResponse } from 'msw';
import { RouterProvider, createMemoryRouter } from 'react-router';
import { cleanup, screen, within } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';

import { AdminLayoutRoute } from '@/app/routes/admin-layout-route';
import { DashboardRoute } from '@/app/routes/dashboard-route';
import { loadStaffSession } from '@/app/routes/staff-session-loader';
import { env } from '@/config/env';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const renderAdminHome = (permissions: string[]) => {
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
      children: [{ index: true, element: <DashboardRoute /> }],
    },
  ], { initialEntries: ['/'] });

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

  it('shows the no-access guidance without any area when the account has no role', async () => {
    renderAdminHome([]);

    expect(await screen.findByRole('heading', { name: 'Você ainda não tem acesso a uma área' })).toBeInTheDocument();
    const navigation = within(screen.getByRole('navigation', { name: 'admin-spa navigation' }));
    expect(navigation.queryByRole('link', { name: 'Acessos' })).not.toBeInTheDocument();
    expect(navigation.queryByRole('link', { name: 'Financeiro' })).not.toBeInTheDocument();
  });
});
