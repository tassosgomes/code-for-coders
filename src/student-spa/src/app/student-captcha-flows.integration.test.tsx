import { act, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';

import { env } from '@/config/env';
import { StudentLoginScreen } from '@/features/student-session/components/student-login-screen';
import { StudentPasswordRecoveryScreen } from '@/features/student-password-recovery/components/student-password-recovery-screen';
import { StudentRegistrationScreen } from '@/features/student-registration/components/student-registration-screen';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';
import { turnstileMock } from '@/testing/turnstile-mock';

const problem = (status: number, code: string, headers: Record<string, string> = {}) =>
  HttpResponse.json(
    { type: 'about:blank', title: code, status, code, traceId: 'trace' },
    { status, headers: { 'Content-Type': 'application/problem+json', ...headers } },
  );

type Screen = {
  name: string;
  path: string;
  button: string;
  render: () => void;
  fill: (user: ReturnType<typeof userEvent.setup>) => Promise<void>;
};

const screens: Screen[] = [
  {
    name: 'login',
    path: '/api/v1/student-sessions',
    button: 'Entrar',
    render: () => renderWithProviders(<MemoryRouter><StudentLoginScreen /></MemoryRouter>),
    fill: async (user) => {
      await user.type(screen.getByRole('textbox', { name: 'E-mail' }), 'ana@example.com');
      await user.type(screen.getByLabelText('Senha'), 'SenhaForte1!');
    },
  },
  {
    name: 'registration',
    path: '/api/v1/student-accounts',
    button: 'Criar conta',
    render: () => renderWithProviders(<MemoryRouter><StudentRegistrationScreen /></MemoryRouter>),
    fill: async (user) => {
      await user.type(screen.getByRole('textbox', { name: 'Nome' }), 'Ana Souza');
      await user.type(screen.getByRole('textbox', { name: 'E-mail' }), 'ana@example.com');
      await user.type(screen.getByLabelText('Senha'), 'SenhaForte1!');
    },
  },
  {
    name: 'password recovery request',
    path: '/api/v1/password-reset-requests',
    button: 'Receber link de recuperação',
    render: () => renderWithProviders(
      <MemoryRouter><StudentPasswordRecoveryScreen mode="request" /></MemoryRouter>,
    ),
    fill: async (user) => {
      await user.type(screen.getByRole('textbox', { name: 'E-mail' }), 'ana@example.com');
    },
  },
];

describe.each(screens)('captcha on $name', ({ path, button, render, fill }) => {
  it('keeps the submit button disabled until the widget produces a token', async () => {
    turnstileMock.autoSolve = false;
    render();

    expect(screen.getByRole('button', { name: button })).toBeDisabled();

    act(() => turnstileMock.solve());

    await waitFor(() => expect(screen.getByRole('button', { name: button })).toBeEnabled());
  });

  it('disables the button again when the token expires', async () => {
    render();
    await waitFor(() => expect(screen.getByRole('button', { name: button })).toBeEnabled());

    act(() => turnstileMock.expire());

    expect(screen.getByRole('button', { name: button })).toBeDisabled();
  });

  it('sends the token in X-Captcha-Token and restarts the widget after the submission', async () => {
    const user = userEvent.setup();
    let captchaHeader: string | null = null;
    server.use(
      http.post(`${env.API_URL}${path}`, ({ request }) => {
        captchaHeader = request.headers.get('X-Captcha-Token');
        return problem(500, 'UNEXPECTED');
      }),
    );
    turnstileMock.token = 'token-from-widget';
    render();
    await waitFor(() => expect(screen.getByRole('button', { name: button })).toBeEnabled());

    await fill(user);
    await user.click(screen.getByRole('button', { name: button }));

    await waitFor(() => expect(captchaHeader).toBe('token-from-widget'));
    await waitFor(() => expect(turnstileMock.resetCount).toBe(1));
    await waitFor(() => expect(screen.getByRole('button', { name: button })).toBeEnabled());
  });

  it('shows the Portuguese message for a rejected captcha (403) and restarts the widget', async () => {
    const user = userEvent.setup();
    server.use(http.post(`${env.API_URL}${path}`, () => problem(403, 'CAPTCHA_INVALID')));
    render();
    await waitFor(() => expect(screen.getByRole('button', { name: button })).toBeEnabled());

    await fill(user);
    await user.click(screen.getByRole('button', { name: button }));

    expect(await screen.findByText('Não conseguimos confirmar que você não é um robô. Tente de novo.'))
      .toBeInTheDocument();
    expect(turnstileMock.resetCount).toBe(1);
  });

  it('shows the wait time from Retry-After on a rate limit (429)', async () => {
    const user = userEvent.setup();
    server.use(
      http.post(`${env.API_URL}${path}`, () => problem(429, 'RATE_LIMITED', { 'Retry-After': '30' })),
    );
    render();
    await waitFor(() => expect(screen.getByRole('button', { name: button })).toBeEnabled());

    await fill(user);
    await user.click(screen.getByRole('button', { name: button }));

    expect(await screen.findByText('Muitas tentativas. Aguarde 30 segundos e tente de novo.'))
      .toBeInTheDocument();
  });

  it('tells the student the verification is unavailable (503)', async () => {
    const user = userEvent.setup();
    server.use(http.post(`${env.API_URL}${path}`, () => problem(503, 'CAPTCHA_UNAVAILABLE')));
    render();
    await waitFor(() => expect(screen.getByRole('button', { name: button })).toBeEnabled());

    await fill(user);
    await user.click(screen.getByRole('button', { name: button }));

    expect(await screen.findByText(/verificação de segurança está indisponível/)).toBeInTheDocument();
  });

  it('shows the message for a missing captcha (400)', async () => {
    const user = userEvent.setup();
    server.use(http.post(`${env.API_URL}${path}`, () => problem(400, 'CAPTCHA_REQUIRED')));
    render();
    await waitFor(() => expect(screen.getByRole('button', { name: button })).toBeEnabled());

    await fill(user);
    await user.click(screen.getByRole('button', { name: button }));

    expect(await screen.findByText('Confirme que você não é um robô para continuar.')).toBeInTheDocument();
  });
});

describe('captcha widget loading', () => {
  it('uses the configured sitekey and warns when the widget fails to load', async () => {
    renderWithProviders(<MemoryRouter><StudentLoginScreen /></MemoryRouter>);
    await waitFor(() => expect(turnstileMock.siteKey).toBe(env.TURNSTILE_SITE_KEY));

    act(() => turnstileMock.fail());

    expect(screen.getByRole('alert')).toHaveTextContent('Não foi possível carregar a verificação de segurança');
    expect(screen.getByRole('button', { name: 'Entrar' })).toBeDisabled();
  });
});
