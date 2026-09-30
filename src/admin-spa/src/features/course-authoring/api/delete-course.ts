import { useMutation, useQueryClient } from '@tanstack/react-query';

import { getCourseQueryOptions } from '@/features/course-authoring/api/get-course';
import { getCoursesQueryOptions } from '@/features/course-authoring/api/get-courses';
import { apiClient } from '@/lib/api-client';

type DeleteCourseInput = { courseId: string; idempotencyKey: string };
export const deleteCourse = async ({ courseId, idempotencyKey }: DeleteCourseInput): Promise<void> => {
  await apiClient.delete(`/api/v1/courses/${courseId}`, { headers: { 'Idempotency-Key': idempotencyKey } });
};
export const useDeleteCourse = () => {
  const client = useQueryClient();
  return useMutation({ mutationFn: deleteCourse, onSuccess: async (_, { courseId }) => {
    await client.cancelQueries(getCourseQueryOptions(courseId));
    client.removeQueries({ queryKey: getCourseQueryOptions(courseId).queryKey });
    await client.invalidateQueries({ queryKey: getCoursesQueryOptions().queryKey.slice(0, 1) });
  } });
};
