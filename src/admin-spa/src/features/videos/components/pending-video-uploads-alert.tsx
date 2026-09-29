import { CircleAlert } from 'lucide-react';

import type { PendingVideoUpload } from '@/features/videos/api/get-pending-video-uploads';

type PendingVideoUploadsAlertProps = {
  uploads: readonly PendingVideoUpload[];
  onSelectFile: () => void;
};

const formatBytes = (bytes: number) => {
  const divisor = bytes >= 1_000_000_000 ? 1_000_000_000 : bytes >= 1_000_000 ? 1_000_000 : bytes >= 1_000 ? 1_000 : 1;
  const unit = divisor === 1_000_000_000 ? 'GB' : divisor === 1_000_000 ? 'MB' : divisor === 1_000 ? 'KB' : 'B';
  return `${new Intl.NumberFormat('pt-BR', { maximumFractionDigits: divisor === 1 ? 0 : 1 }).format(bytes / divisor)} ${unit}`;
};

const receivedBytes = (upload: PendingVideoUpload) => upload.receivedParts.reduce((total, partNumber) => {
  const partOffset = (partNumber - 1) * upload.partSize;
  return total + Math.max(0, Math.min(upload.partSize, upload.fileSize - partOffset));
}, 0);

const formatExpiry = (value: string) => {
  const parts = new Intl.DateTimeFormat('pt-BR', {
    day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit',
  }).formatToParts(new Date(value));
  const part = (type: Intl.DateTimeFormatPartTypes) => parts.find((item) => item.type === type)?.value ?? '';
  return `${part('day')}/${part('month')} às ${part('hour')}:${part('minute')}`;
};

export const PendingVideoUploadsAlert = ({ uploads, onSelectFile }: PendingVideoUploadsAlertProps) => {
  if (uploads.length === 0) return null;

  if (uploads.length > 1) return <section aria-label="Envios incompletos" className="pending-video-uploads">
    <div className="pending-video-upload pending-video-upload-group" role="status">
      <p className="pending-video-upload-title"><strong>{uploads.length} envios incompletos — selecione o mesmo arquivo para continuar cada um.</strong></p>
      {uploads.map((upload) => <div className="pending-video-upload-row" key={upload.uploadId}>
        <p>{upload.fileName} · {formatBytes(receivedBytes(upload))} de {formatBytes(upload.fileSize)} · até {formatExpiry(upload.expiresAt)}</p>
        <button className="outline-button" onClick={onSelectFile} type="button">Selecionar<span className="visually-hidden"> {upload.fileName}</span></button>
      </div>)}
    </div>
  </section>;

  return <section aria-label="Envios incompletos" className="pending-video-uploads">
    {uploads.map((upload) => <div aria-label={`Envio incompleto: ${upload.fileName}`} className="pending-video-upload" key={upload.uploadId} role="status">
      <p className="pending-video-upload-title">
        <CircleAlert aria-hidden="true" size={16} />
        <strong>Envio incompleto de {upload.fileName} — selecione o mesmo arquivo para continuar.</strong>
      </p>
      <p>{formatBytes(receivedBytes(upload))} de {formatBytes(upload.fileSize)} já estão na escola. Você pode continuar até {formatExpiry(upload.expiresAt)}.</p>
      <button className="video-link-button" onClick={onSelectFile} type="button">Selecionar o arquivo</button>
    </div>)}
  </section>;
};
