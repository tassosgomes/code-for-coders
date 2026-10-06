import { queryOptions, useQuery } from '@tanstack/react-query';
import { z } from 'zod';

import { apiClient } from '@/lib/api-client';
import type { FinanceOrderDetail } from '@/features/finance-area/types/finance-order';

export const financeOrderInputSchema = z.object({ orderId: z.uuid() });
export type FinanceOrderInput = z.infer<typeof financeOrderInputSchema>;
export const getFinanceOrder = ({ orderId }: FinanceOrderInput): Promise<FinanceOrderDetail> => apiClient.get(`/api/v1/finance/orders/${orderId}`);
export const financeOrderQueryOptions = (input: FinanceOrderInput) => queryOptions({
  queryKey: ['finance-order', input], queryFn: () => getFinanceOrder(input), gcTime: 0, staleTime: 0, retry: false,
});
export const useFinanceOrder = (input: FinanceOrderInput) => useQuery(financeOrderQueryOptions(input));
