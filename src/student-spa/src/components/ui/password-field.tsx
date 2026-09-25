import { useState, type ReactNode } from 'react';
import { Controller, useFormContext, type FieldValues, type Path } from 'react-hook-form';
import { Circle, CircleCheck, Eye, EyeOff } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  passwordPolicyRequirements,
  passwordPolicySchema,
} from '@/utils/password-policy-schema';

type PasswordFieldProps<T extends FieldValues> = {
  action?: ReactNode;
  autoComplete: string;
  label: string;
  name: Path<T>;
  showRequirements?: boolean;
};

type PasswordRequirementProps = {
  fulfilled: boolean;
  label: string;
};

export const PasswordRequirement = ({ fulfilled, label }: PasswordRequirementProps) => {
  const RequirementIcon = fulfilled ? CircleCheck : Circle;

  return (
    <li className="flex items-center gap-2 text-sm text-muted-foreground">
      <RequirementIcon
        aria-hidden="true"
        className={fulfilled ? 'size-4 text-success-soft-foreground' : 'size-4'}
      />
      <span>{label}</span>
    </li>
  );
};

export function PasswordField<T extends FieldValues>({
  action,
  autoComplete,
  label,
  name,
  showRequirements = false,
}: PasswordFieldProps<T>) {
  const { control } = useFormContext<T>();
  const [visible, setVisible] = useState(false);
  const fieldId = `field-${String(name).replaceAll('.', '-')}`;
  const errorId = `${fieldId}-error`;
  const requirementsId = `${fieldId}-requirements`;

  return (
    <Controller
      control={control}
      name={name}
      render={({ field, fieldState }) => {
        const value = String(field.value ?? '');
        const issues = passwordPolicySchema.safeParse(value).error?.issues.map((issue) => issue.message) ?? [];
        const fulfilledRequirements = new Set(
          passwordPolicyRequirements
            .filter((requirement) => !issues.includes(requirement.message))
            .map((requirement) => requirement.id),
        );

        return (
          <div className="grid gap-2">
            <div className="flex items-center justify-between gap-4">
              <Label htmlFor={fieldId}>{label}</Label>
              {action}
            </div>
            <div className="relative">
              <Input
                {...field}
                autoComplete={autoComplete}
                aria-describedby={[fieldState.error ? errorId : undefined, showRequirements ? requirementsId : undefined]
                  .filter(Boolean)
                  .join(' ') || undefined}
                aria-invalid={fieldState.invalid}
                className="pr-12"
                id={fieldId}
                type={visible ? 'text' : 'password'}
                value={value}
              />
              <Button
                aria-label={`${visible ? 'Ocultar' : 'Mostrar'} ${label.toLocaleLowerCase('pt-BR')}`}
                className="absolute right-1 top-1/2 -translate-y-1/2"
                size="icon-sm"
                type="button"
                variant="ghost"
                onClick={() => setVisible((current) => !current)}
              >
                {visible ? <EyeOff aria-hidden="true" /> : <Eye aria-hidden="true" />}
              </Button>
            </div>
            {fieldState.error?.message ? (
              <p className="text-sm text-destructive" id={errorId} role="alert">
                {fieldState.error.message}
              </p>
            ) : null}
            {showRequirements ? (
              <ul className="grid gap-2" id={requirementsId} aria-label="Requisitos da senha">
                {passwordPolicyRequirements.map((requirement) => (
                  <PasswordRequirement
                    key={requirement.id}
                    fulfilled={fulfilledRequirements.has(requirement.id)}
                    label={requirement.label}
                  />
                ))}
              </ul>
            ) : null}
          </div>
        );
      }}
    />
  );
}
