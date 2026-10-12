import { useRef } from 'react';

import type { VideoPage } from '@/features/videos/api/get-videos';
import { useRequestVideoTranscription } from '@/features/videos/api/request-video-transcription';
import { VideoTranscriptionStatusBadge } from '@/features/videos/components/video-status-badge';
import { getVideoTranscriptionErrorMessage } from '@/features/videos/utils/video-transcription';

type VideoTranscriptionStatusProps = { video: VideoPage['data'][number] };

export const VideoTranscriptionStatus = ({ video }: VideoTranscriptionStatusProps) => {
  const statusText = useRef<HTMLDivElement>(null);
  const requestButton = useRef<HTMLButtonElement>(null);
  const idempotencyKey = useRef<string | null>(null);
  const request = useRequestVideoTranscription({ onSuccess: () => {
    idempotencyKey.current = null;
  } });
  const start = () => {
    if (request.isPending) return;
    // Native browsers drop focus when the active button becomes disabled.
    if (document.activeElement === requestButton.current) statusText.current?.focus();
    idempotencyKey.current ??= crypto.randomUUID();
    request.mutate({ videoId: video.videoId, idempotencyKey: idempotencyKey.current });
  };
  const transcription = video.transcription;
  return <div aria-label={`Transcrição de ${video.title}`} className="video-transcription-cell" role="group">
    <span className="video-transcription-heading">Transcrição</span>
    <div className="video-transcription-state" ref={statusText} tabIndex={-1}>
      <span className="visually-hidden">Transcrição: </span>
      {video.status === 'ready' ? <VideoTranscriptionStatusBadge status={transcription.status} /> : <span aria-hidden="true">—</span>}
      {video.status === 'failed' ? <span className="visually-hidden">Sem transcrição: o vídeo não ficou pronto</span> : null}
    </div>
    {video.status === 'received' || video.status === 'preparing' ? <small>Começa sozinha quando o vídeo ficar pronto.</small> : null}
    {video.status === 'ready' && transcription.status === 'not-requested' && !transcription.canStart ? <small>Transcrição indisponível neste ambiente.</small> : null}
    {video.status === 'ready' && transcription.status === 'in-progress' ? <small>Pode levar um tempo. Você não precisa ficar nesta tela.</small> : null}
    {video.status === 'ready' && transcription.status === 'ready' && transcription.readyAt ? <small>Pronta em {new Intl.DateTimeFormat('pt-BR', {
      day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit',
    }).format(new Date(transcription.readyAt))}</small> : null}
    {request.isError ? <p className="video-transcription-error" role="alert">{getVideoTranscriptionErrorMessage(request.error)}</p> : null}
    {video.status === 'ready' && transcription.status === 'not-requested' && transcription.canStart ? <button
      aria-label={`${request.isError ? 'Tentar de novo a transcrição de' : 'Transcrever'} ${video.title}`}
      className="video-link-button video-transcription-action" disabled={request.isPending} onClick={start} ref={requestButton} type="button"
    >{request.isError ? 'Tentar de novo' : 'Transcrever'}</button> : null}
  </div>;
};
