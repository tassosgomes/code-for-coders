import { useEffect, useRef, useState } from 'react';
import { Eye, EyeOff } from 'lucide-react';
import { useForm } from 'react-hook-form';
import { Link, useNavigate } from 'react-router';

import { zodResolver } from '@hookform/resolvers/zod';
import axios from 'axios';

import { paths } from '@/config/paths';
import { AuthLayout } from '@/components/auth-layout';
import { CaptchaField, type CaptchaFieldHandle } from '@/components/captcha-field';
import { AuthNotice } from '@/components/auth-notice';
import { Toast } from '@/components/toast';
import { getCaptchaErrorMessage } from '@/lib/captcha';
import { activeStaffSessionMarker, expiredStaffSessionMarker, loggedOutStaffSessionMarker } from '@/config/session-markers';
import {
  staffSessionLoginSchema,
  useCreateStaffSession,
  type StaffSessionLoginInput,
} from '@/features/staff-session/api/staff-session';

const readLoggedOutMarker = () => {
  try {
    return window.sessionStorage.getItem(loggedOutStaffSessionMarker) === 'true';
  } catch {
    return false;
  }
};

export const StaffLoginScreen = () => {
  const [requestError, setRequestError] = useState<'credentials' | 'generic' | null>(null);
  const [captchaMessage, setCaptchaMessage] = useState<string | null>(null);
  const [captchaToken, setCaptchaToken] = useState<string | null>(null);
  const captchaRef = useRef<CaptchaFieldHandle>(null);
  const [showSessionExpired, setShowSessionExpired] = useState(
    () => window.sessionStorage.getItem(expiredStaffSessionMarker) === 'true',
  );
  const [showLoggedOut, setShowLoggedOut] = useState(readLoggedOutMarker);
  const [showPassword, setShowPassword] = useState(false);
  const navigate = useNavigate();
  const createSession = useCreateStaffSession();
  const form = useForm<StaffSessionLoginInput>({
    defaultValues: { email: '', password: '' },
    resolver: zodResolver(staffSessionLoginSchema),
  });

  useEffect(() => {
    try {
      window.sessionStorage.removeItem(loggedOutStaffSessionMarker);
    } catch {
      // Sem sessionStorage, o aviso apenas não persiste.
    }
  }, []);

  useEffect(() => {
    if (!showLoggedOut) return undefined;
    const timer = window.setTimeout(() => setShowLoggedOut(false), 6000);
    return () => window.clearTimeout(timer);
  }, [showLoggedOut]);

  const submitCredentials = async (input: StaffSessionLoginInput) => {
    if (!captchaToken) return;
    setRequestError(null);
    setCaptchaMessage(null);
    try {
      await createSession.mutateAsync({ input, captchaToken });
      window.sessionStorage.setItem(activeStaffSessionMarker, 'true');
      window.sessionStorage.removeItem(expiredStaffSessionMarker);
      setShowSessionExpired(false);
      await navigate(paths.home.getHref(), { replace: true });
    } catch (error: unknown) {
      // O token é de uso único: qualquer desfecho exige um novo.
      captchaRef.current?.reset();
      const message = getCaptchaErrorMessage(error);
      if (message) {
        setCaptchaMessage(message);
        return;
      }
      setRequestError(axios.isAxiosError(error) && error.response?.status === 401 ? 'credentials' : 'generic');
    }
  };

  return (
    <AuthLayout>
      <section aria-labelledby="staff-login-title" className="password-reset-card auth-card">
        <div className="auth-card-header">
          <p className="eyebrow">Backoffice</p>
          <h1 id="staff-login-title">Entrar na operação</h1>
          <p>Acesso da equipe da escola. Alunos entram por code4coders.com.br/entrar.</p>
        </div>
        {showSessionExpired ? (
          <AuthNotice role="status" title="Sua sessão foi encerrada" tone="neutral">Entre de novo para continuar.</AuthNotice>
        ) : null}
        {requestError === 'credentials' ? (
          <AuthNotice role="alert" title="E-mail ou senha incorretos" tone="danger">Confira os dados e tente de novo.</AuthNotice>
        ) : null}
        {requestError === 'generic' ? (
          <AuthNotice role="alert" tone="danger">Não foi possível entrar agora. Tente novamente.</AuthNotice>
        ) : null}
        {captchaMessage ? <AuthNotice role="alert" tone="danger">{captchaMessage}</AuthNotice> : null}
        <form noValidate onSubmit={(event) => void form.handleSubmit(submitCredentials)(event)}>
          <div className="auth-field">
            <label htmlFor="email">E-mail</label>
            <input
              autoComplete="username"
              id="email"
              placeholder="voce@escola.com"
              type="email"
              {...form.register('email')}
              aria-invalid={Boolean(form.formState.errors.email)}
              aria-describedby={form.formState.errors.email ? 'email-error' : undefined}
            />
            {form.formState.errors.email ? (
              <p id="email-error" role="alert">Informe um e-mail válido.</p>
            ) : null}
          </div>
          <div className="auth-field">
            <div className="field-heading"><label htmlFor="password">Senha</label><Link to={paths.staffPasswordRecovery.getHref()}>Esqueceu a senha?</Link></div>
            <div className="password-input"><input
              autoComplete="current-password"
              id="password"
              placeholder="Sua senha"
              type={showPassword ? 'text' : 'password'}
              {...form.register('password')}
              aria-invalid={Boolean(form.formState.errors.password)}
              aria-describedby={form.formState.errors.password ? 'password-error' : undefined}
            /><button aria-label={showPassword ? 'Ocultar senha' : 'Mostrar senha'} className="visibility-button" onClick={() => setShowPassword(!showPassword)} type="button">{showPassword ? <EyeOff size={16} /> : <Eye size={16} />}</button></div>
            {form.formState.errors.password ? (
              <p id="password-error" role="alert">Informe sua senha.</p>
            ) : null}
          </div>
          <CaptchaField onTokenChange={setCaptchaToken} ref={captchaRef} />
          <button disabled={createSession.isPending || !captchaToken} type="submit">
            {createSession.isPending ? 'Entrando…' : 'Entrar'}
          </button>
        </form>
        <p className="auth-footnote">Sem conta? O acesso é por convite de um administrador.</p>
      </section>
      {showLoggedOut ? <Toast message="Você saiu do backoffice." /> : null}
    </AuthLayout>
  );
};
