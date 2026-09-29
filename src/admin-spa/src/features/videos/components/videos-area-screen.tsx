import { CircleAlert, Clapperboard, Pencil, Search } from 'lucide-react';
import { useState } from 'react';
import { Link } from 'react-router';

import { paths } from '@/config/paths';
import type { PendingVideoUpload } from '@/features/videos/api/get-pending-video-uploads';
import type { VideoPage, VideoStatus } from '@/features/videos/api/get-videos';
import { PendingVideoUploadsAlert } from '@/features/videos/components/pending-video-uploads-alert';
import { VideoStatusBadge } from '@/features/videos/components/video-status-badge';
import { VideoTransferPanel, type VideoTransferView } from '@/features/videos/components/video-transfer-panel';

type VideosAreaScreenProps = {
  state: 'loading' | 'empty' | 'has-videos' | 'unavailable' | 'forbidden';
  videos?: VideoPage;
  currentAccountId?: string;
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
  successMessage?: string | null;
};

type VideoSummary = VideoPage['data'][number];

const statusLabels: Record<VideoStatus, string> = {
  received: 'Recebido',
  preparing: 'Em preparação',
  ready: 'Pronto',
  failed: 'Falhou',
};

const videoFailureMessages: Record<string, string> = {
  'unreadable-file': 'Arquivo de vídeo ilegível.',
  'unsupported-format': 'Formato de vídeo não suportado.',
  'duration-exceeded': 'Duração acima de 3 horas.',
  'preparation-failed': 'Não foi possível preparar este vídeo — envie novamente.',
};

const formatVideoFailure = (reason: string) => videoFailureMessages[reason]
  ?? 'Não foi possível preparar este vídeo — envie novamente.';

const formatDate = (value: string) => {
  const parts = new Intl.DateTimeFormat('pt-BR', {
    day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit',
  }).formatToParts(new Date(value));
  const part = (type: Intl.DateTimeFormatPartTypes) => parts.find((item) => item.type === type)?.value ?? '';
  return `${part('day')}/${part('month')} ${part('hour')}:${part('minute')}`;
};

const formatDuration = (durationSeconds: number) => {
  const hours = Math.floor(durationSeconds / 3600);
  const minutes = Math.floor((durationSeconds % 3600) / 60);
  const seconds = durationSeconds % 60;
  return hours > 0
    ? `${hours}:${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`
    : `${minutes}:${String(seconds).padStart(2, '0')}`;
};

const describeStatusChanges = (previous: readonly VideoSummary[], current: readonly VideoSummary[]) => {
  const previousStatuses = new Map(previous.map((video) => [video.videoId, video.status]));
  return current
    .filter((video) => {
      const previousStatus = previousStatuses.get(video.videoId);
      return previousStatus !== undefined && previousStatus !== video.status;
    })
    .map((video) => `${video.title}: ${statusLabels[video.status]}`)
    .join('. ');
};

const getEmptySearchMessage = (filter: string, search: string) => {
  const quotedSearch = `“${search}”`;
  if (filter === 'failed' && search) return `Nenhum vídeo que falhou tem ${quotedSearch} no título.`;
  if (filter === 'failed') return 'Nenhum vídeo falhou.';
  if (filter === 'ready' && search) return `Nenhum vídeo pronto tem ${quotedSearch} no título.`;
  if (filter === 'ready') return 'Nenhum vídeo pronto.';
  if (filter === 'progress' && search) return `Nenhum vídeo em andamento tem ${quotedSearch} no título.`;
  if (filter === 'progress') return 'Nenhum vídeo em andamento.';
  return `Nenhum vídeo tem ${quotedSearch} no título.`;
};

