import { http, HttpResponse } from 'msw';

import { env } from '@/config/env';
import { authoringCourseFixture } from '@/testing/authoring-course-handlers';

export const createDeletionBoundary = (published = false, canEdit = true) => {
  let deleted = false;
  let currentVersion = published ? 1 : null;
  let mode: 'success' | 'unavailable' | 'published' = 'success';
  const writes: { key: string | null; csrf: string | null }[] = [];
  const course = () => ({ ...authoringCourseFixture, status: currentVersion ? 'published' : 'draft', currentVersion });
  return {
    writes, setMode: (value: typeof mode) => { mode = value; },
    handlers: [
      http.get(`${env.API_URL}/api/v1/staff-sessions/current`, () => HttpResponse.json({
        accountId: '0198dfac-674a-7000-8000-000000000002', name: 'Professor B', roles: ['professor'],
        permissions: canEdit ? ['autoria.ler', 'autoria.editar'] : ['autoria.ler'], csrfToken: 'delete-csrf',
      })),
      http.get(`${env.API_URL}/api/v1/courses`, () => {
        const { courseId, title, status, currentVersion, currentLevel, hasUnpublishedChanges, lastEditedAt, lastEditedBy } = course();
        return HttpResponse.json({ data: deleted ? [] : [{ courseId, title, status, currentVersion, currentLevel, hasUnpublishedChanges, lastEditedAt, lastEditedBy }], pagination: { page: 1, size: 20, total: deleted ? 0 : 1, totalPages: deleted ? 0 : 1 } });
      }),
      http.get(`${env.API_URL}/api/v1/courses/:courseId`, () => deleted ? HttpResponse.json({ code: 'COURSE_NOT_FOUND' }, { status: 404 }) : HttpResponse.json(course())),
      http.delete(`${env.API_URL}/api/v1/courses/:courseId`, ({ request }) => {
        writes.push({ key: request.headers.get('Idempotency-Key'), csrf: request.headers.get('X-CSRF-Token') });
        if (mode === 'unavailable') return HttpResponse.json({ code: 'LEARNING_UNAVAILABLE' }, { status: 502 });
        if (mode === 'published') { currentVersion = 1; return HttpResponse.json({ code: 'COURSE_ALREADY_PUBLISHED' }, { status: 409 }); }
        deleted = true; return new HttpResponse(null, { status: 204 });
      }),
    ],
  };
};
