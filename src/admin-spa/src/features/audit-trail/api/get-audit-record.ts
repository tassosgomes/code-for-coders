import { queryOptions, useQuery } from '@tanstack/react-query';
import { z } from 'zod';

import { apiClient } from '@/lib/api-client';

const identityReferenceSchema = z.object({
  type: z.string().nullable(),
  id: z.uuid().nullable(),
  label: z.string().nullable().optional(),
}).nullable();

const complementSchema = z.object({
  id: z.uuid(),
  confirmationId: z.uuid(),
  createdAt: z.iso.datetime({ offset: true }),
  author: identityReferenceSchema,
  explanation: z.string(),
});

export const auditRecordDetailSchema = z.object({
  id: z.uuid(),
  type: z.string().nullable(),
  practicedAt: z.iso.datetime({ offset: true }).nullable(),
  author: identityReferenceSchema,
  target: identityReferenceSchema,
  compliant: z.boolean(),
  hasComplements: z.boolean(),
  origin: z.string(),
  receivedAt: z.iso.datetime({ offset: true }),
  reason: z.string().nullable(),
  attributes: z.record(z.string(), z.string()),
  nonComplianceReasons: z.array(z.enum([
    'tipo-desconhecido',
    'autor-ausente',
    'alvo-ausente',
    'motivo-ausente',
    'momento-ausente',
    'complemento-invalido',
    'motivo-excede-limite',
  ])),
  complements: z.array(complementSchema),
});

export type AuditRecordDetail = z.infer<typeof auditRecordDetailSchema>;

export const getAuditRecord = async (recordId: string): Promise<AuditRecordDetail> => {
  const response = await apiClient.get<unknown>(`/api/v1/audit-records/${recordId}`);
  return auditRecordDetailSchema.parse(response);
};

export const getAuditRecordQueryOptions = (recordId: string) => queryOptions({
  queryKey: ['audit-trail', 'detail', recordId],
  queryFn: () => getAuditRecord(recordId),
});

export const useAuditRecord = (recordId: string) => useQuery(getAuditRecordQueryOptions(recordId));
