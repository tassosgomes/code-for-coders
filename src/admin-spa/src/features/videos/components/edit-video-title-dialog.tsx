import { zodResolver } from '@hookform/resolvers/zod';
import { X } from 'lucide-react';
import { useForm, useWatch } from 'react-hook-form';

import { updateVideoTitleInputSchema, type UpdateVideoTitleInput } from '@/features/videos/api/update-video-title';

type EditVideoTitleDialogProps = {
  title: string;
  busy: boolean;
  error: string | null;
  onClose: () => void;
  onSave: (input: UpdateVideoTitleInput) => Promise<void>;
};

export const EditVideoTitleDialog = ({ title, busy, error, onClose, onSave }: EditVideoTitleDialogProps) => {
  const form = useForm<UpdateVideoTitleInput>({
    defaultValues: { title },
    resolver: zodResolver(updateVideoTitleInputSchema),
    mode: 'onChange',
  });
  const currentTitle = useWatch({ control: form.control, name: 'title' });
  const titleError = form.formState.errors.title?.message ?? (error === 'TITLE_REQUIRED' ? 'Dê um título para reconhecer o vídeo.' : null);
  return <div className="dialog-backdrop">
    <section aria-labelledby="edit-video-title" aria-modal="true" className="dialog-card" role="dialog">
      <button aria-label="Fechar" className="dialog-close" disabled={busy} onClick={onClose} type="button"><X size={18} /></button>
      <h2 id="edit-video-title">Editar título</h2>
      <form noValidate onSubmit={form.handleSubmit(onSave)}>
        <label htmlFor="edited-video-title">Título</label>
        <input aria-invalid={Boolean(titleError)} id="edited-video-title" maxLength={200} {...form.register('title')} />
        {titleError ? <p className="field-error" role="alert">{titleError}</p> : null}
        <p className="field-hint">{currentTitle.length}/200</p>
        <p>Mudar o título não altera o vídeo.</p>
        {error && error !== 'TITLE_REQUIRED' ? <p className="inline-alert" role="alert">{error}</p> : null}
        <div className="dialog-actions">
          <button className="outline-button" disabled={busy} onClick={onClose} type="button">Cancelar</button>
          <button className="primary-button" disabled={busy || !currentTitle.trim()} type="submit">{busy ? 'Salvando…' : 'Salvar'}</button>
        </div>
      </form>
    </section>
  </div>;
};
