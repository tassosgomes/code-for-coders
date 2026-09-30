import { queryOptions, useQuery } from '@tanstack/react-query';

import { courseVersionPageSchema } from '@/features/course-authoring/types/course-version';
import { apiClient } from '@/lib/api-client';

export const getCourseVersions = async (courseId: string, page = 1) => courseVersionPageSchema.parse(await apiClient.get<unknown>(`/api/v1/courses/${courseId}/versions`, { params: { _page: page, _size: 20 } }));
export const getCourseVersionsQueryOptions = (courseId: string, page = 1) => queryOptions({ queryKey: ['course-versions', courseId, page], queryFn: () => getCourseVersions(courseId, page) });
export const useCourseVersions = (courseId: string, page = 1) => useQuery(getCourseVersionsQueryOptions(courseId, page));
