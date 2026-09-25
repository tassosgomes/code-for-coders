import { useLayoutEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { Link } from 'react-router';

import { zodResolver } from '@hookform/resolvers/zod';

import { paths } from '@/config/paths';
import {
  useResetStaffPassword,
  staffPasswordResetFormSchema,
  type StaffPasswordResetFormInput,
} from '@/features/staff-password-reset/api/reset-staff-password';

export const StaffPasswordResetScreen = () => {
  const [token] = useState(() => new URLSearchParams(window.location.search).get('token'));
  const [requestError, setRequestError] = useState<string | null>(null);
  const resetPassword = useResetStaffPassword();
  const form = useForm<StaffPasswordResetFormInput>({
    defaultValues: { newPassword: '' },
    resolver: zodResolver(staffPasswordResetFormSchema),
  });

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
      <main className="password-reset-page">
        <section aria-labelledby="password-reset-title" className="password-reset-card">
          <p className="eyebrow">Acesso interno</p>
          <h1 id="password-reset-title">Senha definida</h1>
          <p role="status">Sua senha foi definida. Agora você pode entrar no backoffice.</p>
          <Link className="primary-link" to={paths.staffLogin.getHref()}>Ir para entrar</Link>
        </section>
      </main>
    );
  }

  return (
    <main className="password-reset-page">
      <section aria-labelledby="password-reset-title" className="password-reset-card">
        <p className="eyebrow">Acesso interno</p>
        <h1 id="password-reset-title">Defina sua senha</h1>
        <p>Escolha uma senha para acessar o backoffice.</p>
        {!token ? <p role="alert">Este link de redefinição não é válido. Peça um novo link.</p> : null}
        {requestError ? <p role="alert">{requestError}</p> : null}
        {token ? (
          <form noValidate onSubmit={(event) => void form.handleSubmit(submitPassword)(event)}>
            <label htmlFor="newPassword">Nova senha</label>
            <input
              autoComplete="new-password"
              id="newPassword"
              type="password"
              {...form.register('newPassword')}
              aria-invalid={Boolean(form.formState.errors.newPassword)}
              aria-describedby={form.formState.errors.newPassword ? 'newPassword-error' : undefined}
            />
            {form.formState.errors.newPassword ? (
              <p id="newPassword-error" role="alert">{form.formState.errors.newPassword.message}</p>
            ) : null}
            <p className="password-reset-hint">
              Use 8 caracteres ou mais, com letra maiúscula, minúscula, número e símbolo.
            </p>
            <button disabled={resetPassword.isPending} type="submit">
              {resetPassword.isPending ? 'Salvando senha…' : 'Definir senha'}
            </button>
          </form>
        ) : null}
      </section>
    </main>
  );
};
