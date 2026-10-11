import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { cleanup, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it } from 'vitest';

import { env } from '@/config/env';
import { paths } from '@/config/paths';
import { StaffPasswordResetScreen } from '@/features/staff-password-reset/components/staff-password-reset-screen';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

describe('StaffPasswordReset', () => {
  afterEach(() => cleanup());

  it('removes the link token from the URL and submits the new password', async () => {
    const user = userEvent.setup();
    let requestBody: unknown;
    let idempotencyKey: string | null = null;
    window.history.replaceState({}, '', `/admin${paths.staffPasswordReset.getHref()}?token=staff-secret`);
    server.use(
      http.post(`${env.API_URL}/api/v1/staff-password-resets`, async ({ request }) => {
        requestBody = await request.json();
        idempotencyKey = request.headers.get('Idempotency-Key');
        return new HttpResponse(null, { status: 204 });
      }),
    );

    const router = createMemoryRouter(
      [{ path: paths.staffPasswordReset.path, element: <StaffPasswordResetScreen /> }],
      { initialEntries: [paths.staffPasswordReset.getHref()] },
    );
    renderWithProviders(<RouterProvider router={router} />);

    await waitFor(() => expect(window.location.search).toBe(''));
    await user.type(screen.getByLabelText('Nova senha'), 'SenhaForte1!');
    await user.click(screen.getByRole('button', { name: 'Definir senha' }));

    expect(await screen.findByRole('heading', { name: 'Senha definida' })).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Pronto. Por segurança, encerramos as outras sessões da sua conta.');
    expect(requestBody).toEqual({ token: 'staff-secret', newPassword: 'SenhaForte1!' });
    expect(idempotencyKey).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Entrar com a nova senha' })).toHaveAttribute('href', paths.staffLogin.getHref());
  });

  const renderReset = (search: string) => {
    window.history.replaceState({}, '', `/admin${paths.staffPasswordReset.getHref()}${search}`);
    const router = createMemoryRouter(
      [{ path: paths.staffPasswordReset.path, element: <StaffPasswordResetScreen /> }],
      { initialEntries: [paths.staffPasswordReset.getHref()] },
    );
    renderWithProviders(<RouterProvider router={router} />);
  };

  it('shows the unavailable-link card when there is no token', () => {
    renderReset('');

    expect(screen.getByRole('heading', { name: 'Este link não vale mais' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Pedir novo link' })).toHaveAttribute('href', paths.staffPasswordRecovery.getHref());
    expect(screen.getByRole('link', { name: 'Voltar para a entrada' })).toHaveAttribute('href', paths.staffLogin.getHref());
  });

  it('shows the unavailable-link card when the API rejects the token', async () => {
    const user = userEvent.setup();
    server.use(
      http.post(`${env.API_URL}/api/v1/staff-password-resets`, () =>
        HttpResponse.json({ code: 'RESET_TOKEN_INVALID' }, { status: 422 })),
    );
    renderReset('?token=old');

    await user.type(screen.getByLabelText('Nova senha'), 'SenhaForte1!');
    await user.click(screen.getByRole('button', { name: 'Definir senha' }));

    expect(await screen.findByRole('heading', { name: 'Este link não vale mais' })).toBeInTheDocument();
  });

  it('keeps the form and shows an error box on other failures', async () => {
    const user = userEvent.setup();
    server.use(
      http.post(`${env.API_URL}/api/v1/staff-password-resets`, () => new HttpResponse(null, { status: 500 })),
    );
    renderReset('?token=ok');

    await user.type(screen.getByLabelText('Nova senha'), 'SenhaForte1!');
    await user.click(screen.getByRole('button', { name: 'Definir senha' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível salvar agora. Tente novamente.');
    expect(screen.getByRole('button', { name: 'Definir senha' })).toBeInTheDocument();
  });
});
