import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';

import { env } from '@/config/env';
import { paths } from '@/config/paths';
import { StaffPasswordRecoveryScreen } from '@/features/staff-password-recovery/components/staff-password-recovery-screen';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

describe('StaffPasswordRecovery', () => {
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
    await user.click(screen.getByRole('button', { name: 'Enviar instruções' }));

    expect(await screen.findByRole('status')).toHaveTextContent(
      'Se houver uma conta interna associada a este e-mail, enviaremos as instruções para redefinir sua senha.',
    );
    expect(requestBody).toEqual({ email: 'marina@example.com' });
    expect(idempotencyKey).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Voltar para entrar' })).toHaveAttribute('href', paths.staffLogin.getHref());
  });

});
