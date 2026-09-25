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
import { Skeleton } from '@/components/ui/skeleton';
import { paths } from '@/config/paths';
import {
  useConfirmStudentAccount,
  useRequestAccountConfirmation,
  type StudentConfirmationEmailInput,
} from '@/features/student-confirmation/api/student-confirmation';
import { useStudentConfirmationRequestForm } from '@/features/student-confirmation/hooks/use-student-confirmation-request-form';
import { useDocumentTitle } from '@/hooks/use-document-title';

const confirmationProblemSchema = z.object({ code: z.string().optional() }).passthrough();

const getRequestErrorMessage = (error: unknown) => {
  const body = axios.isAxiosError(error) ? error.response?.data : undefined;
  const problem = confirmationProblemSchema.safeParse(body);

  return problem.success && problem.data.code === 'IDEMPOTENCY_CONFLICT'
    ? 'Esta solicitação já foi enviada com outros dados. Aguarde um instante e tente novamente.'
    : 'Não foi possível solicitar outro link agora. Tente novamente em instantes.';
};

type ConfirmationState = 'checking' | 'confirmed' | 'invalid' | 'request-form' | 'request-sent';

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
  const [state, setState] = useState<ConfirmationState>(token ? 'checking' : 'request-form');
  const [submittedEmail, setSubmittedEmail] = useState('');

  useDocumentTitle('Confirmar e-mail');

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

  const submitConfirmationRequest = async (input: StudentConfirmationEmailInput) => {
    const fingerprint = JSON.stringify(input);
    const attempt = requestAttemptRef.current?.fingerprint === fingerprint
      ? requestAttemptRef.current
      : { fingerprint, key: crypto.randomUUID() };
    requestAttemptRef.current = attempt;
    requestConfirmation.reset();

    try {
      await requestConfirmation.mutateAsync({ input, idempotencyKey: attempt.key });
      requestAttemptRef.current = null;
      setSubmittedEmail(input.email);
      form.reset();
      setState('request-sent');
    } catch {
      // Keep the email in the form so the student can retry without retyping it.
    }
  };

  const onSubmit = (event: FormEvent<HTMLFormElement>) =>
    form.handleSubmit(submitConfirmationRequest)(event);

  if (state === 'checking') {
    return (
      <Card aria-live="polite" aria-busy="true" role="status">
        <CardHeader>
          <Skeleton aria-hidden="true" className="h-7 w-3/4" />
          <Skeleton aria-hidden="true" className="h-4 w-full" />
        </CardHeader>
        <CardContent>
          <p className="text-sm text-muted-foreground">Confirmando seu e-mail…</p>
        </CardContent>
      </Card>
    );
  }

  if (state === 'confirmed') {
    return (
      <StatusTile
        description="Tudo certo. Sua conta está pronta para entrar."
        title="E-mail confirmado"
        tone="success"
      >
          <Button asChild className="w-full">
            <Link to={paths.studentLogin.getHref()}>Entrar na plataforma</Link>
          </Button>
      </StatusTile>
    );
  }

  if (state === 'request-sent') {
    return (
      <StatusTile
        description={(
          <>
            Se houver uma conta pendente para <strong className="font-medium text-foreground">{submittedEmail}</strong>,
            o novo link já está a caminho.
          </>
        )}
        title="Verifique seu e-mail"
      >
          <Button asChild variant="outline">
            <Link to={paths.studentLogin.getHref()}>Ir para a entrada</Link>
          </Button>
      </StatusTile>
    );
  }

  const isResendEntry = state === 'request-form';

  return (
    <Card>
      <CardHeader>
        <p className="typo-overline text-muted-foreground">Conta do aluno</p>
        <CardTitle className="text-2xl">
          <h1>{isResendEntry ? 'Reenviar confirmação' : 'Este link não vale mais'}</h1>
        </CardTitle>
        <CardDescription>
          {isResendEntry
            ? 'Informe o e-mail do cadastro. Se houver uma conta pendente, enviaremos um novo link.'
            : 'Este link expirou ou já foi usado. Sua conta continua salva; peça um novo link para confirmar o e-mail.'}
        </CardDescription>
      </CardHeader>
      <CardContent>
        {!isResendEntry ? (
          <Alert className="mb-5">
            <AlertTitle>Este link não vale mais</AlertTitle>
            <AlertDescription>Você pode pedir outro link de confirmação abaixo.</AlertDescription>
          </Alert>
        ) : null}
        <FormProvider {...form}>
          <form className="grid gap-5" noValidate onSubmit={onSubmit}>
            <FormTextField<StudentConfirmationEmailInput>
              autoComplete="email"
              label="E-mail"
              name="email"
              type="email"
            />
            {requestConfirmation.isError ? (
              <Alert variant="destructive">
                <AlertTitle>Não foi possível enviar o pedido</AlertTitle>
                <AlertDescription>{getRequestErrorMessage(requestConfirmation.error)}</AlertDescription>
              </Alert>
            ) : null}
            <Button className="w-full" disabled={requestConfirmation.isPending} type="submit">
              {requestConfirmation.isPending ? 'Enviando…' : 'Receber novo link'}
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
};
