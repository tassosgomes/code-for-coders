import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';

import { env } from '@/config/env';
import { paths } from '@/config/paths';
import { StaffPasswordResetScreen } from '@/features/staff-password-reset/components/staff-password-reset-screen';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

describe('StaffPasswordReset', () => {
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

    expect(await screen.findByRole('status')).toHaveTextContent('Sua senha foi definida.');
    expect(requestBody).toEqual({ token: 'staff-secret', newPassword: 'SenhaForte1!' });
    expect(idempotencyKey).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Ir para entrar' })).toHaveAttribute('href', paths.staffLogin.getHref());
  });
});
