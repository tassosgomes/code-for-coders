import { z } from 'zod';

import { useCourseWrite } from '@/features/course-authoring/hooks/use-course-write';
import { courseSchema, type Course } from '@/features/course-authoring/types/course';
import { apiClient } from '@/lib/api-client';

export const updateLessonInputSchema = z.object({ title: z.string().trim().min(1, 'Informe o título.').max(200).optional(), description: z.string().max(5000).nullable().optional(), position: z.number().int().positive().optional(), moduleId: z.string().uuid().optional() }).strict();
export type UpdateLessonInput = z.infer<typeof updateLessonInputSchema>;
export const updateLesson = async ({ courseId, lessonId, input, idempotencyKey }: { courseId: string; lessonId: string; input: UpdateLessonInput; idempotencyKey: string }): Promise<Course> =>
  courseSchema.parse(await apiClient.patch<unknown>(`/api/v1/courses/${courseId}/lessons/${lessonId}`, updateLessonInputSchema.parse(input), { headers: { 'Idempotency-Key': idempotencyKey } }));
export const useUpdateLesson = () => useCourseWrite(updateLesson);
