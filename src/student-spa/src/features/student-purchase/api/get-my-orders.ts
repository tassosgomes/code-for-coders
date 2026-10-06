import { queryOptions, useQuery } from '@tanstack/react-query';
import * as z from 'zod';

import { studentOrderSchema } from '@/features/student-purchase/types/purchase';
import { apiClient } from '@/lib/api-client';

export const myOrdersInputSchema = z.object({ page: z.number().int().positive().default(1), size: z.number().int().min(1).max(50).default(10) });
export type MyOrdersInput = z.input<typeof myOrdersInputSchema>;
const myOrdersSchema = z.object({
  data: z.array(studentOrderSchema),
  pagination: z.object({ page: z.number().int().positive(), size: z.number().int().min(1).max(50), total: z.number().int().nonnegative(), totalPages: z.number().int().nonnegative() }),
});

export const getMyOrders = async (input: MyOrdersInput = {}) => {
  const { page, size } = myOrdersInputSchema.parse(input);
  return myOrdersSchema.parse(await apiClient.get('/api/v1/orders', { params: { _page: page, _size: size } }));
};
export const myOrdersQueryOptions = (input: MyOrdersInput = {}) => queryOptions({
  queryKey: ['student-my-orders', myOrdersInputSchema.parse(input)], queryFn: () => getMyOrders(input), retry: false,
});
export const useMyOrders = (input: MyOrdersInput = {}) => useQuery(myOrdersQueryOptions(input));
