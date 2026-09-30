import { zodResolver } from '@hookform/resolvers/zod';
import { useForm, useWatch } from 'react-hook-form';
import type { z } from 'zod';

type RevisionNote = { draftRevision: number; versionNote?: string };
type RevisionNoteFormProps = {
  schema: z.ZodType<RevisionNote, RevisionNote>; revision: number; busy: boolean;
  onSubmit: (input: RevisionNote) => Promise<void>; onCancel: () => void;
};
export const RevisionNoteForm = ({ schema, revision, busy, onSubmit, onCancel }: RevisionNoteFormProps) => {
  const form = useForm<RevisionNote>({ resolver: zodResolver(schema), defaultValues: { draftRevision: revision } });
  const note = useWatch({ control: form.control, name: 'versionNote' }) ?? '';
  return <form noValidate onSubmit={form.handleSubmit(onSubmit)} className="text-details-form">
    <label htmlFor="publication-note">Nota da versão (opcional)</label>
    <textarea id="publication-note" rows={4} maxLength={1000} disabled={busy} {...form.register('versionNote', { setValueAs: (value: string) => value === '' ? undefined : value })} />
    <small>{note.length}/1000 · A nota não aparece na Auditoria.</small>
    {form.formState.errors.versionNote ? <p role="alert" className="field-error">A nota deve ter até 1 000 caracteres.</p> : null}
    <div className="dialog-actions"><button type="button" className="outline-button" disabled={busy} onClick={onCancel}>Cancelar</button><button type="submit" className="primary-button" disabled={busy}>{busy ? 'Publicando…' : 'Publicar versão 1'}</button></div>
  </form>;
};
