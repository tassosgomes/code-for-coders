import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';

import {
  createStaffInvitationSchema,
  getStaffInvitationRequestError,
  staffInvitationRoles,
  useCreateStaffInvitation,
  usePendingStaffInvitations,
  type CreateStaffInvitationInput,
} from '@/features/staff-access/api/staff-invitations';

export const StaffAccessScreen = () => {
  const [requestError, setRequestError] = useState<string | null>(null);
  const invitations = usePendingStaffInvitations();
  const createInvitation = useCreateStaffInvitation();
  const form = useForm<CreateStaffInvitationInput>({
    defaultValues: { email: '', role: 'professor', reason: '' },
    resolver: zodResolver(createStaffInvitationSchema),
  });

  const submitInvitation = async (input: CreateStaffInvitationInput) => {
    setRequestError(null);
    try {
      await createInvitation.mutateAsync({ input });
      form.reset({ email: '', role: 'professor', reason: '' });
    } catch (error: unknown) {
      setRequestError(getStaffInvitationRequestError(error));
    }
  };

  return (
    <main className="page-shell staff-access-page">
      <p className="eyebrow">Gestão de acesso</p>
      <h1>Acessos</h1>

      <section aria-labelledby="staff-invitation-title" className="status-card">
        <h2 id="staff-invitation-title">Convidar pessoa</h2>
        {requestError ? <p role="alert">{requestError}</p> : null}
        {createInvitation.isSuccess ? (
          <p role="status">Convite enviado para {createInvitation.data.email}.</p>
        ) : null}
        <form noValidate onSubmit={(event) => void form.handleSubmit(submitInvitation)(event)}>
          <label htmlFor="invitation-email">E-mail</label>
          <input
            autoComplete="email"
            id="invitation-email"
            type="email"
            {...form.register('email')}
            aria-invalid={Boolean(form.formState.errors.email)}
            aria-describedby={form.formState.errors.email ? 'invitation-email-error' : undefined}
          />
          {form.formState.errors.email ? (
            <p id="invitation-email-error" role="alert">{form.formState.errors.email.message}</p>
          ) : null}

          <label htmlFor="invitation-role">Papel</label>
          <select id="invitation-role" {...form.register('role')}>
            {staffInvitationRoles.map((role) => <option key={role} value={role}>{role}</option>)}
          </select>

          <label htmlFor="invitation-reason">Motivo</label>
          <textarea
            id="invitation-reason"
            rows={3}
            {...form.register('reason')}
            aria-invalid={Boolean(form.formState.errors.reason)}
            aria-describedby={form.formState.errors.reason ? 'invitation-reason-error' : undefined}
          />
          {form.formState.errors.reason ? (
            <p id="invitation-reason-error" role="alert">{form.formState.errors.reason.message}</p>
          ) : null}

          <button disabled={createInvitation.isPending} type="submit">
            {createInvitation.isPending ? 'Enviando convite…' : 'Convidar'}
          </button>
        </form>
      </section>

      <section aria-labelledby="pending-invitations-title" className="status-card">
        <h2 id="pending-invitations-title">Convites pendentes</h2>
        {invitations.isPending ? <p role="status">Carregando convites…</p> : null}
        {invitations.isError ? <p role="alert">Não foi possível carregar os convites pendentes.</p> : null}
        {invitations.data?.data.length === 0 ? <p>Nenhum convite pendente.</p> : null}
        {invitations.data && invitations.data.data.length > 0 ? (
          <ul>
            {invitations.data.data.map((invitation) => (
              <li key={invitation.invitationId}>
                <strong>{invitation.email}</strong>
                <span> · {invitation.offeredRole}</span>
                <span> · válido até {new Date(invitation.expiresAt).toLocaleString('pt-BR')}</span>
              </li>
            ))}
          </ul>
        ) : null}
      </section>
    </main>
  );
};
