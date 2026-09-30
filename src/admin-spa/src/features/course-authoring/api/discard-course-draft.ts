import { z } from 'zod';

import { useCourseWrite } from '@/features/course-authoring/hooks/use-course-write';
import { courseSchema } from '@/features/course-authoring/types/course';
import { apiClient } from '@/lib/api-client';

export const discardCourseDraftInputSchema = z.object({ draftRevision: z.number().int().positive() }).strict();
export type DiscardCourseDraftInput = z.infer<typeof discardCourseDraftInputSchema>;
export const discardCourseDraft = async ({ courseId, input, idempotencyKey }: { courseId: string; input: DiscardCourseDraftInput; idempotencyKey: string }) => courseSchema.parse(await apiClient.post<unknown>(`/api/v1/courses/${courseId}/discard-draft`, discardCourseDraftInputSchema.parse(input), { headers: { 'Idempotency-Key': idempotencyKey } }));
export const useDiscardCourseDraft = () => useCourseWrite(discardCourseDraft);
