import { http, HttpResponse } from 'msw';
import { RouterProvider, createMemoryRouter } from 'react-router';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';

import { StaffLoginScreen } from '@/features/staff-session/components/staff-login-screen';
import { env } from '@/config/env';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

describe('StaffSession login', () => {
  it('shows one generic error when Identity rejects the credentials', async () => {
    server.use(http.post(`${env.API_URL}/api/v1/staff-sessions`, () =>
      HttpResponse.json({ code: 'INVALID_CREDENTIALS' }, { status: 401 }),
    ));
    const router = createMemoryRouter([
      { path: '/entrar', element: <StaffLoginScreen /> },
    ], { initialEntries: ['/entrar'] });

    renderWithProviders(<RouterProvider router={router} />);
    const user = userEvent.setup();
    await user.type(screen.getByLabelText('E-mail'), 'student@example.com');
    await user.type(screen.getByLabelText('Senha'), 'SenhaForte1!');
    await user.click(screen.getByRole('button', { name: 'Entrar' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('E-mail ou senha inválidos.');
  });
});
