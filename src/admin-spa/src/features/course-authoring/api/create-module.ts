import { z } from 'zod';

import { useCourseWrite } from '@/features/course-authoring/hooks/use-course-write';
import { courseSchema, type Course } from '@/features/course-authoring/types/course';
import { apiClient } from '@/lib/api-client';

export const createModuleInputSchema = z.object({ title: z.string().trim().min(1, 'Informe o título.').max(200), position: z.number().int().positive().optional() }).strict();
export type CreateModuleInput = z.infer<typeof createModuleInputSchema>;
export const createModule = async ({ courseId, input, idempotencyKey }: { courseId: string; input: CreateModuleInput; idempotencyKey: string }): Promise<Course> =>
  courseSchema.parse(await apiClient.post<unknown>(`/api/v1/courses/${courseId}/modules`, createModuleInputSchema.parse(input), { headers: { 'Idempotency-Key': idempotencyKey } }));
export const useCreateModule = () => useCourseWrite(createModule);
