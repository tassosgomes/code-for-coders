import { queryOptions, useQuery } from '@tanstack/react-query';

import { courseSchema, type Course } from '@/features/course-authoring/types/course';
import { apiClient } from '@/lib/api-client';

export const getCourse = async (courseId: string): Promise<Course> =>
  courseSchema.parse(await apiClient.get<unknown>(`/api/v1/courses/${courseId}`));
export const getCourseQueryOptions = (courseId: string) => queryOptions({
  queryKey: ['course', courseId], queryFn: () => getCourse(courseId),
});
export const useCourse = (courseId: string) => useQuery(getCourseQueryOptions(courseId));
