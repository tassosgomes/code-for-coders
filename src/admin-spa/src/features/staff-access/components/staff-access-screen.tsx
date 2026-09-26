import { useEffect, useRef, useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Ellipsis, X } from 'lucide-react';

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
  const [tab, setTab] = useState<'people' | 'invitations'>('people');
  const [dialog, setDialog] = useState<'invite' | 'grant' | 'revoke' | 'change' | null>(null);
  const [selectedMember, setSelectedMember] = useState<StaffMember | null>(null);
  const [menuMember, setMenuMember] = useState<string | null>(null);
  const [inviteSent, setInviteSent] = useState(false);
  const dialogRef = useRef<HTMLElement>(null);
  useEffect(() => {
    if (dialog) dialogRef.current?.querySelector('button')?.focus();
  }, [dialog]);
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
  const invitationReason = useWatch({ control: form.control, name: 'reason', defaultValue: '' });

  const submitInvitation = async (input: CreateStaffInvitationInput) => {
    setRequestError(null);
    try {
      await createInvitation.mutateAsync({ input });
      form.reset({ email: '', role: 'professor', reason: '' });
      setInviteSent(true);
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

  const openMemberDialog = (member: StaffMember, kind: 'grant' | 'revoke' | 'change') => {
    setSelectedMember(member);
    setRoleActionError(null);
    setDialog(kind);
    setMenuMember(null);
  };

  return (
    <main className="page-shell staff-access-page">
      <div className="page-heading-row"><div><p className="eyebrow">Acessos</p><h1>Equipe e papéis</h1><p className="page-subtitle">Quem opera a escola e o que cada pessoa pode fazer.</p></div>
        <button className="primary-button" onClick={() => { setInviteSent(false); setRequestError(null); setDialog('invite'); }} type="button">Convidar</button>
      </div>
      {roleActionStatus ? <p className="inline-alert" role="status">{roleActionStatus}</p> : null}
      <div aria-label="Acessos" className="access-tabs" role="tablist">
        <button aria-selected={tab === 'people'} className={tab === 'people' ? 'active' : ''} onClick={() => setTab('people')} role="tab" type="button">Pessoas · {members.data?.data.length ?? 0}</button>
        <button aria-selected={tab === 'invitations'} className={tab === 'invitations' ? 'active' : ''} onClick={() => setTab('invitations')} role="tab" type="button">Convites pendentes · {invitations.data?.data.length ?? 0}</button>
      </div>
      {tab === 'people' ? <section aria-label="Pessoas com acesso" className="access-table" role="tabpanel">
        <div className="access-table-header staff-grid"><span>Nome</span><span>E-mail</span><span>Papéis</span><span /></div>
        {members.isPending ? <p className="table-message" role="status">Carregando pessoas…</p> : null}
        {members.isError ? <p className="table-message" role="alert">Não foi possível carregar as pessoas com acesso.</p> : null}
        {members.data?.data.length === 0 ? <p className="table-message">Nenhuma conta interna.</p> : null}
        {members.data?.data.map((member) => <div className="access-row staff-grid" key={member.accountId}>
          <div className="staff-name"><span className="small-avatar">{member.name.split(/\s+/).slice(0, 2).map((part) => part[0]).join('').toUpperCase()}</span><span><strong>{member.name}</strong>{member.isSelf ? <small className="self-badge">você</small> : null}</span></div>
          <span className="row-muted">{member.email}</span>
          <span className="role-badges">{member.roles.length ? member.roles.map((role) => <span className="role-badge" key={role}>{role}</span>) : <span className="role-badge role-badge-empty">Sem papel</span>}</span>
          {member.isSelf ? <span className="row-muted">—</span> : <div className="row-menu-wrap"><button aria-expanded={menuMember === member.accountId} aria-label={`Ações para ${member.name}`} className="icon-button" onClick={() => setMenuMember(menuMember === member.accountId ? null : member.accountId)} type="button"><Ellipsis size={18} /></button>
            {menuMember === member.accountId ? <div className="row-menu"><button onClick={() => openMemberDialog(member, 'grant')} type="button">Conceder papel</button><button disabled={!member.roles.length} onClick={() => openMemberDialog(member, 'revoke')} type="button">Revogar papel</button><button disabled={!member.roles.length || member.roles.length === staffInvitationRoles.length} onClick={() => openMemberDialog(member, 'change')} type="button">Trocar papel</button></div> : null}</div>}
        </div>)}
      </section> : <section aria-label="Convites pendentes" className="access-table" role="tabpanel">
        <div className="access-table-header invite-grid"><span>E-mail</span><span>Papel oferecido</span><span>Vale até</span><span /></div>
        {invitations.isPending ? <p className="table-message" role="status">Carregando convites…</p> : null}
        {invitations.isError ? <p className="table-message" role="alert">Não foi possível carregar os convites pendentes.</p> : null}
        {invitations.data?.data.length === 0 ? <p className="table-message">Nenhum convite pendente.</p> : null}
        {invitations.data?.data.map((invitation) => <div className="access-row invite-grid" key={invitation.invitationId}><strong>{invitation.email}</strong><span className="role-badge">{invitation.offeredRole}</span><time className="row-muted" dateTime={invitation.expiresAt}>{new Date(invitation.expiresAt).toLocaleString('pt-BR')}</time><button aria-label={`Enviar novo convite para ${invitation.email}`} className="icon-button" onClick={() => { form.reset({ email: invitation.email, role: invitation.offeredRole, reason: '' }); setInviteSent(false); setDialog('invite'); }} type="button"><Ellipsis size={18} /></button></div>)}
      </section>}
      {dialog ? <div className="dialog-backdrop" onMouseDown={(event) => { if (event.target === event.currentTarget) setDialog(null); }}><section aria-labelledby="dialog-title" aria-modal="true" className="dialog-card" onKeyDown={(event) => { if (event.key === 'Escape') setDialog(null); }} ref={dialogRef} role="dialog"><button aria-label="Fechar" className="dialog-close" onClick={() => setDialog(null)} type="button"><X size={18} /></button>
        {dialog === 'invite' ? <><h2 id="dialog-title">{inviteSent ? 'Convite enviado' : 'Convidar para a equipe'}</h2>{inviteSent ? <><p>O convite foi enviado para {createInvitation.data?.email}. O link vale por 7 dias.</p>{createInvitation.data?.supersededInvitationId ? <p>O convite anterior para este e-mail deixou de valer.</p> : null}<div className="dialog-actions"><button className="primary-button" onClick={() => setDialog(null)} type="button">Concluir</button></div></> : <><p>A pessoa recebe um link por e-mail e define a própria senha. O convite vale 7 dias.</p>
          {requestError ? <p className="inline-alert" role="alert">{requestError}</p> : null}
          <form noValidate onSubmit={(event) => void form.handleSubmit(submitInvitation)(event)}><label htmlFor="invitation-email">E-mail</label><input autoComplete="email" id="invitation-email" type="email" {...form.register('email')} aria-invalid={Boolean(form.formState.errors.email)} />{form.formState.errors.email ? <p role="alert">{form.formState.errors.email.message}</p> : null}
            <fieldset className="role-options"><legend>Papel</legend>{staffInvitationRoles.map((role) => <label className="role-option" key={role}><input type="radio" value={role} {...form.register('role')} /><span><strong>{role}</strong><small>{({ professor: 'Autoria de cursos', suporte: 'Atendimento a alunos', financeiro: 'Vendas, pedidos e repasses', administrador: 'Gestão de acessos da equipe' } as Record<string, string>)[role]}</small></span></label>)}</fieldset>
            <label htmlFor="invitation-reason">Motivo</label><textarea id="invitation-reason" maxLength={500} rows={3} {...form.register('reason')} aria-invalid={Boolean(form.formState.errors.reason)} /><p className="field-hint">Fica registrado na auditoria. Não cite dados pessoais de outras pessoas. <span>{invitationReason.length}/500</span></p>{form.formState.errors.reason ? <p role="alert">{form.formState.errors.reason.message}</p> : null}
            <div className="dialog-actions"><button className="outline-button" onClick={() => setDialog(null)} type="button">Cancelar</button><button className="primary-button" disabled={createInvitation.isPending} type="submit">{createInvitation.isPending ? 'Enviando…' : 'Enviar convite'}</button></div></form></>}</> : null}
        {selectedMember && dialog !== 'invite' ? <><h2 id="dialog-title">{dialog === 'grant' ? 'Conceder papel' : dialog === 'revoke' ? 'Revogar papel' : 'Trocar papel'}</h2><p>{selectedMember.name} · {selectedMember.email}</p>{dialog !== 'grant' ? <p className="warning-alert">A pessoa será desconectada agora.</p> : null}{roleActionError ? <p className="inline-alert" role="alert">{roleActionError}</p> : null}
          {dialog === 'change' ? <StaffRoleChangeActions disabled={changeRole.isPending} key={selectedMember.accountId} member={selectedMember} onAction={async (member, input, key) => { const ok = await submitRoleChange(member, input, key); if (ok) setDialog(null); return ok; }} /> : <StaffMemberActions action={dialog} disabled={grantRole.isPending || revokeRole.isPending} key={`${selectedMember.accountId}:${dialog}`} member={selectedMember} onAction={async (member, action, input, key) => { const ok = await submitRoleAction(member, action, input, key); if (ok) setDialog(null); return ok; }} />}</> : null}
      </section></div> : null}
    </main>
  );
};

type StaffMemberActionsProps = {
  action: StaffRoleActionKind;
  disabled: boolean;
  member: StaffMember;
  onAction: (
    member: StaffMember,
    action: StaffRoleActionKind,
    input: StaffRoleActionInput,
    idempotencyKey: string,
  ) => Promise<boolean>;
};

const StaffMemberActions = ({ action, disabled, member, onAction }: StaffMemberActionsProps) => {
  const idempotency = useRef<{ fingerprint: string; key: string } | null>(null);
  const form = useForm<StaffRoleActionInput>({
    defaultValues: { role: (action === 'grant' ? staffInvitationRoles.find((role) => !member.roles.includes(role)) : member.roles[0]) ?? 'professor', reason: '' },
    resolver: zodResolver(staffRoleActionSchema),
  });
  const reason = useWatch({ control: form.control, name: 'reason', defaultValue: '' });
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
        {(action === 'grant' ? staffInvitationRoles.filter((role) => !member.roles.includes(role)) : member.roles).map((role) => <option key={role} value={role}>{role}</option>)}
      </select>

      <label htmlFor={`staff-reason-${member.accountId}`}>Motivo para {member.name}</label>
      <textarea
        id={`staff-reason-${member.accountId}`}
        maxLength={500}
        rows={2}
        {...form.register('reason')}
        aria-invalid={Boolean(form.formState.errors.reason)}
      />
      {form.formState.errors.reason ? <p role="alert">{form.formState.errors.reason.message}</p> : null}

      <p className="field-hint">Fica registrado na auditoria. Não cite dados pessoais de outras pessoas. <span>{reason.length}/500</span></p>
      <div className="dialog-actions"><button className="primary-button" disabled={disabled} onClick={() => void execute(action)} type="button">{action === 'grant' ? 'Conceder papel' : 'Revogar papel'}</button></div>
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
  const reason = useWatch({ control: form.control, name: 'reason', defaultValue: '' });

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
        {staffInvitationRoles.filter((role) => !member.roles.includes(role)).map((role) => <option key={role} value={role}>{role}</option>)}
      </select>
      {form.formState.errors.toRole ? <p role="alert">{form.formState.errors.toRole.message}</p> : null}

      <label htmlFor={`staff-role-change-reason-${member.accountId}`}>Motivo para trocar o papel de {member.name}</label>
      <textarea
        id={`staff-role-change-reason-${member.accountId}`}
        maxLength={500}
        rows={2}
        {...form.register('reason')}
        aria-invalid={Boolean(form.formState.errors.reason)}
      />
      {form.formState.errors.reason ? <p role="alert">{form.formState.errors.reason.message}</p> : null}

      <p className="field-hint">Fica registrado na auditoria. Não cite dados pessoais de outras pessoas. <span>{reason.length}/500</span></p>
      <button className="primary-button"
        disabled={disabled || member.roles.length === 0}
        onClick={() => void execute()}
        type="button"
      >
        Trocar papel
      </button>
    </form>
  );
};
