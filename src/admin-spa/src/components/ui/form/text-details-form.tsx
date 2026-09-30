import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import type { z } from 'zod';

type TextDetails = { title: string; description: string };
type TextDetailsFormProps = {
  schema: z.ZodType<TextDetails, TextDetails>;
  busy: boolean; titleError?: string; error?: string;
  onSubmit: (input: TextDetails) => Promise<void>;
  onCancel: () => void;
};
export const TextDetailsForm = ({ schema, busy, titleError, error, onSubmit, onCancel }: TextDetailsFormProps) => {
  const form = useForm<TextDetails>({ resolver: zodResolver(schema), defaultValues: { title: '', description: '' } });
  const [title, description] = useWatch({ control: form.control, name: ['title', 'description'] });
  const validationError = form.formState.errors.title?.message ?? titleError;
  useEffect(() => { form.setFocus('title'); }, [form]);
  return <form className="text-details-form" noValidate onSubmit={form.handleSubmit(onSubmit)}>
    <label htmlFor="details-title">Título *</label>
    <input aria-invalid={Boolean(validationError)} aria-describedby={validationError ? 'details-title-error' : undefined} id="details-title" maxLength={200} disabled={busy} {...form.register('title')} />
    {validationError ? <p className="field-error" id="details-title-error" role="alert">{validationError}</p> : null}
    <small>{title.length}/200</small>
    <label htmlFor="details-description">Descrição pedagógica (opcional)</label>
    <textarea id="details-description" rows={4} maxLength={5000} disabled={busy} {...form.register('description')} />
    <small>{description.length}/5000</small>
    {form.formState.errors.description ? <p className="field-error" role="alert">{form.formState.errors.description.message}</p> : null}
    <small>O que a pessoa aprenderá neste curso.</small>
    <small>Preço, nível e oferta pertencem à oferta do curso, prevista para uma etapa futura.</small>
    {error ? <p className="inline-alert" role="alert">{error}</p> : null}
    <div className="dialog-actions">
      <button className="outline-button" disabled={busy} onClick={onCancel} type="button">Cancelar</button>
      <button className="primary-button" disabled={busy} type="submit">{busy ? 'Criando…' : 'Criar curso'}</button>
    </div>
  </form>;
};
