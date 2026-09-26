import { zodResolver } from '@hookform/resolvers/zod';
import { Upload, X } from 'lucide-react';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';

import type { PendingVideoUpload } from '@/features/videos/api/get-pending-video-uploads';
import { getVideoFileProblem } from '@/features/videos/utils/video-upload';

const videoTitleSchema = z.object({
  title: z.string().trim().min(1, 'Informe um título para o vídeo.').max(200, 'O título deve ter até 200 caracteres.'),
});

type VideoTitleForm = z.infer<typeof videoTitleSchema>;

type VideoUploadDialogProps = {
  busy: boolean;
  error: string | null;
  onClose: () => void;
  onStart: (file: File, title: string) => Promise<void>;
  pendingUploads?: readonly PendingVideoUpload[];
};

export const VideoUploadDialog = ({ busy, error, onClose, onStart, pendingUploads = [] }: VideoUploadDialogProps) => {
  const [file, setFile] = useState<File | null>(null);
  const [fileProblem, setFileProblem] = useState<string | null>(null);
  const [title, setTitle] = useState('');
  const hasMatchingPendingUpload = file && pendingUploads.some(
    (upload) => upload.fileName === file.name && upload.fileSize === file.size,
  );
  const differentPendingUpload = file && pendingUploads.length > 0 && !hasMatchingPendingUpload
    ? pendingUploads[0]
    : undefined;
  const form = useForm<VideoTitleForm>({
    defaultValues: { title: '' },
    resolver: zodResolver(videoTitleSchema),
  });
  const selectFile = (selected: File | undefined) => {
    if (!selected) return;
    const problem = getVideoFileProblem(selected);
    setFileProblem(problem);
    setFile(problem ? null : selected);
    const suggestedTitle = problem ? '' : selected.name.replace(/\.[^.]+$/, '');
    setTitle(suggestedTitle);
    form.clearErrors('title');
    form.setValue('title', suggestedTitle, { shouldValidate: Boolean(suggestedTitle) });
  };

  return (
    <div className="dialog-backdrop">
      <section aria-labelledby="video-upload-title" aria-modal="true" className="dialog-card video-upload-dialog" role="dialog">
        <button aria-label="Fechar" className="dialog-close" disabled={busy} onClick={onClose} type="button"><X size={18} /></button>
        <p className="eyebrow">Biblioteca de vídeos</p>
        <h2 id="video-upload-title">Enviar vídeo</h2>
        <p className="video-upload-intro">Escolha uma gravação da aula e confira o título antes de enviar.</p>
        <form noValidate onSubmit={form.handleSubmit(async (values) => {
          if (file) await onStart(file, values.title);
        })}>
          <label className="video-file-picker" htmlFor="video-file">
            <Upload aria-hidden="true" size={22} />
            <strong>{file?.name ?? 'Escolher arquivo de vídeo'}</strong>
            <span>{file ? `${(file.size / (1024 * 1024)).toFixed(1)} MiB` : 'MP4, MOV ou MKV · até 5 GiB'}</span>
          </label>
          <input
            accept=".mp4,.mov,.mkv,video/mp4,video/quicktime,video/x-matroska"
            className="visually-hidden"
            id="video-file"
            onChange={(event) => selectFile(event.currentTarget.files?.[0])}
            type="file"
          />
          {differentPendingUpload ? <p className="warning-alert" role="status">
            Este não é o arquivo do envio incompleto ({differentPendingUpload.fileName}). Ele será enviado como um vídeo novo.
          </p> : null}
          {fileProblem ? <p className="inline-alert" role="alert">{fileProblem}</p> : null}
          <label htmlFor="video-title">Título</label>
          <input
            aria-invalid={Boolean(form.formState.errors.title)}
            id="video-title"
            maxLength={200}
            {...form.register('title', {
              onChange: (event) => setTitle(event.currentTarget.value),
            })}
          />
          {form.formState.errors.title ? <p className="field-error" role="alert">{form.formState.errors.title.message}</p> : null}
          <p className="field-hint">{title.length}/200 caracteres</p>
          {error ? <p className="inline-alert" role="alert">{error}</p> : null}
          <p className="video-privacy-note">O vídeo não fica público: ele só é entregue a quem tem acesso à aula.</p>
          <div className="dialog-actions">
            <button className="outline-button" disabled={busy} onClick={onClose} type="button">Cancelar</button>
            <button className="primary-button" disabled={!file || busy} type="submit">{busy ? 'Iniciando…' : 'Enviar vídeo'}</button>
          </div>
        </form>
      </section>
    </div>
  );
};
