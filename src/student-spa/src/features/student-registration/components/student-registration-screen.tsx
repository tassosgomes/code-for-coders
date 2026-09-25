import { useRef, useState, type FormEvent } from 'react';
import { FormProvider } from 'react-hook-form';
import { Link } from 'react-router';
import axios from 'axios';
import * as z from 'zod';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { StatusTile } from '@/components/blocks/status-tile';
import { Button } from '@/components/ui/button';
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
import { useDocumentTitle } from '@/hooks/use-document-title';
import {
  useRegisterStudent,
  type RegisterStudentInput,
} from '@/features/student-registration/api/register-student';
import { useStudentRegistrationForm } from '@/features/student-registration/hooks/use-student-registration-form';

const registrationProblemSchema = z.object({ code: z.string().optional() }).passthrough();

const getRegistrationErrorCode = (error: unknown) => {
  const body = axios.isAxiosError(error) ? error.response?.data : undefined;
  const problem = registrationProblemSchema.safeParse(body);

  return problem.success ? problem.data.code : undefined;
};

const getRegistrationErrorMessage = (error: unknown) => {
  switch (getRegistrationErrorCode(error)) {
    case 'PASSWORD_POLICY_VIOLATION':
      return 'A senha ainda não atende aos requisitos. Confira os itens e tente novamente.';
    case 'IDEMPOTENCY_CONFLICT':
      return 'Esta tentativa já foi enviada com outros dados. Revise o formulário e tente novamente.';
    default:
      return 'Não conseguimos criar sua conta agora. Tente de novo em instantes.';
  }
};

export const StudentRegistrationScreen = () => {
  const form = useStudentRegistrationForm();
  const registration = useRegisterStudent();
  const attemptRef = useRef<{ fingerprint: string; key: string } | null>(null);
  const [registrationStarted, setRegistrationStarted] = useState(false);
  const [submittedEmail, setSubmittedEmail] = useState('');

  useDocumentTitle(registrationStarted ? 'Confira seu e-mail' : 'Criar conta');

  const submitRegistration = async (input: RegisterStudentInput) => {
    const fingerprint = JSON.stringify(input);
    const attempt = attemptRef.current?.fingerprint === fingerprint
      ? attemptRef.current
      : { fingerprint, key: crypto.randomUUID() };
    attemptRef.current = attempt;
    registration.reset();

    try {
      await registration.mutateAsync({ input, idempotencyKey: attempt.key });
      attemptRef.current = null;
      setSubmittedEmail(input.email);
      form.reset();
      setRegistrationStarted(true);
    } catch {
      setRegistrationStarted(false);
    }
  };

  const onSubmit = (event: FormEvent<HTMLFormElement>) =>
    form.handleSubmit(submitRegistration)(event);

  const accountAlreadyExists = registration.isError
    && getRegistrationErrorCode(registration.error) === 'ACCOUNT_ALREADY_EXISTS';

  if (registrationStarted) {
    return (
      <StatusTile
        description={(
          <>
            <p>
            Enviamos um link de confirmação para <strong className="font-medium text-foreground">{submittedEmail}</strong>.
            Ele vale por tempo limitado.
            </p>
            <p className="mt-3">Sua conta ainda não libera acesso a cursos. Isso acontece quando você se matricular.</p>
            <p className="mt-3">Não recebeu? Confira a pasta de spam ou peça outro link de confirmação.</p>
          </>
        )}
        title="Confira seu e-mail"
        tone="success"
      >
          <Button asChild variant="outline">
            <Link to={paths.studentLogin.getHref()}>Ir para a entrada</Link>
          </Button>
          <Button asChild variant="link">
            <Link to={paths.studentAccountConfirmation.getHref()}>Reenviar confirmação</Link>
          </Button>
      </StatusTile>
    );
  }

  return (
    <Card>
      <CardHeader>
        <p className="typo-overline text-muted-foreground">Conta do aluno</p>
        <CardTitle className="typo-h3">
          <h1>Criar conta</h1>
        </CardTitle>
        <CardDescription>
          Enviaremos um link para confirmar seu e-mail antes de liberar o acesso.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <FormProvider {...form}>
          <form className="grid gap-5" noValidate onSubmit={onSubmit}>
            {accountAlreadyExists ? (
              <Alert>
                <AlertTitle>Já existe uma conta com esse e-mail.</AlertTitle>
                <AlertDescription>
                  <p>Escolha como continuar:</p>
                  <div className="flex flex-wrap gap-x-4 gap-y-1">
                    <Link className="font-medium text-primary underline underline-offset-4" to={paths.studentLogin.getHref()}>
                      Entrar
                    </Link>
                    <Link className="font-medium text-primary underline underline-offset-4" to={paths.studentPasswordRecovery.getHref()}>
                      Recuperar senha
                    </Link>
                    <Link className="font-medium text-primary underline underline-offset-4" to={paths.studentAccountConfirmation.getHref()}>
                      Reenviar confirmação
                    </Link>
                  </div>
                </AlertDescription>
              </Alert>
            ) : null}

            <FormTextField<RegisterStudentInput>
              autoComplete="name"
              label="Nome"
              name="name"
            />
            <FormTextField<RegisterStudentInput>
              autoComplete="email"
              label="E-mail"
              name="email"
              type="email"
            />
            <PasswordField<RegisterStudentInput>
              autoComplete="new-password"
              label="Senha"
              name="password"
              showRequirements
            />
            {registration.isError && !accountAlreadyExists ? (
              <Alert variant="destructive">
                <AlertTitle>Não foi possível criar a conta</AlertTitle>
                <AlertDescription>{getRegistrationErrorMessage(registration.error)}</AlertDescription>
              </Alert>
            ) : null}
            <Button className="w-full" disabled={registration.isPending} type="submit">
              {registration.isPending ? 'Criando conta…' : 'Criar conta'}
            </Button>
          </form>
        </FormProvider>
      </CardContent>
      <CardFooter className="justify-center gap-1 text-sm text-muted-foreground">
        <span>Já tem conta?</span>
        <Button asChild className="h-auto p-0" variant="link">
          <Link to={paths.studentLogin.getHref()}>Entrar</Link>
        </Button>
      </CardFooter>
    </Card>
  );
};
