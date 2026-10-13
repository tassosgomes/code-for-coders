import { queryOptions, useQuery } from '@tanstack/react-query';
import * as z from 'zod';

import { apiClient } from '@/lib/api-client';

export const playbackCaptionsInputSchema = z.object({ sessionId: z.uuid() });
export type PlaybackCaptionsInput = z.infer<typeof playbackCaptionsInputSchema>;
export const getPlaybackCaptions = async (sessionId: string, signal: AbortSignal): Promise<string> =>
  z.string().startsWith('WEBVTT').parse(await apiClient.get(`/api/v1/playback-sessions/${encodeURIComponent(sessionId)}/captions`, {
    signal, responseType: 'text', headers: { Accept: 'text/vtt' },
  }));
export const getPlaybackCaptionsQueryOptions = (sessionId: string) => queryOptions({
  queryKey: ['playback-captions', sessionId], queryFn: ({ signal }) => getPlaybackCaptions(sessionId, signal),
  staleTime: Infinity, gcTime: 0, retry: false, refetchOnWindowFocus: false, refetchOnReconnect: false,
});
export const usePlaybackCaptions = (sessionId: string, enabled: boolean) =>
  useQuery({ ...getPlaybackCaptionsQueryOptions(sessionId), enabled });
