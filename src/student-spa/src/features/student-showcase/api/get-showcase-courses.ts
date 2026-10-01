import { queryOptions, useQuery } from '@tanstack/react-query';
import * as z from 'zod';

import { apiClient } from '@/lib/api-client';
import { toContractLevel, type ShowcaseLevelParam } from '@/features/student-showcase/utils/showcase-level';

export const SHOWCASE_PAGE_SIZE = 12;

export const showcaseCourseCardSchema = z.object({
  courseId: z.uuid(),
  title: z.string(),
  level: z.string(),
  summary: z.string(),
  lowestPriceCents: z.number().int().min(1),
  offerCount: z.number().int().min(1),
});

export const showcaseCoursePageSchema = z.object({
  data: z.array(showcaseCourseCardSchema),
  pagination: z.object({
    page: z.number().int().min(1),
    size: z.number().int().min(1),
    total: z.number().int().min(0),
    totalPages: z.number().int().min(0),
  }),
});

export type ShowcaseCourseCard = z.infer<typeof showcaseCourseCardSchema>;

export type ShowcaseCoursePage = z.infer<typeof showcaseCoursePageSchema>;

export type ShowcaseCoursesQuery = {
  level?: ShowcaseLevelParam;
  page: number;
};

export const getShowcaseCourses = async ({ level, page }: ShowcaseCoursesQuery): Promise<ShowcaseCoursePage> => {
  const contractLevel = toContractLevel(level);
  const response = await apiClient.get<ShowcaseCoursePage>('/api/v1/showcase/courses', {
    params: { ...(contractLevel ? { level: contractLevel } : {}), _page: page, _size: SHOWCASE_PAGE_SIZE },
  });

  return showcaseCoursePageSchema.parse(response);
};

export const showcaseCoursesQueryKey = ['student-showcase', 'courses'] as const;

export const getShowcaseCoursesQueryOptions = (query: ShowcaseCoursesQuery) =>
  queryOptions({
    queryKey: [...showcaseCoursesQueryKey, { level: query.level ?? null, page: query.page }],
    queryFn: () => getShowcaseCourses(query),
  });

export const useShowcaseCourses = (query: ShowcaseCoursesQuery) => useQuery(getShowcaseCoursesQueryOptions(query));
