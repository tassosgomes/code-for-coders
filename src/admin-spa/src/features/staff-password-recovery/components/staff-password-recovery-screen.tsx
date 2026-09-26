import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Link } from 'react-router';

import { zodResolver } from '@hookform/resolvers/zod';

import { paths } from '@/config/paths';
import {
  staffPasswordRecoveryRequestSchema,
  useRequestStaffPasswordReset,
  type StaffPasswordRecoveryRequest,
} from '@/features/staff-password-recovery/api/request-staff-password-reset';

export const StaffPasswordRecoveryScreen = () => {
  const [requestError, setRequestError] = useState<string | null>(null);
  const requestPasswordReset = useRequestStaffPasswordReset();
  const form = useForm<StaffPasswordRecoveryRequest>({
    defaultValues: { email: '' },
    resolver: zodResolver(staffPasswordRecoveryRequestSchema),
  });

  const submitEmail = async (input: StaffPasswordRecoveryRequest) => {
    setRequestError(null);
    try {
      await requestPasswordReset.mutateAsync(input);
    } catch {
      setRequestError('Não foi possível processar o pedido agora. Tente novamente.');
    }
  };

  if (requestPasswordReset.isSuccess) {
    return (
      <main className="password-reset-page">
        <section aria-labelledby="password-recovery-title" className="password-reset-card">
          <p className="eyebrow">Acesso interno</p>
          <h1 id="password-recovery-title">Confira seu e-mail</h1>
          <p role="status">Se houver uma conta interna associada a este e-mail, enviaremos as instruções para redefinir sua senha.</p>
          <Link className="primary-link" to={paths.staffLogin.getHref()}>Voltar para entrar</Link>
        </section>
      </main>
    );
  }

  return (
    <main className="password-reset-page">
      <section aria-labelledby="password-recovery-title" className="password-reset-card">
        <p className="eyebrow">Acesso interno</p>
        <h1 id="password-recovery-title">Recuperar senha</h1>
        <p>Informe o e-mail usado para acessar o backoffice.</p>
        {requestError ? <p role="alert">{requestError}</p> : null}
        <form noValidate onSubmit={(event) => void form.handleSubmit(submitEmail)(event)}>
          <label htmlFor="email">E-mail</label>
          <input
            autoComplete="username"
            id="email"
            maxLength={254}
            type="email"
            {...form.register('email')}
            aria-invalid={Boolean(form.formState.errors.email)}
            aria-describedby={form.formState.errors.email ? 'email-error' : undefined}
          />
          {form.formState.errors.email ? (
            <p id="email-error" role="alert">{form.formState.errors.email.message}</p>
          ) : null}
          <button disabled={requestPasswordReset.isPending} type="submit">
            {requestPasswordReset.isPending ? 'Enviando…' : 'Enviar instruções'}
          </button>
        </form>
        <Link className="primary-link" to={paths.staffLogin.getHref()}>Voltar para entrar</Link>
      </section>
    </main>
  );
};
