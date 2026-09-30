import { http, HttpResponse } from 'msw';

import { env } from '@/config/env';
import { courseSchema } from '@/features/course-authoring/types/course';
import { authoringCourseFixture } from '@/testing/authoring-course-handlers';

export const publicationModuleId = '0198dfac-674a-7000-8000-000000000004';
export const publicationLessonId = '0198dfac-674a-7000-8000-000000000005';
export const publicationVideoId = '0198dfac-674a-7000-8000-000000000006';
export const createPublicationBoundary = (complete = true, canEdit = true) => {
  let mode: 'success' | 'unavailable' | 'changed' | 'incomplete' = 'success';
  let course = courseSchema.parse({ ...authoringCourseFixture, modules: [{ moduleId: publicationModuleId, title: 'Fundamentos', position: 1,
    lessons: [{ lessonId: publicationLessonId, title: 'Tipos', position: 1, video: complete ? { videoId: publicationVideoId, title: 'Vídeo pronto' } : null }] }] });
  const writes: { key: string | null; body: unknown }[] = [];
  return { writes, setMode: (value: typeof mode) => { mode = value; }, snapshot: () => course,
    handlers: [
      http.get(`${env.API_URL}/api/v1/staff-sessions/current`, () => HttpResponse.json({ accountId: publicationVideoId, name: 'Professor', roles: ['professor'], permissions: canEdit ? ['autoria.ler', 'autoria.editar', 'midia.enviar'] : ['autoria.ler'], csrfToken: 'publish-csrf' })),
      http.get(`${env.API_URL}/api/v1/courses/:courseId`, () => HttpResponse.json(course)),
      http.post(`${env.API_URL}/api/v1/courses/:courseId/versions`, async ({ request }) => {
        const body: unknown = await request.json(); writes.push({ key: request.headers.get('Idempotency-Key'), body });
        if (mode === 'unavailable') return HttpResponse.json({ code: 'LEARNING_UNAVAILABLE' }, { status: 502 });
        if (mode === 'changed') { course = { ...course, draftRevision: course.draftRevision + 1 }; return HttpResponse.json({ code: 'DRAFT_CHANGED' }, { status: 409 }); }
        if (mode === 'incomplete') return HttpResponse.json({ code: 'COURSE_INCOMPLETE', pendencies: [{ code: 'lesson-without-video', moduleId: publicationModuleId, lessonId: publicationLessonId }] }, { status: 422 });
        course = { ...course, status: 'published', currentVersion: 1, hasUnpublishedChanges: false };
        return HttpResponse.json({ courseId: course.courseId, versionNumber: 1, title: course.title, publishedAt: '2026-09-29T15:00:00Z', publishedBy: { name: 'Professor' }, current: true, modules: [] }, { status: 201 });
      }),
    ],
  };
};
