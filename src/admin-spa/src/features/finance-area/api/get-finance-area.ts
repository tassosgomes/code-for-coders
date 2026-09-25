import { queryOptions, useQuery } from '@tanstack/react-query';
import { z } from 'zod';

import { apiClient } from '@/lib/api-client';

export const financeAreaSchema = z.object({ status: z.literal('reserved') }).strict();
export type FinanceArea = z.infer<typeof financeAreaSchema>;

export const getFinanceArea = async (): Promise<FinanceArea> =>
  financeAreaSchema.parse(await apiClient.get<unknown>('/api/v1/finance-area'));

export const getFinanceAreaQueryOptions = () => queryOptions({
  queryKey: ['finance-area'],
  queryFn: getFinanceArea,
});

export const useFinanceArea = () => useQuery(getFinanceAreaQueryOptions());
