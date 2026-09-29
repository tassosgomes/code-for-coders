import { Check, LoaderCircle, Pause, RotateCw, Upload } from 'lucide-react';

export type VideoTransferStatus = 'uploading' | 'reconnecting' | 'completing' | 'paused' | 'complete';

export type VideoTransferView = {
  fileName: string;
  title: string;
  progress: number;
  transferredBytes: number;
  totalBytes: number;
  remainingSeconds: number | null;
  resumedBytes: number;
  resumed: boolean;
  status: VideoTransferStatus;
  message: string | null;
};

type VideoTransferPanelProps = {
  transfer: VideoTransferView;
  onRetry: () => void;
};

const formatBytes = (bytes: number) => {
  const divisor = bytes >= 1_000_000_000 ? 1_000_000_000 : bytes >= 1_000_000 ? 1_000_000 : bytes >= 1_000 ? 1_000 : 1;
  const unit = divisor === 1_000_000_000 ? 'GB' : divisor === 1_000_000 ? 'MB' : divisor === 1_000 ? 'KB' : 'B';
  return `${new Intl.NumberFormat('pt-BR', { maximumFractionDigits: divisor === 1 ? 0 : 1 }).format(bytes / divisor)} ${unit}`;
};

const formatRemaining = (seconds: number) => {
  const minutes = Math.max(1, Math.ceil(seconds / 60));
  const hours = Math.floor(minutes / 60);
  const remainingMinutes = minutes % 60;
  const duration = hours > 0
    ? `${hours} h${remainingMinutes > 0 ? ` ${remainingMinutes} min` : ''}`
    : `${minutes} min`;
  return `cerca de ${duration} restantes`;
};

const getHeading = (transfer: VideoTransferView) => {
  if (transfer.status === 'uploading') return `${transfer.resumed ? 'Continuando' : 'Enviando'} ${transfer.fileName}`;
  if (transfer.status === 'reconnecting') return 'Conexão instável — tentando de novo...';
  if (transfer.status === 'completing') return 'Conferindo o envio...';
  if (transfer.status === 'paused') return `Envio pausado em ${transfer.progress}%`;
  return `${transfer.title} recebido`;
};

const getMessage = (transfer: VideoTransferView) => {
  if (transfer.message) return transfer.message;
  if (transfer.status === 'uploading') return 'Não feche esta aba até o envio terminar. Depois, a preparação continua sozinha.';
  if (transfer.status === 'reconnecting') return 'A parte que falhou é reenviada sozinha (até 3 tentativas).';
  if (transfer.status === 'completing') return 'Quase lá: confirmando as partes recebidas.';
  if (transfer.status === 'paused') return 'Não conseguimos continuar o envio.';
  return 'A preparação continua sozinha.';
};

export const VideoTransferPanel = ({ transfer, onRetry }: VideoTransferPanelProps) => {
  const Icon = transfer.status === 'complete'
    ? Check
    : transfer.status === 'paused'
      ? Pause
      : transfer.status === 'reconnecting'
        ? RotateCw
        : transfer.status === 'completing'
          ? LoaderCircle
          : Upload;
  const noteClass = transfer.status === 'paused'
    ? 'error'
    : transfer.status === 'reconnecting'
      ? 'warning'
      : transfer.resumed && transfer.status === 'uploading'
        ? 'success'
        : 'default';
  const remaining = transfer.remainingSeconds && transfer.status === 'uploading'
    ? ` · ${formatRemaining(transfer.remainingSeconds)}`
    : '';

  return <section aria-label="Transferência de vídeo" className={`video-transfer-panel ${transfer.status}`}>
    <div className="video-transfer-details">
      <div className="video-transfer-heading">
        <Icon aria-hidden="true" className={transfer.status === 'completing' ? 'video-status-spinner' : undefined} size={20} />
        <strong>{getHeading(transfer)}</strong>
        <span>{transfer.progress}%</span>
      </div>
      <div aria-label="Progresso do envio" aria-valuemax={100} aria-valuemin={0} aria-valuenow={transfer.progress} className="video-transfer-progress" role="progressbar">
        <span style={{ width: `${transfer.progress}%` }} />
      </div>
      <p className="video-transfer-size">{formatBytes(transfer.transferredBytes)} de {formatBytes(transfer.totalBytes)}{remaining}</p>
      <div className="video-transfer-message-row">
        <p className={`video-transfer-message ${noteClass}`} role={transfer.status === 'paused' ? 'alert' : 'status'}>{getMessage(transfer)}</p>
        {transfer.status === 'paused' ? <button className="primary-button video-transfer-retry" onClick={onRetry} type="button">Tentar de novo</button> : null}
      </div>
    </div>
  </section>;
};
