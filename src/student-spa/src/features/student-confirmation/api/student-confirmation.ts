import { useMutation } from '@tanstack/react-query';
import * as z from 'zod';

import { apiClient } from '@/lib/api-client';

export const studentConfirmationTokenSchema = z.object({ token: z.string().min(1) });

export const studentConfirmationEmailSchema = z.object({
  email: z.string().trim().email('Informe um e-mail válido.').toLowerCase(),
});

export type StudentConfirmationTokenInput = z.infer<typeof studentConfirmationTokenSchema>;
export type StudentConfirmationEmailInput = z.infer<typeof studentConfirmationEmailSchema>;

export type StudentConfirmationCommand = {
  input: StudentConfirmationTokenInput;
  idempotencyKey: string;
};

export type StudentConfirmationRequestCommand = {
  input: StudentConfirmationEmailInput;
  idempotencyKey: string;
};

export const confirmStudentAccount = async ({ input, idempotencyKey }: StudentConfirmationCommand) => {
  await apiClient.post<void>('/api/v1/account-confirmations', input, {
    headers: { 'Idempotency-Key': idempotencyKey },
  });
};

export const useConfirmStudentAccount = () => useMutation({ mutationFn: confirmStudentAccount });

export const requestAccountConfirmation = async ({
  input,
  idempotencyKey,
}: StudentConfirmationRequestCommand) => {
  await apiClient.post<void>('/api/v1/account-confirmation-requests', input, {
    headers: { 'Idempotency-Key': idempotencyKey },
  });
};

export const useRequestAccountConfirmation = () => useMutation({ mutationFn: requestAccountConfirmation });
