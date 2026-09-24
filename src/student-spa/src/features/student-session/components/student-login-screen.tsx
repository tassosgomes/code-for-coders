import { useRef, useState } from 'react';
import { FormProvider } from 'react-hook-form';
import { Link, useNavigate } from 'react-router';
import axios from 'axios';
import * as z from 'zod';

import { FormTextField } from '@/components/ui/form';
import { paths } from '@/config/paths';
import {
  useCreateStudentSession,
  type CreateStudentSessionInput,
} from '@/features/student-session/api/student-session';
import { useStudentLoginForm } from '@/features/student-session/hooks/use-student-login-form';
import { useDocumentTitle } from '@/hooks/use-document-title';

const problemSchema = z.object({ code: z.string().optional() }).passthrough();

const getLoginErrorCode = (error: unknown) => {
  const body = axios.isAxiosError(error) ? error.response?.data : undefined;
  const problem = problemSchema.safeParse(body);

  return problem.success ? problem.data.code : undefined;
};

export const StudentLoginScreen = () => {
  useDocumentTitle('Entrar');
  const form = useStudentLoginForm();
  const login = useCreateStudentSession();
  const navigate = useNavigate();
  const attemptRef = useRef<{ fingerprint: string; key: string } | null>(null);
  const [notConfirmed, setNotConfirmed] = useState(false);

  const submitLogin = async (input: CreateStudentSessionInput) => {
    const fingerprint = JSON.stringify(input);
    const attempt = attemptRef.current?.fingerprint === fingerprint
      ? attemptRef.current
      : { fingerprint, key: crypto.randomUUID() };
    attemptRef.current = attempt;
    setNotConfirmed(false);
    login.reset();

    try {
      await login.mutateAsync({ input, idempotencyKey: attempt.key });
      attemptRef.current = null;
      form.reset();
      await navigate(paths.home.getHref(), { replace: true });
    } catch (error) {
      setNotConfirmed(getLoginErrorCode(error) === 'EMAIL_NOT_CONFIRMED');
    }
  };

  const errorMessage = getLoginErrorCode(login.error) === 'INVALID_CREDENTIALS'
    ? 'E-mail ou senha inválidos.'
    : 'Não foi possível entrar agora. Tente novamente em instantes.';

  return (
    <main className="page-shell">
      <p className="eyebrow">Conta do aluno</p>
      <h1>Entrar</h1>
      <p className="lead">Entre para acessar seu espaço de aprendizagem.</p>

      {notConfirmed ? (
        <section aria-live="polite" className="registration-message" role="alert">
          <h2>Confirme seu e-mail</h2>
          <p>Confirme o endereço de e-mail da conta antes de entrar.</p>
          <Link className="primary-link" to={paths.studentAccountConfirmation.getHref()}>
            Solicitar novo link de confirmação
          </Link>
        </section>
      ) : (
        <FormProvider {...form}>
          <form className="registration-form" noValidate onSubmit={(event) => void form.handleSubmit(submitLogin)(event)}>
            <FormTextField<CreateStudentSessionInput>
              autoComplete="email"
              label="E-mail"
              name="email"
              type="email"
            />
            <FormTextField<CreateStudentSessionInput>
              autoComplete="current-password"
              label="Senha"
              name="password"
              type="password"
            />
            {login.isError ? <p className="form-error" role="alert">{errorMessage}</p> : null}
            <button className="primary-button" disabled={login.isPending} type="submit">
              {login.isPending ? 'Entrando…' : 'Entrar'}
            </button>
          </form>
        </FormProvider>
      )}

      <p>Não tem uma conta? <Link className="primary-link" to={paths.studentRegistration.getHref()}>Criar conta</Link></p>
      <p><Link className="primary-link" to={paths.studentPasswordRecovery.getHref()}>Esqueceu sua senha?</Link></p>
    </main>
  );
};
