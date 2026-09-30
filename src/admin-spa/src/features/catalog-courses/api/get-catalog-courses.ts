import { queryOptions, useQuery } from '@tanstack/react-query';
import { z } from 'zod';

import { apiClient } from '@/lib/api-client';

export const catalogCoursesInputSchema = z.object({ page: z.number().int().min(1).default(1), size: z.number().int().min(1).max(50).default(10) });
export type CatalogCoursesInput = z.infer<typeof catalogCoursesInputSchema>;
export type CatalogCourse = {
  courseId: string;
  title: string;
  level: 'beginner' | 'intermediate' | 'advanced' | null;
  inShowcase: boolean;
  offerCounts: { draft: number; published: number; unpublished: number };
};
export type CatalogCoursePage = { data: CatalogCourse[]; pagination: { page: number; size: number; total: number; totalPages: number } };

export const getCatalogCourses = ({ page, size }: CatalogCoursesInput): Promise<CatalogCoursePage> =>
  apiClient.get('/api/v1/catalog/courses', { params: { _page: page, _size: size } });

export const getCatalogCoursesQueryOptions = (input: CatalogCoursesInput) => queryOptions({
  queryKey: ['catalog-courses', input], queryFn: () => getCatalogCourses(input),
});

export const useCatalogCourses = (input: CatalogCoursesInput) => useQuery(getCatalogCoursesQueryOptions(input));
