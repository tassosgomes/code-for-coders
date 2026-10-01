import { useMutation, useQueryClient, type UseMutationOptions } from '@tanstack/react-query';

import { getCatalogCourseQueryOptions } from '@/features/catalog-courses/api/get-catalog-course';
import { getCatalogCoursesQueryOptions } from '@/features/catalog-courses/api/get-catalog-courses';
import { apiClient } from '@/lib/api-client';

type DeleteOfferVariables = { courseId: string; offerId: string; idempotencyKey: string };
export const deleteOffer = ({ offerId, idempotencyKey }: DeleteOfferVariables): Promise<void> =>
  apiClient.delete(`/api/v1/catalog/offers/${offerId}`, { headers: { 'Idempotency-Key': idempotencyKey } });
type DeleteOfferConfig = Omit<UseMutationOptions<Awaited<ReturnType<typeof deleteOffer>>, Error, DeleteOfferVariables>, 'mutationFn'>;
export const useDeleteOffer = (mutationConfig: DeleteOfferConfig = {}) => {
  const client = useQueryClient();
  return useMutation({ ...mutationConfig, mutationFn: deleteOffer, onSuccess: async (data, input, onMutateResult, context) => {
    await client.invalidateQueries({ queryKey: getCatalogCourseQueryOptions({ courseId: input.courseId }).queryKey });
    await client.invalidateQueries({ queryKey: getCatalogCoursesQueryOptions({ page: 1, size: 10 }).queryKey.slice(0, 1) });
    await mutationConfig.onSuccess?.(data, input, onMutateResult, context);
  } });
};
