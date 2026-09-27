import { z } from 'zod';

import { apiClient } from '@/lib/api-client';

export const createVideoUploadResponseSchema = z.object({
  uploadId: z.string().uuid(),
  title: z.string(),
  fileName: z.string(),
  fileSize: z.number().int().positive(),
  partSize: z.number().int().positive(),
  partCount: z.number().int().positive(),
  receivedParts: z.array(z.number().int().positive()),
  expiresAt: z.string().datetime({ offset: true }),
}).strict();

export type VideoUpload = z.infer<typeof createVideoUploadResponseSchema>;

export type CreateVideoUploadInput = {
  title: string;
  fileName: string;
  fileSize: number;
  contentType: string;
  fingerprint: string;
};

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
