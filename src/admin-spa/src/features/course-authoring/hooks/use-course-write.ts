import { useMutation, useQueryClient } from '@tanstack/react-query';

import { getCourseQueryOptions } from '@/features/course-authoring/api/get-course';
import { getCoursesQueryOptions } from '@/features/course-authoring/api/get-courses';
import type { Course } from '@/features/course-authoring/types/course';

type MutationConfig = { onSuccess?: (course: Course) => void };
export const useCourseWrite = <T,>(mutationFn: (input: T) => Promise<Course>, mutationConfig?: MutationConfig) => {
  const client = useQueryClient();
  return useMutation({ mutationFn, onSuccess: async (course) => {
    client.setQueryData(getCourseQueryOptions(course.courseId).queryKey, course);
    await client.invalidateQueries(getCourseQueryOptions(course.courseId));
    await client.invalidateQueries({ queryKey: getCoursesQueryOptions().queryKey.slice(0, 1) });
    mutationConfig?.onSuccess?.(course);
  } });
};
