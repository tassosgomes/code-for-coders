import { queryOptions, useQuery } from '@tanstack/react-query';
import * as z from 'zod';

import { apiClient } from '@/lib/api-client';

export const courseProgressInputSchema = z.object({ courseId: z.uuid() });
export type CourseProgressInput = z.infer<typeof courseProgressInputSchema>;
export const courseProgressSchema = z.object({
  courseId: z.uuid(), versionNumber: z.number().int().positive(),
  completedLessons: z.number().int().nonnegative(), totalLessons: z.number().int().nonnegative(),
  percent: z.number().int().min(0).max(100),
  lessons: z.array(z.object({
    lessonId: z.uuid(), completed: z.boolean(), lastPositionSeconds: z.number().int().nonnegative().nullable(),
    resumeAtSeconds: z.number().int().nonnegative(),
  })),
});
export type CourseProgress = z.infer<typeof courseProgressSchema>;
export const getCourseProgress = async (input: CourseProgressInput): Promise<CourseProgress> => {
  const { courseId } = courseProgressInputSchema.parse(input);
  return courseProgressSchema.parse(await apiClient.get(`/api/v1/courses/${encodeURIComponent(courseId)}/progress`));
};
export const getCourseProgressQueryOptions = (courseId: string) => queryOptions({
  queryKey: ['student-course-progress', courseId], queryFn: () => getCourseProgress({ courseId }),
  retry: false, staleTime: 0, gcTime: 0, refetchOnMount: 'always', refetchOnWindowFocus: false,
});
export const useCourseProgress = (courseId: string | undefined) => useQuery({
  ...getCourseProgressQueryOptions(courseId ?? ''), enabled: Boolean(courseId),
});
