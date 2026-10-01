import { queryOptions, useQuery } from '@tanstack/react-query';
import axios from 'axios';
import * as z from 'zod';

import { apiClient } from '@/lib/api-client';

export const showcaseAccessPeriodSchema = z.object({
  type: z.string(),
  months: z.number().int().min(1).optional(),
});

export const showcaseOfferSchema = z.object({
  offerId: z.uuid(),
  name: z.string(),
  priceCents: z.number().int().min(1),
  accessPeriod: showcaseAccessPeriodSchema,
});

export const showcaseCourseDetailSchema = z.object({
  courseId: z.uuid(),
  title: z.string(),
  level: z.string(),
  description: z.string(),
  prerequisite: z.object({
    text: z.string().nullable(),
    recommendedCourses: z.array(
      z.object({
        courseId: z.uuid(),
        title: z.string(),
        inShowcase: z.boolean(),
      }),
    ),
  }),
  modules: z.array(
    z.object({
      title: z.string(),
      lessons: z.array(z.object({ title: z.string() })),
    }),
  ),
  offers: z.array(showcaseOfferSchema),
});

export type ShowcaseCourseDetail = z.infer<typeof showcaseCourseDetailSchema>;

export type ShowcaseOffer = z.infer<typeof showcaseOfferSchema>;

export const getShowcaseCourse = async (courseId: string): Promise<ShowcaseCourseDetail> => {
  const response = await apiClient.get<ShowcaseCourseDetail>(`/api/v1/showcase/courses/${encodeURIComponent(courseId)}`);

  return showcaseCourseDetailSchema.parse(response);
};

// Outside the showcase, unknown and another school's course are the same 404 (RN-O02): one screen for the three.
export const isShowcaseCourseNotFound = (error: unknown) => axios.isAxiosError(error) && error.response?.status === 404;

export const showcaseCourseQueryKey = (courseId: string) => ['student-showcase', 'course', courseId] as const;

export const getShowcaseCourseQueryOptions = (courseId: string) =>
  queryOptions({
    queryKey: showcaseCourseQueryKey(courseId),
    queryFn: () => getShowcaseCourse(courseId),
  });

export const useShowcaseCourse = (courseId: string) => useQuery(getShowcaseCourseQueryOptions(courseId));
