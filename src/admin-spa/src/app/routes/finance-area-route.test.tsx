import { http, HttpResponse } from 'msw';
import { RouterProvider, createMemoryRouter } from 'react-router';
import { cleanup, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it } from 'vitest';

import { AdminLayoutRoute } from '@/app/routes/admin-layout-route';
import { FinanceAreaRoute } from '@/app/routes/finance-area-route';
import { loadStaffSession } from '@/app/routes/staff-session-loader';
import { env } from '@/config/env';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const renderFinanceArea = (permissions: string[], onFinanceRequest: () => void) => {
  server.use(
    http.get(`${env.API_URL}/api/v1/staff-sessions/current`, () => HttpResponse.json({
      accountId: '3e4f5a6b-7c8d-4e9f-8a0b-1c2d3e4f5a6b',
      name: 'Marina Alves',
      roles: permissions.includes('financeiro.ler') ? ['financeiro'] : ['professor'],
      permissions,
      csrfToken: 'staff-session-csrf',
    })),
    http.get(`${env.API_URL}/api/v1/finance-area`, () => {
      onFinanceRequest();
      return HttpResponse.json({ status: 'reserved' });
    }),
  );

  const router = createMemoryRouter([{
    path: '/',
    loader: loadStaffSession,
    element: <AdminLayoutRoute serviceName="admin-spa" title="Admin Workspace" />,
    children: [{ path: 'financeiro', element: <FinanceAreaRoute /> }],
  }], { initialEntries: ['/financeiro'] });

  renderWithProviders(<RouterProvider router={router} />);
};

describe('FinanceArea', () => {
  afterEach(cleanup);

  it('shows an access message and does not call the finance API for a professor', async () => {
    let financeRequests = 0;
    renderFinanceArea(['autoria.ler'], () => { financeRequests += 1; });

    expect(await screen.findByRole('alert')).toHaveTextContent('Você não tem permissão para esta área.');
    await openNavigation();
    expect(screen.queryByRole('link', { name: 'Financeiro' })).not.toBeInTheDocument();
    expect(financeRequests).toBe(0);
  });

  it('shows the reserved area and links it from navigation for a finance actor', async () => {
    const user = userEvent.setup();
    renderFinanceArea(['financeiro.ler'], () => undefined);

    expect(await screen.findByRole('heading', { name: 'Financeiro' })).toBeInTheDocument();
    expect(await screen.findByText('Área financeira reservada.')).toBeInTheDocument();
    await openNavigation(user);
    expect(screen.getByRole('link', { name: 'Financeiro' })).toHaveAttribute('href', '/financeiro');
  });
});

const openNavigation = async (user = userEvent.setup()) => {
  const toggle = screen.getByRole('button', { name: 'Toggle navigation' });
  if (toggle.getAttribute('aria-expanded') !== 'true') {
    await user.click(toggle);
  }
};
