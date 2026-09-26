import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Link, useNavigate } from 'react-router';

import { zodResolver } from '@hookform/resolvers/zod';
import axios from 'axios';

import { paths } from '@/config/paths';
import {
  staffSessionLoginSchema,
  useCreateStaffSession,
  type StaffSessionLoginInput,
} from '@/features/staff-session/api/staff-session';

export const StaffLoginScreen = () => {
  const [requestError, setRequestError] = useState<string | null>(null);
  const navigate = useNavigate();
  const createSession = useCreateStaffSession();
  const form = useForm<StaffSessionLoginInput>({
    defaultValues: { email: '', password: '' },
    resolver: zodResolver(staffSessionLoginSchema),
  });

  const submitCredentials = async (input: StaffSessionLoginInput) => {
    setRequestError(null);
    try {
      await createSession.mutateAsync(input);
      await navigate(paths.home.getHref(), { replace: true });
    } catch (error: unknown) {
      setRequestError(axios.isAxiosError(error) && error.response?.status === 401
        ? 'E-mail ou senha inválidos.'
        : 'Não foi possível entrar agora. Tente novamente.');
    }
  };

  return (
    <main className="password-reset-page">
      <section aria-labelledby="staff-login-title" className="password-reset-card">
        <p className="eyebrow">Acesso interno</p>
        <h1 id="staff-login-title">Entrar no backoffice</h1>
        <p>Use sua conta interna para acessar as áreas disponíveis para você.</p>
        {requestError ? <p role="alert">{requestError}</p> : null}
        <form noValidate onSubmit={(event) => void form.handleSubmit(submitCredentials)(event)}>
          <label htmlFor="email">E-mail</label>
          <input
            autoComplete="username"
            id="email"
            type="email"
            {...form.register('email')}
            aria-invalid={Boolean(form.formState.errors.email)}
            aria-describedby={form.formState.errors.email ? 'email-error' : undefined}
          />
          {form.formState.errors.email ? (
            <p id="email-error" role="alert">Informe um e-mail válido.</p>
          ) : null}
          <label htmlFor="password">Senha</label>
          <input
            autoComplete="current-password"
            id="password"
            type="password"
            {...form.register('password')}
            aria-invalid={Boolean(form.formState.errors.password)}
            aria-describedby={form.formState.errors.password ? 'password-error' : undefined}
          />
          {form.formState.errors.password ? (
            <p id="password-error" role="alert">Informe sua senha.</p>
          ) : null}
          <button disabled={createSession.isPending} type="submit">
            {createSession.isPending ? 'Entrando…' : 'Entrar'}
          </button>
        </form>
        <Link className="primary-link" to={paths.staffPasswordRecovery.getHref()}>
          Esqueci a senha
        </Link>
      </section>
    </main>
  );
};
