import { useRef, useState } from 'react';
import { FormProvider } from 'react-hook-form';
import { Link, useLocation, useNavigate } from 'react-router';
import axios from 'axios';
import * as z from 'zod';
import { InfoIcon } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { FormTextField } from '@/components/ui/form';
import { PasswordField } from '@/components/ui/password-field';
import { paths } from '@/config/paths';
import { activeStudentSessionMarker, expiredStudentSessionMarker } from '@/config/session-markers';
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

const isSessionExpiredState = (state: unknown) =>
  typeof state === 'object'
  && state !== null
  && 'sessionExpired' in state
  && state.sessionExpired === true;

export const StudentLoginScreen = () => {
  useDocumentTitle('Entrar');
  const form = useStudentLoginForm();
  const login = useCreateStudentSession();
  const navigate = useNavigate();
  const location = useLocation();
  const attemptRef = useRef<{ fingerprint: string; key: string } | null>(null);
  const [notConfirmed, setNotConfirmed] = useState(false);
  const [showSessionExpired, setShowSessionExpired] = useState(() => {
    const fromStorage = window.sessionStorage.getItem(expiredStudentSessionMarker) === 'true';
    return isSessionExpiredState(location.state) || fromStorage;
  });

  const clearSessionExpiredNotice = () => {
    if (!showSessionExpired) {
      return;
    }

    setShowSessionExpired(false);
    window.sessionStorage.removeItem(expiredStudentSessionMarker);
  };

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
      window.localStorage.setItem(activeStudentSessionMarker, 'true');
      attemptRef.current = null;
      form.reset();
      clearSessionExpiredNotice();
      await navigate(paths.home.getHref(), { replace: true });
    } catch (error) {
      setNotConfirmed(getLoginErrorCode(error) === 'EMAIL_NOT_CONFIRMED');
    }
  };

  const errorMessage = getLoginErrorCode(login.error) === 'INVALID_CREDENTIALS'
    ? 'E-mail ou senha inválidos.'
    : 'Não foi possível entrar agora. Tente novamente em instantes.';

  return (
    <Card>
      <CardHeader>
        <p className="typo-overline text-primary">Conta do aluno</p>
        <CardTitle className="typo-h3"><h1>Entrar</h1></CardTitle>
        <CardDescription>Entre para acessar seu espaço de aprendizagem.</CardDescription>
      </CardHeader>
      {showSessionExpired ? (
        <CardContent className="pb-0">
          <Alert aria-live="polite" role="status">
            <InfoIcon aria-hidden="true" />
            <AlertTitle>Sua sessão expirou</AlertTitle>
            <AlertDescription>Entre de novo para continuar no seu espaço de aprendizagem.</AlertDescription>
          </Alert>
        </CardContent>
      ) : null}
      <CardContent>
        {notConfirmed ? (
          <Alert aria-live="polite" role="alert">
            <AlertTitle>Confirme seu e-mail para entrar</AlertTitle>
            <AlertDescription>
              <p>Confirme o endereço de e-mail da conta antes de entrar.</p>
              <Button asChild className="mt-2 px-0" variant="link">
                <Link to={paths.studentAccountConfirmation.getHref()}>Solicitar novo link de confirmação</Link>
              </Button>
            </AlertDescription>
          </Alert>
        ) : (
          <FormProvider {...form}>
            <form className="grid gap-5" noValidate onSubmit={(event) => void form.handleSubmit(submitLogin)(event)}>
              <FormTextField<CreateStudentSessionInput>
                autoComplete="email"
                label="E-mail"
                name="email"
                type="email"
              />
              <PasswordField<CreateStudentSessionInput>
                autoComplete="current-password"
                label="Senha"
                name="password"
              />
              {login.isError ? (
                <Alert variant="destructive">
                  <AlertTitle>Não foi possível entrar</AlertTitle>
                  <AlertDescription>{errorMessage}</AlertDescription>
                </Alert>
              ) : null}
              <Button className="w-full" disabled={login.isPending} type="submit">
                {login.isPending ? 'Entrando…' : 'Entrar'}
              </Button>
            </form>
          </FormProvider>
        )}
      </CardContent>
      <CardFooter className="flex-col items-start gap-3 text-sm">
        <p className="text-muted-foreground">
          Não tem uma conta?{' '}
          <Button asChild className="h-auto p-0" variant="link">
            <Link to={paths.studentRegistration.getHref()}>Criar conta</Link>
          </Button>
        </p>
        <Button asChild className="h-auto p-0" variant="link">
          <Link to={paths.studentPasswordRecovery.getHref()}>Esqueceu sua senha?</Link>
        </Button>
      </CardFooter>
    </Card>
  );
};
