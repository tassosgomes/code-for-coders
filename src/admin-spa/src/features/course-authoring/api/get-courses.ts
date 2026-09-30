import { queryOptions, useQuery } from '@tanstack/react-query';

import { coursePageSchema, type CoursePage } from '@/features/course-authoring/types/course';
import { apiClient } from '@/lib/api-client';

type CourseFilters = { page?: number; status?: 'draft' | 'published' };
export const getCourses = async ({ page = 1, status }: CourseFilters = {}): Promise<CoursePage> =>
  coursePageSchema.parse(await apiClient.get<unknown>('/api/v1/courses', { params: { _page: page, _size: 20, status } }));
export const getCoursesQueryOptions = (filters: CourseFilters = {}) => queryOptions({
  queryKey: ['courses', filters], queryFn: () => getCourses(filters),
});
export const useCourses = (filters: CourseFilters) => useQuery(getCoursesQueryOptions(filters));
