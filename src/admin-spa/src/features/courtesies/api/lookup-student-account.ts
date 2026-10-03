import { useMutation } from '@tanstack/react-query';
import { z } from 'zod';

import { apiClient } from '@/lib/api-client';

export const studentAccountLookupSchema = z.object({ email: z.string().trim().toLowerCase().max(254, 'Use até 254 caracteres.').email('Informe um e-mail válido.') });
export type StudentAccountLookupInput = z.infer<typeof studentAccountLookupSchema>;
export type StudentAccount = { studentId: string; email: string; name: string; emailConfirmed: boolean; status: string };

export const lookupStudentAccount = (input: StudentAccountLookupInput): Promise<StudentAccount> => apiClient.post('/api/v1/student-account-lookups', input);

export const useStudentAccountLookup = () => useMutation({ mutationFn: lookupStudentAccount, gcTime: 0, retry: false });
