import { useState } from 'react';
import { z } from 'zod';

import { useValidatedForm } from '@/components/ui/form/validated-form';
import type { CourtesyCourse } from '@/features/courtesies/api/list-courtesy-courses';

const schema = z.object({ course: z.object({ courseId: z.string(), title: z.string() }).nullable() });
export const useCourtesyForm = () => {
  const form = useValidatedForm(schema, { course: null });
  const [step, setStep] = useState(0);
  return { step, setStep, course: form.watch('course'),
    selectCourse: (course: CourtesyCourse) => form.setValue('course', course, { shouldDirty: true }),
    clearCourse: () => form.reset({ course: null }) };
};
