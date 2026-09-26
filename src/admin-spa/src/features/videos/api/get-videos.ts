import { queryOptions, useQuery } from '@tanstack/react-query';
import { z } from 'zod';

import { apiClient } from '@/lib/api-client';

const videoStatusSchema = z.enum(['received', 'preparing', 'ready', 'failed']);

export const videoSchema = z.object({
  videoId: z.string().uuid(),
  title: z.string(),
  status: videoStatusSchema,
  uploadedBy: z.object({
    accountId: z.string().uuid(),
    name: z.string(),
  }).strict(),
  uploadedAt: z.string().datetime({ offset: true }),
  durationSeconds: z.number().int().nullable(),
  failureReason: z.string().nullable(),
}).strict();

export const videoPageSchema = z.object({
  data: z.array(videoSchema),
  pagination: z.object({
    page: z.number().int().positive(),
    size: z.number().int().positive(),
    total: z.number().int().nonnegative(),
    totalPages: z.number().int().nonnegative(),
  }).strict(),
}).strict();

export type VideoPage = z.infer<typeof videoPageSchema>;

export const getVideos = async (page = 1, size = 10): Promise<VideoPage> =>
  videoPageSchema.parse(await apiClient.get<unknown>('/api/v1/videos', {
    params: { _page: page, _size: size },
  }));

export const getVideosQueryOptions = (page = 1, size = 10) => queryOptions({
  queryKey: ['videos', { page, size }],
  queryFn: () => getVideos(page, size),
});

export const useVideos = (page = 1, size = 10) => useQuery(getVideosQueryOptions(page, size));
