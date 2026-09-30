import { useState } from 'react';
import axios from 'axios';

import { Dialog } from '@/components/ui/dialog';
import { TextDetailsForm } from '@/components/ui/form/text-details-form';
import { createCourseInputSchema, useCreateCourse, type CreateCourseInput } from '@/features/course-authoring/api/create-course';
import type { Course } from '@/features/course-authoring/types/course';

type CreateCourseDialogProps = { onClose: () => void; onCreated: (course: Course) => void };
export const CreateCourseDialog = ({ onClose, onCreated }: CreateCourseDialogProps) => {
  const create = useCreateCourse(onCreated);
  const [intent, setIntent] = useState<{ body: string; key: string } | null>(null);
  const [titleError, setTitleError] = useState<string>();
  const [error, setError] = useState<string>();
  const submit = async (input: CreateCourseInput) => {
    const body = JSON.stringify(input);
    const key = intent?.body === body ? intent.key : crypto.randomUUID();
    setIntent({ body, key }); setTitleError(undefined); setError(undefined);
    try { await create.mutateAsync({ input, idempotencyKey: key }); }
    catch (failure) {
      if (axios.isAxiosError<{ code?: string }>(failure) && failure.response?.data.code === 'TITLE_REQUIRED')
        setTitleError('Informe o título do curso.');
      else setError('Não foi possível criar o curso. Seus dados foram mantidos. Tente novamente.');
    }
  };
  return <Dialog title="Novo curso" description="Crie o rascunho. Você poderá montar as aulas depois." busy={create.isPending} onClose={onClose}>
    <TextDetailsForm schema={createCourseInputSchema} busy={create.isPending} titleError={titleError} error={error} onSubmit={submit} onCancel={onClose} />
  </Dialog>;
};
