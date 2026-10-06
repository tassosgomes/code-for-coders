import { useMutation } from '@tanstack/react-query';
import * as z from 'zod';

import { apiClient } from '@/lib/api-client';

export const startOrderPaymentInputSchema = z.object({ orderId: z.uuid(), csrfToken: z.string().min(1) });
export type StartOrderPaymentInput = z.infer<typeof startOrderPaymentInputSchema>;
const paymentSessionSchema = z.object({ orderId: z.string().uuid(), kind: z.enum(['checkout', 'pix-instructions', 'boleto-instructions']), paymentUrl: z.string().url(), method: z.string().nullable().optional(), expiresAt: z.string().datetime({ offset: true }) });
export const startOrderPayment = async (input: StartOrderPaymentInput): Promise<void> => {
  const { orderId, csrfToken } = startOrderPaymentInputSchema.parse(input);
  const session = paymentSessionSchema.parse(await apiClient.post(`/api/v1/orders/${encodeURIComponent(orderId)}/payment-session`, undefined, { headers: { 'X-CSRF-Token': csrfToken } }));
  const protocol = new URL(session.paymentUrl).protocol;
  if (session.orderId !== orderId || (protocol !== 'https:' && protocol !== 'http:')) throw new Error('Invalid payment session');
  window.location.assign(session.paymentUrl);
};
export const useStartOrderPayment = () => useMutation({ mutationFn: startOrderPayment, gcTime: 0 });
