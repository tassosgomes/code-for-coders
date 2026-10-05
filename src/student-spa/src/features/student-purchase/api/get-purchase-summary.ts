import { queryOptions, useQuery } from '@tanstack/react-query';

import { purchaseSummarySchema } from '@/features/student-purchase/types/purchase';
import { apiClient } from '@/lib/api-client';

export const getPurchaseSummary = async (offerId: string) => purchaseSummarySchema.parse(await apiClient.get(`/api/v1/offers/${encodeURIComponent(offerId)}/purchase-summary`));
export const purchaseSummaryQueryOptions = (offerId: string) => queryOptions({ queryKey: ['purchase-summary', offerId], queryFn: () => getPurchaseSummary(offerId), retry: false, staleTime: 0 });
export const usePurchaseSummary = (offerId: string) => useQuery(purchaseSummaryQueryOptions(offerId));
