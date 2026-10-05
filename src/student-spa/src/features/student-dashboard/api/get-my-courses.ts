import { queryOptions, useQuery } from '@tanstack/react-query';
import * as z from 'zod';

import { apiClient } from '@/lib/api-client';

const progressSummarySchema = z.object({
  completedLessons: z.number().int().nonnegative(),
  totalLessons: z.number().int().nonnegative(),
  percent: z.number().int().min(0).max(100),
});
export const myCoursesSchema = z.object({
  progressAvailable: z.boolean(),
  active: z.array(z.object({
    courseId: z.uuid(), title: z.string().min(1),
    started: z.boolean().nullable(), lastActivityAt: z.iso.datetime({ offset: true }).nullable(),
    continueLessonId: z.uuid(), progress: progressSummarySchema.nullable(),
  })).max(500),
  ended: z.array(z.object({
    courseId: z.uuid(), title: z.string().min(1), endedOn: z.iso.date(),
    endedReason: z.string(), progress: progressSummarySchema.nullable(),
  })).max(500),
});
export type MyCourses = z.infer<typeof myCoursesSchema>;
export const getMyCourses = async (): Promise<MyCourses> =>
  myCoursesSchema.parse(await apiClient.get('/api/v1/my-courses'));
export const getMyCoursesQueryOptions = () => queryOptions({
  queryKey: ['student-my-courses'], queryFn: getMyCourses,
  retry: false, staleTime: 0, gcTime: 0, refetchOnMount: 'always',
});
export const useMyCourses = (enabled: boolean) => useQuery({
  ...getMyCoursesQueryOptions(), enabled,
});
