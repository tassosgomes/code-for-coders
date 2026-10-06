import { queryOptions, useQuery } from '@tanstack/react-query';
import { z } from 'zod';

import { apiClient } from '@/lib/api-client';
import type { FinanceOrderPage } from '@/features/finance-area/types/finance-order';

export const financeOrderFiltersSchema = z.object({
  status: z.enum(['', 'awaiting-payment', 'paid', 'expired', 'cancelled']),
  courseId: z.union([z.uuid(), z.literal('')]), studentId: z.union([z.uuid(), z.literal('')]),
  createdFrom: z.union([z.iso.date(), z.literal('')]), createdTo: z.union([z.iso.date(), z.literal('')]),
}).refine((input) => !input.createdFrom || !input.createdTo || input.createdTo >= input.createdFrom,
  { path: ['createdTo'], message: 'A data final não pode ser anterior à inicial.' });
export type FinanceOrderFilters = z.infer<typeof financeOrderFiltersSchema>;
export type FinanceOrderListInput = FinanceOrderFilters & { page: number };

export const listFinanceOrders = (input: FinanceOrderListInput): Promise<FinanceOrderPage> => apiClient.get('/api/v1/finance/orders', {
  params: { ...Object.fromEntries(Object.entries(input).filter(([key, value]) => key !== 'page' && value !== '')), _page: input.page, _size: 20 },
});
export const financeOrdersQueryOptions = (input: FinanceOrderListInput) => queryOptions({
  queryKey: ['finance-orders', input], queryFn: () => listFinanceOrders(input), gcTime: 0, staleTime: 0, retry: false,
});
export const useFinanceOrders = (input: FinanceOrderListInput) => useQuery(financeOrdersQueryOptions(input));
