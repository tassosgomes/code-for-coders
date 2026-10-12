import { useState } from 'react';

import { Dialog } from '@/components/ui/dialog';
import { useValidatedForm } from '@/components/ui/form/validated-form';
import { replaceVideoTagsInputSchema, type ReplaceVideoTagsInput } from '@/features/videos/api/replace-video-tags';
import { VideoTagField } from '@/features/videos/components/video-tag-field';
import { distinctVideoTags, sameVideoTags } from '@/features/videos/utils/video-tags';

type EditVideoTagsDialogProps = {
  title: string; tags: string[]; busy: boolean; error: string | null;
  onClose: () => void; onSave: (input: ReplaceVideoTagsInput) => Promise<void>;
};
export const EditVideoTagsDialog = ({ title, tags, busy, error, onClose, onSave }: EditVideoTagsDialogProps) => {
  const form = useValidatedForm(replaceVideoTagsInputSchema, { tags });
  const current = form.watch('tags');
  const [draft, setDraft] = useState('');
  const [submitError, setSubmitError] = useState<string | null>(null);
  const fieldError = error === 'TAG_INVALID' ? 'Cada tag precisa ter de 1 a 30 caracteres.'
    : error === 'TAGS_LIMIT_EXCEEDED' ? 'Um vídeo pode ter no máximo 10 tags.' : submitError;
  const serviceError = error && error !== 'TAG_INVALID' && error !== 'TAGS_LIMIT_EXCEEDED';
  const submit = async () => {
    const result = replaceVideoTagsInputSchema.safeParse({ tags: draft ? [...current, draft] : current });
    if (!result.success) { setSubmitError(result.error.issues[0]?.message ?? null); return; }
    setSubmitError(null);
    await onSave({ tags: distinctVideoTags(result.data.tags) });
  };
  return <Dialog title="Editar tags" description={title} busy={busy} onClose={onClose} className="video-tags-dialog">
    <form noValidate onSubmit={(event) => { event.preventDefault(); void submit(); }}>
      <VideoTagField tags={current} disabled={busy} error={fieldError}
        onChange={(value) => { form.setValue('tags', value, { shouldValidate: true }); setSubmitError(null); }} onDraftChange={setDraft} />
      <p>Remover todas deixa o vídeo sem tags.</p>
      {serviceError ? <div className="inline-alert" role="alert"><p>Não foi possível salvar as tags. Tente de novo.</p>
        <button className="outline-button" disabled={busy} type="button" onClick={() => void submit()}>Tentar de novo</button></div> : null}
      <div className="dialog-actions">
        <button className="outline-button" disabled={busy} onClick={onClose} type="button">Cancelar</button>
        <button className="primary-button" disabled={busy || (!draft && sameVideoTags(current, tags))} type="submit">{busy ? 'Salvando…' : 'Salvar'}</button>
      </div>
    </form>
  </Dialog>;
};
