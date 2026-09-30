import axios from 'axios';
import { useLayoutEffect, useRef, useState } from 'react';

import { useCreateLesson } from '@/features/course-authoring/api/create-lesson';
import { useCreateModule } from '@/features/course-authoring/api/create-module';
import { useDeleteLesson } from '@/features/course-authoring/api/delete-lesson';
import { useDeleteModule } from '@/features/course-authoring/api/delete-module';
import { useUpdateCourse } from '@/features/course-authoring/api/update-course';
import { useUpdateLesson, type UpdateLessonInput } from '@/features/course-authoring/api/update-lesson';
import { useUpdateModule, type UpdateModuleInput } from '@/features/course-authoring/api/update-module';
import type { Course, CourseLesson } from '@/features/course-authoring/types/course';
import type { CourseEditTarget, CourseRemoveTarget } from '@/features/course-authoring/types/course-edit-target';

export const useCourseStructure = (course: Course) => {
  const updateCourse = useUpdateCourse(); const createModule = useCreateModule(); const updateModule = useUpdateModule();
  const deleteModule = useDeleteModule(); const createLesson = useCreateLesson(); const updateLesson = useUpdateLesson(); const deleteLesson = useDeleteLesson();
  const [editTarget, setEditTarget] = useState<CourseEditTarget | null>(null);
  const [removeTarget, setRemoveTarget] = useState<CourseRemoveTarget | null>(null);
  const [videoTarget, setVideoTarget] = useState<CourseLesson | null>(null);
  const [error, setError] = useState<string>(); const [notice, setNotice] = useState('');
  const intent = useRef<{ body: string; key: string } | null>(null);
  const pendingFocus = useRef<string | null>(null); const elements = useRef(new Map<string, HTMLElement>());
  const busy = [updateCourse, createModule, updateModule, deleteModule, createLesson, updateLesson, deleteLesson].some((mutation) => mutation.isPending);
  useLayoutEffect(() => {
    if (pendingFocus.current) {
      elements.current.get(pendingFocus.current)?.focus();
      pendingFocus.current = null;
    }
  }, [course.draftRevision]);
  const run = async (operation: string, payload: unknown, action: (idempotencyKey: string) => Promise<Course>, focus?: string) => {
    const body = JSON.stringify({ operation, payload });
    const key = intent.current?.body === body ? intent.current.key : crypto.randomUUID();
    intent.current = { body, key }; setError(undefined); setNotice('');
    pendingFocus.current = focus ?? null;
    try {
      await action(key); intent.current = null; setNotice('Alterações salvas.'); return true;
    } catch (failure) {
      pendingFocus.current = null;
      const code = axios.isAxiosError<{ code?: string }>(failure) ? failure.response?.data.code : undefined;
      setError(code === 'VIDEO_NOT_AVAILABLE' ? 'Este vídeo ainda não está disponível para vínculo. Aguarde alguns instantes e tente de novo.' : code === 'STRUCTURE_LIMIT_REACHED' ? 'Limite atingido: até 100 módulos por curso e 200 aulas por módulo.' : code === 'INVALID_POSITION' ? 'A ordem mudou. Atualize o curso e tente novamente.' : code === 'TITLE_REQUIRED' ? 'Informe o título.' : 'Não foi possível salvar. Seus dados foram mantidos. Tente novamente.');
      return false;
    }
  };
  const editModule = (moduleId: string, input: UpdateModuleInput) => run(`module:${moduleId}`, input, (idempotencyKey) => updateModule.mutateAsync({ courseId: course.courseId, moduleId, input, idempotencyKey }), `modulo-${moduleId}`);
  const editLesson = (lessonId: string, input: UpdateLessonInput) => run(`lesson:${lessonId}`, input, (idempotencyKey) => updateLesson.mutateAsync({ courseId: course.courseId, lessonId, input, idempotencyKey }), `aula-${lessonId}`);
  const saveDetails = async (input: { title: string; description: string }) => {
    if (!editTarget) return;
    let saved = false;
    if (editTarget.kind === 'course') saved = await run('course', input, (idempotencyKey) => updateCourse.mutateAsync({ courseId: course.courseId, input, idempotencyKey }));
    if (editTarget.kind === 'create-module') saved = await run('create-module', { title: input.title }, (idempotencyKey) => createModule.mutateAsync({ courseId: course.courseId, input: { title: input.title }, idempotencyKey }));
    if (editTarget.kind === 'module') saved = await editModule(editTarget.module.moduleId, { title: input.title });
    if (editTarget.kind === 'create-lesson') {
      const moduleId = editTarget.module.moduleId;
      saved = await run(`create-lesson:${moduleId}`, input, (idempotencyKey) => createLesson.mutateAsync({ courseId: course.courseId, moduleId, input, idempotencyKey }));
    }
    if (editTarget.kind === 'lesson') saved = await editLesson(editTarget.lesson.lessonId, input);
    if (saved) setEditTarget(null);
  };
  const remove = async () => {
    if (!removeTarget) return;
    const saved = removeTarget.kind === 'module'
      ? await run(`remove-module:${removeTarget.module.moduleId}`, null, (idempotencyKey) => deleteModule.mutateAsync({ courseId: course.courseId, moduleId: removeTarget.module.moduleId, idempotencyKey }))
      : await run(`remove-lesson:${removeTarget.lesson.lessonId}`, null, (idempotencyKey) => deleteLesson.mutateAsync({ courseId: course.courseId, lessonId: removeTarget.lesson.lessonId, idempotencyKey }));
    if (saved) setRemoveTarget(null);
  };
  return { busy, error, notice, elements, editTarget, removeTarget, videoTarget,
    openVideo: (lesson: CourseLesson) => { setError(undefined); setVideoTarget(lesson); },
    closeVideo: () => { intent.current = null; setError(undefined); setVideoTarget(null); }, saveDetails, remove, editModule, editLesson,
    openEdit: (target: CourseEditTarget) => { setError(undefined); setEditTarget(target); },
    openRemove: (target: CourseRemoveTarget) => { setError(undefined); setRemoveTarget(target); },
    closeEdit: () => { intent.current = null; setEditTarget(null); }, closeRemove: () => { intent.current = null; setRemoveTarget(null); } };
};
