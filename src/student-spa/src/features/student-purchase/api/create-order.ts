import { useMutation } from '@tanstack/react-query';
import * as z from 'zod';

import { studentOrderSchema } from '@/features/student-purchase/types/purchase';
import { apiClient } from '@/lib/api-client';

export const createOrderInputSchema = z.object({ offerId: z.uuid() }).strict();
export type CreateOrderInput = z.infer<typeof createOrderInputSchema>;
type CreateOrderCommand = { input: CreateOrderInput; idempotencyKey: string; csrfToken: string };
export const createOrder = async ({ input, idempotencyKey, csrfToken }: CreateOrderCommand) => studentOrderSchema.parse(await apiClient.post('/api/v1/orders', createOrderInputSchema.parse(input), { headers: { 'Idempotency-Key': idempotencyKey, 'X-CSRF-Token': csrfToken } }));
export const useCreateOrder = () => useMutation({ mutationFn: createOrder });
