import type { StudentLessonScreenData } from '../api/get-student-lesson';

const STORAGE_KEY = 'cfc-student-known-lessons';
const LAST_LESSON_KEY = 'cfc-student-last-lesson';

type StoredMapping = Record<string, { courseId: string; siblingLessonIds: string[] }>;

export const recordKnownCourse = (
  course: StudentLessonScreenData['course'],
  currentLessonId?: string,
): void => {
  try {
    const allLessonIds = course.modules.flatMap((m) => m.lessons.map((l) => l.lessonId));
    const raw =
      window.sessionStorage.getItem(STORAGE_KEY) ??
      window.localStorage.getItem(STORAGE_KEY);
    const existing: StoredMapping = raw ? (JSON.parse(raw) as StoredMapping) : {};

    for (const id of allLessonIds) {
      existing[id] = {
        courseId: course.courseId,
        siblingLessonIds: allLessonIds.filter((siblingId) => siblingId !== id),
      };
    }

    const serialized = JSON.stringify(existing);
    window.sessionStorage.setItem(STORAGE_KEY, serialized);
    window.localStorage.setItem(STORAGE_KEY, serialized);

    if (currentLessonId) {
      window.sessionStorage.setItem(LAST_LESSON_KEY, currentLessonId);
      window.localStorage.setItem(LAST_LESSON_KEY, currentLessonId);
    }
  } catch {
    // Storage access may throw in sandboxed environments or when quota exceeded
  }
};

export const findKnownSiblingLessonId = (lessonId: string): string | undefined => {
  try {
    const raw =
      window.sessionStorage.getItem(STORAGE_KEY) ??
      window.localStorage.getItem(STORAGE_KEY);
    if (raw) {
      const mapping = JSON.parse(raw) as StoredMapping;
      const entry = mapping[lessonId];
      if (entry && entry.siblingLessonIds.length > 0) {
        return entry.siblingLessonIds[0];
      }
    }

    const lastLesson =
      window.sessionStorage.getItem(LAST_LESSON_KEY) ??
      window.localStorage.getItem(LAST_LESSON_KEY);
    if (lastLesson && lastLesson !== lessonId) {
      return lastLesson;
    }
  } catch {
    // Storage access may throw
  }
  return undefined;
};
