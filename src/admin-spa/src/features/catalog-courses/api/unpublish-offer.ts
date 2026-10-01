import { useMutation, useQueryClient, type UseMutationOptions } from '@tanstack/react-query';

import { getCatalogCourseQueryOptions } from '@/features/catalog-courses/api/get-catalog-course';
import { getCatalogCoursesQueryOptions } from '@/features/catalog-courses/api/get-catalog-courses';
import type { CatalogOffer } from '@/features/catalog-courses/types/catalog-offer';
import { apiClient } from '@/lib/api-client';

type UnpublishOfferVariables = { courseId: string; offerId: string; idempotencyKey: string };
export const unpublishOffer = ({ offerId, idempotencyKey }: UnpublishOfferVariables): Promise<CatalogOffer> =>
  apiClient.post(`/api/v1/catalog/offers/${offerId}/unpublish`, undefined, { headers: { 'Idempotency-Key': idempotencyKey } });
type UnpublishOfferConfig = Omit<UseMutationOptions<Awaited<ReturnType<typeof unpublishOffer>>, Error, UnpublishOfferVariables>, 'mutationFn'>;
export const useUnpublishOffer = (mutationConfig: UnpublishOfferConfig = {}) => {
  const client = useQueryClient();
  return useMutation({ ...mutationConfig, mutationFn: unpublishOffer, onSuccess: async (data, input, onMutateResult, context) => {
    await client.invalidateQueries({ queryKey: getCatalogCourseQueryOptions({ courseId: input.courseId }).queryKey });
    await client.invalidateQueries({ queryKey: getCatalogCoursesQueryOptions({ page: 1, size: 10 }).queryKey.slice(0, 1) });
    await mutationConfig.onSuccess?.(data, input, onMutateResult, context);
  } });
};
