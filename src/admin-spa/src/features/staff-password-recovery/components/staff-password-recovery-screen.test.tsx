import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { cleanup, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it } from 'vitest';

import { env } from '@/config/env';
import { paths } from '@/config/paths';
import { StaffPasswordRecoveryScreen } from '@/features/staff-password-recovery/components/staff-password-recovery-screen';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

describe('StaffPasswordRecovery', () => {
  afterEach(() => cleanup());

  it('submits the email and shows the same confirmation message', async () => {
    const user = userEvent.setup();
    let requestBody: unknown;
    let idempotencyKey: string | null = null;
    server.use(
      http.post(`${env.API_URL}/api/v1/staff-password-reset-requests`, async ({ request }) => {
        requestBody = await request.json();
        idempotencyKey = request.headers.get('Idempotency-Key');
        return new HttpResponse(null, { status: 202 });
      }),
    );

    const router = createMemoryRouter(
      [{ path: paths.staffPasswordRecovery.path, element: <StaffPasswordRecoveryScreen /> }],
      { initialEntries: [paths.staffPasswordRecovery.getHref()] },
    );
    renderWithProviders(<RouterProvider router={router} />);

    await user.type(screen.getByLabelText('E-mail'), 'marina@example.com');
    await user.click(screen.getByRole('button', { name: 'Receber link de recuperação' }));

    expect(await screen.findByRole('heading', { name: 'Confira seu e-mail' })).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent(
      'Se esse e-mail for de uma conta da equipe, enviamos um link para criar uma nova senha. Ele vale por tempo limitado.',
    );
    expect(requestBody).toEqual({ email: 'marina@example.com' });
    expect(idempotencyKey).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Voltar para a entrada' })).toHaveAttribute('href', paths.staffLogin.getHref());
  });

  it('shows the error box when the request fails', async () => {
    const user = userEvent.setup();
    server.use(
      http.post(`${env.API_URL}/api/v1/staff-password-reset-requests`, () => new HttpResponse(null, { status: 500 })),
    );
    const router = createMemoryRouter(
      [{ path: paths.staffPasswordRecovery.path, element: <StaffPasswordRecoveryScreen /> }],
      { initialEntries: [paths.staffPasswordRecovery.getHref()] },
    );
    renderWithProviders(<RouterProvider router={router} />);

    expect(screen.getByPlaceholderText('voce@escola.com')).toBeInTheDocument();
    await user.type(screen.getByLabelText('E-mail'), 'marina@example.com');
    await user.click(screen.getByRole('button', { name: 'Receber link de recuperação' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível processar o pedido agora. Tente novamente.');
    expect(screen.getByRole('link', { name: 'Voltar para a entrada' })).toBeInTheDocument();
  });
});
