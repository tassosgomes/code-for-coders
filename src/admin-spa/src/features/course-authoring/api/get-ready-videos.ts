import { queryOptions, useQuery } from '@tanstack/react-query';
import { z } from 'zod';

import { apiClient } from '@/lib/api-client';

const readyVideoSchema = z.object({
  videoId: z.string().uuid(), title: z.string(), status: z.literal('ready'),
  durationSeconds: z.number().int().positive(), uploadedBy: z.object({ accountId: z.string().uuid(), name: z.string() }),
  tags: z.array(z.string()),
});
const readyVideoPageSchema = z.object({ data: z.array(readyVideoSchema), pagination: z.object({
  page: z.number().int().positive(), size: z.number().int().positive(), total: z.number().int().nonnegative(), totalPages: z.number().int().nonnegative(),
}) });
export type ReadyVideoFilters = { page: number; query: string; tags: string[]; tagMatch: 'all' | 'any' };
export const getReadyVideos = async ({ page, query, tags, tagMatch }: ReadyVideoFilters) => {
  const params = new URLSearchParams({ _page: String(page), _size: '20', status: 'ready' });
  if (query) params.set('q', query);
  for (const tag of tags) params.append('tag', tag);
  if (tags.length) params.set('tagMatch', tagMatch);
  return readyVideoPageSchema.parse(await apiClient.get<unknown>('/api/v1/videos', { params }));
};
export const getReadyVideosQueryOptions = (filters: ReadyVideoFilters) => queryOptions({
  queryKey: ['authoring-ready-videos', filters], queryFn: () => getReadyVideos(filters),
});
export const useReadyVideos = (filters: ReadyVideoFilters) => useQuery(getReadyVideosQueryOptions(filters));
