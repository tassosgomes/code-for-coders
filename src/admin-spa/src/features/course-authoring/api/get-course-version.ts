import { queryOptions, useQuery } from '@tanstack/react-query';

import { courseVersionSchema } from '@/features/course-authoring/types/course-version';
import { apiClient } from '@/lib/api-client';

export const getCourseVersion = async (courseId: string, versionNumber: number) => courseVersionSchema.parse(await apiClient.get<unknown>(`/api/v1/courses/${courseId}/versions/${versionNumber}`));
export const getCourseVersionQueryOptions = (courseId: string, versionNumber: number) => queryOptions({ queryKey: ['course-version', courseId, versionNumber], queryFn: () => getCourseVersion(courseId, versionNumber) });
export const useCourseVersion = (courseId: string, versionNumber: number) => useQuery(getCourseVersionQueryOptions(courseId, versionNumber));
