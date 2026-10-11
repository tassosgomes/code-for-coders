import { useEffect, useState } from 'react';

import { usePendingStaffInvitations, type PendingStaffInvitation } from '@/features/staff-access/api/staff-invitations';
import { useStaffMembers, type StaffMember } from '@/features/staff-access/api/staff-members';
import { ChangeRoleDialog } from '@/features/staff-access/components/change-role-dialog';
import { GrantRoleDialog } from '@/features/staff-access/components/grant-role-dialog';
import { InvitationRow } from '@/features/staff-access/components/invitation-row';
import { InviteDialog } from '@/features/staff-access/components/invite-dialog';
import { RevokeRoleDialog } from '@/features/staff-access/components/revoke-role-dialog';
import { StaffMemberRow } from '@/features/staff-access/components/staff-member-row';
import type { StaffRole } from '@/features/staff-access/components/staff-role-labels';
import { Toast } from '@/components/toast';

type ActiveDialog =
  | { kind: 'invite'; initial?: { email: string; role: StaffRole } }
  | { kind: 'grant'; member: StaffMember }
  | { kind: 'revoke'; member: StaffMember; role: StaffRole }
  | { kind: 'change'; member: StaffMember };

const toastDurationMs = 5000;

export const StaffAccessScreen = () => {
  const [tab, setTab] = useState<'people' | 'invitations'>('people');
  const [dialog, setDialog] = useState<ActiveDialog | null>(null);
  const [toast, setToast] = useState<string | null>(null);
  const invitations = usePendingStaffInvitations();
  const members = useStaffMembers();
  useEffect(() => {
    if (!toast) return;
    const timer = window.setTimeout(() => setToast(null), toastDurationMs);
    return () => window.clearTimeout(timer);
  }, [toast]);

  const closeDialog = () => setDialog(null);
  const finishDialog = (message: string) => {
    setDialog(null);
    setToast(message);
  };
  const resend = (invitation: PendingStaffInvitation) => setDialog({ kind: 'invite', initial: { email: invitation.email, role: invitation.offeredRole } });
  const pagination = members.data?.pagination;
  const peopleTotal = pagination?.total ?? members.data?.data.length ?? 0;
  const invitationTotal = invitations.data?.data.length ?? 0;

  return (
    <main className="page-shell staff-access-page">
      <div className="page-heading-row"><div><p className="eyebrow">Acessos</p><h1>Equipe e papéis</h1><p className="page-subtitle">Quem opera a escola e o que cada pessoa pode fazer.</p></div>
        <button className="primary-button" onClick={() => setDialog({ kind: 'invite' })} type="button">Convidar</button>
      </div>
      <div aria-label="Acessos" className="access-tabs" role="tablist">
        <button aria-selected={tab === 'people'} className={tab === 'people' ? 'active' : ''} onClick={() => setTab('people')} role="tab" type="button">Pessoas · {members.data?.data.length ?? 0}</button>
        <button aria-selected={tab === 'invitations'} className={tab === 'invitations' ? 'active' : ''} onClick={() => setTab('invitations')} role="tab" type="button">Convites pendentes · {invitationTotal}</button>
      </div>
      {tab === 'people' ? <section aria-label="Pessoas com acesso" className="access-table" role="tabpanel">
        <div className="access-table-header staff-grid"><span>Nome</span><span>E-mail</span><span>Papéis</span><span /></div>
        {members.isPending ? <p className="table-message" role="status">Carregando pessoas…</p> : null}
        {members.isError ? <p className="table-message" role="alert">Não foi possível carregar as pessoas com acesso.</p> : null}
        {members.data?.data.length === 0 ? <p className="table-message">Nenhuma conta interna.</p> : null}
        {members.data?.data.map((member) => <StaffMemberRow key={member.accountId} member={member} onChange={(target) => setDialog({ kind: 'change', member: target })} onGrant={(target) => setDialog({ kind: 'grant', member: target })} onRevoke={(target, role) => setDialog({ kind: 'revoke', member: target, role })} />)}
        {members.data ? <p className="access-table-footer">{peopleTotal} {peopleTotal === 1 ? 'pessoa' : 'pessoas'} · página {pagination?.page ?? 1} de {Math.max(pagination?.totalPages ?? 1, 1)}</p> : null}
      </section> : <section aria-label="Convites pendentes" className="access-table" role="tabpanel">
        <div className="access-table-header invite-grid"><span>E-mail</span><span>Papel oferecido</span><span>Convidado em</span><span>Vale até</span><span /></div>
        {invitations.isPending ? <p className="table-message" role="status">Carregando convites…</p> : null}
        {invitations.isError ? <p className="table-message" role="alert">Não foi possível carregar os convites pendentes.</p> : null}
        {invitations.data?.data.length === 0 ? <p className="table-message">Nenhum convite pendente.</p> : null}
        {invitations.data?.data.map((invitation) => <InvitationRow invitation={invitation} key={invitation.invitationId} onResend={resend} />)}
        {invitations.data ? <p className="access-table-footer">{invitationTotal} {invitationTotal === 1 ? 'convite pendente' : 'convites pendentes'} · cada convite vale 7 dias</p> : null}
      </section>}
      {dialog?.kind === 'invite' ? <InviteDialog initial={dialog.initial} onClose={closeDialog} /> : null}
      {dialog?.kind === 'grant' ? <GrantRoleDialog member={dialog.member} onClose={closeDialog} onDone={finishDialog} /> : null}
      {dialog?.kind === 'revoke' ? <RevokeRoleDialog member={dialog.member} onClose={closeDialog} onDone={finishDialog} role={dialog.role} /> : null}
      {dialog?.kind === 'change' ? <ChangeRoleDialog member={dialog.member} onClose={closeDialog} onDone={finishDialog} /> : null}
      {toast ? <Toast message={toast} /> : null}
    </main>
  );
};
