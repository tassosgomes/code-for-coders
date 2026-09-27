import { Check, RotateCw, Upload } from 'lucide-react';

export type VideoTransferStatus = 'uploading' | 'reconnecting' | 'completing' | 'paused' | 'complete';

export type VideoTransferView = {
  fileName: string;
  title: string;
  progress: number;
  status: VideoTransferStatus;
  message: string | null;
};

type VideoTransferPanelProps = {
  transfer: VideoTransferView;
  onRetry: () => void;
};

const statusLabels: Record<VideoTransferStatus, string> = {
  uploading: 'Enviando vídeo',
  reconnecting: 'Reconectando…',
  completing: 'Concluindo envio…',
  paused: 'Envio pausado',
  complete: 'Vídeo recebido',
};

export const VideoTransferPanel = ({ transfer, onRetry }: VideoTransferPanelProps) => (
  <section aria-label="Transferência de vídeo" className="video-transfer-panel">
    <div aria-hidden="true" className={`video-transfer-icon ${transfer.status}`}>
      {transfer.status === 'complete' ? <Check size={18} /> : transfer.status === 'paused' ? <RotateCw size={18} /> : <Upload size={18} />}
    </div>
    <div className="video-transfer-details">
      <div className="video-transfer-heading">
        <strong>{transfer.title}</strong>
        <span aria-label={statusLabels[transfer.status]} role={transfer.status === 'complete' ? 'status' : 'text'}>{statusLabels[transfer.status]}</span>
      </div>
      <progress aria-label="Progresso do envio" max={100} value={transfer.progress} />
      {transfer.message ? <p className={transfer.status === 'paused' ? 'video-transfer-error' : 'video-transfer-note'} role={transfer.status === 'paused' ? 'alert' : 'status'}>{transfer.message}</p> : null}
      {transfer.status === 'paused' ? <button className="outline-button video-transfer-retry" onClick={onRetry} type="button">Tentar novamente</button> : null}
    </div>
  </section>
);
