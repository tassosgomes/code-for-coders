import { zodResolver } from '@hookform/resolvers/zod';
import type { ReactNode } from 'react';
import { useForm } from 'react-hook-form';
import type { z } from 'zod';

type SingleChoiceFormProps = {
  schema: z.ZodType<Record<string, string | null>, Record<string, string | null>>; fieldName: string; defaultValues: Record<string, string | null>;
  options: { value: string; label: ReactNode }[]; legend: string; submitLabel: string;
  disabled: boolean; onSubmit: (input: Record<string, string | null>) => Promise<void>; children: ReactNode;
};
export const SingleChoiceForm = ({ schema, fieldName, defaultValues, options, legend, submitLabel, disabled, onSubmit, children }: SingleChoiceFormProps) => {
  const form = useForm<Record<string, string | null>>({ resolver: zodResolver(schema), defaultValues });
  const selected = form.watch(fieldName);
  return <form onSubmit={form.handleSubmit(onSubmit)}>
    <fieldset className="single-choice-options" disabled={disabled}><legend>{legend}</legend>{options.map((option) => <label key={option.value} className="single-choice-option">
      <input type="radio" value={option.value} {...form.register(fieldName)} /><span>{option.label}</span>
    </label>)}</fieldset>
    {children}
    <div className="dialog-actions"><button className="primary-button" type="submit" disabled={disabled || !options.some((option) => option.value === selected)}>{submitLabel}</button></div>
  </form>;
};
