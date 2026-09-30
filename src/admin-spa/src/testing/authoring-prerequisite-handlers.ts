import { http, HttpResponse } from 'msw';

import { env } from '@/config/env';
import { updateCourseInputSchema } from '@/features/course-authoring/api/update-course';
import type { Course, CourseSummary } from '@/features/course-authoring/types/course';

export const createCoursePrerequisiteHandlers = (initial: Course, candidates: CourseSummary[]) => {
  let course = structuredClone(initial);
  let failure: { code: string; status: number; errors?: Record<string, string[]> } | undefined;
  const writes: { body: unknown; key: string | null; csrf: string | null }[] = [];
  const searches: URLSearchParams[] = [];
  return {
    writes, searches,
    failWith: (value?: typeof failure) => { failure = value; },
    handlers: [
      http.get(`${env.API_URL}/api/v1/courses/:courseId`, () => HttpResponse.json(course)),
      http.get(`${env.API_URL}/api/v1/courses`, ({ request }) => {
        const params = new URL(request.url).searchParams; searches.push(params);
        const normalize = (title: string) => title.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase();
        const filtered = candidates.filter((candidate) => candidate.status === params.get('status') && normalize(candidate.title).includes(normalize(params.get('title') ?? '')));
        return HttpResponse.json({ data: filtered, pagination: { page: 1, size: 20, total: filtered.length, totalPages: filtered.length ? 1 : 0 } });
      }),
      http.patch(`${env.API_URL}/api/v1/courses/:courseId`, async ({ request }) => {
        const body: unknown = await request.json();
        writes.push({ body, key: request.headers.get('Idempotency-Key'), csrf: request.headers.get('X-CSRF-Token') });
        if (failure) return HttpResponse.json({ code: failure.code, errors: failure.errors }, { status: failure.status });
        const input = updateCourseInputSchema.parse(body);
        course = { ...course, level: input.level === undefined ? course.level : input.level,
          prerequisite: { text: input.prerequisiteText === undefined ? course.prerequisite.text : input.prerequisiteText,
            recommendedCourses: input.recommendedCourseIds?.map((id) => ({ courseId: id, title: candidates.find((item) => item.courseId === id)?.title ?? '' })) ?? course.prerequisite.recommendedCourses },
          draftRevision: course.draftRevision + 1, hasUnpublishedChanges: true, lastEditedBy: { name: 'Professor confirmado' },
        };
        return HttpResponse.json(course);
      }),
    ],
  };
};
