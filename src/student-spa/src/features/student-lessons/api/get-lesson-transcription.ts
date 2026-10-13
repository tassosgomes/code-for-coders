import { queryOptions, useQuery } from '@tanstack/react-query';
import * as z from 'zod';

import { apiClient } from '@/lib/api-client';

const segmentSchema = z.object({ startMs: z.number().nonnegative(), endMs: z.number().nonnegative(), text: z.string() });
export const lessonTranscriptionSchema = z.discriminatedUnion('status', [
  z.object({ lessonId: z.uuid(), status: z.literal('ready'), revision: z.string().min(1),
    language: z.literal('pt-BR'), durationMs: z.number().nonnegative(), segments: z.array(segmentSchema) }),
  z.object({ lessonId: z.uuid(), status: z.literal('unavailable'), revision: z.null(), language: z.null(),
    durationMs: z.null(), segments: z.array(segmentSchema).length(0) }),
]);
export type LessonTranscription = z.infer<typeof lessonTranscriptionSchema>;
export type TranscriptSegment = z.infer<typeof segmentSchema>;
export const getLessonTranscription = async (lessonId: string, signal: AbortSignal): Promise<LessonTranscription> =>
  lessonTranscriptionSchema.parse(await apiClient.get(`/api/v1/lessons/${encodeURIComponent(lessonId)}/transcription`, { signal }));
export const getLessonTranscriptionQueryOptions = (lessonId: string) => queryOptions({
  queryKey: ['lesson-transcription', lessonId], queryFn: ({ signal }) => getLessonTranscription(lessonId, signal),
  staleTime: 0, gcTime: 0, retry: false, refetchOnWindowFocus: false, refetchOnReconnect: false,
});
export const useLessonTranscription = (lessonId: string, enabled = true) =>
  useQuery({ ...getLessonTranscriptionQueryOptions(lessonId), enabled });
