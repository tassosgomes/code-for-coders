import type { UseFormRegisterReturn } from 'react-hook-form';

type ReasonFieldProps = {
  error?: string;
  id: string;
  length: number;
  register: UseFormRegisterReturn;
};

export const ReasonField = ({ error, id, length, register }: ReasonFieldProps) => (
  <div className="staff-field">
    <label htmlFor={id}>Motivo</label>
    <textarea id={id} maxLength={500} rows={3} {...register} aria-invalid={Boolean(error)} />
    <p className="field-hint"><span>Fica registrado na auditoria. Não cite dados pessoais de outras pessoas.</span><span>{length}/500</span></p>
    {error ? <p role="alert">{error}</p> : null}
  </div>
);
