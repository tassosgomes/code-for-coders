import { useRef, useState } from 'react';
import { FormProvider } from 'react-hook-form';
import axios from 'axios';
import * as z from 'zod';

import { FormTextField } from '@/components/ui/form';
import {
  useRegisterStudent,
  type RegisterStudentInput,
} from '@/features/student-registration/api/register-student';
import { useStudentRegistrationForm } from '@/features/student-registration/hooks/use-student-registration-form';

const registrationProblemSchema = z.object({ code: z.string().optional() }).passthrough();

const getRegistrationErrorMessage = (error: unknown) => {
  const body = axios.isAxiosError(error) ? error.response?.data : undefined;
  const problem = registrationProblemSchema.safeParse(body);

  switch (problem.success ? problem.data.code : undefined) {
    case 'ACCOUNT_ALREADY_EXISTS':
      return 'Já existe uma conta ativa com esse e-mail. Entre, recupere sua senha ou solicite um novo link de confirmação.';
    case 'PASSWORD_POLICY_VIOLATION':
      return 'A senha não atende aos requisitos de segurança. Revise os requisitos e tente novamente.';
    case 'IDEMPOTENCY_CONFLICT':
      return 'Esta tentativa já foi enviada com outros dados. Revise o formulário e envie novamente.';
    default:
      return 'Não foi possível iniciar seu cadastro agora. Tente novamente em instantes.';
  }
};

export const StudentRegistrationScreen = () => {
  const form = useStudentRegistrationForm();
  const registration = useRegisterStudent();
  const attemptRef = useRef<{ fingerprint: string; key: string } | null>(null);
  const [registrationStarted, setRegistrationStarted] = useState(false);

  const onSubmit = form.handleSubmit(async (input: RegisterStudentInput) => {
    const fingerprint = JSON.stringify(input);
    const attempt = attemptRef.current?.fingerprint === fingerprint
      ? attemptRef.current
      : { fingerprint, key: crypto.randomUUID() };
    attemptRef.current = attempt;
    registration.reset();

    try {
      await registration.mutateAsync({ input, idempotencyKey: attempt.key });
      attemptRef.current = null;
      form.reset();
      setRegistrationStarted(true);
    } catch {
      setRegistrationStarted(false);
    }
  });

  return (
    <main className="page-shell">
      <p className="eyebrow">Conta do aluno</p>
      <h1>Criar conta</h1>
      <p className="lead">
        Informe seus dados. Enviaremos um link para confirmar seu e-mail antes de liberar o acesso.
      </p>

      {registrationStarted ? (
        <section aria-live="polite" className="registration-message" role="status">
          <h2>Cadastro iniciado</h2>
          <p>Confira seu e-mail para confirmar sua conta. Você ainda não tem acesso a cursos.</p>
        </section>
      ) : (
        <FormProvider {...form}>
          <form className="registration-form" noValidate onSubmit={onSubmit}>
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
            <FormTextField<RegisterStudentInput>
              autoComplete="new-password"
              label="Senha"
              name="password"
              type="password"
            />
            <p className="password-hint">
              Use oito ou mais caracteres, com maiúscula, minúscula, número e símbolo.
            </p>
            {registration.isError ? (
              <p className="form-error" role="alert">
                {getRegistrationErrorMessage(registration.error)}
              </p>
            ) : null}
            <button className="primary-button" disabled={registration.isPending} type="submit">
              {registration.isPending ? 'Enviando…' : 'Criar conta'}
            </button>
          </form>
        </FormProvider>
      )}
    </main>
  );
};
