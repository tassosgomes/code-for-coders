import axios from 'axios';
import { useRef, useState } from 'react';

import { useUpdateCatalogCourse } from '@/features/catalog-courses/api/update-catalog-course';
import type { UpdateCatalogCourseInput } from '@/features/catalog-courses/api/update-catalog-course';

export const useCatalogTagline = (courseId: string) => {
  const update = useUpdateCatalogCourse();
  const intent = useRef<{ body: string; key: string } | null>(null);
  const [error, setError] = useState<string>();
  const [fieldError, setFieldError] = useState<string>();
  const [notice, setNotice] = useState('');
  const save = async (input: UpdateCatalogCourseInput) => {
    const body = JSON.stringify(input);
    const key = intent.current?.body === body ? intent.current.key : crypto.randomUUID();
    intent.current = { body, key }; setError(undefined); setFieldError(undefined); setNotice('');
    try {
      await update.mutateAsync({ courseId, input, idempotencyKey: key });
      intent.current = null; setNotice('Chamada salva.'); return true;
    } catch (failure) {
      const problem = axios.isAxiosError<{ code?: string; detail?: string }>(failure) ? failure.response?.data : undefined;
      if (problem?.code === 'FIELD_INVALID') setFieldError('Use até 160 caracteres na chamada comercial.');
      else setError(problem?.code === 'IDEMPOTENCY_KEY_REUSED' ? 'Esta tentativa já foi usada para outra alteração. Edite a chamada e salve novamente.'
        : 'Não foi possível confirmar o salvamento. Tente de novo para confirmar a mesma alteração.');
      return false;
    }
  };
  return { save, busy: update.isPending, error, fieldError, notice };
};
