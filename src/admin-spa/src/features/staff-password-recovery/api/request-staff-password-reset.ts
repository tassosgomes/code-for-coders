import { useMutation } from '@tanstack/react-query';
import { z } from 'zod';

import { apiClient } from '@/lib/api-client';

export const staffPasswordRecoveryRequestSchema = z.object({
  email: z.string().min(1, 'Informe seu e-mail.').max(254, 'O e-mail deve ter até 254 caracteres.').email('Informe um e-mail válido.'),
});

export type StaffPasswordRecoveryRequest = z.infer<typeof staffPasswordRecoveryRequestSchema>;

export const requestStaffPasswordReset = async (input: StaffPasswordRecoveryRequest): Promise<void> => {
  const request = staffPasswordRecoveryRequestSchema.parse(input);
  await apiClient.post<void>('/api/v1/staff-password-reset-requests', request, {
    headers: { 'Idempotency-Key': crypto.randomUUID() },
  });
};

export const useRequestStaffPasswordReset = () => useMutation({ mutationFn: requestStaffPasswordReset });
