import { useMutation } from '@tanstack/react-query';
import * as z from 'zod';

import { apiClient } from '@/lib/api-client';
import { studentPasswordSchema } from '@/features/student-password-recovery/utils/student-password-schema';

export const studentPasswordResetSchema = z.object({
  token: z.string().min(1),
  newPassword: studentPasswordSchema,
});

export const studentPasswordResetFormSchema = studentPasswordResetSchema.omit({ token: true });

export type StudentPasswordResetInput = z.infer<typeof studentPasswordResetSchema>;
export type StudentPasswordResetFormInput = z.infer<typeof studentPasswordResetFormSchema>;

export type StudentPasswordResetCommand = {
  token: string;
  input: StudentPasswordResetFormInput;
  idempotencyKey: string;
};

export const resetStudentPassword = async ({ token, input, idempotencyKey }: StudentPasswordResetCommand) => {
  const body: StudentPasswordResetInput = studentPasswordResetSchema.parse({ token, ...input });
  await apiClient.post<void>('/api/v1/password-resets', body, {
    headers: { 'Idempotency-Key': idempotencyKey },
  });
};

export const useResetStudentPassword = () => useMutation({ mutationFn: resetStudentPassword });