export const VideosAreaScreen = ({
  state,
  videos,
  currentAccountId,
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
  successMessage,
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
    Enviar vídeo
  </button>;
  const hasProcessingVideo = videos?.data.some((video) => video.status === 'received' || video.status === 'preparing') ?? false;
  const showControls = state !== 'empty' || filter !== 'all' || Boolean(search);
  const totalPages = videos?.pagination.totalPages ?? 0;

  return <main className="page-shell videos-page">
    <div className="page-heading-row">
      <div>
        <p className="eyebrow">Vídeos</p>
        <h1>Vídeos da escola</h1>
        <p className="page-subtitle"><span className="video-subtitle-desktop">Envie as gravações das aulas e acompanhe até ficarem prontas.</span><span className="video-subtitle-mobile">Envie as gravações e acompanhe até ficarem prontas.</span></p>
      </div>
      <div className="video-header-action">
        {sendButton}
        {uploadDisabled ? <span>Aguarde o envio atual terminar.</span> : null}
      </div>
    </div>

    {pendingUploads.length > 0 ? <PendingVideoUploadsAlert uploads={pendingUploads} onSelectFile={onResumeUpload ?? (() => undefined)} /> : null}
    {transfer ? <VideoTransferPanel transfer={transfer} onRetry={onRetryTransfer ?? (() => undefined)} /> : null}

    {showControls && onFilterChange && onSearchChange ? <div className="video-library-controls">
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
      <label className="video-search-control">
        <Search aria-hidden="true" className="video-search-icon" size={14} />
        <input aria-label="Buscar título" maxLength={120} onChange={(event) => onSearchChange(event.currentTarget.value)} placeholder="Buscar título" type="search" value={search} />
      </label>
    </div> : null}

    {state === 'has-videos' && hasProcessingVideo ? <p className="video-refresh-status"><span aria-hidden="true" />Atualizando automaticamente enquanto há vídeo em andamento</p> : null}

    {state === 'loading' ? <section aria-label="Carregando vídeos" aria-busy="true" className="videos-table videos-loading">
      <div className="videos-table-header"><span>Título</span><span>Estado</span><span>Autor</span><span>Enviado</span><span>Ações</span></div>
      {Array.from({ length: 5 }, (_, index) => <div className="videos-skeleton-row" key={index}>
        <span className="skeleton-title" /><span className="skeleton-status" /><span className="skeleton-author" /><span className="skeleton-date" />
      </div>)}
    </section> : null}

    {state === 'empty' && (filter !== 'all' || search) ? <section className="video-empty-filter">
      <p>{getEmptySearchMessage(filter, search)}</p>
      <button className="video-link-button" onClick={() => { onFilterChange?.('all'); onSearchChange?.(''); }} type="button">Limpar filtros</button>
    </section> : null}

    {state === 'empty' && filter === 'all' && !search ? <section aria-label="Biblioteca de vídeos vazia" className="video-empty-library">
      <span aria-hidden="true" className="video-empty-icon"><Clapperboard size={20} /></span>
      <h2>Nenhum vídeo ainda</h2>
      <p>Envie a primeira gravação. Ela fica pronta para a aula sozinha.</p>
      {sendButton}
    </section> : null}

    {state === 'has-videos' ? <>
      <section aria-label="Biblioteca de vídeos" className="videos-table">
      <p aria-atomic="true" aria-live="polite" className="visually-hidden" role="status">{statusAnnouncement}</p>
      <div className="videos-table-header"><span>Título</span><span>Estado</span><span>Autor</span><span>Enviado</span><span>Ações</span></div>
      {videos?.data.map((video) => <div className="videos-table-row" key={video.videoId}>
        <strong className="video-title-cell">{video.title}</strong>
        <div className={`video-status-cell ${video.status}`}>
          <div className="video-status-content">
            <VideoStatusBadge status={video.status} />
            {video.status === 'ready' && video.durationSeconds !== null ? <span className="video-duration">{formatDuration(video.durationSeconds)}</span> : null}
          </div>
          {video.failureReason ? <small className="video-failure-reason">{formatVideoFailure(video.failureReason)}</small> : null}
          {video.status === 'failed' && onUpload ? <button
            aria-label={`Enviar ${video.title} de novo`}
            className="video-retry-link"
            disabled={uploadDisabled}
            onClick={onUpload}
            type="button"
          >Enviar de novo</button> : null}
        </div>
        <div className="video-meta">
          <span className="video-author">
            {video.uploadedBy.name}
            {video.uploadedBy.accountId === currentAccountId ? <small>(você)</small> : null}
          </span>
          <span aria-hidden="true" className="video-meta-separator">·</span>
          <time className="video-date" dateTime={video.uploadedAt}>{formatDate(video.uploadedAt)}</time>
        </div>
        {onEditTitle ? <button aria-label={`Editar título de ${video.title}`} className="video-edit-title" onClick={() => onEditTitle(video)} type="button"><Pencil aria-hidden="true" size={16} /></button> : <span className="video-edit-placeholder" />}
      </div>)}
      </section>
      {videos && totalPages > 1 ? <nav aria-label="Paginação dos vídeos" className="video-pagination">
        <span>{videos.pagination.total} vídeos · página {videos.pagination.page} de {totalPages}</span>
        <div className="video-pagination-pages">
          <button aria-label="Página anterior" disabled={videos.pagination.page <= 1} onClick={() => onPageChange?.(videos.pagination.page - 1)} type="button">‹</button>
          {Array.from({ length: totalPages }, (_, index) => index + 1).map((pageNumber) => <button
            aria-current={videos.pagination.page === pageNumber ? 'page' : undefined}
            aria-label={`Página ${pageNumber}`}
            className={videos.pagination.page === pageNumber ? 'video-page-active' : ''}
            key={pageNumber}
            onClick={() => onPageChange?.(pageNumber)}
            type="button"
          >{pageNumber}</button>)}
          <button aria-label="Próxima página" disabled={videos.pagination.page >= totalPages} onClick={() => onPageChange?.(videos.pagination.page + 1)} type="button">›</button>
        </div>
      </nav> : null}
    </> : null}

    {state === 'unavailable' ? <section className="video-unavailable-alert" role="alert">
      <p className="video-unavailable-title"><CircleAlert aria-hidden="true" size={16} />Não conseguimos carregar os vídeos agora.</p>
      <p>O serviço de vídeos não respondeu. Seus vídeos continuam guardados; tente de novo em instantes.</p>
      <button className="video-link-button" onClick={onRetry} type="button">Tentar de novo</button>
    </section> : null}

    {successMessage ? <p className="videos-toast" role="status"><span aria-hidden="true">✓</span>{successMessage}</p> : null}
  </main>;
};
