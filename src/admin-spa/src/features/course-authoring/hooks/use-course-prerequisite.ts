import axios from 'axios';
import { useRef, useState } from 'react';

import { useUpdateCourse, type CoursePrerequisiteInput } from '@/features/course-authoring/api/update-course';

export const useCoursePrerequisite = (courseId: string) => {
  const update = useUpdateCourse();
  const intent = useRef<{ body: string; key: string } | null>(null);
  const [error, setError] = useState<{ field: 'prerequisiteText' | 'recommendedCourseIds' | 'service'; message: string; index?: number }>();
  const [notice, setNotice] = useState('');
  const save = async (input: CoursePrerequisiteInput) => {
    const body = JSON.stringify(input);
    const key = intent.current?.body === body ? intent.current.key : crypto.randomUUID();
    intent.current = { body, key }; setError(undefined); setNotice('');
    try {
      await update.mutateAsync({ courseId, input, idempotencyKey: key });
      intent.current = null; setNotice('Pré-requisito salvo'); return true;
    } catch (failure) {
      const problem = axios.isAxiosError<{ code?: string; errors?: Record<string, string[]> }>(failure) ? failure.response?.data : undefined;
      if (problem?.code === 'RECOMMENDED_COURSE_INVALID') {
        const field = Object.keys(problem.errors ?? {}).find((name) => /^recommendedCourseIds\[\d+\]$/.test(name));
        const index = field ? Number(field.match(/\d+/)?.[0]) : undefined;
        setError({ field: 'recommendedCourseIds', index, message: 'Não foi possível salvar: um curso recomendado não está disponível. Remova-o ou escolha outro.' });
        requestAnimationFrame(() => document.getElementById(`recommended-${input.recommendedCourseIds[index ?? 0]}`)?.focus());
      } else if (problem?.code === 'FIELD_INVALID') {
        setError({ field: 'prerequisiteText', message: 'O pré-requisito deve ter até 1 000 caracteres.' });
        requestAnimationFrame(() => document.getElementById('prerequisite-text')?.focus());
      } else setError({ field: 'service', message: 'Não foi possível salvar. Tente de novo.' });
      return false;
    }
  };
  return { busy: update.isPending, error, notice, save, clear: () => { setError(undefined); setNotice(''); intent.current = null; } };
};
