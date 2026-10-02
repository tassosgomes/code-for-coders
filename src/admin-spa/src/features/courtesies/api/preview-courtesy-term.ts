import { queryOptions, useQuery } from '@tanstack/react-query';
import { z } from 'zod';

import type { CourtesyTermPreview } from '@/features/courtesies/types/courtesy-grant';
import { apiClient } from '@/lib/api-client';

export const courtesyTermPreviewSchema = z.object({ months: z.number().int().min(1).max(60) });
export type CourtesyTermPreviewInput = z.infer<typeof courtesyTermPreviewSchema>;
export const previewCourtesyTerm = (input: CourtesyTermPreviewInput): Promise<CourtesyTermPreview> =>
  apiClient.get('/api/v1/courtesy-term-preview', { params: input });
export const courtesyTermQueryOptions = (months: number) => queryOptions({
  queryKey: ['courtesy-term', months], queryFn: () => previewCourtesyTerm({ months }), staleTime: 0, gcTime: 0, retry: false,
});
export const useCourtesyTerm = (months: number, enabled: boolean) => useQuery({ ...courtesyTermQueryOptions(months), enabled });
