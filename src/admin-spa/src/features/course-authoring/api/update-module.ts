import { z } from 'zod';

import { useCourseWrite } from '@/features/course-authoring/hooks/use-course-write';
import { courseSchema, type Course } from '@/features/course-authoring/types/course';
import { apiClient } from '@/lib/api-client';

export const updateModuleInputSchema = z.object({ title: z.string().trim().min(1, 'Informe o título.').max(200).optional(), position: z.number().int().positive().optional() }).strict();
export type UpdateModuleInput = z.infer<typeof updateModuleInputSchema>;
export const updateModule = async ({ courseId, moduleId, input, idempotencyKey }: { courseId: string; moduleId: string; input: UpdateModuleInput; idempotencyKey: string }): Promise<Course> =>
  courseSchema.parse(await apiClient.patch<unknown>(`/api/v1/courses/${courseId}/modules/${moduleId}`, updateModuleInputSchema.parse(input), { headers: { 'Idempotency-Key': idempotencyKey } }));
export const useUpdateModule = () => useCourseWrite(updateModule);
