import { useLayoutEffect, useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { Link } from 'react-router';
import { CircleAlert, CircleCheck, Eye, EyeOff } from 'lucide-react';
import axios from 'axios';

import { zodResolver } from '@hookform/resolvers/zod';

import { paths } from '@/config/paths';
import { AuthLayout } from '@/components/auth-layout';
import { AuthNotice } from '@/components/auth-notice';
import { AuthResultCard } from '@/components/auth-result-card';
import { PasswordRequirements } from '@/components/password-requirements';
import {
  useResetStaffPassword,
  staffPasswordResetFormSchema,
  type StaffPasswordResetFormInput,
} from '@/features/staff-password-reset/api/reset-staff-password';

export const StaffPasswordResetScreen = () => {
  const [token] = useState(() => new URLSearchParams(window.location.search).get('token'));
  const [requestError, setRequestError] = useState<string | null>(null);
  const [linkInvalid, setLinkInvalid] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const resetPassword = useResetStaffPassword();
  const form = useForm<StaffPasswordResetFormInput>({
    defaultValues: { newPassword: '' },
    resolver: zodResolver(staffPasswordResetFormSchema),
  });
  const password = useWatch({ control: form.control, name: 'newPassword', defaultValue: '' });

  useLayoutEffect(() => {
    const currentUrl = new URL(window.location.href);
    if (currentUrl.searchParams.has('token')) {
      currentUrl.search = '';
      currentUrl.hash = '';
      window.history.replaceState(window.history.state, '', currentUrl.toString());
    }
  }, []);

  const submitPassword = async (input: StaffPasswordResetFormInput) => {
    if (!token) {
      setLinkInvalid(true);
      return;
    }

    setRequestError(null);
    try {
      await resetPassword.mutateAsync({ token, input });
    } catch (error: unknown) {
      if (axios.isAxiosError<{ code?: unknown }>(error) && error.response?.data?.code === 'RESET_TOKEN_INVALID') {
        setLinkInvalid(true);
        return;
      }
      setRequestError('Não foi possível salvar agora. Tente novamente.');
    }
  };

  if (resetPassword.isSuccess) {
    return (
      <AuthLayout>
        <AuthResultCard
          description="Pronto. Por segurança, encerramos as outras sessões da sua conta."
          icon={CircleCheck}
          title="Senha definida"
          titleId="password-reset-title"
          tone="success"
        >
          <Link className="auth-solid-link" to={paths.staffLogin.getHref()}>Entrar com a nova senha</Link>
        </AuthResultCard>
      </AuthLayout>
    );
  }

  if (!token || linkInvalid) {
    return (
      <AuthLayout>
        <AuthResultCard
          alert
          description="Ele expirou ou já foi usado. Peça um novo link na recuperação de senha do backoffice."
          icon={CircleAlert}
          title="Este link não vale mais"
          titleId="password-reset-title"
          tone="warning"
        >
          <Link className="auth-solid-link" to={paths.staffPasswordRecovery.getHref()}>Pedir novo link</Link>
          <Link className="primary-link" to={paths.staffLogin.getHref()}>Voltar para a entrada</Link>
        </AuthResultCard>
      </AuthLayout>
    );
  }

  return (
    <AuthLayout>
      <section aria-labelledby="password-reset-title" className="password-reset-card auth-card">
        <div className="auth-card-header">
          <p className="eyebrow">Backoffice</p>
          <h1 id="password-reset-title">Defina sua senha</h1>
          <p>Escolha uma senha que você ainda não usa nesta conta.</p>
        </div>
        {requestError ? <AuthNotice role="alert" title="Falha ao salvar" tone="danger">{requestError}</AuthNotice> : null}
        <form noValidate onSubmit={(event) => void form.handleSubmit(submitPassword)(event)}>
          <div className="auth-field">
            <label htmlFor="newPassword">Nova senha</label>
            <div className="password-input"><input
              autoComplete="new-password"
              id="newPassword"
              type={showPassword ? 'text' : 'password'}
              {...form.register('newPassword')}
              aria-invalid={Boolean(form.formState.errors.newPassword)}
              aria-describedby={form.formState.errors.newPassword ? 'newPassword-error' : undefined}
            /><button aria-label={showPassword ? 'Ocultar senha' : 'Mostrar senha'} className="visibility-button" onClick={() => setShowPassword(!showPassword)} type="button">{showPassword ? <EyeOff size={16} /> : <Eye size={16} />}</button></div>
            <PasswordRequirements password={password} />
            {form.formState.errors.newPassword ? (
              <p id="newPassword-error" role="alert">{form.formState.errors.newPassword.message}</p>
            ) : null}
          </div>
          <button disabled={resetPassword.isPending} type="submit">
            {resetPassword.isPending ? 'Salvando senha…' : 'Definir senha'}
          </button>
        </form>
      </section>
    </AuthLayout>
  );
};
