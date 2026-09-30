import { http, HttpResponse } from 'msw';

import { env } from '@/config/env';
import { updateCourseInputSchema } from '@/features/course-authoring/api/update-course';
import type { Course } from '@/features/course-authoring/types/course';

export const createCourseLevelHandlers = (initial: Course) => {
  let course = structuredClone(initial);
  let failure: { code: string; status: number } | undefined;
  const requests: { body: unknown; key: string | null; csrf: string | null }[] = [];
  return {
    requests,
    failWith: (value?: { code: string; status: number }) => { failure = value; },
    handlers: [
      http.get(`${env.API_URL}/api/v1/courses/:courseId`, () => HttpResponse.json(course)),
      http.get(`${env.API_URL}/api/v1/courses`, () => HttpResponse.json({
        data: [{ courseId: course.courseId, title: course.title, status: course.status, currentVersion: course.currentVersion,
          currentLevel: course.currentLevel, hasUnpublishedChanges: course.hasUnpublishedChanges, lastEditedAt: course.lastEditedAt, lastEditedBy: course.lastEditedBy }],
        pagination: { page: 1, size: 20, total: 1, totalPages: 1 },
      })),
      http.patch(`${env.API_URL}/api/v1/courses/:courseId`, async ({ request }) => {
        const body: unknown = await request.json();
        requests.push({ body, key: request.headers.get('Idempotency-Key'), csrf: request.headers.get('X-CSRF-Token') });
        if (failure) return HttpResponse.json({ code: failure.code, errors: { level: ['Invalid field: level.'] } }, { status: failure.status });
        const input = updateCourseInputSchema.parse(body);
        course = { ...course, ...input, draftRevision: course.draftRevision + 1, lastEditedBy: { name: 'Professor confirmado' } };
        course.hasUnpublishedChanges = course.currentVersion !== null && course.level !== course.currentLevel;
        return HttpResponse.json(course);
      }),
    ],
  };
};
