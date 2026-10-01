import { queryOptions, useQuery } from '@tanstack/react-query';
import { z } from 'zod';

import { apiClient } from '@/lib/api-client';

export const catalogCourseInputSchema = z.object({ courseId: z.uuid() });
export type CatalogCourseInput = z.infer<typeof catalogCourseInputSchema>;
export const catalogCourseRecordSchema = z.object({
  courseId: z.uuid(), title: z.string(), level: z.enum(['beginner', 'intermediate', 'advanced']).nullable(),
  prerequisite: z.object({ text: z.string().nullable(), recommendedCourses: z.array(z.object({ courseId: z.uuid(), title: z.string() })) }),
  tagline: z.string().nullable(), inShowcase: z.boolean(), offers: z.array(z.object({ purchaseIntentCount: z.number().int().min(0) })),
});
export type CatalogCourseRecord = z.infer<typeof catalogCourseRecordSchema>;

export const getCatalogCourse = async (input: CatalogCourseInput): Promise<CatalogCourseRecord> =>
  catalogCourseRecordSchema.parse(await apiClient.get<unknown>(`/api/v1/catalog/courses/${catalogCourseInputSchema.parse(input).courseId}`));

export const getCatalogCourseQueryOptions = (input: CatalogCourseInput) => queryOptions({
  queryKey: ['catalog-course', input], queryFn: () => getCatalogCourse(input),
});

export const useCatalogCourse = (input: CatalogCourseInput) => useQuery(getCatalogCourseQueryOptions(input));
