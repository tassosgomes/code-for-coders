import { useLayoutEffect, useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { Link, useNavigate } from 'react-router';
import { CircleAlert, Eye, EyeOff } from 'lucide-react';

import { zodResolver } from '@hookform/resolvers/zod';
import axios from 'axios';

import { paths } from '@/config/paths';
import { AuthLayout } from '@/components/auth-layout';
import { AuthNotice } from '@/components/auth-notice';
import { AuthResultCard } from '@/components/auth-result-card';
import { PasswordRequirements } from '@/components/password-requirements';
import {
  getStaffInvitationAcceptanceError,
  staffInvitationAcceptanceFormSchema,
  useAcceptStaffInvitation,
  useStaffInvitationPreview,
  type StaffInvitationAcceptanceFormInput,
} from '@/features/staff-access/api/staff-invitation-acceptance';

const staffRoleLabels: Record<string, string> = {
  administrador: 'Administrador',
  financeiro: 'Financeiro',
  professor: 'Professor',
  suporte: 'Suporte',
};

const formatInvitationExpiry = (value: string) => {
  const date = new Date(value);
  const day = date.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric' });
  const time = date.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
  return `${day} às ${time}`;
};

const getErrorCode = (error: unknown) =>
  axios.isAxiosError<{ code?: unknown }>(error) ? error.response?.data?.code : undefined;

const InvitationUnavailable = ({ error }: { error?: unknown }) => {
  const code = error === undefined ? 'INVITATION_INVALID' : getErrorCode(error);
  const isInvalid = code === 'INVITATION_INVALID';
  const isEmailUnavailable = code === 'INVITATION_EMAIL_UNAVAILABLE';
  const title = isInvalid
    ? 'Este convite não vale mais'
    : isEmailUnavailable ? 'E-mail indisponível' : 'Não foi possível abrir o convite';
  const description = isInvalid
    ? 'Ele expirou, já foi usado ou foi substituído por um mais novo. Peça um novo ao administrador que te convidou.'
    : getStaffInvitationAcceptanceError(error);

  return (
    <AuthResultCard alert description={description} icon={CircleAlert} title={title} titleId="invitation-acceptance-title" tone="warning">
      <Link className="primary-link" to={paths.staffLogin.getHref()}>Já tem conta? Entrar</Link>
    </AuthResultCard>
  );
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

  if (!token) {
    return <AuthLayout><InvitationUnavailable /></AuthLayout>;
  }

  if (preview.isError) {
    return <AuthLayout><InvitationUnavailable error={preview.error} /></AuthLayout>;
  }

  if (!preview.data) {
    return (
      <AuthLayout>
        <section aria-busy="true" aria-labelledby="invitation-loading-title" className="password-reset-card auth-card auth-skeleton-card">
          <div aria-hidden="true" className="auth-skeleton-tile" />
          <div aria-hidden="true" className="auth-skeleton-lines">
            <span className="auth-skeleton-bar auth-skeleton-title" />
            <span className="auth-skeleton-bar" />
            <span className="auth-skeleton-bar auth-skeleton-short" />
          </div>
          <p id="invitation-loading-title" role="status">Conferindo seu convite…</p>
        </section>
      </AuthLayout>
    );
  }

  return (
    <AuthLayout>
      <section aria-labelledby="invitation-acceptance-title" className="password-reset-card auth-card">
        <div className="auth-card-header">
          <p className="eyebrow">Backoffice</p>
          <h1 id="invitation-acceptance-title">Você foi convidado para a equipe</h1>
          <p>Crie seu acesso para entrar no backoffice. O e-mail é o mesmo que recebeu este convite.</p>
        </div>
        <dl className="auth-invitation-summary">
          <div>
            <dt>Papel oferecido</dt>
            <dd><span className="auth-role-badge">{staffRoleLabels[preview.data.offeredRole] ?? preview.data.offeredRole}</span></dd>
          </div>
          <div>
            <dt>Vale até</dt>
            <dd><time dateTime={preview.data.expiresAt}>{formatInvitationExpiry(preview.data.expiresAt)}</time></dd>
          </div>
        </dl>
        {requestError ? <AuthNotice role="alert" tone="danger">{requestError}</AuthNotice> : null}
        <form noValidate onSubmit={(event) => void form.handleSubmit(submitAcceptance)(event)}>
          <div className="auth-field">
            <label htmlFor="invitation-name">Seu nome</label>
            <input
              autoComplete="name"
              id="invitation-name"
              placeholder="Seu nome completo"
              type="text"
              {...form.register('name')}
              aria-invalid={Boolean(form.formState.errors.name)}
              aria-describedby={form.formState.errors.name ? 'invitation-name-error' : undefined}
            />
            {form.formState.errors.name ? (
              <p id="invitation-name-error" role="alert">{form.formState.errors.name.message}</p>
            ) : null}
          </div>
          <div className="auth-field">
            <label htmlFor="invitation-password">Crie uma senha</label>
            <div className="password-input"><input
              autoComplete="new-password"
              id="invitation-password"
              type={showPassword ? 'text' : 'password'}
              {...form.register('password')}
              aria-invalid={Boolean(form.formState.errors.password)}
              aria-describedby={form.formState.errors.password ? 'invitation-password-error' : undefined}
            /><button aria-label={showPassword ? 'Ocultar senha' : 'Mostrar senha'} className="visibility-button" onClick={() => setShowPassword(!showPassword)} type="button">{showPassword ? <EyeOff size={16} /> : <Eye size={16} />}</button></div>
            <PasswordRequirements password={password} />
            {form.formState.errors.password ? (
              <p id="invitation-password-error" role="alert">{form.formState.errors.password.message}</p>
            ) : null}
          </div>
          <button disabled={acceptInvitation.isPending} type="submit">
            {acceptInvitation.isPending ? 'Ativando conta…' : 'Aceitar e entrar'}
          </button>
        </form>
      </section>
    </AuthLayout>
  );
};
