import { z } from 'zod';

import type { Course } from '@/features/course-authoring/types/course';

export const publicationPendencySchema = z.object({ code: z.enum(['course-without-modules', 'module-without-lessons', 'lesson-without-video']), moduleId: z.string().uuid().nullable().optional(), lessonId: z.string().uuid().nullable().optional() });
export type PublicationPendency = z.infer<typeof publicationPendencySchema>;
export const publicationPendencies = (course: Course): PublicationPendency[] => {
  if (!course.modules.length) return [{ code: 'course-without-modules' }];
  return course.modules.flatMap<PublicationPendency>((module) => !module.lessons.length
    ? [{ code: 'module-without-lessons' as const, moduleId: module.moduleId }]
    : module.lessons.filter((lesson) => !lesson.video).map((lesson) => ({ code: 'lesson-without-video' as const, moduleId: module.moduleId, lessonId: lesson.lessonId })));
};
export const pendencyLabel = (pendency: PublicationPendency, course: Course) => {
  const module = course.modules.find((item) => item.moduleId === pendency.moduleId);
  if (pendency.code === 'course-without-modules') return 'Adicione ao menos um módulo ao curso.';
  if (pendency.code === 'module-without-lessons') return `${module?.title ?? 'Módulo'} — adicione ao menos uma aula.`;
  return `${module?.lessons.find((item) => item.lessonId === pendency.lessonId)?.title ?? 'Aula'} — escolha um vídeo.`;
};
