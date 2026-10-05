import type { MyCourses } from '@/features/student-dashboard/api/get-my-courses';

export const continueLessonId = '018f1000-0000-7000-8000-000000000011';
export const startLessonId = '018f1000-0000-7000-8000-000000000012';
export const myCoursesData: MyCourses = {
  progressAvailable: true,
  active: [
    {
      courseId: '018f1000-0000-7000-8000-000000000001', title: 'React do zero', started: true,
      lastActivityAt: '2026-10-04T14:00:00Z', continueLessonId,
      progress: { completedLessons: 3, totalLessons: 8, percent: 37 },
    },
    {
      courseId: '018f1000-0000-7000-8000-000000000002', title: 'TypeScript na prática', started: false,
      lastActivityAt: null, continueLessonId: startLessonId,
      progress: { completedLessons: 0, totalLessons: 10, percent: 0 },
    },
  ],
  ended: [],
};

export const endedCoursesData: MyCourses = {
  progressAvailable: true,
  active: [],
  ended: [{
    courseId: '018f1000-0000-7000-8000-000000000003', title: 'C# e ASP.NET Core',
    endedOn: '2026-10-01', endedReason: 'grant-ended',
    progress: { completedLessons: 5, totalLessons: 10, percent: 50 },
  }],
};

export const unavailableProgressData: MyCourses = {
  progressAvailable: false,
  active: myCoursesData.active.map((course) => ({
    ...course, started: null, lastActivityAt: null, progress: null,
    continueLessonId: startLessonId,
  })),
  ended: endedCoursesData.ended.map((course) => ({ ...course, progress: null })),
};
