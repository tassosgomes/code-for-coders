import { z } from 'zod';

import { apiClient } from '@/lib/api-client';

const videoPartUrlsResponseSchema = z.object({
  parts: z.array(z.object({
    partNumber: z.number().int().positive(),
    url: z.string().url(),
    expiresAt: z.string().datetime({ offset: true }),
  }).strict()),
  uploadExpiresAt: z.string().datetime({ offset: true }),
}).strict();

export type VideoPartUrls = z.infer<typeof videoPartUrlsResponseSchema>;

export const createVideoUploadPartUrls = async (
  uploadId: string,
  partNumbers: readonly number[],
  signal?: AbortSignal,
): Promise<VideoPartUrls> => videoPartUrlsResponseSchema.parse(
  await apiClient.post<unknown>(`/api/v1/video-uploads/${uploadId}/part-urls`, { partNumbers }, { signal }),
);
