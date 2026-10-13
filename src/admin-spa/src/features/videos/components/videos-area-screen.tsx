import { CircleAlert, Clapperboard, Pencil, Search, Tag } from 'lucide-react';
import { useState } from 'react';

import { ForbiddenScreen } from '@/components/forbidden-screen';
import { Toast } from '@/components/toast';
import type { PendingVideoUpload } from '@/features/videos/api/get-pending-video-uploads';
import type { VideoPage, VideoStatus } from '@/features/videos/api/get-videos';
import { PendingVideoUploadsAlert } from '@/features/videos/components/pending-video-uploads-alert';
import { VideoTagField } from '@/features/videos/components/video-tag-field';
import { VideoStatusBadge, transcriptionStatusLabels } from '@/features/videos/components/video-status-badge';
import { VideoTranscriptionStatus } from '@/features/videos/components/video-transcription-status';
import { getVideoTranscriptionFailureMessage } from '@/features/videos/utils/video-transcription';
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
  tags?: string[];
  tagMatch?: 'all' | 'any';
  onTagsChange?: (tags: string[]) => void;
  onTagMatchChange?: (match: 'all' | 'any') => void;
  onClearFilters?: () => void;
  onFilterChange?: (filter: string) => void;
  onSearchChange?: (search: string) => void;
  onEditTitle?: (video: VideoPage['data'][number]) => void;
  onEditTags?: (video: VideoPage['data'][number]) => void;
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
  const previousVideos = new Map(previous.map((video) => [video.videoId, video]));
  return current
    .flatMap((video) => {
      const before = previousVideos.get(video.videoId);
      if (!before) return [];
      const changes = [];
      if (before.status !== video.status) changes.push(statusLabels[video.status]);
      if (before.transcription.status !== video.transcription.status) {
        changes.push(video.transcription.status === 'failed'
          ? `a transcrição falhou. ${getVideoTranscriptionFailureMessage(video.transcription.failureReason)}`
          : `transcrição ${transcriptionStatusLabels[video.transcription.status].toLocaleLowerCase('pt-BR')}`);
      }
      return changes.length ? [`${video.title}: ${changes.join(', ')}`] : [];
    })
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
  tags = [],
  tagMatch = 'all',
  onTagsChange,
  onTagMatchChange,
  onClearFilters,
  onFilterChange,
  onSearchChange,
  onEditTitle,
  onEditTags,
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

  if (state === 'forbidden') return <ForbiddenScreen />;

  const sendButton = <button className="primary-button" disabled={uploadDisabled} onClick={onUpload} type="button" title={uploadDisabled ? 'Aguarde o envio atual terminar.' : undefined}>
    Enviar vídeo
  </button>;
  const hasProcessingVideo = videos?.data.some((video) => video.status === 'received' || video.status === 'preparing' || video.transcription.status === 'in-progress') ?? false;
  const showControls = state !== 'empty' || filter !== 'all' || Boolean(search) || tags.length > 0;
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

    {showControls && onTagsChange ? <div className="video-library-tag-controls">
      <VideoTagField autoFocus={false} tags={tags} disabled={false} mode="suggestion-only"
        onChange={onTagsChange} onDraftChange={() => undefined} />
      {tags.length >= 2 ? <div className="video-tag-match">
        <span id="video-tag-match-label">Combinar tags</span>
        <div aria-labelledby="video-tag-match-label" className="video-filter-options" role="group">
          <button aria-pressed={tagMatch === 'all'} className={tagMatch === 'all' ? 'video-filter-active' : ''}
            onClick={() => onTagMatchChange?.('all')} type="button">E</button>
          <button aria-pressed={tagMatch === 'any'} className={tagMatch === 'any' ? 'video-filter-active' : ''}
            onClick={() => onTagMatchChange?.('any')} type="button">OU</button>
        </div>
      </div> : null}
    </div> : null}

    {state === 'has-videos' && hasProcessingVideo ? <p className="video-refresh-status"><span aria-hidden="true" />Atualizando automaticamente enquanto há vídeo em andamento</p> : null}

    {state === 'loading' ? <section aria-label="Carregando vídeos" aria-busy="true" className="videos-table videos-loading">
      <div className="videos-table-header"><span>Título</span><span>Estado</span><span>Transcrição</span><span>Autor</span><span>Enviado</span><span>Ações</span></div>
      {Array.from({ length: 5 }, (_, index) => <div className="videos-skeleton-row" key={index}>
        <span className="skeleton-title" /><span className="skeleton-status" /><span className="skeleton-transcription" /><span className="skeleton-author" /><span className="skeleton-date" />
      </div>)}
    </section> : null}

    {state === 'empty' && (filter !== 'all' || search || tags.length > 0) ? <section className="video-empty-filter">
      <p>{tags.length ? 'Nenhum vídeo atende aos filtros.' : getEmptySearchMessage(filter, search)}</p>
      <button className="video-link-button" onClick={onClearFilters ?? (() => { onFilterChange?.('all'); onSearchChange?.(''); })} type="button">Limpar filtros</button>
    </section> : null}

    {state === 'empty' && filter === 'all' && !search && tags.length === 0 ? <section aria-label="Biblioteca de vídeos vazia" className="video-empty-library">
      <span aria-hidden="true" className="video-empty-icon"><Clapperboard size={20} /></span>
      <h2>Nenhum vídeo ainda</h2>
      <p>Envie a primeira gravação. Ela fica pronta para a aula sozinha.</p>
      {sendButton}
    </section> : null}

    {state === 'has-videos' ? <>
      <section aria-label="Biblioteca de vídeos" className="videos-table">
      <p aria-atomic="true" aria-live="polite" className="visually-hidden" role="status">{statusAnnouncement}</p>
      <div className="videos-table-header"><span>Título</span><span>Estado</span><span>Transcrição</span><span>Autor</span><span>Enviado</span><span>Ações</span></div>
      {videos?.data.map((video) => <div className="videos-table-row" key={video.videoId}>
        <div className="video-title-cell"><strong>{video.title}</strong>
          {video.tags.length ? <div className="video-row-tags" aria-label={`Tags de ${video.title}`}>{video.tags.map((tag) => <span className="video-tag-chip" key={tag}>{tag}</span>)}</div> : null}
        </div>
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
        <VideoTranscriptionStatus video={video} />
        <div className="video-meta">
          <span className="video-author">
            {video.uploadedBy.name}
            {video.uploadedBy.accountId === currentAccountId ? <small>(você)</small> : null}
          </span>
          <span aria-hidden="true" className="video-meta-separator">·</span>
          <time className="video-date" dateTime={video.uploadedAt}>{formatDate(video.uploadedAt)}</time>
        </div>
        <div className="video-row-actions">
        {onEditTitle ? <button aria-label={`Editar título de ${video.title}`} className="video-edit-title" onClick={() => onEditTitle(video)} type="button"><Pencil aria-hidden="true" size={16} /></button> : <span className="video-edit-placeholder" />}
        {onEditTags ? <button aria-label={`Editar tags de ${video.title}`} className="video-edit-title" onClick={() => onEditTags(video)} type="button"><Tag aria-hidden="true" size={16} /></button> : null}
        </div>
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

    {successMessage ? <Toast message={successMessage} /> : null}
  </main>;
};
