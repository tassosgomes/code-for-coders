import { Clapperboard, Pencil, Upload } from 'lucide-react';
import { useState } from 'react';
import { Link } from 'react-router';

import { paths } from '@/config/paths';
import type { PendingVideoUpload } from '@/features/videos/api/get-pending-video-uploads';
import type { VideoPage } from '@/features/videos/api/get-videos';
import { PendingVideoUploadsAlert } from '@/features/videos/components/pending-video-uploads-alert';
import { VideoTransferPanel, type VideoTransferView } from '@/features/videos/components/video-transfer-panel';

type VideosAreaScreenProps = {
  state: 'loading' | 'empty' | 'has-videos' | 'unavailable' | 'forbidden';
  videos?: VideoPage;
  uploadDisabled?: boolean;
  transfer?: VideoTransferView | null;
  onUpload?: () => void;
  onRetry?: () => void;
  onRetryTransfer?: () => void;
  pendingUploads?: readonly PendingVideoUpload[];
  onResumeUpload?: () => void;
  filter?: string;
  search?: string;
  onFilterChange?: (filter: string) => void;
  onSearchChange?: (search: string) => void;
  onEditTitle?: (video: VideoPage['data'][number]) => void;
  onPageChange?: (page: number) => void;
  titleUpdated?: boolean;
};

type VideoSummary = VideoPage['data'][number];

const videoStatusLabels = {
  received: 'Recebido',
  preparing: 'Em preparação',
  ready: 'Pronto',
  failed: 'Falhou',
} as const;

const videoFailureMessages: Record<string, string> = {
  'unreadable-file': 'Arquivo de vídeo ilegível.',
  'unsupported-format': 'Formato de vídeo não suportado.',
  'duration-exceeded': 'Duração acima de 3 horas.',
  'preparation-failed': 'Não foi possível preparar este vídeo — envie novamente.',
};

const formatVideoFailure = (reason: string) => videoFailureMessages[reason]
  ?? 'Não foi possível preparar este vídeo — envie novamente.';

const formatDate = (value: string) => new Intl.DateTimeFormat('pt-BR', {
  dateStyle: 'medium',
  timeStyle: 'short',
}).format(new Date(value));

const formatDuration = (durationSeconds: number) => {
  const hours = Math.floor(durationSeconds / 3600);
  const minutes = Math.floor((durationSeconds % 3600) / 60);
  const seconds = durationSeconds % 60;
  return hours > 0
    ? `${hours}:${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`
    : `${minutes}:${String(seconds).padStart(2, '0')}`;
};

const formatVideoStatus = (video: VideoSummary) => video.status === 'ready' && video.durationSeconds !== null
  ? `Pronto · ${formatDuration(video.durationSeconds)}`
  : videoStatusLabels[video.status];

const describeStatusChanges = (previous: readonly VideoSummary[], current: readonly VideoSummary[]) => {
  const previousStatuses = new Map(previous.map((video) => [video.videoId, video.status]));
  return current
    .filter((video) => {
      const previousStatus = previousStatuses.get(video.videoId);
      return previousStatus !== undefined && previousStatus !== video.status;
    })
    .map((video) => `${video.title}: ${formatVideoStatus(video)}`)
    .join('. ');
};

