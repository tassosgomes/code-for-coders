import { Film, Info, ShieldCheck, TriangleAlert, Upload, X } from 'lucide-react';
import { useRef, useState, type DragEvent } from 'react';

import { useValidatedForm } from '@/components/ui/form/validated-form';
import { createVideoUploadInputSchema } from '@/features/videos/api/create-video-upload';
import { VideoTagField } from '@/features/videos/components/video-tag-field';
import { distinctVideoTags } from '@/features/videos/utils/video-tags';
import type { PendingVideoUpload } from '@/features/videos/api/get-pending-video-uploads';
import { getVideoFileProblem } from '@/features/videos/utils/video-upload';

const videoUploadFormSchema = createVideoUploadInputSchema.pick({ title: true, tags: true });

type VideoUploadDialogProps = {
  busy: boolean;
  error: string | null;
  tagError?: string | null;
  onClose: () => void;
  onStart: (file: File, title: string, tags: string[]) => Promise<void>;
  pendingUploads?: readonly PendingVideoUpload[];
};

const getFileProblemMessage = (problem: string, fileName: string) => {
  if (problem === 'O arquivo deve ter até 5 GiB.') {
    return 'O limite é 5 GB. Divida a gravação ou exporte em qualidade menor.';
  }
  if (problem === 'Formato não aceito. Escolha um arquivo MP4, MOV ou MKV.') {
    return `${fileName} não é um formato aceito. Envie MP4, MOV ou MKV.`;
  }
  return problem;
};

