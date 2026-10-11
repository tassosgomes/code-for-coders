import { useRef, useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';

import {
  getStaffRoleActionRequestError,
  staffRoleChangeSchema,
  useChangeStaffRole,
  type StaffMember,
  type StaffRoleChangeInput,
} from '@/features/staff-access/api/staff-members';
import { ReasonField } from '@/features/staff-access/components/reason-field';
import { RoleRadioOptions } from '@/features/staff-access/components/role-radio-options';
import { StaffDialog } from '@/features/staff-access/components/staff-dialog';
import { StaffDialogNotice } from '@/features/staff-access/components/staff-dialog-notice';
import { firstName, sortStaffRoles, staffRoleLabels, staffRoleOrder } from '@/features/staff-access/components/staff-role-labels';

type ChangeRoleDialogProps = {
  member: StaffMember;
  onClose: () => void;
  onDone: (message: string) => void;
};

export const ChangeRoleDialog = ({ member, onClose, onDone }: ChangeRoleDialogProps) => {
  const idempotency = useRef<{ fingerprint: string; key: string } | null>(null);
  const [error, setError] = useState<string | null>(null);
  const changeRole = useChangeStaffRole();
  const current = sortStaffRoles(member.roles);
  const available = staffRoleOrder.filter((role) => !member.roles.includes(role));
  const form = useForm<StaffRoleChangeInput>({
    defaultValues: { fromRole: current[0] ?? 'professor', toRole: available[0] ?? 'professor', reason: '' },
    resolver: zodResolver(staffRoleChangeSchema),
  });
  const [fromRole, toRole, reason] = useWatch({ control: form.control, name: ['fromRole', 'toRole', 'reason'] });
  const result = sortStaffRoles([...member.roles.filter((role) => role !== fromRole), toRole]);
  const name = firstName(member.name);
  const errors = form.formState.errors;

  const submit = async (input: StaffRoleChangeInput) => {
    const fingerprint = JSON.stringify([input.fromRole, input.toRole, input.reason]);
    if (idempotency.current?.fingerprint !== fingerprint) idempotency.current = { fingerprint, key: crypto.randomUUID() };
    setError(null);
    try {
      const response = await changeRole.mutateAsync({ accountId: member.accountId, input, idempotencyKey: idempotency.current.key });
      onDone(response.changed ? `Papel trocado. A sessão de ${name} foi encerrada.` : `Nenhuma alteração de papel foi necessária para ${member.name}.`);
    } catch (caught: unknown) {
      setError(getStaffRoleActionRequestError(caught));
    }
  };

  return (
    <StaffDialog description="Uma ação só: o papel de origem sai e o novo entra juntos. Se algo falhar, nada muda." onClose={onClose} title={`Trocar papel de ${member.name}`}>
      {error ? <p className="inline-alert" role="alert">{error}</p> : null}
      <form noValidate onSubmit={(event) => void form.handleSubmit(submit)(event)}>
        <div className="staff-role-columns">
          <RoleRadioOptions error={errors.fromRole?.message} legend="De" register={form.register('fromRole')} roles={current} />
          <RoleRadioOptions error={errors.toRole?.message} legend="Para" register={form.register('toRole')} roles={available} />
        </div>
        <div className="staff-current-roles"><span>Resultado:</span>{result.map((role) => <span className="role-badge" key={role}>{role}</span>)}</div>
        <ReasonField error={errors.reason?.message} id={`staff-reason-${member.accountId}`} length={reason.length} register={form.register('reason')} />
        <StaffDialogNotice title={`Sessão de ${name} será encerrada agora`} tone="warning">
          A trilha registra papel revogado ({staffRoleLabels[fromRole]}) e papel concedido ({staffRoleLabels[toRole]}), com este motivo.
        </StaffDialogNotice>
        <div className="dialog-actions"><button className="outline-button" onClick={onClose} type="button">Cancelar</button><button className="primary-button" disabled={changeRole.isPending} type="submit">Trocar e desconectar</button></div>
      </form>
    </StaffDialog>
  );
};
