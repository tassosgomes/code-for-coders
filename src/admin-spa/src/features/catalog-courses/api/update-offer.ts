import { useMutation, useQueryClient, type UseMutationOptions } from '@tanstack/react-query';

import { getCatalogCourseQueryOptions } from '@/features/catalog-courses/api/get-catalog-course';
import { getCatalogCoursesQueryOptions } from '@/features/catalog-courses/api/get-catalog-courses';
import { offerBody } from '@/features/catalog-courses/api/offer-input';
import type { OfferVariables } from '@/features/catalog-courses/api/offer-input';
import { catalogOfferSchema } from '@/features/catalog-courses/types/catalog-offer';
import { apiClient } from '@/lib/api-client';

export const updateOffer = async ({ offerId, input, idempotencyKey }: OfferVariables) =>
  catalogOfferSchema.parse(await apiClient.patch<unknown>(`/api/v1/catalog/offers/${offerId}`, offerBody(input), { headers: { 'Idempotency-Key': idempotencyKey } }));

type UpdateOfferConfig = Omit<UseMutationOptions<Awaited<ReturnType<typeof updateOffer>>, Error, OfferVariables>, 'mutationFn'>;
export const useUpdateOffer = (mutationConfig: UpdateOfferConfig = {}) => {
  const client = useQueryClient();
  return useMutation({ ...mutationConfig, mutationFn: updateOffer, onSuccess: async (data, input, onMutateResult, context) => {
    await client.invalidateQueries({ queryKey: getCatalogCourseQueryOptions({ courseId: input.courseId }).queryKey });
    await client.invalidateQueries({ queryKey: getCatalogCoursesQueryOptions({ page: 1, size: 10 }).queryKey.slice(0, 1) });
    await mutationConfig.onSuccess?.(data, input, onMutateResult, context);
  } });
};
