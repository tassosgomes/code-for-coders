import { queryOptions, useQuery } from '@tanstack/react-query';
import { z } from 'zod';

import { apiClient } from '@/lib/api-client';

export const courtesyCourseSearchSchema = z.object({ title: z.string().max(100, 'Use até 100 caracteres.') });
export type CourtesyCourseSearchInput = z.infer<typeof courtesyCourseSearchSchema>;
export type CourtesyCourse = { courseId: string; title: string };
export type CourtesyCoursePage = { data: CourtesyCourse[]; pagination: { page: number; size: number; total: number; totalPages: number } };
export type CourtesyCoursesInput = { page: number; title: string };

export const listCourtesyCourses = ({ page, title }: CourtesyCoursesInput): Promise<CourtesyCoursePage> =>
  apiClient.get('/api/v1/courtesy-courses', { params: { _page: page, _size: 10, ...(title ? { title } : {}) } });
export const courtesyCoursesQueryOptions = (input: CourtesyCoursesInput) => queryOptions({
  queryKey: ['courtesy-courses', input], queryFn: () => listCourtesyCourses(input),
});
export const useCourtesyCourses = (input: CourtesyCoursesInput) => useQuery(courtesyCoursesQueryOptions(input));
