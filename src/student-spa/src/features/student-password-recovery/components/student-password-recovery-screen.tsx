import { useEffect, useRef, useState, type FormEvent } from 'react';
import { FormProvider } from 'react-hook-form';
import { Link, useLocation, useNavigate } from 'react-router';
import axios from 'axios';
import * as z from 'zod';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { StatusTile } from '@/components/blocks/status-tile';
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { FormTextField } from '@/components/ui/form';
import { PasswordField } from '@/components/ui/password-field';
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

const resetErrorMessage = (error: unknown) => {
  if (getErrorCode(error) === 'PASSWORD_POLICY_VIOLATION') {
    return 'A senha ainda não atende aos requisitos. Confira os itens e tente novamente.';
  }

  return 'Não foi possível redefinir sua senha agora. Tente novamente em instantes.';
};

export const StudentPasswordRecoveryScreen = ({ mode }: StudentPasswordRecoveryScreenProps) => {
  const location = useLocation();
  const navigate = useNavigate();
  const requestForm = useStudentPasswordResetRequestForm();
  const resetForm = useStudentPasswordResetForm();
  const request = useRequestStudentPasswordReset();
  const reset = useResetStudentPassword();
  const [initialToken] = useState(() => new URLSearchParams(location.search).get('token'));
  const resetTokenRef = useRef<string | null>(initialToken);
  const handledTokenRef = useRef(false);
  const requestAttemptRef = useRef<{ fingerprint: string; key: string } | null>(null);
  const resetAttemptRef = useRef<{ fingerprint: string; key: string } | null>(null);
  const [submittedEmail, setSubmittedEmail] = useState('');
  const [state, setState] = useState<RecoveryState>(mode === 'request'
    ? 'request-form'
    : initialToken ? 'reset-form' : 'reset-rejected');

  useDocumentTitle(mode === 'request' ? 'Recuperar senha' : 'Redefinir senha');

  useEffect(() => {
    if (mode !== 'reset' || handledTokenRef.current) {
      return;
    }

    handledTokenRef.current = true;
    if (!initialToken) {
      return;
    }

    resetTokenRef.current = initialToken;
    navigate(paths.studentPasswordReset.getHref(), { replace: true });
  }, [initialToken, mode, navigate]);

  const submitResetRequest = async (input: StudentPasswordResetRequestInput) => {
    const fingerprint = JSON.stringify(input);
    const attempt = requestAttemptRef.current?.fingerprint === fingerprint
      ? requestAttemptRef.current
      : { fingerprint, key: crypto.randomUUID() };
    requestAttemptRef.current = attempt;
    request.reset();

    try {
      await request.mutateAsync({ input, idempotencyKey: attempt.key });
      requestAttemptRef.current = null;
      setSubmittedEmail(input.email);
      requestForm.reset();
      setState('request-sent');
    } catch {
      // Keep the submitted email in the form so a network failure is easy to retry.
    }
  };

  const submitRequest = (event: FormEvent<HTMLFormElement>) =>
    requestForm.handleSubmit(submitResetRequest)(event);

  const submitPasswordReset = async (input: StudentPasswordResetFormInput) => {
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
      await reset.mutateAsync({ token, input, idempotencyKey: attempt.key });
      resetAttemptRef.current = null;
      resetForm.reset();
      resetTokenRef.current = null;
      setState('reset-done');
    } catch (error) {
      if (getErrorCode(error) === 'PASSWORD_RESET_REJECTED') {
        resetTokenRef.current = null;
        setState('reset-rejected');
        return;
      }

      // A password policy or network failure does not consume or discard the token.
      setState('reset-error');
    }
  };

  const submitReset = (event: FormEvent<HTMLFormElement>) =>
    resetForm.handleSubmit(submitPasswordReset)(event);

  if (mode === 'request' && state === 'request-form') {
    return (
      <Card>
        <CardHeader>
          <p className="typo-overline text-muted-foreground">Conta do aluno</p>
          <CardTitle className="typo-h3">
            <h1>Recuperar senha</h1>
          </CardTitle>
          <CardDescription>
            Informe o e-mail da conta. Se ela existir, enviaremos um link para criar uma nova senha.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <FormProvider {...requestForm}>
            <form className="grid gap-5" noValidate onSubmit={submitRequest}>
              <FormTextField<StudentPasswordResetRequestInput>
                autoComplete="email"
                label="E-mail"
                name="email"
                type="email"
              />
              {request.isError ? (
                <Alert variant="destructive">
                  <AlertTitle>Não foi possível enviar o pedido</AlertTitle>
                  <AlertDescription>
                    Não conseguimos solicitar a recuperação agora. Tente novamente em instantes.
                  </AlertDescription>
                </Alert>
              ) : null}
              <Button className="w-full" disabled={request.isPending} type="submit">
                {request.isPending ? 'Enviando…' : 'Receber link de recuperação'}
              </Button>
            </form>
          </FormProvider>
        </CardContent>
        <CardFooter>
          <Button asChild className="px-0" variant="link">
            <Link to={paths.studentLogin.getHref()}>Voltar para a entrada</Link>
          </Button>
        </CardFooter>
      </Card>
    );
  }

  if (mode === 'request' && state === 'request-sent') {
    return (
      <StatusTile
        description={(
          <>
            Se houver uma conta de aluno com <strong className="font-medium text-foreground">{submittedEmail}</strong>,
            enviamos um link para criar uma nova senha. Ele vale por tempo limitado.
          </>
        )}
        title="Verifique seu e-mail"
      >
          <Button asChild variant="outline">
            <Link to={paths.studentLogin.getHref()}>Voltar para a entrada</Link>
          </Button>
      </StatusTile>
    );
  }

  if (mode === 'reset' && (state === 'reset-form' || state === 'reset-error')) {
    return (
      <Card>
        <CardHeader>
          <p className="typo-overline text-muted-foreground">Conta do aluno</p>
          <CardTitle className="typo-h3">
            <h1>Crie uma nova senha</h1>
          </CardTitle>
          <CardDescription>Escolha uma senha que você ainda não usa nesta conta.</CardDescription>
        </CardHeader>
        <CardContent>
          <FormProvider {...resetForm}>
            <form className="grid gap-5" noValidate onSubmit={submitReset}>
              <PasswordField<StudentPasswordResetFormInput>
                autoComplete="new-password"
                label="Nova senha"
                name="newPassword"
                showRequirements
              />
              {state === 'reset-error' ? (
                <Alert variant="destructive">
                  <AlertTitle>Não foi possível redefinir a senha</AlertTitle>
                  <AlertDescription>{resetErrorMessage(reset.error)}</AlertDescription>
                </Alert>
              ) : null}
              <Button className="w-full" disabled={reset.isPending} type="submit">
                {reset.isPending ? 'Salvando…' : 'Redefinir senha'}
              </Button>
            </form>
          </FormProvider>
        </CardContent>
      </Card>
    );
  }

  if (mode === 'reset' && state === 'reset-done') {
    return (
      <StatusTile
        description="Pronto. Por segurança, encerramos as outras sessões da sua conta."
        title="Senha redefinida"
        tone="success"
      >
          <Button asChild className="w-full">
            <Link to={paths.studentLogin.getHref()}>Entrar com a nova senha</Link>
          </Button>
      </StatusTile>
    );
  }

  return (
    <StatusTile
      announceAs="alert"
      description="Este link expirou ou já foi usado. Peça um novo link para criar outra senha."
      title="Este link não vale mais"
      tone="warning"
    >
        <Button asChild className="w-full">
          <Link to={paths.studentPasswordRecovery.getHref()}>Pedir novo link</Link>
        </Button>
    </StatusTile>
  );
};
