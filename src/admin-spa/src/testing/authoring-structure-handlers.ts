import { http, HttpResponse } from 'msw';

import { env } from '@/config/env';
import { authoringCourseFixture } from '@/testing/authoring-course-handlers';

type StructureLesson = { lessonId: string; title: string; description?: string; position: number; video: null };
type StructureModule = { moduleId: string; title: string; position: number; lessons: StructureLesson[] };
type StructureCourse = Omit<typeof authoringCourseFixture, 'modules'> & { modules: StructureModule[] };
export const structureCourseFixture: StructureCourse = { ...authoringCourseFixture, modules: [
  { moduleId: '0198dfac-674a-7000-8000-000000000010', title: 'Fundamentos', position: 1, lessons: [
    { lessonId: '0198dfac-674a-7000-8000-000000000011', title: 'Tipos', position: 1, video: null },
    { lessonId: '0198dfac-674a-7000-8000-000000000012', title: 'Fluxo', position: 2, video: null },
  ] },
  { moduleId: '0198dfac-674a-7000-8000-000000000020', title: 'Coleções', position: 2, lessons: [] },
] };
export const createStructureHandlers = (initial = structureCourseFixture) => {
  let course = structuredClone(initial);
  const requests: { path: string; method: string; body: Record<string, unknown>; key: string | null; csrf: string | null }[] = [];
  let fail = false;
  const renumber = () => {
    course.modules.forEach((module, index) => { module.position = index + 1; module.lessons.forEach((lesson, lessonIndex) => { lesson.position = lessonIndex + 1; }); });
  };
  const write = http.all(`${env.API_URL}/api/v1/courses/*`, async ({ request }) => {
    if (request.method === 'GET') return HttpResponse.json(course);
    const path = new URL(request.url).pathname;
    const body: Record<string, unknown> = request.method === 'DELETE' ? {} : await request.json() as Record<string, unknown>; // MSW receives JSON objects sent by the typed endpoint clients.
    requests.push({ path, method: request.method, body, key: request.headers.get('Idempotency-Key'), csrf: request.headers.get('X-CSRF-Token') });
    if (fail) return HttpResponse.json({ code: 'LEARNING_UNAVAILABLE' }, { status: 502 });
    const parts = path.split('/');
    if (parts.length === 5) {
      if (typeof body.title === 'string') course.title = body.title;
      if (typeof body.description === 'string') course.description = body.description;
    } else if (parts[5] === 'modules') {
      if (parts.length === 6) course.modules.push({ moduleId: crypto.randomUUID(), title: String(body.title), position: course.modules.length + 1, lessons: [] });
      else {
        const module = course.modules.find((item) => item.moduleId === parts[6]);
        if (!module) return HttpResponse.json({ code: 'MODULE_NOT_FOUND' }, { status: 404 });
        if (parts[7] === 'lessons') module.lessons.push({ lessonId: crypto.randomUUID(), title: String(body.title), description: String(body.description ?? ''), position: module.lessons.length + 1, video: null });
        else if (request.method === 'DELETE') course.modules = course.modules.filter((item) => item !== module);
        else {
          if (typeof body.title === 'string') module.title = body.title;
          if (typeof body.position === 'number') { course.modules = course.modules.filter((item) => item !== module); course.modules.splice(body.position - 1, 0, module); }
        }
      }
    } else {
      const source = course.modules.find((module) => module.lessons.some((lesson) => lesson.lessonId === parts[6]));
      const lesson = source?.lessons.find((item) => item.lessonId === parts[6]);
      if (!source || !lesson) return HttpResponse.json({ code: 'LESSON_NOT_FOUND' }, { status: 404 });
      if (request.method === 'DELETE') source.lessons = source.lessons.filter((item) => item !== lesson);
      else {
        if (typeof body.title === 'string') lesson.title = body.title;
        if (typeof body.description === 'string') lesson.description = body.description;
        if (typeof body.position === 'number' || typeof body.moduleId === 'string') {
          const destination = course.modules.find((module) => module.moduleId === body.moduleId) ?? source;
          source.lessons = source.lessons.filter((item) => item !== lesson);
          destination.lessons.splice(typeof body.position === 'number' ? body.position - 1 : destination.lessons.length, 0, lesson);
        }
      }
    }
    renumber(); course = { ...course, draftRevision: course.draftRevision + 1, lastEditedBy: { name: 'Editor confirmado' } };
    return HttpResponse.json(course, { status: request.method === 'POST' ? 201 : 200 });
  });
  return { handlers: [write], requests, setFail: (value: boolean) => { fail = value; }, snapshot: () => structuredClone(course) };
};
