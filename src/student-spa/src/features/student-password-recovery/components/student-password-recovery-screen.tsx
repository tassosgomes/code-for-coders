import { useEffect, useRef, useState } from 'react';
import { FormProvider } from 'react-hook-form';
import { Link, useLocation, useNavigate } from 'react-router';
import axios from 'axios';
import * as z from 'zod';

import { FormTextField } from '@/components/ui/form';
import { paths } from '@/config/paths';
import {
  useRequestStudentPasswordReset,
  type StudentPasswordResetRequestInput,
} from '@/features/student-password-recovery/api/request-student-password-reset';
import {
  useResetStudentPassword,
  type StudentPasswordResetFormInput,
} from '@/features/student-password-recovery/api/reset-student-password';
import { useStudentPasswordResetForm } from '@/features/student-password-recovery/hooks/use-student-password-reset-form';
import { useStudentPasswordResetRequestForm } from '@/features/student-password-recovery/hooks/use-student-password-reset-request-form';
import { useDocumentTitle } from '@/hooks/use-document-title';

const problemSchema = z.object({ code: z.string().optional() }).passthrough();

const getErrorCode = (error: unknown) => {
  const body = axios.isAxiosError(error) ? error.response?.data : undefined;
  const problem = problemSchema.safeParse(body);
  return problem.success ? problem.data.code : undefined;
};

type StudentPasswordRecoveryScreenProps = { mode: 'request' | 'reset' };
type RecoveryState = 'request-form' | 'request-sent' | 'reset-form' | 'reset-done' | 'reset-rejected' | 'reset-error';

