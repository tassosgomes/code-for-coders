import type { AuditRecordSearchInput } from '@/features/audit-trail/api/search-audit-records';
import { auditRecordSearchSchema } from '@/features/audit-trail/api/search-audit-records';
import { z } from 'zod';

export type AuditTrailComplianceFilter = 'all' | 'compliant' | 'non-compliant';

export type AuditTrailDraftFilters = {
  from: string;
  to: string;
  type: string;
};

export type AuditTrailPersonFilter = {
  kind: 'author' | 'target';
  id: string;
  label: string;
};

export type AuditTrailListNavigation = {
  search: AuditRecordSearchInput;
  draft: AuditTrailDraftFilters;
  compliance: AuditTrailComplianceFilter;
  generation: number;
  personFilter: AuditTrailPersonFilter | null;
};

export type AuditTrailNavigationState = {
  personFilter?: AuditTrailPersonFilter;
  returnToAuditList?: AuditTrailListNavigation;
  restoreAuditList?: AuditTrailListNavigation;
  returnToAuditDetail?: string;
};

const personFilterSchema = z.object({
  kind: z.enum(['author', 'target']),
  id: z.uuid(),
  label: z.string(),
});

const listNavigationSchema = z.object({
  search: auditRecordSearchSchema,
  draft: z.object({ from: z.string(), to: z.string(), type: z.string() }),
  compliance: z.enum(['all', 'compliant', 'non-compliant']),
  generation: z.number().int().nonnegative(),
  personFilter: personFilterSchema.nullable(),
});

const navigationStateSchema = z.object({
  personFilter: personFilterSchema.optional(),
  returnToAuditList: listNavigationSchema.optional(),
  restoreAuditList: listNavigationSchema.optional(),
  returnToAuditDetail: z.string().optional(),
});

export const parseAuditTrailNavigationState = (value: unknown): AuditTrailNavigationState | null => {
  const result = navigationStateSchema.safeParse(value);
  return result.success ? result.data : null;
};
