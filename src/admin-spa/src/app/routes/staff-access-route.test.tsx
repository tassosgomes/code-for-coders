import { http, HttpResponse } from 'msw';
import { RouterProvider, createMemoryRouter } from 'react-router';
import { cleanup, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it } from 'vitest';

import { AdminLayoutRoute } from '@/app/routes/admin-layout-route';
import { StaffAccessRoute } from '@/app/routes/staff-access-route';
import { loadStaffSession } from '@/app/routes/staff-session-loader';
import { env } from '@/config/env';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const renderStaffAccess = (createCode?: string) => {
  server.use(
    http.get(`${env.API_URL}/api/v1/staff-sessions/current`, () => HttpResponse.json({
      accountId: '3e4f5a6b-7c8d-4e9f-8a0b-1c2d3e4f5a6b',
      name: 'Marina Alves',
      roles: ['administrador'],
      permissions: ['acesso.gerir'],
      csrfToken: 'staff-session-csrf',
    })),
    http.get(`${env.API_URL}/api/v1/staff-invitations`, () => HttpResponse.json({
      data: [],
      pagination: { page: 1, size: 100, total: 0, totalPages: 0 },
    })),
    http.post(`${env.API_URL}/api/v1/staff-invitations`, async ({ request }) => {
      if (createCode) {
        return HttpResponse.json({ code: createCode }, { status: 422 });
      }

      const body = await request.json() as { email: string; role: string };
      return HttpResponse.json({
        invitationId: '5f6e7d8c-9b0a-4c1d-8e2f-3a4b5c6d7e8f',
        email: body.email,
        offeredRole: body.role,
        invitedAt: '2026-10-02T14:05:11Z',
        expiresAt: '2026-10-09T14:05:11Z',
        supersededInvitationId: null,
      }, { status: 201 });
    }),
  );

  const router = createMemoryRouter([
    {
      path: '/',
      loader: loadStaffSession,
      element: <AdminLayoutRoute serviceName="admin-spa" title="Admin Workspace" />,
      children: [{ path: 'acessos', element: <StaffAccessRoute /> }],
    },
  ], { initialEntries: ['/acessos'] });

  renderWithProviders(<RouterProvider router={router} />);
};

describe('StaffInvitationIssuing', () => {
  afterEach(cleanup);

  it('submits an invitation and shows the pending invitation area', async () => {
    const user = userEvent.setup();
    renderStaffAccess();

    await user.type(await screen.findByLabelText('E-mail'), 'convidada@example.com');
    await user.selectOptions(screen.getByLabelText('Papel'), 'suporte');
    await user.type(screen.getByLabelText('Motivo'), 'Vai atuar no suporte interno.');
    await user.click(screen.getByRole('button', { name: 'Convidar' }));

    expect(await screen.findByRole('status')).toHaveTextContent('Convite enviado para convidada@example.com.');
    expect(screen.getByRole('heading', { name: 'Convites pendentes' })).toBeInTheDocument();
    expect(screen.getByText('Nenhum convite pendente.')).toBeInTheDocument();
  });

  it.each([
    ['EMAIL_BELONGS_TO_STAFF', 'Este e-mail já pertence a uma conta interna.'],
    ['EMAIL_BELONGS_TO_STUDENT', 'Este e-mail já pertence a uma conta de aluno.'],
    ['REASON_REQUIRED', 'Informe o motivo do convite.'],
  ])('shows the specific message for %s', async (code, message) => {
    const user = userEvent.setup();
    renderStaffAccess(code);

    await user.type(await screen.findByLabelText('E-mail'), 'convidada@example.com');
    await user.type(screen.getByLabelText('Motivo'), 'Motivo informado.');
    await user.click(screen.getByRole('button', { name: 'Convidar' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(message);
  });

  it('refuses a reason longer than 1000 characters without sending the invitation', async () => {
    const user = userEvent.setup();
    let invitationRequests = 0;
    server.events.on('request:start', ({ request }) => {
      if (request.method === 'POST' && request.url.endsWith('/api/v1/staff-invitations')) {
        invitationRequests += 1;
      }
    });
    renderStaffAccess();

    await user.type(await screen.findByLabelText('E-mail'), 'convidada@example.com');
    await user.click(screen.getByLabelText('Motivo'));
    await user.paste('m'.repeat(1001));
    await user.click(screen.getByRole('button', { name: 'Convidar' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('O motivo deve ter no máximo 1000 caracteres.');
    expect(invitationRequests).toBe(0);
    server.events.removeAllListeners('request:start');
  });
});
