import { useMutation, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';

import { catalogCourseRecordSchema, getCatalogCourseQueryOptions } from '@/features/catalog-courses/api/get-catalog-course';
import type { CatalogCourseRecord } from '@/features/catalog-courses/api/get-catalog-course';
import { apiClient } from '@/lib/api-client';

export const updateCatalogCourseInputSchema = z.object({ tagline: z.string().min(1).max(160, 'Use até 160 caracteres.').nullable() });
export type UpdateCatalogCourseInput = z.infer<typeof updateCatalogCourseInputSchema>;
type UpdateCatalogCourseVariables = { courseId: string; input: UpdateCatalogCourseInput; idempotencyKey: string };

export const updateCatalogCourse = async ({ courseId, input, idempotencyKey }: UpdateCatalogCourseVariables): Promise<CatalogCourseRecord> =>
  catalogCourseRecordSchema.parse(await apiClient.patch<unknown>(`/api/v1/catalog/courses/${courseId}`, updateCatalogCourseInputSchema.parse(input), { headers: { 'Idempotency-Key': idempotencyKey } }));

export const useUpdateCatalogCourse = () => {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: updateCatalogCourse, onSuccess: async (_, variables) => {
    await queryClient.invalidateQueries({ queryKey: getCatalogCourseQueryOptions({ courseId: variables.courseId }).queryKey });
  } });
};
