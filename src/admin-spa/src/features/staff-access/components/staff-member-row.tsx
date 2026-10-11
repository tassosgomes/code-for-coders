import { ArrowLeftRight, CircleMinus, UserPlus } from 'lucide-react';

import { staffInvitationRoles } from '@/features/staff-access/api/staff-invitations';
import type { StaffMember } from '@/features/staff-access/api/staff-members';
import { StaffRowMenu, type StaffMenuItem } from '@/features/staff-access/components/staff-row-menu';
import { staffRoleLabels, type StaffRole } from '@/features/staff-access/components/staff-role-labels';

type StaffMemberRowProps = {
  member: StaffMember;
  onChange: (member: StaffMember) => void;
  onGrant: (member: StaffMember) => void;
  onRevoke: (member: StaffMember, role: StaffRole) => void;
};

const initials = (name: string) => name.split(/\s+/).slice(0, 2).map((part) => part[0]).join('').toUpperCase();

export const StaffMemberRow = ({ member, onChange, onGrant, onRevoke }: StaffMemberRowProps) => {
  const hasAllRoles = member.roles.length === staffInvitationRoles.length;
  const safeItems: StaffMenuItem[] = [
    ...(hasAllRoles ? [] : [{ icon: UserPlus, label: 'Conceder papel', onSelect: () => onGrant(member) }]),
    ...(member.roles.length && !hasAllRoles ? [{ icon: ArrowLeftRight, label: 'Trocar papel', onSelect: () => onChange(member) }] : []),
  ];
  const revokeItems: StaffMenuItem[] = member.roles.map((role) => ({ destructive: true, icon: CircleMinus, label: `Revogar ${staffRoleLabels[role]}`, onSelect: () => onRevoke(member, role) }));

  return (
    <div className="access-row staff-grid">
      <div className="staff-name"><span className="small-avatar">{initials(member.name)}</span><span><strong>{member.name}</strong>{member.isSelf ? <small className="self-badge">você</small> : null}</span></div>
      <span className="row-muted">{member.email}</span>
      <span className="role-badges">{member.roles.length ? member.roles.map((role) => <span className="role-badge" key={role}>{role}</span>) : <span className="role-badge role-badge-empty">Sem papel</span>}</span>
      {member.isSelf ? <span className="row-muted">—</span> : <StaffRowMenu groups={[safeItems, revokeItems]} label={`Ações para ${member.name}`} />}
    </div>
  );
};
