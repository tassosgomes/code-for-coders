import { useEffect, useRef, useState } from 'react';
import { FormProvider } from 'react-hook-form';
import { useLocation, useNavigate } from 'react-router';
import axios from 'axios';
import * as z from 'zod';

import { FormTextField } from '@/components/ui/form';
import { paths } from '@/config/paths';
import {
  useConfirmStudentAccount,
  useRequestAccountConfirmation,
  type StudentConfirmationEmailInput,
} from '@/features/student-confirmation/api/student-confirmation';
import { useStudentConfirmationRequestForm } from '@/features/student-confirmation/hooks/use-student-confirmation-request-form';

const confirmationProblemSchema = z.object({ code: z.string().optional() }).passthrough();

const getRequestErrorMessage = (error: unknown) => {
  const body = axios.isAxiosError(error) ? error.response?.data : undefined;
  const problem = confirmationProblemSchema.safeParse(body);

  return problem.success && problem.data.code === 'IDEMPOTENCY_CONFLICT'
    ? 'Esta solicitação já foi enviada com outros dados. Aguarde um instante e tente novamente.'
    : 'Não foi possível solicitar outro link agora. Tente novamente em instantes.';
};

type ConfirmationState = 'checking' | 'confirmed' | 'invalid' | 'request-sent';

export const StudentConfirmationScreen = () => {
  const location = useLocation();
  const navigate = useNavigate();
  const token = new URLSearchParams(location.search).get('token');
  const confirmation = useConfirmStudentAccount();
  const requestConfirmation = useRequestAccountConfirmation();
  const form = useStudentConfirmationRequestForm();
  const handledTokenRef = useRef<string | null>(null);
  const confirmationKeyRef = useRef<string | null>(null);
  const requestAttemptRef = useRef<{ fingerprint: string; key: string } | null>(null);
  const [state, setState] = useState<ConfirmationState>(token ? 'checking' : 'invalid');

  const confirmAsync = confirmation.mutateAsync;

  useEffect(() => {
    if (!token || handledTokenRef.current === token) {
      return;
    }

    handledTokenRef.current = token;
    confirmationKeyRef.current ??= crypto.randomUUID();
    const idempotencyKey = confirmationKeyRef.current;
    navigate(paths.studentAccountConfirmation.getHref(), { replace: true });

    void confirmAsync({ input: { token }, idempotencyKey })
      .then(() => {
        confirmationKeyRef.current = null;
        setState('confirmed');
      })
      .catch(() => setState('invalid'));
  }, [confirmAsync, navigate, token]);

  const onSubmit = form.handleSubmit(async (input: StudentConfirmationEmailInput) => {
    const fingerprint = JSON.stringify(input);
    const attempt = requestAttemptRef.current?.fingerprint === fingerprint
      ? requestAttemptRef.current
      : { fingerprint, key: crypto.randomUUID() };
    requestAttemptRef.current = attempt;
    requestConfirmation.reset();

    try {
      await requestConfirmation.mutateAsync({ input, idempotencyKey: attempt.key });
      requestAttemptRef.current = null;
      form.reset();
      setState('request-sent');
    } catch {
      setState('invalid');
    }
  });

  return (
    <main className="page-shell">
      <p className="eyebrow">Conta do aluno</p>
      {state === 'checking' ? (
        <section aria-live="polite" className="registration-message" role="status">
          <h1>Confirmando conta</h1>
          <p>Aguarde enquanto validamos seu link.</p>
        </section>
      ) : null}
      {state === 'confirmed' ? (
        <section aria-live="polite" className="registration-message" role="status">
          <h1>Conta confirmada</h1>
          <p>Seu e-mail foi confirmado. Você já pode entrar na sua conta.</p>
        </section>
      ) : null}
      {state === 'request-sent' ? (
        <section aria-live="polite" className="registration-message" role="status">
          <h1>Verifique seu e-mail</h1>
          <p>Se houver uma conta pendente para esse e-mail, enviaremos um novo link de confirmação.</p>
        </section>
      ) : null}
      {state === 'invalid' ? (
        <>
          <h1>Link de confirmação indisponível</h1>
          <p className="lead">Este link não é válido ou expirou. Você pode solicitar um novo link.</p>
          <FormProvider {...form}>
            <form className="registration-form" noValidate onSubmit={onSubmit}>
              <FormTextField<StudentConfirmationEmailInput>
                autoComplete="email"
                label="E-mail"
                name="email"
                type="email"
              />
              {requestConfirmation.isError ? (
                <p className="form-error" role="alert">
                  {getRequestErrorMessage(requestConfirmation.error)}
                </p>
              ) : null}
              <button className="primary-button" disabled={requestConfirmation.isPending} type="submit">
                {requestConfirmation.isPending ? 'Enviando…' : 'Enviar novo link'}
              </button>
            </form>
          </FormProvider>
        </>
      ) : null}
    </main>
  );
};
