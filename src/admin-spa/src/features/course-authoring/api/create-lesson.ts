import { z } from 'zod';

import { useCourseWrite } from '@/features/course-authoring/hooks/use-course-write';
import { courseSchema, type Course } from '@/features/course-authoring/types/course';
import { apiClient } from '@/lib/api-client';

export const createLessonInputSchema = z.object({ title: z.string().trim().min(1, 'Informe o título.').max(200), description: z.string().max(5000).optional(), position: z.number().int().positive().optional() }).strict();
export type CreateLessonInput = z.infer<typeof createLessonInputSchema>;
export const createLesson = async ({ courseId, moduleId, input, idempotencyKey }: { courseId: string; moduleId: string; input: CreateLessonInput; idempotencyKey: string }): Promise<Course> =>
  courseSchema.parse(await apiClient.post<unknown>(`/api/v1/courses/${courseId}/modules/${moduleId}/lessons`, createLessonInputSchema.parse(input), { headers: { 'Idempotency-Key': idempotencyKey } }));
export const useCreateLesson = () => useCourseWrite(createLesson);
