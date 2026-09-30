import { useCourseWrite } from '@/features/course-authoring/hooks/use-course-write';
import { courseSchema, type Course } from '@/features/course-authoring/types/course';
import { apiClient } from '@/lib/api-client';

export const deleteModule = async ({ courseId, moduleId, idempotencyKey }: { courseId: string; moduleId: string; idempotencyKey: string }): Promise<Course> =>
  courseSchema.parse(await apiClient.delete<unknown>(`/api/v1/courses/${courseId}/modules/${moduleId}`, { headers: { 'Idempotency-Key': idempotencyKey } }));
export const useDeleteModule = () => useCourseWrite(deleteModule);
