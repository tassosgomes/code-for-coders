import { useLayoutEffect, useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { Link, useNavigate } from 'react-router';
import { Eye, EyeOff } from 'lucide-react';

import { zodResolver } from '@hookform/resolvers/zod';

import { paths } from '@/config/paths';
import { AuthLayout } from '@/components/auth-layout';
import { PasswordRequirements } from '@/components/password-requirements';
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
  const [showPassword, setShowPassword] = useState(false);
  const navigate = useNavigate();
  const preview = useStaffInvitationPreview(token, Boolean(token));
  const acceptInvitation = useAcceptStaffInvitation();
  const form = useForm<StaffInvitationAcceptanceFormInput>({
    defaultValues: { name: '', password: '' },
    resolver: zodResolver(staffInvitationAcceptanceFormSchema),
  });
  const password = useWatch({ control: form.control, name: 'password', defaultValue: '' });

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
    <AuthLayout>
      <section aria-labelledby="invitation-acceptance-title" className="password-reset-card">
        <p className="eyebrow">Backoffice</p>
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
              <div className="password-input"><input
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
                type={showPassword ? 'text' : 'password'}
                {...form.register('password')}
                aria-invalid={Boolean(form.formState.errors.password)}
                aria-describedby={form.formState.errors.password ? 'invitation-password-error' : undefined}
              /><button aria-label={showPassword ? 'Ocultar senha' : 'Mostrar senha'} className="visibility-button" onClick={() => setShowPassword(!showPassword)} type="button">{showPassword ? <EyeOff size={16} /> : <Eye size={16} />}</button></div>
              {form.formState.errors.password ? (
                <p id="invitation-password-error" role="alert">{form.formState.errors.password.message}</p>
              ) : null}
              <PasswordRequirements password={password} />
              <button disabled={acceptInvitation.isPending} type="submit">
                {acceptInvitation.isPending ? 'Ativando conta…' : 'Aceitar convite'}
              </button>
            </form>
          </>
        ) : null}
      </section>
    </AuthLayout>
  );
};
