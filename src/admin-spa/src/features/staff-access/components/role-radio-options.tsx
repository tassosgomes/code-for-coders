import type { UseFormRegisterReturn } from 'react-hook-form';

import { staffRoleDescriptions, staffRoleLabels, type StaffRole } from '@/features/staff-access/components/staff-role-labels';

type RoleRadioOptionsProps = {
  error?: string;
  legend: string;
  register: UseFormRegisterReturn;
  roles: StaffRole[];
  showDescriptions?: boolean;
};

export const RoleRadioOptions = ({ error, legend, register, roles, showDescriptions = false }: RoleRadioOptionsProps) => (
  <fieldset className="role-options">
    <legend>{legend}</legend>
    {roles.map((role) => <label className="role-option" key={role}>
      <input type="radio" value={role} {...register} />
      <span><strong>{staffRoleLabels[role]}</strong>{showDescriptions ? <small>{staffRoleDescriptions[role]}</small> : null}</span>
    </label>)}
    {error ? <p role="alert">{error}</p> : null}
  </fieldset>
);
