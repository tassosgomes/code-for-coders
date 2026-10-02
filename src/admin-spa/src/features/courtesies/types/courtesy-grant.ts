export type AccessPeriod = { type: 'months'; months: number } | { type: 'lifetime' };
export type CourtesyGrant = { grantId: string; studentId: string; courseId: string; courseTitle: string;
  origin: string; status: string; accessPeriod: AccessPeriod; grantedAt: string; endsOn: string | null;
  expiresAt: string | null; reason: string | null };
export type CourtesyTermPreview = { months: number; computedAt: string; endsOn: string; expiresAt: string };
