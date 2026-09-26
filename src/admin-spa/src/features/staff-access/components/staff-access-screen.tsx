import { useRef, useState } from 'react';
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
import {
  getStaffRoleActionRequestError,
  staffRoleActionSchema,
  staffRoleChangeSchema,
  useChangeStaffRole,
  useGrantStaffRole,
  useRevokeStaffRole,
  useStaffMembers,
  type StaffMember,
  type StaffRoleActionInput,
  type StaffRoleActionKind,
  type StaffRoleChangeInput,
} from '@/features/staff-access/api/staff-members';

export const StaffAccessScreen = () => {
  const [requestError, setRequestError] = useState<string | null>(null);
  const [roleActionError, setRoleActionError] = useState<string | null>(null);
  const [roleActionStatus, setRoleActionStatus] = useState<string | null>(null);
  const invitations = usePendingStaffInvitations();
  const members = useStaffMembers();
  const createInvitation = useCreateStaffInvitation();
  const grantRole = useGrantStaffRole();
  const revokeRole = useRevokeStaffRole();
  const changeRole = useChangeStaffRole();
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

  const submitRoleAction = async (
    member: StaffMember,
    action: StaffRoleActionKind,
    input: StaffRoleActionInput,
    idempotencyKey: string,
  ): Promise<boolean> => {
    setRoleActionError(null);
    setRoleActionStatus(null);
    try {
      const command = { accountId: member.accountId, input, idempotencyKey };
      const result = action === 'grant'
        ? await grantRole.mutateAsync(command)
        : await revokeRole.mutateAsync(command);
      if (!result.changed) {
        setRoleActionStatus(action === 'grant'
          ? `${input.role} já estava concedido a ${member.name}.`
          : `${input.role} já estava ausente de ${member.name}.`);
      } else if (action === 'revoke') {
        setRoleActionStatus(`${input.role} revogado; ${member.name} foi desconectada agora.`);
      } else {
        setRoleActionStatus(`${input.role} concedido a ${member.name}.`);
      }

      return true;
    } catch (error: unknown) {
      setRoleActionError(getStaffRoleActionRequestError(error));
      return false;
    }
  };

  const submitRoleChange = async (
    member: StaffMember,
    input: StaffRoleChangeInput,
    idempotencyKey: string,
  ): Promise<boolean> => {
    setRoleActionError(null);
    setRoleActionStatus(null);
    try {
      const result = await changeRole.mutateAsync({
        accountId: member.accountId,
        input,
        idempotencyKey,
      });
      setRoleActionStatus(result.changed
        ? `${input.fromRole} substituído por ${input.toRole}; ${member.name} foi desconectada agora.`
        : `Nenhuma alteração de papel foi necessária para ${member.name}.`);
      return true;
    } catch (error: unknown) {
      setRoleActionError(getStaffRoleActionRequestError(error));
      return false;
    }
  };

  return (
    <main className="page-shell staff-access-page">
      <p className="eyebrow">Gestão de acesso</p>
      <h1>Acessos</h1>

      <section aria-labelledby="staff-members-title" className="status-card">
        <h2 id="staff-members-title">Pessoas com acesso</h2>
        <p>Ao revogar ou trocar um papel, a pessoa será desconectada agora.</p>
        {roleActionError ? <p role="alert">{roleActionError}</p> : null}
        {roleActionStatus ? <p role="status">{roleActionStatus}</p> : null}
        {members.isPending ? <p role="status">Carregando pessoas…</p> : null}
        {members.isError ? <p role="alert">Não foi possível carregar as pessoas com acesso.</p> : null}
        {members.data?.data.length === 0 ? <p>Nenhuma conta interna.</p> : null}
        {members.data && members.data.data.length > 0 ? (
          <ul>
            {members.data.data.map((member) => (
              <li key={member.accountId}>
                <strong>{member.name}</strong>
                {member.isSelf ? <span> (você)</span> : null}
                <span> · {member.email}</span>
                <p>Papéis: {member.roles.length > 0 ? member.roles.join(', ') : 'Sem papel'}</p>
                {!member.isSelf ? (
                  <>
                    <StaffMemberActions
                      disabled={grantRole.isPending || revokeRole.isPending || changeRole.isPending}
                      member={member}
                      onAction={submitRoleAction}
                    />
                    <StaffRoleChangeActions
                      disabled={grantRole.isPending || revokeRole.isPending || changeRole.isPending}
                      key={`${member.accountId}:${member.roles.join('|')}`}
                      member={member}
                      onAction={submitRoleChange}
                    />
                  </>
                ) : null}
              </li>
            ))}
          </ul>
        ) : null}
      </section>

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

type StaffMemberActionsProps = {
  disabled: boolean;
  member: StaffMember;
  onAction: (
    member: StaffMember,
    action: StaffRoleActionKind,
    input: StaffRoleActionInput,
    idempotencyKey: string,
  ) => Promise<boolean>;
};

const StaffMemberActions = ({ disabled, member, onAction }: StaffMemberActionsProps) => {
  const idempotency = useRef<{ fingerprint: string; key: string } | null>(null);
  const form = useForm<StaffRoleActionInput>({
    defaultValues: { role: staffInvitationRoles.find((role) => !member.roles.includes(role)) ?? 'professor', reason: '' },
    resolver: zodResolver(staffRoleActionSchema),
  });
  const execute = async (action: StaffRoleActionKind) => {
    await form.handleSubmit(async (input) => {
      const fingerprint = JSON.stringify([action, input.role, input.reason]);
      if (idempotency.current?.fingerprint !== fingerprint) {
        idempotency.current = { fingerprint, key: crypto.randomUUID() };
      }

      const succeeded = await onAction(member, action, input, idempotency.current.key);
      if (succeeded) {
        idempotency.current = null;
        form.reset({ role: input.role, reason: '' });
      }
    })();
  };

  return (
    <form noValidate onSubmit={(event) => event.preventDefault()}>
      <label htmlFor={`staff-role-${member.accountId}`}>Papel para {member.name}</label>
      <select id={`staff-role-${member.accountId}`} {...form.register('role')}>
        {staffInvitationRoles.map((role) => <option key={role} value={role}>{role}</option>)}
      </select>

      <label htmlFor={`staff-reason-${member.accountId}`}>Motivo para {member.name}</label>
      <textarea
        id={`staff-reason-${member.accountId}`}
        maxLength={1000}
        rows={2}
        {...form.register('reason')}
        aria-invalid={Boolean(form.formState.errors.reason)}
      />
      {form.formState.errors.reason ? <p role="alert">{form.formState.errors.reason.message}</p> : null}

      <button disabled={disabled} onClick={() => void execute('grant')} type="button">
        Conceder
      </button>
      <button disabled={disabled} onClick={() => void execute('revoke')} type="button">
        Revogar
      </button>
    </form>
  );
};

type StaffRoleChangeActionsProps = {
  disabled: boolean;
  member: StaffMember;
  onAction: (
    member: StaffMember,
    input: StaffRoleChangeInput,
    idempotencyKey: string,
  ) => Promise<boolean>;
};

const StaffRoleChangeActions = ({ disabled, member, onAction }: StaffRoleChangeActionsProps) => {
  const idempotency = useRef<{ fingerprint: string; key: string } | null>(null);
  const initialFromRole = member.roles[0] ?? 'professor';
  const initialToRole = staffInvitationRoles.find((role) => role !== initialFromRole) ?? initialFromRole;
  const form = useForm<StaffRoleChangeInput>({
    defaultValues: { fromRole: initialFromRole, toRole: initialToRole, reason: '' },
    resolver: zodResolver(staffRoleChangeSchema),
  });

  const execute = async () => {
    await form.handleSubmit(async (input) => {
      const fingerprint = JSON.stringify([input.fromRole, input.toRole, input.reason]);
      if (idempotency.current?.fingerprint !== fingerprint) {
        idempotency.current = { fingerprint, key: crypto.randomUUID() };
      }

      const succeeded = await onAction(member, input, idempotency.current.key);
      if (succeeded) {
        idempotency.current = null;
        const nextToRole = staffInvitationRoles.find((role) => role !== input.toRole) ?? input.toRole;
        form.reset({ fromRole: input.toRole, toRole: nextToRole, reason: '' });
      }
    })();
  };

  return (
    <form noValidate onSubmit={(event) => event.preventDefault()}>
      <label htmlFor={`staff-role-from-${member.accountId}`}>Papel atual para trocar de {member.name}</label>
      <select id={`staff-role-from-${member.accountId}`} {...form.register('fromRole')}>
        {member.roles.length === 0
          ? <option value="">Sem papel atual</option>
          : member.roles.map((role) => <option key={role} value={role}>{role}</option>)}
      </select>
      {form.formState.errors.fromRole ? <p role="alert">{form.formState.errors.fromRole.message}</p> : null}

      <label htmlFor={`staff-role-to-${member.accountId}`}>Novo papel para {member.name}</label>
      <select id={`staff-role-to-${member.accountId}`} {...form.register('toRole')}>
        {staffInvitationRoles.map((role) => <option key={role} value={role}>{role}</option>)}
      </select>
      {form.formState.errors.toRole ? <p role="alert">{form.formState.errors.toRole.message}</p> : null}

      <label htmlFor={`staff-role-change-reason-${member.accountId}`}>Motivo para trocar o papel de {member.name}</label>
      <textarea
        id={`staff-role-change-reason-${member.accountId}`}
        maxLength={1000}
        rows={2}
        {...form.register('reason')}
        aria-invalid={Boolean(form.formState.errors.reason)}
      />
      {form.formState.errors.reason ? <p role="alert">{form.formState.errors.reason.message}</p> : null}

      <button
        disabled={disabled || member.roles.length === 0}
        onClick={() => void execute()}
        type="button"
      >
        Trocar papel
      </button>
    </form>
  );
};
