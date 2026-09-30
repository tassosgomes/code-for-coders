import { useCourseWrite } from '@/features/course-authoring/hooks/use-course-write';
import { courseSchema, type Course } from '@/features/course-authoring/types/course';
import { apiClient } from '@/lib/api-client';

export const deleteLesson = async ({ courseId, lessonId, idempotencyKey }: { courseId: string; lessonId: string; idempotencyKey: string }): Promise<Course> =>
  courseSchema.parse(await apiClient.delete<unknown>(`/api/v1/courses/${courseId}/lessons/${lessonId}`, { headers: { 'Idempotency-Key': idempotencyKey } }));
export const useDeleteLesson = () => useCourseWrite(deleteLesson);
