import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { env } from '@/config/env';
import { StudentConfirmationScreen } from '@/features/student-confirmation/components/student-confirmation-screen';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const LocationProbe = () => {
  const location = useLocation();
  return <p data-testid="current-url">{`${location.pathname}${location.search}`}</p>;
};

const renderConfirmationRoute = (entry: string) =>
  renderWithProviders(
    <MemoryRouter initialEntries={[entry]}>
      <Routes>
        <Route
          element={
            <>
              <StudentConfirmationScreen />
              <LocationProbe />
            </>
          }
          path="/confirm-account"
        />
      </Routes>
    </MemoryRouter>,
  );

describe('StudentConfirmation', () => {
  it('StudentConfirmation submits the token then replaces the URL with one without the token', async () => {
    let requestBody: unknown;
    let idempotencyKey: string | null = null;

    server.use(
      http.post(`${env.API_URL}/api/v1/account-confirmations`, async ({ request }) => {
        requestBody = await request.json();
        idempotencyKey = request.headers.get('Idempotency-Key');
        return new HttpResponse(null, { status: 204 });
      }),
    );

    renderConfirmationRoute('/confirm-account?token=one-time-secret');

    expect(await screen.findByRole('heading', { name: 'Conta confirmada' })).toBeInTheDocument();
    await waitFor(() => expect(screen.getByTestId('current-url')).toHaveTextContent('/confirm-account'));
    expect(screen.getByTestId('current-url')).not.toHaveTextContent('one-time-secret');
    expect(requestBody).toEqual({ token: 'one-time-secret' });
    expect(idempotencyKey).toBeTruthy();
  });

  it('StudentConfirmation offers a neutral resend after an invalid link', async () => {
    const user = userEvent.setup();
    let requestBody: unknown;
    let idempotencyKey: string | null = null;

    server.use(
      http.post(`${env.API_URL}/api/v1/account-confirmations`, () =>
        HttpResponse.json({
          type: 'about:blank',
          title: 'The confirmation link is invalid or expired.',
          status: 422,
          code: 'CONFIRMATION_LINK_INVALID',
          traceId: 'test-trace',
        }, { status: 422 })),
      http.post(`${env.API_URL}/api/v1/account-confirmation-requests`, async ({ request }) => {
        requestBody = await request.json();
        idempotencyKey = request.headers.get('Idempotency-Key');
        return new HttpResponse(null, { status: 202 });
      }),
    );

    renderConfirmationRoute('/confirm-account?token=expired-token');

    expect(await screen.findByRole('heading', { name: 'Link de confirmação indisponível' })).toBeInTheDocument();
    await user.type(screen.getByRole('textbox', { name: 'E-mail' }), 'ANA@example.com');
    await user.click(screen.getByRole('button', { name: 'Enviar novo link' }));

    expect(await screen.findByRole('heading', { name: 'Verifique seu e-mail' })).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Se houver uma conta pendente para esse e-mail');
    expect(requestBody).toEqual({ email: 'ana@example.com' });
    expect(idempotencyKey).toBeTruthy();
    expect(screen.getByTestId('current-url')).toHaveTextContent('/confirm-account');
  });
});
