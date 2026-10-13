import { isAxiosError } from 'axios';
import { useState } from 'react';
import { CircleAlert, FileMinus, Info } from 'lucide-react';

import { Dialog } from '@/components/ui/dialog';
import { useVideoTranscription } from '@/features/videos/api/get-video-transcription';
import type { VideoPage } from '@/features/videos/api/get-videos';

type VideoTranscriptionReaderProps = { video: VideoPage['data'][number]; onClose: () => void };

const formatTime = (milliseconds: number) => {
  const seconds = Math.floor(milliseconds / 1000);
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor(seconds % 3600 / 60);
  return `${hours ? `${hours}:` : ''}${String(minutes).padStart(2, '0')}:${String(seconds % 60).padStart(2, '0')}`;
};
const formatReadyAt = (value: string) => new Intl.DateTimeFormat('pt-BR', {
  day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit',
}).format(new Date(value));

export const VideoTranscriptionReader = ({ video, onClose }: VideoTranscriptionReaderProps) => {
  const [restartingAtOpen] = useState(video.transcription.status === 'in-progress');
  const transcription = useVideoTranscription({ videoId: video.videoId });
  const notReady = isAxiosError<{ code?: string }>(transcription.error)
    && transcription.error.response?.status === 409 && transcription.error.response.data.code === 'TRANSCRIPTION_NOT_READY';
  return <Dialog busy={false} className="video-transcription-reader-dialog" description={video.title} onClose={onClose} title="Transcrição">
    {transcription.isPending ? <div aria-label="Carregando transcrição" aria-busy="true" className="video-transcription-reader-loading" role="status">
      {Array.from({ length: 8 }, (_, index) => <div aria-hidden="true" className="video-transcription-skeleton" key={index}><span /><span /></div>)}
      <p>Carregando a transcrição…</p>
    </div> : notReady ? <div className="video-transcription-reader-empty" role="alert">
      <span aria-hidden="true"><FileMinus size={24} /></span><p>Ainda não há transcrição pronta para este vídeo.</p>
    </div> : transcription.isError ? <div className="video-transcription-reader-alert" role="alert">
      <CircleAlert aria-hidden="true" size={16} /><p>Não foi possível abrir a transcrição agora.</p>
      <button className="outline-button" onClick={() => void transcription.refetch()} type="button">Tentar de novo</button>
    </div> : <>
      <p className="video-transcription-reader-meta">Pronta em {formatReadyAt(transcription.data.readyAt)} · somente leitura</p>
      {restartingAtOpen ? <div className="video-transcription-reader-notice" role="status"><Info aria-hidden="true" size={16} />
        <p>Há uma transcrição nova em andamento. Você está lendo a atual, que os alunos continuam vendo.</p></div> : null}
      <div aria-label="Texto da transcrição" className="video-transcription-reader-text" role="region" tabIndex={0}>
        <ol>{transcription.data.segments.map((segment, index) => <li key={`${segment.startMs}-${index}`}>
          <span className="video-transcription-reader-time">{formatTime(segment.startMs)}</span><span>{segment.text}</span>
        </li>)}</ol>
      </div>
    </>}
    <div className="dialog-actions"><button className="outline-button" onClick={onClose} type="button">Fechar</button></div>
  </Dialog>;
};
