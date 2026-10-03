import { useState } from 'react';
import { CircleAlert, Eye, EyeOff } from 'lucide-react';
import { useForm } from 'react-hook-form';
import { Link, useNavigate } from 'react-router';

import { zodResolver } from '@hookform/resolvers/zod';
import axios from 'axios';

import { paths } from '@/config/paths';
import { AuthLayout } from '@/components/auth-layout';
import { activeStaffSessionMarker, expiredStaffSessionMarker } from '@/config/session-markers';
import {
  staffSessionLoginSchema,
  useCreateStaffSession,
  type StaffSessionLoginInput,
} from '@/features/staff-session/api/staff-session';

export const StaffLoginScreen = () => {
  const [requestError, setRequestError] = useState<'credentials' | 'generic' | null>(null);
  const [showSessionExpired, setShowSessionExpired] = useState(
    () => window.sessionStorage.getItem(expiredStaffSessionMarker) === 'true',
  );
  const [showPassword, setShowPassword] = useState(false);
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
      window.sessionStorage.setItem(activeStaffSessionMarker, 'true');
      window.sessionStorage.removeItem(expiredStaffSessionMarker);
      setShowSessionExpired(false);
      await navigate(paths.home.getHref(), { replace: true });
    } catch (error: unknown) {
      setRequestError(axios.isAxiosError(error) && error.response?.status === 401 ? 'credentials' : 'generic');
    }
  };

  return (
    <AuthLayout>
      <section aria-labelledby="staff-login-title" className="password-reset-card">
        <p className="eyebrow">Backoffice</p>
        <h1 id="staff-login-title">Entrar na operação</h1>
        <p>Acesso da equipe da escola. Alunos entram por code4coders.com.br/entrar.</p>
        {showSessionExpired ? (
          <div className="login-request-error" role="status">
            <CircleAlert aria-hidden="true" size={16} />
            <div className="login-request-error-content">
              <p className="login-request-error-title">Sessão Expirada</p>
              <p>Entre de novo para continuar.</p>
            </div>
          </div>
        ) : null}
        {requestError ? (
          <div className="login-request-error" role="alert">
            <CircleAlert aria-hidden="true" size={16} />
            <div className="login-request-error-content">
              {requestError === 'credentials' ? (
                <>
                  <p className="login-request-error-title">E-mail ou senha incorretos</p>
                  <p>Confira os dados e tente de novo.</p>
                </>
              ) : (
                <p>Não foi possível entrar agora. Tente novamente.</p>
              )}
            </div>
          </div>
        ) : null}
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
          <div className="field-heading"><label htmlFor="password">Senha</label><Link to={paths.staffPasswordRecovery.getHref()}>Esqueceu a senha?</Link></div>
          <div className="password-input"><input
            autoComplete="current-password"
            id="password"
            type={showPassword ? 'text' : 'password'}
            {...form.register('password')}
            aria-invalid={Boolean(form.formState.errors.password)}
            aria-describedby={form.formState.errors.password ? 'password-error' : undefined}
          /><button aria-label={showPassword ? 'Ocultar senha' : 'Mostrar senha'} className="visibility-button" onClick={() => setShowPassword(!showPassword)} type="button">{showPassword ? <EyeOff size={16} /> : <Eye size={16} />}</button></div>
          {form.formState.errors.password ? (
            <p id="password-error" role="alert">Informe sua senha.</p>
          ) : null}
          <button disabled={createSession.isPending} type="submit">
            {createSession.isPending ? 'Entrando…' : 'Entrar'}
          </button>
        </form>
        <p className="auth-footnote">Sem conta? O acesso é por convite de um administrador.</p>
      </section>
    </AuthLayout>
  );
};
