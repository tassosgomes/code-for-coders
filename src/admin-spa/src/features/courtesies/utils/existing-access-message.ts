import type { CourtesyGrant } from '@/features/courtesies/types/courtesy-grant';
import { formatTermDate } from '@/features/courtesies/utils/format-term-date';

export const existingAccessMessage = (grants: CourtesyGrant[] | undefined, courseId: string) => {
  const active = grants?.filter((grant) => grant.courseId === courseId && grant.status === 'active') ?? [];
  if (active.some((grant) => grant.expiresAt === null)) return 'Este aluno já tem acesso vitalício a este curso.';
  const last = active.filter((grant) => grant.endsOn).sort((a, b) => (b.expiresAt ?? '').localeCompare(a.expiresAt ?? ''))[0];
  return last?.endsOn ? `Este aluno já tem acesso até ${formatTermDate(last.endsOn)}.` : null;
};
