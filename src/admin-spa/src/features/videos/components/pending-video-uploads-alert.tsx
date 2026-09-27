import type { PendingVideoUpload } from '@/features/videos/api/get-pending-video-uploads';

type PendingVideoUploadsAlertProps = {
  uploads: readonly PendingVideoUpload[];
  onSelectFile: () => void;
};

const formatBytes = (bytes: number) => {
  const divisor = bytes >= 1024 * 1024 * 1024 ? 1024 * 1024 * 1024 : 1024 * 1024;
  const unit = divisor === 1024 * 1024 * 1024 ? 'GiB' : 'MiB';
  return `${new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 1 }).format(bytes / divisor)} ${unit}`;
};

const receivedBytes = (upload: PendingVideoUpload) => upload.receivedParts.reduce((total, partNumber) => {
  const partOffset = (partNumber - 1) * upload.partSize;
  return total + Math.max(0, Math.min(upload.partSize, upload.fileSize - partOffset));
}, 0);

const formatExpiry = (value: string) => new Intl.DateTimeFormat('pt-BR', {
  day: '2-digit',
  month: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
}).format(new Date(value));

export const PendingVideoUploadsAlert = ({ uploads, onSelectFile }: PendingVideoUploadsAlertProps) => {
  if (uploads.length === 0) return null;

  return <section aria-label="Envios incompletos" className="pending-video-uploads">
    {uploads.map((upload) => <div aria-label={`Envio incompleto: ${upload.fileName}`} className="pending-video-upload warning-alert" key={upload.uploadId} role="status">
      <p className="pending-video-upload-title">
        <strong>⚠ Envio incompleto de {upload.fileName} — selecione o mesmo arquivo para continuar.</strong>
      </p>
      <p>{formatBytes(receivedBytes(upload))} de {formatBytes(upload.fileSize)} já estão na escola. Você pode continuar até {formatExpiry(upload.expiresAt)}.</p>
      <button className="outline-button" onClick={onSelectFile} type="button">Selecionar o arquivo</button>
    </div>)}
  </section>;
};
