import { useMutation } from '@tanstack/react-query';

import { playbackSessionSchema } from '@/features/student-lessons/api/open-playback-session';
import { apiClient } from '@/lib/api-client';

type RenewPlaybackInput = { sessionId: string; csrfToken: string; signal: AbortSignal };

export const renewPlaybackSession = async ({ sessionId, csrfToken, signal }: RenewPlaybackInput) =>
  playbackSessionSchema.parse(await apiClient.post('/api/v1/playback-sessions/' + encodeURIComponent(sessionId) + '/renewals', undefined,
    { headers: { 'X-CSRF-Token': csrfToken }, signal }));

export const useRenewPlaybackSession = () => useMutation({ mutationFn: renewPlaybackSession, retry: false });
