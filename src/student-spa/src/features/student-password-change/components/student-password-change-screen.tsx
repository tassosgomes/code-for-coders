import { useRef } from 'react';
import { FormProvider } from 'react-hook-form';
import { Link, useNavigate } from 'react-router';
import { toast } from 'sonner';
import axios from 'axios';
import * as z from 'zod';

import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { PasswordField } from '@/components/ui/password-field';
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
  const navigate = useNavigate();
  const attemptRef = useRef<{ fingerprint: string; key: string } | null>(null);

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
      toast.success('Senha trocada. Encerramos as outras sessões.');
      await navigate(paths.home.getHref(), { replace: true });
    } catch {
      // The mutation error is rendered below while preserving the entered values.
    }
  };

  const errorCode = change.isError ? getErrorCode(change.error) : undefined;
  return (
    <div className="mx-auto flex w-full max-w-7xl flex-col gap-6">
      <Link className="inline-flex w-fit items-center gap-2 text-sm text-muted-foreground hover:text-foreground" to={paths.home.getHref()}>
        ← Início
      </Link>
      <header className="space-y-2">
        <p className="typo-overline text-primary">Conta</p>
        <h2 className="typo-h2" id="student-password-change-title">Trocar senha</h2>
      </header>
      <FormProvider {...form}>
        <form
          aria-labelledby="student-password-change-title"
          className="w-full max-w-xl"
          noValidate
          onSubmit={(event) => void form.handleSubmit(changePassword)(event)}
        >
          <Card>
            <CardContent className="grid gap-6">
              <PasswordField<StudentPasswordChangeInput>
                autoComplete="current-password"
                label="Senha atual"
                name="currentPassword"
              />
              <PasswordField<StudentPasswordChangeInput>
                autoComplete="new-password"
                label="Nova senha"
                name="newPassword"
                showRequirements
              />
              <Alert className="border-primary/30 bg-primary/5 text-foreground" role="note">
                <AlertDescription>
                  Ao trocar, as outras sessões da sua conta serão encerradas. Esta sessão continua ativa.
                </AlertDescription>
              </Alert>
              {change.isError ? (
                <Alert variant="destructive">
                  <AlertDescription>
                    {errorCode === 'PASSWORD_CHANGE_REJECTED'
                      ? 'A senha atual não confere ou a nova senha não cumpre os requisitos. Confira e tente de novo.'
                      : 'Não foi possível trocar sua senha agora. Tente novamente em instantes.'}
                  </AlertDescription>
                </Alert>
              ) : null}
              <div className="flex flex-col-reverse justify-end gap-3 sm:flex-row">
                <Button asChild variant="outline">
                  <Link to={paths.home.getHref()}>Cancelar</Link>
                </Button>
                <Button disabled={change.isPending} type="submit">
                  {change.isPending ? 'Salvando…' : 'Trocar senha'}
                </Button>
              </div>
            </CardContent>
          </Card>
        </form>
      </FormProvider>
    </div>
  );
};
