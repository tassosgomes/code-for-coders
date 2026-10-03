import { queryOptions, useQuery } from '@tanstack/react-query';
import * as z from 'zod';

import { apiClient } from '@/lib/api-client';

const outline = z.object({ lessonId: z.uuid(), title: z.string(), position: z.number().int().positive() });
export const studentLessonSchema = z.object({
  lesson: outline.extend({ moduleId: z.uuid() }),
  course: z.object({
    courseId: z.uuid(), title: z.string(), versionNumber: z.number().int().positive(),
    modules: z.array(z.object({ moduleId: z.uuid(), title: z.string(), position: z.number().int().positive(), lessons: z.array(outline) })),
  }),
});
export type StudentLessonScreenData = z.infer<typeof studentLessonSchema>;
export const getStudentLesson = async (lessonId: string): Promise<StudentLessonScreenData> =>
  studentLessonSchema.parse(await apiClient.get(`/api/v1/lessons/${encodeURIComponent(lessonId)}`));
export const getStudentLessonQueryOptions = (lessonId: string) => queryOptions({
  queryKey: ['student-lessons', lessonId], queryFn: () => getStudentLesson(lessonId), retry: false,
  // Access is checked on every navigation; a previous successful response must never mask a new denial.
  staleTime: 0, gcTime: 0,
});
export const useStudentLesson = (lessonId: string) => useQuery(getStudentLessonQueryOptions(lessonId));
