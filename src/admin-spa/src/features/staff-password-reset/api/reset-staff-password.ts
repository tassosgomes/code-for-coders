import { useMutation } from '@tanstack/react-query';
import { z } from 'zod';

import { apiClient } from '@/lib/api-client';

const staffPasswordSchema = z.string()
  .min(8, 'Use pelo menos 8 caracteres.')
  .refine((password) => /\p{Lu}/u.test(password), 'Inclua uma letra maiúscula.')
  .refine((password) => /\p{Ll}/u.test(password), 'Inclua uma letra minúscula.')
  .refine((password) => /\p{Nd}/u.test(password), 'Inclua um número.')
  .refine((password) => /[^\p{L}\p{N}\s]/u.test(password), 'Inclua um símbolo ou sinal de pontuação.');

export const staffPasswordResetSchema = z.object({
  token: z.string().min(1, 'O link de redefinição não é válido.'),
  newPassword: staffPasswordSchema,
});

export const staffPasswordResetFormSchema = staffPasswordResetSchema.omit({ token: true });

export type StaffPasswordResetInput = z.infer<typeof staffPasswordResetSchema>;
export type StaffPasswordResetFormInput = z.infer<typeof staffPasswordResetFormSchema>;

export type ResetStaffPasswordCommand = {
  token: string;
  input: StaffPasswordResetFormInput;
};

export const resetStaffPassword = async ({ token, input }: ResetStaffPasswordCommand) => {
  const request = staffPasswordResetSchema.parse({ token, ...input });
  await apiClient.post<void>('/api/v1/staff-password-resets', request, {
    headers: { 'Idempotency-Key': crypto.randomUUID() },
  });
};

export const useResetStaffPassword = () => useMutation({ mutationFn: resetStaffPassword });
