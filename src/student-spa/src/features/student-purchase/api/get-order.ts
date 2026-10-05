import { queryOptions, useQuery } from '@tanstack/react-query';

import { studentOrderSchema } from '@/features/student-purchase/types/purchase';
import { apiClient } from '@/lib/api-client';

export const getOrder = async (orderId: string) => studentOrderSchema.parse(await apiClient.get(`/api/v1/orders/${encodeURIComponent(orderId)}`));
export const orderQueryOptions = (orderId: string) => queryOptions({ queryKey: ['student-order', orderId], queryFn: () => getOrder(orderId), retry: false });
export const useOrder = (orderId: string) => useQuery(orderQueryOptions(orderId));
