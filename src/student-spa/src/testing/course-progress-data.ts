import { studentLessonData } from '@/testing/student-lesson-data';

export const courseProgressData = {
  courseId: studentLessonData.course.courseId, versionNumber: studentLessonData.course.versionNumber,
  completedLessons: 0, totalLessons: 2, percent: 0, lessons: [],
};
