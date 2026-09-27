import { queryOptions, useQuery } from '@tanstack/react-query';
import { z } from 'zod';

import { apiClient } from '@/lib/api-client';

const identityReferenceSchema = z.object({
  type: z.string(),
  id: z.uuid(),
  label: z.string().optional(),
});

const auditRecordSummarySchema = z.object({
  id: z.uuid(),
  type: z.string().nullable(),
  practicedAt: z.iso.datetime({ offset: true }).nullable(),
  author: identityReferenceSchema.nullable(),
  target: identityReferenceSchema.nullable(),
  compliant: z.boolean(),
  hasComplements: z.boolean(),
});

const auditRecordPageSchema = z.object({
  data: z.array(auditRecordSummarySchema),
  pagination: z.object({
    page: z.number().int().positive(),
    size: z.number().int().positive(),
    total: z.number().int().nonnegative(),
    totalPages: z.number().int().nonnegative(),
    snapshot: z.string().min(1),
  }),
});

export const auditRecordSearchSchema = z.object({
  _page: z.number().int().positive().default(1),
  _size: z.number().int().min(1).max(50).default(20),
  snapshot: z.string().min(1).optional(),
  from: z.iso.datetime({ offset: true }).optional(),
  to: z.iso.datetime({ offset: true }).optional(),
  type: z.string().min(1).optional(),
  authorId: z.uuid().optional(),
  targetId: z.uuid().optional(),
  compliant: z.boolean().optional(),
});

export type AuditRecordSearchInput = z.input<typeof auditRecordSearchSchema>;
export type AuditRecordSearch = z.output<typeof auditRecordSearchSchema>;
export type AuditRecordSummary = z.infer<typeof auditRecordSummarySchema>;
export type AuditRecordPage = z.infer<typeof auditRecordPageSchema>;

export const searchAuditRecords = async (input: AuditRecordSearchInput): Promise<AuditRecordPage> => {
  const request = auditRecordSearchSchema.parse(input);
  const response = await apiClient.post<unknown>('/api/v1/audit-record-searches', request);
  return auditRecordPageSchema.parse(response);
};

export const searchAuditRecordsQueryOptions = (input: AuditRecordSearchInput, generation = 0) => {
  const request = auditRecordSearchSchema.parse(input);
  return queryOptions({
    queryKey: ['audit-trail', 'search', request, generation],
    queryFn: () => searchAuditRecords(request),
  });
};

export const useAuditRecordSearch = (input: AuditRecordSearchInput, generation = 0) =>
  useQuery(searchAuditRecordsQueryOptions(input, generation));