export const VideosAreaScreen = ({
  state,
  videos,
  uploadDisabled = false,
  transfer,
  onUpload,
  onRetry,
  onRetryTransfer,
  pendingUploads = [],
  onResumeUpload,
  filter = 'all',
  search = '',
  onFilterChange,
  onSearchChange,
  onEditTitle,
  onPageChange,
  titleUpdated = false,
}: VideosAreaScreenProps) => {
  const [announcedVideos, setAnnouncedVideos] = useState(videos?.data);
  const [statusAnnouncement, setStatusAnnouncement] = useState('');
  if (videos?.data !== announcedVideos) {
    const changes = announcedVideos && videos ? describeStatusChanges(announcedVideos, videos.data) : '';
    setAnnouncedVideos(videos?.data);
    if (changes) setStatusAnnouncement(changes);
  }

  if (state === 'forbidden') {
    return <main className="page-shell videos-page">
      <p className="eyebrow">Vídeos</p>
      <h1>Sem permissão</h1>
      <section className="empty-state" role="alert">
        <h2>Você não tem permissão para acessar esta área.</h2>
        <Link className="outline-button" to={paths.home.getHref()}>Voltar para o início</Link>
      </section>
    </main>;
  }

  const sendButton = <button className="primary-button" disabled={uploadDisabled} onClick={onUpload} type="button" title={uploadDisabled ? 'Aguarde o envio atual terminar.' : undefined}>
    <Upload aria-hidden="true" size={16} />Enviar vídeo
  </button>;
  const hasProcessingVideo = videos?.data.some((video) => video.status === 'received' || video.status === 'preparing') ?? false;

  return <main className="page-shell videos-page">
    <div className="page-heading-row">
      <div>
        <p className="eyebrow">Vídeos</p>
        <h1>Vídeos da escola</h1>
        <p className="page-subtitle">Envie as gravações das aulas e acompanhe até ficarem prontas.</p>
      </div>
      {sendButton}
    </div>

    {pendingUploads.length > 0 ? <PendingVideoUploadsAlert uploads={pendingUploads} onSelectFile={onResumeUpload ?? (() => undefined)} /> : null}
    {transfer ? <VideoTransferPanel transfer={transfer} onRetry={onRetryTransfer ?? (() => undefined)} /> : null}

    {onFilterChange && onSearchChange ? <div className="video-library-controls">
      <div aria-label="Filtrar por estado" className="video-filter-options" role="group">
        {([
          ['all', 'Todos'],
          ['progress', 'Em andamento'],
          ['ready', 'Prontos'],
          ['failed', 'Falharam'],
        ] as const).map(([value, label]) => <button
          aria-pressed={filter === value}
          className={filter === value ? 'video-filter-active' : ''}
          key={value}
          onClick={() => onFilterChange(value)}
          type="button"
        >{label}</button>)}
      </div>
      <input aria-label="Buscar título" maxLength={120} onChange={(event) => onSearchChange(event.currentTarget.value)} placeholder="Buscar título" type="search" value={search} />
    </div> : null}

    {titleUpdated ? <p role="status">Título atualizado</p> : null}

    {state === 'loading' ? <section aria-label="Carregando vídeos" aria-busy="true" className="empty-state videos-state">
      <p>Carregando vídeos…</p>
    </section> : null}

    {state === 'empty' && (filter !== 'all' || search) ? <section className="empty-state videos-state">
      <p>Nenhum vídeo encontrado com esses filtros.</p>
      <button className="outline-button" onClick={() => { onFilterChange?.('all'); onSearchChange?.(''); }} type="button">Limpar filtros</button>
    </section> : null}

    {state === 'empty' && filter === 'all' && !search ? <section aria-label="Biblioteca de vídeos vazia" className="empty-state videos-state">
      <div aria-hidden="true" className="code-window">
        <div className="code-title"><span className="window-dots"><i /><i /><i /></span>videos.http</div>
        <pre>1  $ ls videos/{'\n'}2  <span>(vazio)</span></pre>
      </div>
      <h2>Nenhum vídeo ainda</h2>
      <p>Envie a primeira gravação. Ela fica pronta para a aula sozinha.</p>
      {sendButton}
    </section> : null}

    {state === 'has-videos' ? <section aria-label="Biblioteca de vídeos" className="videos-table">
      <p aria-atomic="true" aria-live="polite" className="visually-hidden" data-testid="video-status-announcement" role="status">{statusAnnouncement}</p>
      {hasProcessingVideo ? <p className="video-refresh-status">Atualizando automaticamente</p> : null}
      <div className="videos-table-header"><span>Vídeo</span><span>Autor</span><span>Estado</span><span>Enviado em</span></div>
      {videos?.data.map((video) => <div className="videos-table-row" key={video.videoId}>
        <div className="video-title-cell"><Clapperboard aria-hidden="true" size={18} /><strong>{video.title}</strong>
          {onEditTitle ? <button aria-label={`Editar título de ${video.title}`} className="video-edit-title" onClick={() => onEditTitle(video)} type="button"><Pencil aria-hidden="true" size={16} /></button> : null}
        </div>
        <span className="row-muted">{video.uploadedBy.name}</span>
        <div className={`video-status ${video.status}`}><span aria-hidden="true" />
          {formatVideoStatus(video)}
          {video.failureReason ? <small>{formatVideoFailure(video.failureReason)}</small> : null}
          {video.status === 'failed' && onUpload ? <button
            aria-label={`Enviar ${video.title} de novo`}
            className="video-retry-link"
            disabled={uploadDisabled}
            onClick={onUpload}
            type="button"
          >Enviar de novo</button> : null}
        </div>
        <time className="row-muted" dateTime={video.uploadedAt}>{formatDate(video.uploadedAt)}</time>
      </div>)}
      {videos && videos.pagination.totalPages > 1 ? <nav aria-label="Páginas de vídeos" className="video-pagination">
        <button disabled={videos.pagination.page <= 1} onClick={() => onPageChange?.(videos.pagination.page - 1)} type="button">Anterior</button>
        <span>{videos.pagination.page} de {videos.pagination.totalPages}</span>
        <button disabled={videos.pagination.page >= videos.pagination.totalPages} onClick={() => onPageChange?.(videos.pagination.page + 1)} type="button">Próxima</button>
      </nav> : null}
    </section> : null}

    {state === 'unavailable' ? <section className="empty-state videos-state" role="alert">
      <h2>Não conseguimos carregar os vídeos agora.</h2>
      <button className="outline-button" onClick={onRetry} type="button">Tentar de novo</button>
    </section> : null}
  </main>;
};
