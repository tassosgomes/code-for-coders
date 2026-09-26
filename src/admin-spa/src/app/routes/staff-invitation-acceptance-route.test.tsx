import { http, HttpResponse } from 'msw';
import { RouterProvider, createMemoryRouter } from 'react-router';
import { cleanup, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it } from 'vitest';

import { StaffInvitationAcceptanceRoute } from '@/app/routes/staff-invitation-acceptance-route';
import { env } from '@/config/env';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const renderStaffInvitationAcceptance = (token: string | null) => {
  window.history.replaceState(
    window.history.state,
    '',
    token ? `/admin/convite?token=${encodeURIComponent(token)}#invitation` : '/admin/convite',
  );
  const router = createMemoryRouter([
    { path: '/convite', element: <StaffInvitationAcceptanceRoute /> },
    { path: '/', element: <p>Início do backoffice</p> },
  ], { initialEntries: ['/convite'] });

  renderWithProviders(<RouterProvider router={router} />);
};

describe('StaffInvitationAcceptance', () => {
  afterEach(() => {
    cleanup();
    server.resetHandlers();
    window.history.replaceState(window.history.state, '', '/');
  });

  it('removes the token from the address, looks up the role, accepts and opens the backoffice', async () => {
    const user = userEvent.setup();
    let lookupUrl = '';
    let lookupToken = '';
    let lookupAddressSearch = '';
    let acceptanceUrl = '';
    let acceptanceToken = '';
    let acceptedName = '';
    let acceptedPassword = '';
    let idempotencyKey = '';
    server.use(
      http.post(`${env.API_URL}/api/v1/staff-invitation-lookups`, async ({ request }) => {
        lookupUrl = request.url;
        lookupAddressSearch = window.location.search;
        lookupToken = ((await request.json()) as { token: string }).token;
        return HttpResponse.json({ offeredRole: 'professor', expiresAt: '2026-10-09T14:05:11Z' });
      }),
      http.post(`${env.API_URL}/api/v1/staff-invitation-acceptances`, async ({ request }) => {
        acceptanceUrl = request.url;
        const body = await request.json() as { token: string; name: string; password: string };
        acceptanceToken = body.token;
        acceptedName = body.name;
        acceptedPassword = body.password;
        idempotencyKey = request.headers.get('Idempotency-Key') ?? '';
        return HttpResponse.json({
          accountId: '7a8b9c0d-1e2f-4a3b-9c4d-5e6f7a8b9c0d',
          name: body.name,
          roles: ['professor'],
          permissions: ['autoria.ler'],
          csrfToken: 'accepted-staff-session-csrf',
        });
      }),
    );
    renderStaffInvitationAcceptance('invitation-secret');

    expect(await screen.findByText('Você recebeu o papel de professor.')).toBeInTheDocument();
    expect(window.location.search).toBe('');
    expect(window.location.hash).toBe('');
    expect(lookupToken).toBe('invitation-secret');
    expect(lookupAddressSearch).toBe('');
    expect(lookupUrl).not.toContain('invitation-secret');

    await user.type(screen.getByLabelText('Nome'), 'Marina Alves');
    await user.type(screen.getByLabelText('Senha'), 'Tr1lha!Segura');
    await user.click(screen.getByRole('button', { name: 'Aceitar convite' }));

    expect(await screen.findByText('Início do backoffice')).toBeInTheDocument();
    expect(acceptanceUrl).not.toContain('invitation-secret');
    expect(acceptanceToken).toBe('invitation-secret');
    expect(acceptedName).toBe('Marina Alves');
    expect(acceptedPassword).toBe('Tr1lha!Segura');
    expect(idempotencyKey).not.toBe('');
  });

  it('uses the same expired-link guidance for an invalid invitation', async () => {
    server.use(
      http.post(`${env.API_URL}/api/v1/staff-invitation-lookups`, () =>
        HttpResponse.json({ code: 'INVITATION_INVALID' }, { status: 422 })),
    );
    renderStaffInvitationAcceptance('expired-token');

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Este convite não vale mais. Peça um novo ao administrador.',
    );
    expect(screen.queryByLabelText('Nome')).not.toBeInTheDocument();
  });

  it('does not submit a password that violates the local policy', async () => {
    const user = userEvent.setup();
    let acceptanceRequests = 0;
    server.use(
      http.post(`${env.API_URL}/api/v1/staff-invitation-lookups`, () =>
        HttpResponse.json({ offeredRole: 'suporte', expiresAt: '2026-10-09T14:05:11Z' })),
      http.post(`${env.API_URL}/api/v1/staff-invitation-acceptances`, () => {
        acceptanceRequests += 1;
        return HttpResponse.json({});
      }),
    );
    renderStaffInvitationAcceptance('valid-token');
    await screen.findByText('Você recebeu o papel de suporte.');

    await user.type(screen.getByLabelText('Nome'), 'Marina Alves');
    await user.type(screen.getByLabelText('Senha'), 'senha-fraca');
    await user.click(screen.getByRole('button', { name: 'Aceitar convite' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Inclua uma letra maiúscula.');
    expect(acceptanceRequests).toBe(0);
  });
});