export const VideoUploadDialog = ({ busy, error, tagError, onClose, onStart, pendingUploads = [] }: VideoUploadDialogProps) => {
  const fileInput = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [fileProblem, setFileProblem] = useState<string | null>(null);
  const [rejectedFile, setRejectedFile] = useState<File | null>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [title, setTitle] = useState('');
  const [tagDraft, setTagDraft] = useState('');
  const hasMatchingPendingUpload = file && pendingUploads.some(
    (upload) => upload.fileName === file.name && upload.fileSize === file.size,
  );
  const differentPendingUpload = file && pendingUploads.length > 0 && !hasMatchingPendingUpload
    ? pendingUploads[0]
    : undefined;
  const form = useValidatedForm(videoUploadFormSchema, { title: '', tags: [] });
  const tags = form.watch('tags') ?? [];
  const tagMessage = tagError === 'TAG_INVALID' ? 'Cada tag precisa ter de 1 a 30 caracteres.'
    : tagError === 'TAGS_LIMIT_EXCEEDED' ? 'Um vídeo pode ter no máximo 10 tags.' : form.formState.errors.tags?.message;
  const selectFile = (selected: File | undefined) => {
    if (!selected) return;
    const problem = getVideoFileProblem(selected);
    setFileProblem(problem);
    setRejectedFile(problem ? selected : null);
    setFile(problem ? null : selected);
    const suggestedTitle = problem ? '' : selected.name.replace(/\.[^.]+$/, '');
    setTitle(suggestedTitle);
    form.clearErrors('title');
    form.setValue('title', suggestedTitle, { shouldValidate: Boolean(suggestedTitle) });
  };
  const formatExpiry = (value: string) => {
    const parts = new Intl.DateTimeFormat('pt-BR', {
      day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit',
    }).formatToParts(new Date(value));
    const part = (type: Intl.DateTimeFormatPartTypes) => parts.find((item) => item.type === type)?.value ?? '';
    return `${part('day')}/${part('month')} às ${part('hour')}:${part('minute')}`;
  };
  const formatSize = (bytes: number) => {
    const divisor = bytes >= 1_000_000_000 ? 1_000_000_000 : bytes >= 1_000_000 ? 1_000_000 : bytes >= 1_000 ? 1_000 : 1;
    const unit = divisor === 1_000_000_000 ? 'GB' : divisor === 1_000_000 ? 'MB' : divisor === 1_000 ? 'KB' : 'B';
    return `${new Intl.NumberFormat('pt-BR', { maximumFractionDigits: divisor === 1 ? 0 : 1 }).format(bytes / divisor)} ${unit}`;
  };
  const handleDrop = (event: DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    setIsDragging(false);
    selectFile(event.dataTransfer.files[0]);
  };

  return <div className="dialog-backdrop">
    <section aria-labelledby="video-upload-title" aria-modal="true" className="dialog-card video-upload-dialog" role="dialog">
      <div className="video-dialog-heading">
        <h2 id="video-upload-title">Enviar vídeo</h2>
        <button aria-label="Fechar" className="dialog-close" disabled={busy} onClick={onClose} type="button"><X size={18} /></button>
      </div>
      <form noValidate onSubmit={form.handleSubmit(async (values) => {
        const result = videoUploadFormSchema.safeParse({ ...values, tags: tagDraft ? [...tags, tagDraft] : tags });
        if (!result.success) {
          form.setError('tags', { message: result.error.issues[0]?.message });
          return;
        }
        if (file) await onStart(file, result.data.title, distinctVideoTags(result.data.tags ?? []));
      })}>
        {differentPendingUpload ? <div className="video-upload-alert info" role="status">
          <Info aria-hidden="true" size={16} />
          <p>Este não é o arquivo do envio incompleto ({differentPendingUpload.fileName}). Ele será enviado como um vídeo novo; o envio incompleto continua disponível até {formatExpiry(differentPendingUpload.expiresAt)}.</p>
        </div> : null}

        {file ? <>
          <div className="video-selected-file">
            <Film aria-hidden="true" size={18} />
            <span>{file.name} · {formatSize(file.size)}</span>
            <button className="video-link-button" disabled={busy} onClick={() => fileInput.current?.click()} type="button">Trocar arquivo</button>
          </div>
          <label className="video-upload-label" htmlFor="video-title">Título</label>
          <input
            aria-invalid={Boolean(form.formState.errors.title)}
            className="video-title-input"
            id="video-title"
            maxLength={200}
            {...form.register('title', {
              onChange: (event) => setTitle(event.currentTarget.value),
            })}
          />
          {form.formState.errors.title ? <p className="field-error" role="alert">{form.formState.errors.title.message}</p> : null}
          <p className="video-title-hint"><span>Só para reconhecer o vídeo na escola. Você pode mudar depois.</span><span>{title.length}/200</span></p>
          <VideoTagField label="Tags (opcional)" tags={tags} disabled={busy} error={tagMessage}
            onChange={(value) => form.setValue('tags', value, { shouldValidate: true })} onDraftChange={setTagDraft} />
          {error ? <p className="video-upload-alert error" role="alert">{error}</p> : null}
          <p className="video-duration-note"><Info aria-hidden="true" size={16} /><span>A duração só é conferida depois do envio: vídeos acima de 3 horas falham na preparação.</span></p>
        </> : <>
          <div
            className={`video-file-dropzone ${isDragging ? 'is-dragging' : ''} ${fileProblem && !isDragging ? 'is-rejected' : ''}`}
            onDragEnter={(event) => { event.preventDefault(); setIsDragging(true); }}
            onDragLeave={(event) => { event.preventDefault(); setIsDragging(false); }}
            onDragOver={(event) => event.preventDefault()}
            onDrop={handleDrop}
          >
            {isDragging ? <>
              <span aria-hidden="true" className="video-dropzone-icon"><Upload size={22} /></span>
              <strong>Solte para enviar</strong>
              <span className="video-file-rules">MP4, MOV ou MKV · até 5 GB e 3 horas</span>
            </> : fileProblem ? <>
              <span aria-hidden="true" className="video-dropzone-icon video-rejected-tile"><TriangleAlert size={22} /></span>
              <strong>{rejectedFile?.name}{rejectedFile ? ` · ${formatSize(rejectedFile.size)}` : ''}</strong>
              <span className="video-file-problem" role="alert">{getFileProblemMessage(fileProblem, rejectedFile?.name ?? '')}</span>
              <label className="video-choose-file" htmlFor="video-file">Escolher outro arquivo</label>
            </> : <>
              <span aria-hidden="true" className="video-dropzone-icon"><Upload size={22} /></span>
              <strong>Arraste o vídeo para cá</strong>
              <label className="video-choose-file" htmlFor="video-file">Escolher arquivo</label>
              <span className="video-file-rules">MP4, MOV ou MKV · até 5 GB e 3 horas</span>
            </>}
          </div>
          <p className="video-privacy-note"><ShieldCheck aria-hidden="true" size={16} />O vídeo não fica público: ele só é entregue a quem tem acesso à aula.</p>
        </>}
        <input
          accept=".mp4,.mov,.mkv,video/mp4,video/quicktime,video/x-matroska"
          aria-label="Escolher arquivo de vídeo"
          className="visually-hidden"
          id="video-file"
          onChange={(event) => selectFile(event.currentTarget.files?.[0])}
          ref={fileInput}
          type="file"
        />
        <div className="dialog-actions video-upload-actions">
          <button className="outline-button" disabled={busy} onClick={onClose} type="button">Cancelar</button>
          {file ? <button className="primary-button" disabled={busy || !title.trim()} type="submit">{busy ? 'Preparando o envio…' : 'Enviar'}</button> : null}
        </div>
      </form>
    </section>
  </div>;
};
