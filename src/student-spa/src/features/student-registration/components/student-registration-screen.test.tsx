import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { env } from '@/config/env';
import { StudentRegistrationScreen } from '@/features/student-registration/components/student-registration-screen';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

describe('StudentRegistration', () => {
  it('StudentRegistration submits valid details and tells the student to check email', async () => {
    const user = userEvent.setup();
    let requestBody: unknown;
    let idempotencyKey: string | null = null;

    server.use(
      http.post(`${env.API_URL}/api/v1/student-accounts`, async ({ request }) => {
        requestBody = await request.json();
        idempotencyKey = request.headers.get('Idempotency-Key');
        return new HttpResponse(null, { status: 202 });
      }),
    );

    renderWithProviders(
      <MemoryRouter>
        <StudentRegistrationScreen />
      </MemoryRouter>,
    );

    await user.type(screen.getByRole('textbox', { name: 'Nome' }), 'Ana Souza');
    await user.type(screen.getByRole('textbox', { name: 'E-mail' }), 'ANA@example.com');
    await user.type(screen.getByLabelText('Senha'), 'SenhaForte1!');
    await user.click(screen.getByRole('button', { name: 'Criar conta' }));

    expect(await screen.findByRole('heading', { name: 'Confira seu e-mail' })).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Enviamos um link de confirmação');
    await waitFor(() => expect(idempotencyKey).toBeTruthy());
    expect(requestBody).toEqual({
      name: 'Ana Souza',
      email: 'ana@example.com',
      password: 'SenhaForte1!',
    });
  });
});
