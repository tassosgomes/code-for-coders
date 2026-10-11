import { z } from 'zod';

import { replaceVideoTagsInputSchema } from '@/features/videos/api/replace-video-tags';
import { apiClient } from '@/lib/api-client';

export const createVideoUploadResponseSchema = z.object({
  uploadId: z.string().uuid(),
  title: z.string(),
  fileName: z.string(),
  fileSize: z.number().int().positive(),
  partSize: z.number().int().positive(),
  partCount: z.number().int().positive(),
  receivedParts: z.array(z.number().int().positive()),
  tags: z.array(z.string()),
  expiresAt: z.string().datetime({ offset: true }),
}).strict();

export type VideoUpload = z.infer<typeof createVideoUploadResponseSchema>;

export const createVideoUploadInputSchema = z.object({
  title: z.string().trim().min(1, 'Dê um título para reconhecer o vídeo.').max(200, 'O título deve ter até 200 caracteres.'),
  fileName: z.string(),
  fileSize: z.number().int().positive(),
  contentType: z.string(),
  fingerprint: z.string(),
  tags: replaceVideoTagsInputSchema.shape.tags.optional(),
});
export type CreateVideoUploadInput = z.infer<typeof createVideoUploadInputSchema>;

export const createVideoUpload = async (
  input: CreateVideoUploadInput,
  idempotencyKey: string,
  signal?: AbortSignal,
): Promise<VideoUpload> => createVideoUploadResponseSchema.parse(
  await apiClient.post<unknown>('/api/v1/video-uploads', input, {
    headers: { 'Idempotency-Key': idempotencyKey },
    signal,
  }),
);
