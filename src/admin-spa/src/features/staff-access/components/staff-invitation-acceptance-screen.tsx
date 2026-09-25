import { useLayoutEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { Link, useNavigate } from 'react-router';

import { zodResolver } from '@hookform/resolvers/zod';

import { paths } from '@/config/paths';
import {
  getStaffInvitationAcceptanceError,
  staffInvitationAcceptanceFormSchema,
  useAcceptStaffInvitation,
  useStaffInvitationPreview,
  type StaffInvitationAcceptanceFormInput,
} from '@/features/staff-access/api/staff-invitation-acceptance';

const staffRoleLabels: Record<string, string> = {
  administrador: 'administrador',
  financeiro: 'financeiro',
  professor: 'professor',
  suporte: 'suporte',
};

export const StaffInvitationAcceptanceScreen = () => {
  const [token] = useState(() => new URLSearchParams(window.location.search).get('token') ?? '');
  const [requestError, setRequestError] = useState<string | null>(null);
  const navigate = useNavigate();
  const preview = useStaffInvitationPreview(token, Boolean(token));
  const acceptInvitation = useAcceptStaffInvitation();
  const form = useForm<StaffInvitationAcceptanceFormInput>({
    defaultValues: { name: '', password: '' },
    resolver: zodResolver(staffInvitationAcceptanceFormSchema),
  });

  useLayoutEffect(() => {
    const currentUrl = new URL(window.location.href);
    if (currentUrl.searchParams.has('token')) {
      currentUrl.search = '';
      currentUrl.hash = '';
      window.history.replaceState(window.history.state, '', currentUrl.toString());
    }

  }, []);

  const submitAcceptance = async (input: StaffInvitationAcceptanceFormInput) => {
    setRequestError(null);
    try {
      await acceptInvitation.mutateAsync({ token, input });
      await navigate(paths.home.getHref(), { replace: true });
    } catch (error: unknown) {
      setRequestError(getStaffInvitationAcceptanceError(error));
    }
  };

  return (
    <main className="password-reset-page">
      <section aria-labelledby="invitation-acceptance-title" className="password-reset-card">
        <p className="eyebrow">Acesso interno</p>
        <h1 id="invitation-acceptance-title">Aceite seu convite</h1>

        {!token ? (
          <>
            <p role="alert">Este convite não vale mais. Peça um novo ao administrador.</p>
            <Link className="primary-link" to={paths.staffLogin.getHref()}>Ir para entrar</Link>
          </>
        ) : null}

        {token && preview.isPending ? <p role="status">Validando convite…</p> : null}
        {token && preview.isError ? (
          <>
            <p role="alert">{getStaffInvitationAcceptanceError(preview.error)}</p>
            <Link className="primary-link" to={paths.staffLogin.getHref()}>Ir para entrar</Link>
          </>
        ) : null}

        {token && preview.data ? (
          <>
            <p>Você recebeu o papel de {staffRoleLabels[preview.data.offeredRole] ?? preview.data.offeredRole}.</p>
            <p>
              Válido até{' '}
              <time dateTime={preview.data.expiresAt}>{new Date(preview.data.expiresAt).toLocaleString('pt-BR')}</time>.
            </p>
            {requestError ? <p role="alert">{requestError}</p> : null}
            <form noValidate onSubmit={(event) => void form.handleSubmit(submitAcceptance)(event)}>
              <label htmlFor="invitation-name">Nome</label>
              <input
                autoComplete="name"
                id="invitation-name"
                type="text"
                {...form.register('name')}
                aria-invalid={Boolean(form.formState.errors.name)}
                aria-describedby={form.formState.errors.name ? 'invitation-name-error' : undefined}
              />
              {form.formState.errors.name ? (
                <p id="invitation-name-error" role="alert">{form.formState.errors.name.message}</p>
              ) : null}

              <label htmlFor="invitation-password">Senha</label>
              <input
                autoComplete="new-password"
                id="invitation-password"
                type="password"
                {...form.register('password')}
                aria-invalid={Boolean(form.formState.errors.password)}
                aria-describedby={form.formState.errors.password ? 'invitation-password-error' : undefined}
              />
              {form.formState.errors.password ? (
                <p id="invitation-password-error" role="alert">{form.formState.errors.password.message}</p>
              ) : null}
              <p className="password-reset-hint">
                Use 8 caracteres ou mais, com letra maiúscula, minúscula, número e símbolo.
              </p>
              <button disabled={acceptInvitation.isPending} type="submit">
                {acceptInvitation.isPending ? 'Ativando conta…' : 'Aceitar convite'}
              </button>
            </form>
          </>
        ) : null}
      </section>
    </main>
  );
};
