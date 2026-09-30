import { http, HttpResponse } from 'msw';
import { z } from 'zod';

import { env } from '@/config/env';
import { courseSchema } from '@/features/course-authoring/types/course';
import type { CourseVersion } from '@/features/course-authoring/types/course-version';
import { createPublicationBoundary, publicationLessonId, publicationModuleId, publicationVideoId } from '@/testing/authoring-publication-handlers';

export const createVersionBoundary = (canEdit = true, published = true, audience?: Pick<CourseVersion, 'level' | 'prerequisite'>) => {
  const initial = createPublicationBoundary(true, canEdit);
  let course = courseSchema.parse({ ...initial.snapshot(), status: published ? 'published' : 'draft', currentVersion: published ? 1 : null, hasUnpublishedChanges: published, draftRevision: 4, title: 'Rascunho alterado' });
  const first: CourseVersion = { courseId: course.courseId, versionNumber: 1, title: 'Título publicado', description: 'Descrição publicada', publishedAt: '2026-09-29T12:00:00Z', publishedBy: { name: 'Marina' }, versionNote: 'Primeira versão', current: true, level: audience?.level ?? null, prerequisite: audience?.prerequisite ?? { text: null, recommendedCourses: [] },
    modules: [{ moduleId: publicationModuleId, title: 'Fundamentos', position: 1, lessons: [{ lessonId: publicationLessonId, title: 'Tipos originais', description: 'Descrição da aula', position: 1, videoId: publicationVideoId }] }] };
  let versions = published ? [first] : [];
  let mode: 'success' | 'changed' | 'unavailable' | 'missing' = 'success';
  const writes: { operation: string; key: string | null; revision: number }[] = [];
  const reads: number[] = [];
  return { snapshot: () => course, writes, reads, setMode: (value: typeof mode) => { mode = value; }, handlers: [
    ...initial.handlers.slice(0, 1),
    http.get(`${env.API_URL}/api/v1/courses/:courseId`, () => HttpResponse.json(course)),
    http.get(`${env.API_URL}/api/v1/courses/:courseId/versions`, () => mode === 'unavailable' ? HttpResponse.json({ code: 'LEARNING_UNAVAILABLE' }, { status: 502 }) : HttpResponse.json({ data: versions.map(({ versionNumber, publishedAt, publishedBy, versionNote, current }) => ({ versionNumber, publishedAt, publishedBy, versionNote, current })), pagination: { page: 1, size: 20, total: versions.length, totalPages: versions.length ? 1 : 0 } })),
    http.get(`${env.API_URL}/api/v1/courses/:courseId/versions/:number`, ({ params }) => {
      const number = Number(params.number); reads.push(number); const version = versions.find((item) => item.versionNumber === number);
      return !version || mode === 'missing' ? HttpResponse.json({ code: 'VERSION_NOT_FOUND' }, { status: 404 }) : HttpResponse.json(version);
    }),
    http.post(`${env.API_URL}/api/v1/courses/:courseId/:operation`, async ({ params, request }) => {
      const input = z.object({ draftRevision: z.number(), versionNote: z.string().optional() }).parse(await request.json());
      writes.push({ operation: String(params.operation), key: request.headers.get('Idempotency-Key'), revision: input.draftRevision });
      if (mode === 'changed') { course = { ...course, title: 'Edição do colega', draftRevision: 5 }; return HttpResponse.json({ code: 'DRAFT_CHANGED' }, { status: 409 }); }
      if (mode === 'unavailable') return HttpResponse.json({ code: 'LEARNING_UNAVAILABLE' }, { status: 502 });
      if (params.operation === 'discard-draft') {
        course = { ...course, title: first.title, description: first.description, level: first.level, currentLevel: first.level, prerequisite: first.prerequisite, hasUnpublishedChanges: false, draftRevision: course.draftRevision + 1 };
        return HttpResponse.json(course);
      }
      const version: CourseVersion = { ...first, title: course.title, level: course.level, prerequisite: course.prerequisite, versionNumber: (course.currentVersion ?? 0) + 1, publishedBy: { name: 'Rafael' }, versionNote: input.versionNote, publishedAt: '2026-09-30T12:00:00Z' };
      versions = [version, ...versions.map((item) => ({ ...item, current: false }))];
      course = { ...course, currentVersion: version.versionNumber, currentLevel: version.level, hasUnpublishedChanges: false };
      return HttpResponse.json(version, { status: 201 });
    }),
  ] };
};
