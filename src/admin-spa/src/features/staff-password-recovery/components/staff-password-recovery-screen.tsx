import { useRef, useState } from 'react';
import { Mail } from 'lucide-react';
import { useForm } from 'react-hook-form';
import { Link } from 'react-router';

import { zodResolver } from '@hookform/resolvers/zod';

import { paths } from '@/config/paths';
import { AuthLayout } from '@/components/auth-layout';
import { CaptchaField, type CaptchaFieldHandle } from '@/components/captcha-field';
import { AuthNotice } from '@/components/auth-notice';
import { AuthResultCard } from '@/components/auth-result-card';
import { getCaptchaErrorMessage } from '@/lib/captcha';
import {
  staffPasswordRecoveryRequestSchema,
  useRequestStaffPasswordReset,
  type StaffPasswordRecoveryRequest,
} from '@/features/staff-password-recovery/api/request-staff-password-reset';

export const StaffPasswordRecoveryScreen = () => {
  const [requestError, setRequestError] = useState<string | null>(null);
  const [captchaToken, setCaptchaToken] = useState<string | null>(null);
  const captchaRef = useRef<CaptchaFieldHandle>(null);
  const requestPasswordReset = useRequestStaffPasswordReset();
  const form = useForm<StaffPasswordRecoveryRequest>({
    defaultValues: { email: '' },
    resolver: zodResolver(staffPasswordRecoveryRequestSchema),
  });

  const submitEmail = async (input: StaffPasswordRecoveryRequest) => {
    if (!captchaToken) return;
    setRequestError(null);
    try {
      await requestPasswordReset.mutateAsync({ input, captchaToken });
    } catch (error: unknown) {
      // O token é de uso único: qualquer desfecho exige um novo.
      captchaRef.current?.reset();
      setRequestError(
        getCaptchaErrorMessage(error) ?? 'Não foi possível processar o pedido agora. Tente novamente.',
      );
    }
  };

  if (requestPasswordReset.isSuccess) {
    return (
      <AuthLayout>
        <AuthResultCard
          description="Se esse e-mail for de uma conta da equipe, enviamos um link para criar uma nova senha. Ele vale por tempo limitado."
          icon={Mail}
          title="Confira seu e-mail"
          titleId="password-recovery-title"
          tone="neutral"
        >
          <Link className="auth-outline-link" to={paths.staffLogin.getHref()}>Voltar para a entrada</Link>
        </AuthResultCard>
      </AuthLayout>
    );
  }

  return (
    <AuthLayout>
      <section aria-labelledby="password-recovery-title" className="password-reset-card auth-card">
        <div className="auth-card-header">
          <p className="eyebrow">Backoffice</p>
          <h1 id="password-recovery-title">Recuperar senha</h1>
          <p>Informe o e-mail da sua conta da equipe. Se ela existir, enviamos um link para criar uma nova senha.</p>
        </div>
        {requestError ? <AuthNotice role="alert" tone="danger">{requestError}</AuthNotice> : null}
        <form noValidate onSubmit={(event) => void form.handleSubmit(submitEmail)(event)}>
          <div className="auth-field">
            <label htmlFor="email">E-mail</label>
            <input
              autoComplete="username"
              id="email"
              maxLength={254}
              placeholder="voce@escola.com"
              type="email"
              {...form.register('email')}
              aria-invalid={Boolean(form.formState.errors.email)}
              aria-describedby={form.formState.errors.email ? 'email-error' : undefined}
            />
            {form.formState.errors.email ? (
              <p id="email-error" role="alert">{form.formState.errors.email.message}</p>
            ) : null}
          </div>
          <CaptchaField onTokenChange={setCaptchaToken} ref={captchaRef} />
          <button disabled={requestPasswordReset.isPending || !captchaToken} type="submit">
            {requestPasswordReset.isPending ? 'Enviando…' : 'Receber link de recuperação'}
          </button>
        </form>
        <Link className="primary-link" to={paths.staffLogin.getHref()}>Voltar para a entrada</Link>
      </section>
    </AuthLayout>
  );
};
