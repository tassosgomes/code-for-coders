import { useMutation, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';

import { getCourseQueryOptions } from '@/features/course-authoring/api/get-course';
import { getCoursesQueryOptions } from '@/features/course-authoring/api/get-courses';
import { apiClient } from '@/lib/api-client';

export const publishCourseInputSchema = z.object({ draftRevision: z.number().int().positive(), versionNote: z.string().min(1).max(1000).optional() }).strict();
export type PublishCourseInput = z.infer<typeof publishCourseInputSchema>;
const versionSchema = z.object({
  courseId: z.string().uuid(), versionNumber: z.number().int().positive(), title: z.string(), description: z.string().nullable().optional(),
  publishedAt: z.string(), publishedBy: z.object({ name: z.string() }), versionNote: z.string().nullable().optional(), current: z.boolean(), modules: z.array(z.unknown()),
}).strict();
export type PublishedCourse = z.infer<typeof versionSchema>;
export const publishCourse = async ({ courseId, input, idempotencyKey }: { courseId: string; input: PublishCourseInput; idempotencyKey: string }): Promise<PublishedCourse> =>
  versionSchema.parse(await apiClient.post<unknown>(`/api/v1/courses/${courseId}/versions`, publishCourseInputSchema.parse(input), { headers: { 'Idempotency-Key': idempotencyKey } }));
export const usePublishCourse = () => {
  const client = useQueryClient();
  return useMutation({ mutationFn: publishCourse, onSuccess: async (_, variables) => {
    await Promise.all([
      client.invalidateQueries({ queryKey: getCourseQueryOptions(variables.courseId).queryKey }),
      client.invalidateQueries({ queryKey: getCoursesQueryOptions().queryKey.slice(0, 1) }),
    ]);
  } });
};
