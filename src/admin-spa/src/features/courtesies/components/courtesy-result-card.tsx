import { Check, CircleCheck } from 'lucide-react';
import { useEffect, useState } from 'react';

import type { CourtesyGrant } from '@/features/courtesies/types/courtesy-grant';
import { formatGrantedPeriod } from '@/features/courtesies/utils/format-access-period';

type CourtesyResultCardProps = { grant: CourtesyGrant; onRestart: () => void; onViewGrants: () => void };
export const CourtesyResultCard = ({ grant, onRestart, onViewGrants }: CourtesyResultCardProps) => {
  const [toast, setToast] = useState(true);
  useEffect(() => {
    const timer = window.setTimeout(() => setToast(false), 5000);
    return () => window.clearTimeout(timer);
  }, []);
  return <>
    <section className="catalog-record-card courtesy-lookup-card" aria-label="Resultado da cortesia">
      <div className="courtesy-step-heading"><h2>Resultado</h2><span>6 de 6</span></div>
      <p className="courtesy-result-title"><CircleCheck size={20} aria-hidden="true" />Cortesia concedida.</p>
      <div className="courtesy-result-summary">
        <strong>{grant.courseTitle}</strong>
        <p>{formatGrantedPeriod(grant.accessPeriod, grant.endsOn)}</p>
        <p className="courtesy-hint">Motivo: {grant.reason}</p>
      </div>
      <div className="courtesy-actions courtesy-result-actions">
        <button type="button" className="text-button courtesy-view-grants" onClick={onViewGrants}>Ver concessões do aluno</button>
        <button type="button" className="primary-button" onClick={onRestart}>Conceder outra cortesia</button>
      </div>
    </section>
    {toast ? <p className="courtesy-toast" role="status"><Check size={16} aria-hidden="true" />Cortesia concedida.</p> : null}
  </>;
};
