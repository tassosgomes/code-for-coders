import { useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { CircleCheck } from 'lucide-react';

import {
  createStaffInvitationSchema,
  getStaffInvitationRequestError,
  useCreateStaffInvitation,
  type CreateStaffInvitationInput,
} from '@/features/staff-access/api/staff-invitations';
import { ReasonField } from '@/features/staff-access/components/reason-field';
import { RoleRadioOptions } from '@/features/staff-access/components/role-radio-options';
import { StaffDialog } from '@/features/staff-access/components/staff-dialog';
import { StaffDialogNotice } from '@/features/staff-access/components/staff-dialog-notice';
import { formatStaffDate, formatStaffTime, staffRoleLabels, staffRoleOrder, type StaffRole } from '@/features/staff-access/components/staff-role-labels';

type InviteDialogProps = {
  initial?: { email: string; role: StaffRole };
  onClose: () => void;
};

export const InviteDialog = ({ initial, onClose }: InviteDialogProps) => {
  const [requestError, setRequestError] = useState<string | null>(null);
  const createInvitation = useCreateStaffInvitation();
  const emptyValues: CreateStaffInvitationInput = { email: '', role: 'professor', reason: '' };
  const form = useForm<CreateStaffInvitationInput>({
    defaultValues: initial ? { email: initial.email, role: initial.role, reason: '' } : emptyValues,
    resolver: zodResolver(createStaffInvitationSchema),
  });
  const reason = useWatch({ control: form.control, name: 'reason', defaultValue: '' });
  const errors = form.formState.errors;
  const sent = createInvitation.data;

  const submit = async (input: CreateStaffInvitationInput) => {
    setRequestError(null);
    try {
      await createInvitation.mutateAsync({ input });
    } catch (error: unknown) {
      setRequestError(getStaffInvitationRequestError(error));
    }
  };

  const inviteAnother = () => {
    createInvitation.reset();
    setRequestError(null);
    form.reset(emptyValues);
  };

  if (sent) {
    return (
      <StaffDialog onClose={onClose} title="Convite enviado">
        <div className="staff-success-tile"><CircleCheck aria-hidden="true" size={24} /></div>
        <p className="staff-dialog-text">{sent.email} recebeu o convite como {staffRoleLabels[sent.offeredRole]}. Ele vale até {formatStaffDate(sent.expiresAt)} às {formatStaffTime(sent.expiresAt)}.</p>
        {sent.supersededInvitationId ? <StaffDialogNotice tone="info">Havia outro convite pendente para esse e-mail. Ele deixou de valer.</StaffDialogNotice> : null}
        <div className="dialog-actions"><button className="outline-button" onClick={inviteAnother} type="button">Convidar outra pessoa</button><button className="primary-button" onClick={onClose} type="button">Concluir</button></div>
      </StaffDialog>
    );
  }

  return (
    <StaffDialog description="A pessoa recebe um link por e-mail e define a própria senha. O convite vale 7 dias." onClose={onClose} title="Convidar para a equipe">
      {requestError ? <p className="inline-alert" role="alert">{requestError}</p> : null}
      <form noValidate onSubmit={(event) => void form.handleSubmit(submit)(event)}>
        <div className="staff-field">
          <label htmlFor="invitation-email">E-mail</label>
          <input autoComplete="email" id="invitation-email" placeholder="ana@escola.com" type="email" {...form.register('email')} aria-invalid={Boolean(errors.email)} />
          {errors.email ? <p role="alert">{errors.email.message}</p> : null}
        </div>
        <RoleRadioOptions legend="Papel" register={form.register('role')} roles={staffRoleOrder} showDescriptions />
        <ReasonField error={errors.reason?.message} id="invitation-reason" length={reason.length} register={form.register('reason')} />
        <div className="dialog-actions"><button className="outline-button" onClick={onClose} type="button">Cancelar</button><button className="primary-button" disabled={createInvitation.isPending} type="submit">{createInvitation.isPending ? 'Enviando…' : 'Enviar convite'}</button></div>
      </form>
    </StaffDialog>
  );
};
