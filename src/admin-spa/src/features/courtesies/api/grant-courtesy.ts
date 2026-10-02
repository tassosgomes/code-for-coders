import { useMutation } from '@tanstack/react-query';
import { z } from 'zod';

import type { CourtesyGrant } from '@/features/courtesies/types/courtesy-grant';
import { apiClient } from '@/lib/api-client';

export const courtesyGrantSchema = z.object({
  studentId: z.uuid(), courseId: z.uuid(),
  accessPeriod: z.discriminatedUnion('type', [
    z.object({ type: z.literal('months'), months: z.number().int().min(1, 'Informe de 1 a 60 meses inteiros.').max(60, 'Informe de 1 a 60 meses inteiros.') }),
    z.object({ type: z.literal('lifetime') }),
  ]),
  reason: z.string().min(1, 'Informe o motivo da cortesia.').max(500, 'Use até 500 caracteres.').refine((value) => /\S/.test(value), 'Informe o motivo da cortesia.'),
});
export type CourtesyGrantInput = z.infer<typeof courtesyGrantSchema>;
export const grantCourtesy = (input: { body: CourtesyGrantInput; key: string }): Promise<CourtesyGrant> =>
  apiClient.post('/api/v1/courtesy-grants', input.body, { headers: { 'Idempotency-Key': input.key } });
export const useGrantCourtesy = () => useMutation({ mutationFn: grantCourtesy, retry: false });
