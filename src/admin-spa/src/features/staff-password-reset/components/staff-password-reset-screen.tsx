import { useLayoutEffect, useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { Link } from 'react-router';
import { Eye, EyeOff } from 'lucide-react';

import { zodResolver } from '@hookform/resolvers/zod';

import { paths } from '@/config/paths';
import { AuthLayout } from '@/components/auth-layout';
import { PasswordRequirements } from '@/components/password-requirements';
import {
  useResetStaffPassword,
  staffPasswordResetFormSchema,
  type StaffPasswordResetFormInput,
} from '@/features/staff-password-reset/api/reset-staff-password';

export const StaffPasswordResetScreen = () => {
  const [token] = useState(() => new URLSearchParams(window.location.search).get('token'));
  const [requestError, setRequestError] = useState<string | null>(null);
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
      setRequestError('Este link de redefinição não é válido. Peça um novo link.');
      return;
    }

    setRequestError(null);
    try {
      await resetPassword.mutateAsync({ token, input });
    } catch {
      setRequestError('Não foi possível redefinir a senha. Solicite um novo link e tente novamente.');
    }
  };

  if (resetPassword.isSuccess) {
    return (
      <AuthLayout>
        <section aria-labelledby="password-reset-title" className="password-reset-card">
          <p className="eyebrow">Backoffice</p>
          <h1 id="password-reset-title">Senha definida</h1>
          <p role="status">Pronto. Por segurança, encerramos as outras sessões da sua conta.</p>
          <Link className="primary-link" to={paths.staffLogin.getHref()}>Ir para entrar</Link>
        </section>
      </AuthLayout>
    );
  }

  return (
    <AuthLayout>
      <section aria-labelledby="password-reset-title" className="password-reset-card">
        <p className="eyebrow">Backoffice</p>
        <h1 id="password-reset-title">Defina sua senha</h1>
        <p>Escolha uma senha que você ainda não usa nesta conta.</p>
        {!token ? <p role="alert">Este link de redefinição não é válido. Peça um novo link.</p> : null}
        {requestError ? <p role="alert">{requestError}</p> : null}
        {token ? (
          <form noValidate onSubmit={(event) => void form.handleSubmit(submitPassword)(event)}>
            <label htmlFor="newPassword">Nova senha</label>
            <div className="password-input"><input
              autoComplete="new-password"
              id="newPassword"
              type={showPassword ? 'text' : 'password'}
              {...form.register('newPassword')}
              aria-invalid={Boolean(form.formState.errors.newPassword)}
              aria-describedby={form.formState.errors.newPassword ? 'newPassword-error' : undefined}
            /><button aria-label={showPassword ? 'Ocultar senha' : 'Mostrar senha'} className="visibility-button" onClick={() => setShowPassword(!showPassword)} type="button">{showPassword ? <EyeOff size={16} /> : <Eye size={16} />}</button></div>
            {form.formState.errors.newPassword ? (
              <p id="newPassword-error" role="alert">{form.formState.errors.newPassword.message}</p>
            ) : null}
            <PasswordRequirements password={password} />
            <button disabled={resetPassword.isPending} type="submit">
              {resetPassword.isPending ? 'Salvando senha…' : 'Definir senha'}
            </button>
          </form>
        ) : null}
      </section>
    </AuthLayout>
  );
};
