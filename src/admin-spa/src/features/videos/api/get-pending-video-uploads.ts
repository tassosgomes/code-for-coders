import { queryOptions, useQuery } from '@tanstack/react-query';
import { z } from 'zod';

import { createVideoUploadResponseSchema } from '@/features/videos/api/create-video-upload';
import { apiClient } from '@/lib/api-client';

const pendingVideoUploadPageSchema = z.object({
  data: z.array(createVideoUploadResponseSchema),
  pagination: z.object({
    page: z.number().int().positive(),
    size: z.number().int().positive(),
    total: z.number().int().nonnegative(),
    totalPages: z.number().int().nonnegative(),
  }).strict(),
}).strict();

export type PendingVideoUpload = z.infer<typeof createVideoUploadResponseSchema>;

export const getPendingVideoUploads = async (
  page = 1,
  size = 50,
  signal?: AbortSignal,
) => pendingVideoUploadPageSchema.parse(await apiClient.get<unknown>('/api/v1/video-uploads', {
  params: { _page: page, _size: size },
  signal,
}));

export const getPendingVideoUploadsQueryOptions = (page = 1, size = 50) => queryOptions({
  queryKey: ['pending-video-uploads', { page, size }],
  queryFn: ({ signal }) => getPendingVideoUploads(page, size, signal),
  staleTime: 10_000,
  refetchInterval: 10_000,
  refetchIntervalInBackground: false,
});

export const usePendingVideoUploads = (page = 1, size = 50) => useQuery(getPendingVideoUploadsQueryOptions(page, size));
