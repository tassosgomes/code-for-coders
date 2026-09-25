import { useMutation } from '@tanstack/react-query';
import * as z from 'zod';

import { apiClient } from '@/lib/api-client';
import { passwordPolicySchema } from '@/utils/password-policy-schema';

export const studentPasswordSchema = passwordPolicySchema;

export const registerStudentSchema = z.object({
  name: z.string().trim().min(1, 'Informe seu nome.'),
  email: z.string().trim().email('Informe um e-mail válido.').toLowerCase(),
  password: studentPasswordSchema,
});

export type RegisterStudentInput = z.infer<typeof registerStudentSchema>;

export type RegisterStudentCommand = {
  idempotencyKey: string;
  input: RegisterStudentInput;
};

export const registerStudent = async ({ input, idempotencyKey }: RegisterStudentCommand) => {
  await apiClient.post<void>('/api/v1/student-accounts', input, {
    headers: { 'Idempotency-Key': idempotencyKey },
  });
};

export const useRegisterStudent = () =>
  useMutation({ mutationFn: registerStudent });
