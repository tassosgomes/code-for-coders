import { queryOptions, useQuery } from '@tanstack/react-query';
import { z } from 'zod';

import { apiClient } from '@/lib/api-client';

export const getVideoTranscriptionInputSchema = z.object({ videoId: z.string().uuid() });
export type GetVideoTranscriptionInput = z.infer<typeof getVideoTranscriptionInputSchema>;

export const videoTranscriptionContentSchema = z.object({
  videoId: z.string().uuid(),
  revision: z.string().regex(/^tr_[a-f0-9]{8}$/),
  language: z.string(),
  durationMs: z.number().int().nonnegative(),
  readyAt: z.string().datetime({ offset: true }),
  segments: z.array(z.object({
    startMs: z.number().int().nonnegative(), endMs: z.number().int().nonnegative(), text: z.string(),
  }).strict()),
}).strict();
export type VideoTranscriptionContent = z.infer<typeof videoTranscriptionContentSchema>;

export const getVideoTranscription = async ({ videoId }: GetVideoTranscriptionInput, signal?: AbortSignal): Promise<VideoTranscriptionContent> =>
  videoTranscriptionContentSchema.parse(await apiClient.get<unknown>(`/api/v1/videos/${videoId}/transcription`, { signal }));

export const getVideoTranscriptionQueryOptions = (input: GetVideoTranscriptionInput) => queryOptions({
  queryKey: ['video-transcription-content', input.videoId],
  queryFn: ({ signal }) => getVideoTranscription(input, signal),
  // The open reader is a snapshot; closing discards it so reopening reads the current revision.
  staleTime: Infinity, gcTime: 0, retry: false,
  refetchOnWindowFocus: false, refetchOnReconnect: false,
});

export const useVideoTranscription = (input: GetVideoTranscriptionInput) => useQuery(getVideoTranscriptionQueryOptions(input));
