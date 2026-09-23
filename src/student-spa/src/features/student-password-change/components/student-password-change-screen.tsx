import { useRef, useState } from 'react';
import { FormProvider } from 'react-hook-form';
import { Link } from 'react-router';
import axios from 'axios';
import * as z from 'zod';

import { FormTextField } from '@/components/ui/form';
import { paths } from '@/config/paths';
import {
  useChangeStudentPassword,
  type StudentPasswordChangeInput,
} from '@/features/student-password-change/api/change-student-password';
import { useStudentPasswordChangeForm } from '@/features/student-password-change/hooks/use-student-password-change-form';
import { useDocumentTitle } from '@/hooks/use-document-title';

const problemSchema = z.object({ code: z.string().optional() }).passthrough();

const getErrorCode = (error: unknown) => {
  const body = axios.isAxiosError(error) ? error.response?.data : undefined;
  const problem = problemSchema.safeParse(body);
  return problem.success ? problem.data.code : undefined;
};

type StudentPasswordChangeScreenProps = {
  csrfToken: string;
};

export const StudentPasswordChangeScreen = ({ csrfToken }: StudentPasswordChangeScreenProps) => {
  const form = useStudentPasswordChangeForm();
  const change = useChangeStudentPassword();
  const attemptRef = useRef<{ fingerprint: string; key: string } | null>(null);
  const [changed, setChanged] = useState(false);

  useDocumentTitle('Trocar senha');

  const changePassword = async (input: StudentPasswordChangeInput) => {
    const fingerprint = JSON.stringify(input);
    const attempt = attemptRef.current?.fingerprint === fingerprint
      ? attemptRef.current
      : { fingerprint, key: crypto.randomUUID() };
    attemptRef.current = attempt;
    change.reset();

    try {
      await change.mutateAsync({ input, csrfToken, idempotencyKey: attempt.key });
      attemptRef.current = null;
      form.reset();
      setChanged(true);
    } catch {
      setChanged(false);
    }
  };

  if (changed) {
    return (
      <main className="page-shell">
        <p className="eyebrow">Conta do aluno</p>
        <section aria-live="polite" className="registration-message" role="status">
          <h1>Senha alterada</h1>
          <p>Sua senha foi alterada. Esta sessão continua ativa.</p>
          <Link className="primary-link" to={paths.home.getHref()}>Voltar ao início</Link>
        </section>
      </main>
    );
  }

  const errorCode = change.isError ? getErrorCode(change.error) : undefined;
  return (
    <main className="page-shell">
      <p className="eyebrow">Conta do aluno</p>
      <h1>Trocar senha</h1>
      <p className="lead">Confirme sua senha atual e escolha uma nova senha.</p>
      <FormProvider {...form}>
        <form className="registration-form" noValidate onSubmit={(event) => void form.handleSubmit(changePassword)(event)}>
          <FormTextField<StudentPasswordChangeInput>
            autoComplete="current-password"
            label="Senha atual"
            name="currentPassword"
            type="password"
          />
          <FormTextField<StudentPasswordChangeInput>
            autoComplete="new-password"
            label="Nova senha"
            name="newPassword"
            type="password"
          />
          <p className="password-hint">Use oito ou mais caracteres, com maiúscula, minúscula, número e símbolo.</p>
          {change.isError ? (
            <p className="form-error" role="alert">
              {errorCode === 'PASSWORD_CHANGE_REJECTED'
                ? 'A senha atual está incorreta ou a nova senha não atende à política.'
                : 'Não foi possível trocar sua senha agora. Tente novamente em instantes.'}
            </p>
          ) : null}
          <button className="primary-button" disabled={change.isPending} type="submit">
            {change.isPending ? 'Salvando…' : 'Trocar senha'}
          </button>
        </form>
      </FormProvider>
      <p><Link className="primary-link" to={paths.home.getHref()}>Voltar ao início</Link></p>
    </main>
  );
};
