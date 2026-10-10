import { CircleAlert, TriangleAlert } from 'lucide-react';

import type { ResultError } from '@/features/courtesies/hooks/use-courtesy-confirmation';

type CourtesyResultErrorProps = { kind: ResultError; retrying: boolean; setStep: (step: number) => void; onRetry: () => void; onBackToReview: () => void };
export const CourtesyResultError = ({ kind, retrying, setStep, onRetry, onBackToReview }: CourtesyResultErrorProps) =>
  <section className="catalog-record-card courtesy-lookup-card" aria-label="Resultado da cortesia">
    <div className="courtesy-step-heading"><h2>Resultado</h2><span>6 de 6</span></div>
    {kind === 'ineligible' ? <div className="courtesy-callout courtesy-callout-warning" role="alert">
      <TriangleAlert size={16} aria-hidden="true" /><p>Não foi possível conceder: verifique o aluno e o curso.</p>
      <button type="button" className="text-button" onClick={() => setStep(0)}>Voltar ao Aluno</button>
      <button type="button" className="text-button" onClick={() => setStep(1)}>Voltar ao Curso</button>
    </div> : kind === 'idempotency' ? <div className="courtesy-callout courtesy-callout-warning" role="alert">
      <TriangleAlert size={16} aria-hidden="true" /><p>Esta confirmação já foi usada de outro jeito. Revise e confirme de novo.</p>
      <button type="button" className="text-button" onClick={onBackToReview}>Voltar à revisão</button>
    </div> : <div className="courtesy-callout courtesy-callout-danger" role="alert">
      <CircleAlert size={16} aria-hidden="true" /><p>Não foi possível conceder agora.</p>
      <button type="button" className="text-button" disabled={retrying} onClick={onRetry}>{retrying ? 'Tentando…' : 'Tentar de novo'}</button>
    </div>}
  </section>;
