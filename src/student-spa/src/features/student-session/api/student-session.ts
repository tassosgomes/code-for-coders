import { queryOptions, useMutation, useQuery } from '@tanstack/react-query';
import * as z from 'zod';

import { apiClient } from '@/lib/api-client';

export const studentSessionSchema = z.object({
  accountId: z.uuid(),
  name: z.string(),
  csrfToken: z.string().min(1),
});

export type StudentSession = z.infer<typeof studentSessionSchema>;

export const createStudentSessionSchema = z.object({
  email: z.string().trim().email('Informe um e-mail válido.').toLowerCase(),
  password: z.string().min(1, 'Informe sua senha.'),
});

export type CreateStudentSessionInput = z.infer<typeof createStudentSessionSchema>;

export type CreateStudentSessionCommand = {
  input: CreateStudentSessionInput;
  idempotencyKey: string;
};

export type EndStudentSessionCommand = {
  csrfToken: string;
  idempotencyKey: string;
};

export const studentSessionQueryKey = ['student-session'] as const;

export const createStudentSession = async ({ input, idempotencyKey }: CreateStudentSessionCommand) => {
  const response = await apiClient.post<StudentSession>('/api/v1/student-sessions', input, {
    headers: { 'Idempotency-Key': idempotencyKey },
  });

  return studentSessionSchema.parse(response);
};

export const getCurrentStudentSession = async () => {
  const response = await apiClient.get<StudentSession>('/api/v1/student-sessions/current');

  return studentSessionSchema.parse(response);
};

export const endCurrentStudentSession = async ({ csrfToken, idempotencyKey }: EndStudentSessionCommand) => {
  await apiClient.delete<void>('/api/v1/student-sessions/current', {
    headers: {
      'Idempotency-Key': idempotencyKey,
      'X-CSRF-Token': csrfToken,
    },
  });
};

export const studentSessionQueryOptions = () =>
  queryOptions({
    queryKey: studentSessionQueryKey,
    queryFn: getCurrentStudentSession,
    staleTime: 30_000,
  });

export const useStudentSession = () => useQuery(studentSessionQueryOptions());

export const useCreateStudentSession = () => useMutation({ mutationFn: createStudentSession });

export const useEndStudentSession = () => useMutation({ mutationFn: endCurrentStudentSession });
