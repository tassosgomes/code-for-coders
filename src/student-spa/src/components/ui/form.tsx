import { Controller, useFormContext, type FieldValues, type Path } from 'react-hook-form';

type FormTextFieldProps<T extends FieldValues> = {
  autoComplete: string;
  label: string;
  name: Path<T>;
  type?: 'email' | 'password' | 'text';
};

export function FormTextField<T extends FieldValues>({
  autoComplete,
  label,
  name,
  type = 'text',
}: FormTextFieldProps<T>) {
  const { control } = useFormContext<T>();
  const fieldId = `field-${String(name).replaceAll('.', '-')}`;
  const errorId = `${fieldId}-error`;

  return (
    <Controller
      control={control}
      name={name}
      render={({ field, fieldState }) => (
        <div className="form-field">
          <label htmlFor={fieldId}>{label}</label>
          <input
            {...field}
            autoComplete={autoComplete}
            aria-describedby={fieldState.error ? errorId : undefined}
            aria-invalid={fieldState.invalid}
            id={fieldId}
            type={type}
            value={String(field.value ?? '')}
          />
          {fieldState.error?.message ? (
            <p className="field-error" id={errorId} role="alert">
              {fieldState.error.message}
            </p>
          ) : null}
        </div>
      )}
    />
  );
}
