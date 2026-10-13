import { useEffect, useRef, useState } from 'react';

import { Toast } from '@/components/toast';
import { Dialog } from '@/components/ui/dialog';
import type { VideoPage } from '@/features/videos/api/get-videos';
import { useRequestVideoTranscription } from '@/features/videos/api/request-video-transcription';
import { VideoTranscriptionStatusBadge } from '@/features/videos/components/video-status-badge';
import {
  currentTranscriptionRemainsMessage, getVideoTranscriptionErrorMessage, getVideoTranscriptionFailureMessage,
} from '@/features/videos/utils/video-transcription';

type VideoTranscriptionStatusProps = { video: VideoPage['data'][number] };

const toastDurationMs = 5000;
const formatReadyAt = (value: string) => new Intl.DateTimeFormat('pt-BR', {
  day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit',
}).format(new Date(value));

export const VideoTranscriptionStatus = ({ video }: VideoTranscriptionStatusProps) => {
  const statusText = useRef<HTMLDivElement>(null);
  const requestButton = useRef<HTMLButtonElement>(null);
  const idempotencyKey = useRef<string | null>(null);
  const focusStatusAfterRequest = useRef(false);
  const replacesCurrentText = useRef(false);
  const [confirming, setConfirming] = useState(false);
  const [toast, setToast] = useState<string | null>(null);
  const request = useRequestVideoTranscription({ onSuccess: () => {
    idempotencyKey.current = null;
    focusStatusAfterRequest.current = true;
    setConfirming(false);
    if (replacesCurrentText.current) setToast('Transcrição pedida');
  }, onError: () => setConfirming(false) });
  const transcription = video.transcription;
  const restarting = transcription.status === 'ready' || transcription.status === 'failed';
  useEffect(() => {
    // The action that was clicked disappears once the server reports the attempt in progress.
    if (focusStatusAfterRequest.current) {
      focusStatusAfterRequest.current = false;
      statusText.current?.focus();
    }
  });
  useEffect(() => {
    if (!toast) return;
    const timer = window.setTimeout(() => setToast(null), toastDurationMs);
    return () => window.clearTimeout(timer);
  }, [toast]);
  const start = () => {
    if (request.isPending) return;
    // Native browsers drop focus when the active button becomes disabled.
    if (document.activeElement === requestButton.current) statusText.current?.focus();
    idempotencyKey.current ??= crypto.randomUUID();
    replacesCurrentText.current = restarting && transcription.readable;
    request.mutate({ videoId: video.videoId, idempotencyKey: idempotencyKey.current });
  };
  // Replacing the text students see asks first; a retry after an error was already confirmed.
  const askToRestart = () => {
    if (restarting && transcription.readable && !request.isError) setConfirming(true);
    else start();
  };
  const ready = video.status === 'ready';
  const canTranscribe = ready && transcription.status === 'not-requested' && transcription.canStart;
  const canRestart = ready && restarting && transcription.canStart;
  return <div aria-label={`Transcrição de ${video.title}`} className="video-transcription-cell" role="group">
    <span className="video-transcription-heading">Transcrição</span>
    <div className="video-transcription-state" ref={statusText} tabIndex={-1}>
      <span className="visually-hidden">Transcrição: </span>
      {ready ? <VideoTranscriptionStatusBadge status={transcription.status} /> : <span aria-hidden="true">—</span>}
      {video.status === 'failed' ? <span className="visually-hidden">Sem transcrição: o vídeo não ficou pronto</span> : null}
    </div>
    {video.status === 'received' || video.status === 'preparing' ? <small>Começa sozinha quando o vídeo ficar pronto.</small> : null}
    {ready && transcription.status === 'not-requested' && !transcription.canStart ? <small>Transcrição indisponível neste ambiente.</small> : null}
    {ready && transcription.status === 'in-progress' && !transcription.readable ? <small>Pode levar um tempo. Você não precisa ficar nesta tela.</small> : null}
    {ready && transcription.status === 'in-progress' && transcription.readable ? <small>{currentTranscriptionRemainsMessage}</small> : null}
    {ready && transcription.status === 'ready' && transcription.readyAt ? <small>Pronta em {formatReadyAt(transcription.readyAt)}</small> : null}
    {ready && transcription.status === 'failed' ? <small>{getVideoTranscriptionFailureMessage(transcription.failureReason)}</small> : null}
    {ready && transcription.status === 'failed' && transcription.readable ? <small>{currentTranscriptionRemainsMessage}</small> : null}
    {request.isError ? <p className="video-transcription-error" role="alert">{getVideoTranscriptionErrorMessage(request.error)}</p> : null}
    {canTranscribe || canRestart ? <button
      aria-label={`${request.isError ? 'Tentar de novo a transcrição de' : canRestart ? 'Refazer' : 'Transcrever'} ${video.title}`}
      className="video-link-button video-transcription-action" disabled={request.isPending} onClick={canRestart ? askToRestart : start} ref={requestButton} type="button"
    >{request.isError ? 'Tentar de novo' : canRestart ? 'Refazer' : 'Transcrever'}</button> : null}
    {confirming ? <Dialog busy={request.isPending} description={video.title} onClose={() => setConfirming(false)} role="alertdialog" title="Refazer a transcrição?">
      <p>Os alunos continuam vendo a transcrição atual até a nova ficar pronta. Quando ficar, ela substitui a atual na aula, sem precisar publicar o curso de novo.</p>
      <div className="dialog-actions">
        <button className="outline-button" disabled={request.isPending} onClick={() => setConfirming(false)} type="button">Cancelar</button>
        <button className="primary-button" disabled={request.isPending} onClick={start} type="button">{request.isPending ? 'Pedindo…' : 'Refazer'}</button>
      </div>
    </Dialog> : null}
    {toast ? <Toast message={toast} /> : null}
  </div>;
};
