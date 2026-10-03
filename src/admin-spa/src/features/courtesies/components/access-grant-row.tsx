import type { CourtesyGrant } from '@/features/courtesies/types/courtesy-grant';
import { formatTermDate } from '@/features/courtesies/utils/format-term-date';

const origins: Record<string, string> = { courtesy: 'Cortesia', purchase: 'Compra', subscription: 'Assinatura', cohort: 'Turma' };
const statuses: Record<string, string> = { active: 'Ativa', expired: 'Vencida' };
type AccessGrantRowProps = { grant: CourtesyGrant };
export const AccessGrantRow = ({ grant }: AccessGrantRowProps) => <li className="courtesy-access-grant-row">
  <strong>{grant.courseTitle}</strong>
  <span className="status-badge courtesy-grant-origin">{origins[grant.origin] ?? grant.origin}</span>
  <span className="courtesy-grant-mobile-meta">{origins[grant.origin] ?? grant.origin}{grant.accessPeriod.type === 'months' ? ` · ${grant.accessPeriod.months} meses` : ''}</span>
  <span>{grant.endsOn ? <><span className="courtesy-grant-months">{grant.accessPeriod.type === 'months' ? `${grant.accessPeriod.months} meses · ` : ''}</span>até {formatTermDate(grant.endsOn)}</> : 'Acesso vitalício'}</span>
  <span className={`status-badge ${grant.status === 'active' ? 'status-active' : ''}`}>{statuses[grant.status] ?? grant.status}</span>
</li>;
