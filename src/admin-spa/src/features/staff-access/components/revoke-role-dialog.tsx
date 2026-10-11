import { useRef, useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';

import {
  getStaffRoleActionRequestError,
  staffRoleActionSchema,
  useRevokeStaffRole,
  type StaffMember,
  type StaffRoleActionInput,
} from '@/features/staff-access/api/staff-members';
import { ReasonField } from '@/features/staff-access/components/reason-field';
import { StaffDialog } from '@/features/staff-access/components/staff-dialog';
import { StaffDialogNotice } from '@/features/staff-access/components/staff-dialog-notice';
import { firstName, formatRoleList, staffRoleLabels, type StaffRole } from '@/features/staff-access/components/staff-role-labels';

type RevokeRoleDialogProps = {
  member: StaffMember;
  onClose: () => void;
  onDone: (message: string) => void;
  role: StaffRole;
};

export const RevokeRoleDialog = ({ member, onClose, onDone, role }: RevokeRoleDialogProps) => {
  const idempotency = useRef<{ fingerprint: string; key: string } | null>(null);
  const [error, setError] = useState<string | null>(null);
  const revokeRole = useRevokeStaffRole();
  const form = useForm<StaffRoleActionInput>({
    defaultValues: { role, reason: '' },
    resolver: zodResolver(staffRoleActionSchema),
  });
  const reason = useWatch({ control: form.control, name: 'reason', defaultValue: '' });
  const remaining = member.roles.filter((current) => current !== role);
  const name = firstName(member.name);

  const submit = async (input: StaffRoleActionInput) => {
    const fingerprint = JSON.stringify(['revoke', input.role, input.reason]);
    if (idempotency.current?.fingerprint !== fingerprint) idempotency.current = { fingerprint, key: crypto.randomUUID() };
    setError(null);
    try {
      const result = await revokeRole.mutateAsync({ accountId: member.accountId, input, idempotencyKey: idempotency.current.key });
      onDone(result.changed ? `Papel revogado. A sessão de ${name} foi encerrada.` : `${staffRoleLabels[role]} já estava ausente de ${member.name}.`);
    } catch (caught: unknown) {
      setError(getStaffRoleActionRequestError(caught));
    }
  };

  return (
    <StaffDialog onClose={onClose} title={`Revogar ${staffRoleLabels[role]} de ${member.name}?`}>
      {error ? <p className="inline-alert" role="alert">{error}</p> : null}
      <form noValidate onSubmit={(event) => void form.handleSubmit(submit)(event)}>
        <StaffDialogNotice title={`Sessão de ${name} será encerrada agora`} tone="warning">
          {remaining.length
            ? `Continua com ${remaining.length === 1 ? 'o papel' : 'os papéis'} ${formatRoleList(remaining)} e entra de novo só com essas permissões.`
            : 'Fica sem acesso a nenhuma área. Para devolver acesso, conceda um papel depois.'}
        </StaffDialogNotice>
        <ReasonField error={form.formState.errors.reason?.message} id={`staff-reason-${member.accountId}`} length={reason.length} register={form.register('reason')} />
        <div className="dialog-actions"><button className="outline-button" onClick={onClose} type="button">Cancelar</button><button className="destructive-button" disabled={revokeRole.isPending} type="submit">Revogar e desconectar</button></div>
      </form>
    </StaffDialog>
  );
};
