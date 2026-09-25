import { useMutation } from '@tanstack/react-query';
import { z } from 'zod';

import { apiClient, clearCsrfToken, setCsrfToken } from '@/lib/api-client';

export const staffSessionLoginSchema = z.object({
  email: z.email(),
  password: z.string().min(1),
});

export const staffSessionSchema = z.object({
  accountId: z.uuid(),
  name: z.string(),
  roles: z.array(z.string()),
  permissions: z.array(z.string()),
  csrfToken: z.string().min(1),
});

export type StaffSessionLoginInput = z.infer<typeof staffSessionLoginSchema>;
export type StaffSession = z.infer<typeof staffSessionSchema>;

export const createStaffSession = async (input: StaffSessionLoginInput): Promise<StaffSession> => {
  const request = staffSessionLoginSchema.parse(input);
  const response = await apiClient.post<StaffSession>('/api/v1/staff-sessions', request, {
    headers: { 'Idempotency-Key': crypto.randomUUID() },
  });
  const session = staffSessionSchema.parse(response);
  setCsrfToken(session.csrfToken);
  return session;
};

export const getCurrentStaffSession = async (): Promise<StaffSession> => {
  const response = await apiClient.get<StaffSession>('/api/v1/staff-sessions/current');
  const session = staffSessionSchema.parse(response);
  setCsrfToken(session.csrfToken);
  return session;
};

export const endCurrentStaffSession = async (): Promise<void> => {
  await apiClient.delete('/api/v1/staff-sessions/current', {
    headers: { 'Idempotency-Key': crypto.randomUUID() },
  });
  clearCsrfToken();
};

export const useCreateStaffSession = () => useMutation({ mutationFn: createStaffSession });

export const useEndCurrentStaffSession = () => useMutation({ mutationFn: endCurrentStaffSession });
