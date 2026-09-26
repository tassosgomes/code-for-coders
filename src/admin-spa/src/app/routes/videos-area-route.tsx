import { useOutletContext } from 'react-router';

import type { StaffSession } from '@/features/staff-session/api/staff-session';
import { useVideos } from '@/features/videos/api/get-videos';
import { VideosAreaScreen } from '@/features/videos/components/videos-area-screen';

export const VideosAreaRoute = () => {
  const session = useOutletContext<StaffSession>();
  if (!session.permissions.includes('midia.enviar')) {
    return <VideosAreaScreen state="forbidden" />;
  }

  return <VideosAreaContent />;
};

const VideosAreaContent = () => {
  const videos = useVideos();
  const state = videos.isPending
    ? 'loading'
    : videos.isError
      ? 'unavailable'
      : videos.data.data.length === 0
        ? 'empty'
        : 'has-videos';

  return <VideosAreaScreen state={state} onRetry={() => void videos.refetch()} />;
};
