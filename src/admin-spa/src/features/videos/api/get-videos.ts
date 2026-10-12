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
  tags: z.array(z.string()),
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

export type VideoStatus = z.infer<typeof videoStatusSchema>;

export const videoListFiltersSchema = z.object({
  page: z.number().int().positive().optional(),
  size: z.number().int().min(1).max(50).optional(),
  statuses: z.array(videoStatusSchema).optional(),
  query: z.string().trim().min(1).max(120).optional(),
  tags: z.array(z.string().min(1).max(100)).max(10).optional(),
  tagMatch: z.enum(['all', 'any']).optional(),
});

export type VideoListFilters = z.infer<typeof videoListFiltersSchema>;

export const getVideos = async ({ page = 1, size = 20, statuses = [], query, tags = [], tagMatch = 'all' }: VideoListFilters = {}): Promise<VideoPage> => {
  const params = new URLSearchParams({ _page: String(page), _size: String(size) });
  for (const status of statuses) params.append('status', status);
  if (query) params.set('q', query);
  for (const tag of tags) params.append('tag', tag);
  if (tags.length) params.set('tagMatch', tagMatch);
  return videoPageSchema.parse(await apiClient.get<unknown>('/api/v1/videos', { params }));
};

export const getVideosQueryOptions = ({ page = 1, size = 20, statuses = [], query, tags = [], tagMatch = 'all' }: VideoListFilters = {}) => queryOptions({
  queryKey: ['videos', { page, size, statuses, query: query || undefined, tags, tagMatch }],
  queryFn: () => getVideos({ page, size, statuses, query, tags, tagMatch }),
});

export const useVideos = (filters: VideoListFilters = {}) => useQuery({
  ...getVideosQueryOptions(filters),
  refetchInterval: (query) => query.state.data?.data.some((video) =>
    video.status === 'received' || video.status === 'preparing',
  ) ? 10_000 : false,
  refetchIntervalInBackground: false,
});
