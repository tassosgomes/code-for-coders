import { CircleCheck, Clock, FileMinus, LoaderCircle, TriangleAlert } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';

import type { VideoStatus, VideoTranscription } from '@/features/videos/api/get-videos';

type VideoStatusBadgeProps = {
  status: VideoStatus;
};

export const transcriptionStatusLabels: Record<VideoTranscription['status'], string> = {
  'not-requested': 'Sem transcrição',
  'in-progress': 'Transcrevendo',
  ready: 'Pronta',
  failed: 'Falhou',
};

const transcriptionIcons = {
  'not-requested': FileMinus, 'in-progress': LoaderCircle, ready: CircleCheck, failed: TriangleAlert,
};

type VideoTranscriptionStatusBadgeProps = { status: VideoTranscription['status'] };

export const VideoTranscriptionStatusBadge = ({ status }: VideoTranscriptionStatusBadgeProps) => {
  const Icon = transcriptionIcons[status];
  return <span className={`video-status-badge video-transcription-badge ${status}`}>
    <Icon aria-hidden="true" className={status === 'in-progress' ? 'video-status-spinner' : undefined} size={14} />
    {transcriptionStatusLabels[status]}
  </span>;
};

const statusContent = {
  received: { Icon: Clock, label: 'Recebido' },
  preparing: { Icon: LoaderCircle, label: 'Em preparação' },
  ready: { Icon: CircleCheck, label: 'Pronto' },
  failed: { Icon: TriangleAlert, label: 'Falhou' },
} satisfies Record<VideoStatus, { Icon: LucideIcon; label: string }>;

export const VideoStatusBadge = ({ status }: VideoStatusBadgeProps) => {
  const { Icon, label } = statusContent[status];

  return <span className={`video-status-badge ${status}`}>
    <Icon aria-hidden="true" className={status === 'preparing' ? 'video-status-spinner' : undefined} size={14} />
    {label}
  </span>;
};
