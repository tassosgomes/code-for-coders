import { useCallback, useEffect, useState } from 'react';

import { useCourseProgress } from '@/features/student-lessons/api/get-course-progress';

const resumeWaitMilliseconds = 2000;

export const useLessonResumeProgress = (lessonId: string, courseId: string | undefined, versionNumber?: number) => {
  const query = useCourseProgress(courseId);
  const [initialPosition, setInitialPosition] = useState<number>();
  useEffect(() => {
    if (!courseId) return;
    const timeout = setTimeout(() => setInitialPosition((position) => position ?? 0), resumeWaitMilliseconds);
    return () => clearTimeout(timeout);
  }, [courseId]);
  if (initialPosition === undefined && versionNumber && query.isFetchedAfterMount && !query.isFetching) {
    const position = !query.isError && query.data?.versionNumber === versionNumber
      ? query.data.lessons.find((lesson) => lesson.lessonId === lessonId)?.resumeAtSeconds ?? 0 : 0;
    setInitialPosition(position);
  }
  const { refetch } = query;
  const refresh = useCallback(() => { void refetch(); }, [refetch]);
  const progress = !query.isError && query.data?.versionNumber === versionNumber ? query.data : undefined;
  return { initialPosition, progress, refresh };
};
