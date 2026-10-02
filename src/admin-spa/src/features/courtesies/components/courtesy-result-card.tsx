import type { CourtesyGrant } from '@/features/courtesies/types/courtesy-grant';
import { formatTermDate } from '@/features/courtesies/utils/format-term-date';

type CourtesyResultCardProps = { grant: CourtesyGrant; onRestart: () => void };
export const CourtesyResultCard = ({ grant, onRestart }: CourtesyResultCardProps) => <section className="catalog-record-card courtesy-lookup-card" aria-label="Resultado da cortesia">
  <h2>Cortesia concedida</h2><p>{grant.courseTitle}</p><p>{grant.accessPeriod?.type === 'months' ? `${grant.accessPeriod.months} meses · ` : 'Vitalícia · '}{grant.endsOn ? `Acesso até ${formatTermDate(grant.endsOn)}` : 'Acesso vitalício'}</p>
  <p>Motivo: {grant.reason}</p><p>O ato foi registrado para auditoria.</p><button type="button" className="primary-button" onClick={onRestart}>Conceder outra cortesia</button>
</section>;
