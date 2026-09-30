import { useMutation, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';

import { getCoursesQueryOptions } from '@/features/course-authoring/api/get-courses';
import { courseSchema, type Course } from '@/features/course-authoring/types/course';
import { apiClient } from '@/lib/api-client';

export const createCourseInputSchema = z.object({
  title: z.string().trim().min(1, 'Informe o título do curso.').max(200, 'Use até 200 caracteres.'),
  description: z.string().max(5000, 'Use até 5000 caracteres.'),
}).strict();
export type CreateCourseInput = z.infer<typeof createCourseInputSchema>;
export const createCourse = async ({ input, idempotencyKey }: { input: CreateCourseInput; idempotencyKey: string }): Promise<Course> =>
  courseSchema.parse(await apiClient.post<unknown>('/api/v1/courses', input, { headers: { 'Idempotency-Key': idempotencyKey } }));
export const useCreateCourse = (onSuccess: (course: Course) => void) => {
  const client = useQueryClient();
  return useMutation({ mutationFn: createCourse, onSuccess: async (course) => {
    await client.invalidateQueries({ queryKey: getCoursesQueryOptions().queryKey.slice(0, 1) });
    onSuccess(course);
  } });
};
