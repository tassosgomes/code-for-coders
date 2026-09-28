import { CircleCheck, Clock, LoaderCircle, TriangleAlert } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';

import type { VideoStatus } from '@/features/videos/api/get-videos';

type VideoStatusBadgeProps = {
  status: VideoStatus;
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
