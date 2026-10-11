import { useRef, useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';

import {
  getStaffRoleActionRequestError,
  staffRoleActionSchema,
  useGrantStaffRole,
  type StaffMember,
  type StaffRoleActionInput,
} from '@/features/staff-access/api/staff-members';
import { ReasonField } from '@/features/staff-access/components/reason-field';
import { RoleRadioOptions } from '@/features/staff-access/components/role-radio-options';
import { StaffDialog } from '@/features/staff-access/components/staff-dialog';
import { StaffDialogNotice } from '@/features/staff-access/components/staff-dialog-notice';
import { staffRoleLabels, staffRoleOrder } from '@/features/staff-access/components/staff-role-labels';

type GrantRoleDialogProps = {
  member: StaffMember;
  onClose: () => void;
  onDone: (message: string) => void;
};

export const GrantRoleDialog = ({ member, onClose, onDone }: GrantRoleDialogProps) => {
  const idempotency = useRef<{ fingerprint: string; key: string } | null>(null);
  const [error, setError] = useState<string | null>(null);
  const grantRole = useGrantStaffRole();
  const available = staffRoleOrder.filter((role) => !member.roles.includes(role));
  const form = useForm<StaffRoleActionInput>({
    defaultValues: { role: available[0] ?? 'professor', reason: '' },
    resolver: zodResolver(staffRoleActionSchema),
  });
  const reason = useWatch({ control: form.control, name: 'reason', defaultValue: '' });

  const submit = async (input: StaffRoleActionInput) => {
    const fingerprint = JSON.stringify(['grant', input.role, input.reason]);
    if (idempotency.current?.fingerprint !== fingerprint) idempotency.current = { fingerprint, key: crypto.randomUUID() };
    setError(null);
    try {
      const result = await grantRole.mutateAsync({ accountId: member.accountId, input, idempotencyKey: idempotency.current.key });
      onDone(result.changed ? `Papel concedido a ${member.name}.` : `${staffRoleLabels[input.role]} já estava concedido a ${member.name}.`);
    } catch (caught: unknown) {
      setError(getStaffRoleActionRequestError(caught));
    }
  };

  return (
    <StaffDialog onClose={onClose} title={`Conceder papel a ${member.name}`}>
      {error ? <p className="inline-alert" role="alert">{error}</p> : null}
      <form noValidate onSubmit={(event) => void form.handleSubmit(submit)(event)}>
        <div className="staff-current-roles"><span>Papéis atuais:</span>{member.roles.length ? member.roles.map((role) => <span className="role-badge" key={role}>{role}</span>) : <span className="role-badge role-badge-empty">Sem papel</span>}</div>
        <RoleRadioOptions legend="Novo papel" register={form.register('role')} roles={available} />
        <ReasonField error={form.formState.errors.reason?.message} id={`staff-reason-${member.accountId}`} length={reason.length} register={form.register('reason')} />
        <StaffDialogNotice tone="info">Vale na próxima ação dele, sem desconectar.</StaffDialogNotice>
        <div className="dialog-actions"><button className="outline-button" onClick={onClose} type="button">Cancelar</button><button className="primary-button" disabled={grantRole.isPending} type="submit">Conceder papel</button></div>
      </form>
    </StaffDialog>
  );
};
