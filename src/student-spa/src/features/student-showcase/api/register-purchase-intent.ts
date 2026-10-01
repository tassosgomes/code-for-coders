import { useMutation } from '@tanstack/react-query';
import { z } from 'zod';

import { apiClient } from '@/lib/api-client';

export const purchaseIntentInputSchema = z.object({ offerId: z.uuid(), idempotencyKey: z.string().min(1).max(128) });
export type PurchaseIntentInput = z.infer<typeof purchaseIntentInputSchema>;

export const registerPurchaseIntent = ({ offerId, idempotencyKey }: PurchaseIntentInput): Promise<{ purchaseAvailability: 'coming-soon' }> =>
  apiClient.post(`/api/v1/showcase/offers/${offerId}/purchase-intents`, undefined, {
    headers: { 'Idempotency-Key': idempotencyKey },
    adapter: 'fetch',
    withCredentials: false,
    withXSRFToken: false,
  });

export const useRegisterPurchaseIntent = () => useMutation({ mutationFn: registerPurchaseIntent, retry: false });
