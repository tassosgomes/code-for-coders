import { useMutation } from '@tanstack/react-query';
import * as z from 'zod';

import { apiClient } from '@/lib/api-client';

export const studentPasswordResetRequestSchema = z.object({
  email: z.string().trim().email('Informe um e-mail válido.').toLowerCase(),
});

export type StudentPasswordResetRequestInput = z.infer<typeof studentPasswordResetRequestSchema>;

export type StudentPasswordResetRequestCommand = {
  input: StudentPasswordResetRequestInput;
  idempotencyKey: string;
};

export const requestStudentPasswordReset = async ({ input, idempotencyKey }: StudentPasswordResetRequestCommand) => {
  await apiClient.post<void>('/api/v1/password-reset-requests', input, {
    headers: { 'Idempotency-Key': idempotencyKey },
  });
};

export const useRequestStudentPasswordReset = () => useMutation({ mutationFn: requestStudentPasswordReset });
