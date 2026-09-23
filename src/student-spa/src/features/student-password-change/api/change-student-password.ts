import { useMutation } from '@tanstack/react-query';
import * as z from 'zod';

import { apiClient } from '@/lib/api-client';
import { passwordPolicySchema } from '@/utils/password-policy-schema';

export const studentPasswordChangeSchema = z.object({
  currentPassword: z.string().min(1, 'Informe sua senha atual.'),
  newPassword: passwordPolicySchema,
});

export type StudentPasswordChangeInput = z.infer<typeof studentPasswordChangeSchema>;

export type StudentPasswordChangeCommand = {
  input: StudentPasswordChangeInput;
  csrfToken: string;
  idempotencyKey: string;
};

export const changeStudentPassword = async ({ input, csrfToken, idempotencyKey }: StudentPasswordChangeCommand) => {
  await apiClient.post<void>('/api/v1/password-changes', input, {
    headers: {
      'Idempotency-Key': idempotencyKey,
      'X-CSRF-Token': csrfToken,
    },
  });
};

export const useChangeStudentPassword = () => useMutation({ mutationFn: changeStudentPassword });
