import { http, HttpResponse } from 'msw';
import { RouterProvider, createMemoryRouter } from 'react-router';
import { cleanup, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it } from 'vitest';

import { AdminLayoutRoute } from '@/app/routes/admin-layout-route';
import { StaffAccessRoute } from '@/app/routes/staff-access-route';
import { loadStaffSession } from '@/app/routes/staff-session-loader';
import { env } from '@/config/env';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

type StaffMemberFixture = {
  accountId: string;
  name: string;
  email: string;
  roles: string[];
  isSelf: boolean;
};

type RoleActionFixture = { role: string; reason: string };

const renderStaffAccess = (
  createCode?: string,
  staffMembers: StaffMemberFixture[] = [],
  onRoleAction?: (action: 'grant' | 'revoke', request: Request, body: RoleActionFixture) => void,
) => {
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
    http.get(`${env.API_URL}/api/v1/staff-members`, () => HttpResponse.json({
      data: staffMembers,
      pagination: { page: 1, size: 100, total: staffMembers.length, totalPages: staffMembers.length === 0 ? 0 : 1 },
    })),
    http.post(`${env.API_URL}/api/v1/staff-members/:accountId/role-grants`, async ({ params, request }) => {
      const body = await request.json() as RoleActionFixture;
      onRoleAction?.('grant', request, body);
      const member = staffMembers.find((candidate) => candidate.accountId === params.accountId);
      return HttpResponse.json({
        member: { ...member, roles: [...(member?.roles ?? []), body.role] },
        changed: true,
        sessionsEnded: false,
      });
    }),
    http.post(`${env.API_URL}/api/v1/staff-members/:accountId/role-revocations`, async ({ params, request }) => {
      const body = await request.json() as RoleActionFixture;
      onRoleAction?.('revoke', request, body);
      const member = staffMembers.find((candidate) => candidate.accountId === params.accountId);
      return HttpResponse.json({
        member: { ...member, roles: (member?.roles ?? []).filter((role) => role !== body.role) },
        changed: true,
        sessionsEnded: true,
      });
    }),
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

describe('StaffMembers', () => {
  afterEach(cleanup);

  it('lists roles and omits actions for the current actor', async () => {
    const user = userEvent.setup();
    renderStaffAccess(undefined, [
      {
        accountId: '3e4f5a6b-7c8d-4e9f-8a0b-1c2d3e4f5a6b',
        name: 'Marina Alves',
        email: 'marina@example.com',
        roles: ['administrador'],
        isSelf: true,
      },
      {
        accountId: '7a8b9c0d-1e2f-4a3b-9c4d-5e6f7a8b9c0d',
        name: 'Rafaela Lima',
        email: 'rafaela@example.com',
        roles: ['professor'],
        isSelf: false,
      },
    ]);

    expect(await screen.findByRole('heading', { name: 'Pessoas com acesso' })).toBeInTheDocument();
    expect(await screen.findByText('Papéis: administrador')).toBeInTheDocument();
    expect(screen.getByText('Papéis: professor')).toBeInTheDocument();
    expect(screen.getByText('Ao revogar um papel, a pessoa será desconectada agora.')).toBeInTheDocument();
    const selfRow = screen.getByText('Marina Alves').closest('li');
    expect(selfRow).not.toBeNull();
    expect(within(selfRow!).queryByRole('button')).not.toBeInTheDocument();
    const memberRow = screen.getByText('Rafaela Lima').closest('li');
    expect(memberRow).not.toBeNull();
    expect(within(memberRow!).getByRole('button', { name: 'Conceder' })).toBeInTheDocument();
    await user.selectOptions(screen.getByLabelText('Papel para Rafaela Lima'), 'suporte');
  });

  it('grants a role with its reason and idempotency key', async () => {
    const user = userEvent.setup();
    let requestCount = 0;
    let idempotencyKey: string | null = null;
    let submittedInput: RoleActionFixture | null = null;
    renderStaffAccess(undefined, [{
      accountId: '7a8b9c0d-1e2f-4a3b-9c4d-5e6f7a8b9c0d',
      name: 'Rafaela Lima',
      email: 'rafaela@example.com',
      roles: [],
      isSelf: false,
    }], (action, request, body) => {
      expect(action).toBe('grant');
      requestCount += 1;
      idempotencyKey = request.headers.get('Idempotency-Key');
      submittedInput = body;
    });

    await user.type(await screen.findByLabelText('Motivo para Rafaela Lima'), 'Cobertura do suporte.');
    await user.selectOptions(screen.getByLabelText('Papel para Rafaela Lima'), 'suporte');
    await user.click(screen.getByRole('button', { name: 'Conceder' }));

    expect(await screen.findByRole('status')).toHaveTextContent('suporte concedido a Rafaela Lima.');
    expect(requestCount).toBe(1);
    expect(idempotencyKey).toMatch(/^[0-9a-f-]{36}$/i);
    expect(submittedInput).toEqual({ role: 'suporte', reason: 'Cobertura do suporte.' });
  });

  it('revokes the selected role and tells the user that the member was disconnected', async () => {
    const user = userEvent.setup();
    let requestCount = 0;
    renderStaffAccess(undefined, [{
      accountId: '7a8b9c0d-1e2f-4a3b-9c4d-5e6f7a8b9c0d',
      name: 'Rafaela Lima',
      email: 'rafaela@example.com',
      roles: ['professor'],
      isSelf: false,
    }], (action) => {
      expect(action).toBe('revoke');
      requestCount += 1;
    });

    await user.selectOptions(await screen.findByLabelText('Papel para Rafaela Lima'), 'professor');
    await user.type(screen.getByLabelText('Motivo para Rafaela Lima'), 'Fim da cobertura.');
    await user.click(screen.getByRole('button', { name: 'Revogar' }));

    expect(await screen.findByRole('status')).toHaveTextContent('professor revogado; Rafaela Lima foi desconectada agora.');
    expect(requestCount).toBe(1);
  });

  it('requires a reason before sending a role action', async () => {
    const user = userEvent.setup();
    let requestCount = 0;
    renderStaffAccess(undefined, [{
      accountId: '7a8b9c0d-1e2f-4a3b-9c4d-5e6f7a8b9c0d',
      name: 'Rafaela Lima',
      email: 'rafaela@example.com',
      roles: [],
      isSelf: false,
    }], () => { requestCount += 1; });

    await user.click(await screen.findByRole('button', { name: 'Conceder' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Informe o motivo da alteração.');
    expect(requestCount).toBe(0);
  });
});
