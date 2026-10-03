import * as z from 'zod';

import { apiClient } from '@/lib/api-client';

export const playbackSessionSchema = z.object({
  sessionId: z.uuid(), lessonId: z.uuid(), expiresAt: z.iso.datetime({ offset: true }), renewAfter: z.iso.datetime({ offset: true }),
  watermark: z.object({ text: z.email(), repositionSeconds: z.number().int().positive() }),
  progress: z.object({ intervalSeconds: z.number().int().positive(), minGapSeconds: z.number().int().positive() }),
  segmentAccess: z.object({ query: z.string().min(1).max(2048), expiresAt: z.iso.datetime({ offset: true }) }),
});
export type PlaybackSession = z.infer<typeof playbackSessionSchema>;

export const openPlaybackSession = async (lessonId: string, csrfToken: string, signal: AbortSignal) =>
  playbackSessionSchema.parse(await apiClient.post('/api/v1/lessons/' + encodeURIComponent(lessonId) + '/playback-sessions', undefined,
    { headers: { 'X-CSRF-Token': csrfToken }, signal }));
