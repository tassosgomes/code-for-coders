import { z } from 'zod';

import { useCourseWrite } from '@/features/course-authoring/hooks/use-course-write';
import { courseSchema, type Course } from '@/features/course-authoring/types/course';
import { apiClient } from '@/lib/api-client';

export const updateCourseInputSchema = z.object({ title: z.string().trim().min(1, 'Informe o título.').max(200), description: z.string().max(5000).nullable().optional() }).strict();
export type UpdateCourseInput = z.infer<typeof updateCourseInputSchema>;
export const updateCourse = async ({ courseId, input, idempotencyKey }: { courseId: string; input: UpdateCourseInput; idempotencyKey: string }): Promise<Course> =>
  courseSchema.parse(await apiClient.patch<unknown>(`/api/v1/courses/${courseId}`, updateCourseInputSchema.parse(input), { headers: { 'Idempotency-Key': idempotencyKey } }));
export const useUpdateCourse = () => useCourseWrite(updateCourse);
