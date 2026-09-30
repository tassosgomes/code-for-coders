import { zodResolver } from '@hookform/resolvers/zod';
import { useForm, useWatch } from 'react-hook-form';
import type { z } from 'zod';

type ChoiceInput = Record<string, string | null | undefined>;
type AutoSaveChoiceFormProps = {
  schema: z.ZodType<ChoiceInput, ChoiceInput>; fieldName: string; value: string | null;
  options: { value: string | null; label: string }[]; legend: string; help: string;
  busy: boolean; error?: string; invalid: boolean;
  onSubmit: (input: ChoiceInput) => Promise<void>;
};
export const AutoSaveChoiceForm = ({ schema, fieldName, value, options, legend, help, busy, error, invalid, onSubmit }: AutoSaveChoiceFormProps) => {
  const form = useForm<ChoiceInput>({ resolver: zodResolver(schema), values: { [fieldName]: value } });
  const selected = useWatch({ control: form.control, name: fieldName });
  const submit = async (input: ChoiceInput) => { await onSubmit(input); requestAnimationFrame(() => document.querySelector<HTMLInputElement>(`#${fieldName}-options input:checked`)?.focus()); };
  return <form noValidate onSubmit={form.handleSubmit(submit)}>
    <fieldset id={`${fieldName}-options`} className="single-choice-options" disabled={busy} aria-invalid={invalid} aria-describedby={`${fieldName}-help${error ? ` ${fieldName}-error` : ''}`}>
      <legend>{legend}</legend>
      {options.map((option) => <label className="single-choice-option" key={option.value ?? 'none'}>
        <input {...form.register(fieldName)} type="radio" value={option.value ?? ''} checked={selected === option.value} onChange={() => {
          form.setValue(fieldName, option.value, { shouldDirty: true }); void form.handleSubmit(submit)();
        }} /><span>{option.label}</span>
      </label>)}
    </fieldset>
    <p id={`${fieldName}-help`}>{help}</p>
    {error ? <><p id={`${fieldName}-error`} className="field-error" role="alert">{error}</p><button type="submit" className="outline-button" disabled={busy}>Tentar de novo</button></> : null}
  </form>;
};
