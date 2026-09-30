import { queryOptions, useQuery } from '@tanstack/react-query';
import { z } from 'zod';

import { apiClient } from '@/lib/api-client';

const readyVideoSchema = z.object({
  videoId: z.string().uuid(), title: z.string(), status: z.literal('ready'),
  durationSeconds: z.number().int().positive(), uploadedBy: z.object({ accountId: z.string().uuid(), name: z.string() }),
});
const readyVideoPageSchema = z.object({ data: z.array(readyVideoSchema), pagination: z.object({
  page: z.number().int().positive(), size: z.number().int().positive(), total: z.number().int().nonnegative(), totalPages: z.number().int().nonnegative(),
}) });
export type ReadyVideoFilters = { page: number; query: string };
export const getReadyVideos = async ({ page, query }: ReadyVideoFilters) => readyVideoPageSchema.parse(
  await apiClient.get<unknown>('/api/v1/videos', { params: { _page: page, _size: 20, status: 'ready', ...(query ? { q: query } : {}) } }),
);
export const getReadyVideosQueryOptions = (filters: ReadyVideoFilters) => queryOptions({
  queryKey: ['authoring-ready-videos', filters], queryFn: () => getReadyVideos(filters),
});
export const useReadyVideos = (filters: ReadyVideoFilters) => useQuery(getReadyVideosQueryOptions(filters));
