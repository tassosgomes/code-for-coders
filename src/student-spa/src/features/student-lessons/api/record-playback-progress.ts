import { useMutation } from '@tanstack/react-query';
import * as z from 'zod';

import { apiClient } from '@/lib/api-client';

export const progressReasonSchema = z.enum(['heartbeat', 'paused', 'left', 'ended']);
export type ProgressReason = z.infer<typeof progressReasonSchema>;

export const progressInputSchema = z.object({
  sequence: z.number().int().positive(),
  positionSeconds: z.number().int().min(0).max(43200),
  reason: progressReasonSchema,
});
export type ProgressInput = z.infer<typeof progressInputSchema>;

export const progressAckSchema = z.object({
  recorded: z.boolean(),
});
export type ProgressAck = z.infer<typeof progressAckSchema>;

export const recordPlaybackProgress = async (
  sessionId: string,
  csrfToken: string,
  input: ProgressInput,
  signal?: AbortSignal
): Promise<ProgressAck> =>
  progressAckSchema.parse(
    await apiClient.post(
      `/api/v1/playback-sessions/${encodeURIComponent(sessionId)}/progress`,
      input,
      {
        headers: { 'X-CSRF-Token': csrfToken },
        signal,
      }
    )
  );

export const useRecordPlaybackProgress = () =>
  useMutation({
    mutationFn: ({
      sessionId,
      csrfToken,
      input,
      signal,
    }: {
      sessionId: string;
      csrfToken: string;
      input: ProgressInput;
      signal?: AbortSignal;
    }) => recordPlaybackProgress(sessionId, csrfToken, input, signal),
    retry: false,
  });