export const StudentPasswordRecoveryScreen = ({ mode }: StudentPasswordRecoveryScreenProps) => {
  const location = useLocation();
  const navigate = useNavigate();
  const requestForm = useStudentPasswordResetRequestForm();
  const resetForm = useStudentPasswordResetForm();
  const request = useRequestStudentPasswordReset();
  const reset = useResetStudentPassword();
  const initialTokenRef = useRef<string | null>(new URLSearchParams(location.search).get('token'));
  const resetTokenRef = useRef<string | null>(initialTokenRef.current);
  const handledTokenRef = useRef(false);
  const requestAttemptRef = useRef<{ fingerprint: string; key: string } | null>(null);
  const resetAttemptRef = useRef<{ fingerprint: string; key: string } | null>(null);
  const [state, setState] = useState<RecoveryState>(mode === 'request'
    ? 'request-form'
    : initialTokenRef.current ? 'reset-form' : 'reset-rejected');

  useDocumentTitle(mode === 'request' ? 'Recuperar senha' : 'Redefinir senha');

  const resetAsync = reset.mutateAsync;

  useEffect(() => {
    if (mode !== 'reset' || handledTokenRef.current) {
      return;
    }

    handledTokenRef.current = true;
    const token = initialTokenRef.current;
    if (!token) {
      return;
    }

    resetTokenRef.current = token;
    navigate(paths.studentPasswordReset.getHref(), { replace: true });
  }, [mode, navigate]);

  const submitRequest = requestForm.handleSubmit(async (input: StudentPasswordResetRequestInput) => {
    const fingerprint = JSON.stringify(input);
    const attempt = requestAttemptRef.current?.fingerprint === fingerprint
      ? requestAttemptRef.current
      : { fingerprint, key: crypto.randomUUID() };
    requestAttemptRef.current = attempt;
    request.reset();

    try {
      await request.mutateAsync({ input, idempotencyKey: attempt.key });
      requestAttemptRef.current = null;
      requestForm.reset();
      setState('request-sent');
    } catch {
      setState('request-form');
    }
  });

  const submitReset = resetForm.handleSubmit(async (input: StudentPasswordResetFormInput) => {
    const token = resetTokenRef.current;
    if (!token) {
      setState('reset-rejected');
      return;
    }

    const fingerprint = JSON.stringify({ token, ...input });
    const attempt = resetAttemptRef.current?.fingerprint === fingerprint
      ? resetAttemptRef.current
      : { fingerprint, key: crypto.randomUUID() };
    resetAttemptRef.current = attempt;
    reset.reset();

    try {
      await resetAsync({ token, input, idempotencyKey: attempt.key });
      resetAttemptRef.current = null;
      resetForm.reset();
      resetTokenRef.current = null;
      setState('reset-done');
    } catch (error) {
      setState(getErrorCode(error) === 'PASSWORD_RESET_REJECTED' ? 'reset-rejected' : 'reset-error');
    }
  });

  return (
    <main className="page-shell">
      <p className="eyebrow">Conta do aluno</p>
      {mode === 'request' && state === 'request-form' ? (
        <>
          <h1>Recuperar senha</h1>
          <p className="lead">Informe o e-mail da sua conta. Se houver uma conta elegível, enviaremos um link para criar outra senha.</p>
          <FormProvider {...requestForm}>
            <form className="registration-form" noValidate onSubmit={submitRequest}>
              <FormTextField<StudentPasswordResetRequestInput>
                autoComplete="email"
                label="E-mail"
                name="email"
                type="email"
              />
              {request.isError ? (
                <p className="form-error" role="alert">Não foi possível solicitar a recuperação agora. Tente novamente em instantes.</p>
              ) : null}
              <button className="primary-button" disabled={request.isPending} type="submit">
                {request.isPending ? 'Enviando…' : 'Enviar link de recuperação'}
              </button>
            </form>
          </FormProvider>
        </>
      ) : null}
      {mode === 'request' && state === 'request-sent' ? (
        <section aria-live="polite" className="registration-message" role="status">
          <h1>Verifique seu e-mail</h1>
          <p>Se houver uma conta de aluno elegível para esse e-mail, enviaremos um link para redefinir sua senha.</p>
        </section>
      ) : null}
      {mode === 'reset' && (state === 'reset-form' || state === 'reset-error') ? (
        <>
          <h1>Crie uma nova senha</h1>
          <p className="lead">Escolha uma senha que você ainda não usa nesta conta.</p>
          <FormProvider {...resetForm}>
            <form className="registration-form" noValidate onSubmit={submitReset}>
              <FormTextField<StudentPasswordResetFormInput>
                autoComplete="new-password"
                label="Nova senha"
                name="newPassword"
                type="password"
              />
              <p className="password-hint">Use oito ou mais caracteres, com maiúscula, minúscula, número e símbolo.</p>
              {reset.isError ? (
                <p className="form-error" role="alert">{getErrorCode(reset.error) === 'PASSWORD_RESET_REJECTED'
                  ? 'O link expirou ou a senha não atende à política. Solicite outro link ou revise a senha.'
                  : 'Não foi possível redefinir sua senha agora. Tente novamente em instantes.'}</p>
              ) : null}
              <button className="primary-button" disabled={reset.isPending} type="submit">
                {reset.isPending ? 'Salvando…' : 'Redefinir senha'}
              </button>
            </form>
          </FormProvider>
        </>
      ) : null}
      {mode === 'reset' && state === 'reset-done' ? (
        <section aria-live="polite" className="registration-message" role="status">
          <h1>Senha redefinida</h1>
          <p>Sua senha foi alterada. Entre usando a nova senha.</p>
          <Link className="primary-link" to={paths.studentLogin.getHref()}>Entrar</Link>
        </section>
      ) : null}
      {mode === 'reset' && state === 'reset-rejected' ? (
        <section aria-live="polite" className="registration-message" role="alert">
          <h1>Link de recuperação indisponível</h1>
          <p>Este link não é válido ou expirou. Você pode solicitar um novo.</p>
          <Link className="primary-link" to={paths.studentPasswordRecovery.getHref()}>Solicitar novo link</Link>
        </section>
      ) : null}
      {mode === 'request' ? <p><Link className="primary-link" to={paths.studentLogin.getHref()}>Voltar para entrar</Link></p> : null}
    </main>
  );
};
