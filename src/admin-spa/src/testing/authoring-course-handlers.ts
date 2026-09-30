import { http, HttpResponse } from 'msw';

import { env } from '@/config/env';

export const authoringCourseFixture = {
  courseId: '0198dfac-674a-7000-8000-000000000001', title: '.NET do zero à API', description: 'Do primeiro projeto à API e aos testes.',
  status: 'draft', currentVersion: null, hasUnpublishedChanges: false, draftRevision: 1,
  level: null, currentLevel: null,
  prerequisite: { text: null, recommendedCourses: [] },
  createdAt: '2026-09-29T12:00:00Z', createdBy: { name: 'Professor A' },
  lastEditedAt: '2026-09-29T12:00:00Z', lastEditedBy: { name: 'Professor B' }, modules: [],
};
export const authoringCourseSummaryFixture = {
  courseId: authoringCourseFixture.courseId, title: authoringCourseFixture.title,
  status: 'draft', currentVersion: null, hasUnpublishedChanges: false,
  currentLevel: null,
  lastEditedAt: authoringCourseFixture.lastEditedAt, lastEditedBy: authoringCourseFixture.lastEditedBy,
};
export const authoringCourseHandlers = [
  http.get(`${env.API_URL}/api/v1/courses`, () => HttpResponse.json({
    data: [authoringCourseSummaryFixture], pagination: { page: 1, size: 20, total: 1, totalPages: 1 },
  })),
  http.get(`${env.API_URL}/api/v1/courses/:courseId`, () => HttpResponse.json(authoringCourseFixture)),
  http.post(`${env.API_URL}/api/v1/courses`, async ({ request }) => {
    const input: unknown = await request.json();
    if (!input || typeof input !== 'object' || !('title' in input)) return HttpResponse.json({ code: 'INVALID_REQUEST' }, { status: 400 });
    return HttpResponse.json({ ...authoringCourseFixture, ...input }, { status: 201 });
  }),
];
