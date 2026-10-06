import { useMutation, useQueryClient } from '@tanstack/react-query';
import * as z from 'zod';

import { studentOrderSchema, type StudentOrder } from '@/features/student-purchase/types/purchase';
import { apiClient } from '@/lib/api-client';

export const cancelOrderInputSchema = z.object({
  orderId: z.string().uuid(),
  csrfToken: z.string().min(1),
});
export type CancelOrderInput = z.infer<typeof cancelOrderInputSchema>;

export const cancelOrder = async (input: CancelOrderInput): Promise<StudentOrder> => {
  const { orderId, csrfToken } = cancelOrderInputSchema.parse(input);
  const data = await apiClient.post(
    `/api/v1/orders/${encodeURIComponent(orderId)}/cancellation`,
    undefined,
    { headers: { 'X-CSRF-Token': csrfToken } },
  );
  return studentOrderSchema.parse(data);
};

export const useCancelOrder = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: cancelOrder,
    onSuccess: (data) => {
      queryClient.setQueryData(['student-order', data.orderId], data);
    },
  });
};
