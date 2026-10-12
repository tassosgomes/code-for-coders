import { queryOptions, useQuery } from '@tanstack/react-query';
import { z } from 'zod';

import { apiClient } from '@/lib/api-client';

export const videoTagQuerySchema = z.string().min(1).max(100).optional();
export type VideoTagQuery = z.infer<typeof videoTagQuerySchema>;
const videoTagListSchema = z.object({ data: z.array(z.string()).max(20) }).strict();

export const getVideoTags = async (query?: VideoTagQuery) =>
  videoTagListSchema.parse(await apiClient.get<unknown>('/api/v1/video-tags', { params: { q: query } }));

export const getVideoTagsQueryOptions = (query?: VideoTagQuery) => queryOptions({
  queryKey: ['video-tags', { query }],
  queryFn: () => getVideoTags(query),
});

export const useVideoTags = (query: VideoTagQuery, enabled: boolean) => useQuery({
  ...getVideoTagsQueryOptions(query), enabled,
});
