import { Send } from 'lucide-react';

import type { PendingStaffInvitation } from '@/features/staff-access/api/staff-invitations';
import { StaffRowMenu } from '@/features/staff-access/components/staff-row-menu';
import { formatStaffDateTime } from '@/features/staff-access/components/staff-role-labels';

type InvitationRowProps = {
  invitation: PendingStaffInvitation;
  onResend: (invitation: PendingStaffInvitation) => void;
};

export const InvitationRow = ({ invitation, onResend }: InvitationRowProps) => (
  <div className="access-row invite-grid">
    <strong>{invitation.email}</strong>
    <span className="role-badge">{invitation.offeredRole}</span>
    <time className="row-muted" dateTime={invitation.invitedAt}>{formatStaffDateTime(invitation.invitedAt)}</time>
    <time className="row-muted" dateTime={invitation.expiresAt}>{formatStaffDateTime(invitation.expiresAt)}</time>
    <StaffRowMenu groups={[[{ icon: Send, label: 'Enviar novo convite', onSelect: () => onResend(invitation) }]]} label={`Ações do convite de ${invitation.email}`} />
  </div>
);
