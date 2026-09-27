import { useMutation } from '@tanstack/react-query';
import { z } from 'zod';

import { apiClient } from '@/lib/api-client';

const acceptedSchema = z.object({
  confirmationId: z.uuid(),
  status: z.literal('accepted'),
});

export type AuditComplementConfirmationAccepted = z.infer<typeof acceptedSchema>;

export const confirmAuditRecordComplement = async (
  recordId: string,
  explanation: string,
  idempotencyKey: string,
): Promise<AuditComplementConfirmationAccepted> => {
  const response = await apiClient.post<unknown>(
    `/api/v1/audit-records/${recordId}/complement-confirmations`,
    { explanation },
    { headers: { 'Idempotency-Key': idempotencyKey } },
  );
  return acceptedSchema.parse(response);
};

export const useConfirmAuditRecordComplement = () => useMutation({
  mutationFn: ({ recordId, explanation, idempotencyKey }: {
    recordId: string;
    explanation: string;
    idempotencyKey: string;
  }) => confirmAuditRecordComplement(recordId, explanation, idempotencyKey),
});
