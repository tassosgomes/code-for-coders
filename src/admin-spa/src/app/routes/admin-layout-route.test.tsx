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

    const areas = within(await screen.findByRole('region', { name: 'Áreas disponíveis' }));
    expect(areas.getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      'Financeiro',
      'Suporte',
    ]);
    expect(areas.queryByText('Acessos')).not.toBeInTheDocument();
    expect(areas.queryByText('Sua conta ainda não tem acesso a nenhuma área do backoffice.')).not.toBeInTheDocument();
  });

  it('shows the no-access guidance without any area when the account has no role', async () => {
    renderAdminHome([]);

    const areas = within(await screen.findByRole('region', { name: 'Áreas disponíveis' }));
    expect(areas.getByText('Sua conta ainda não tem acesso a nenhuma área do backoffice.')).toBeInTheDocument();
    expect(areas.queryByRole('list')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Sair' })).toBeInTheDocument();
  });
});
