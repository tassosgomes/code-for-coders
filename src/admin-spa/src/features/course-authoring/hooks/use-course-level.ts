import axios from 'axios';
import { useRef, useState } from 'react';

import { updateCourseInputSchema, useUpdateCourse } from '@/features/course-authoring/api/update-course';

export const useCourseLevel = (courseId: string) => {
  const update = useUpdateCourse();
  const intent = useRef<{ body: string; key: string } | null>(null);
  const [error, setError] = useState<string>();
  const [invalid, setInvalid] = useState(false);
  const [notice, setNotice] = useState('');
  const save = async (values: Record<string, string | null | undefined>) => {
    const input = updateCourseInputSchema.parse(values);
    const body = JSON.stringify(input);
    const key = intent.current?.body === body ? intent.current.key : crypto.randomUUID();
    intent.current = { body, key }; setError(undefined); setInvalid(false); setNotice('');
    try {
      await update.mutateAsync({ courseId, input, idempotencyKey: key }); intent.current = null; setNotice('Nível salvo');
    } catch (failure) {
      const code = axios.isAxiosError<{ code?: string }>(failure) ? failure.response?.data.code : undefined;
      setInvalid(code === 'FIELD_INVALID');
      setError(code === 'FIELD_INVALID' ? 'Escolha um dos níveis.' : 'Não foi possível salvar. Tente de novo.');
    }
  };
  return { busy: update.isPending, error, invalid, notice, save };
};
