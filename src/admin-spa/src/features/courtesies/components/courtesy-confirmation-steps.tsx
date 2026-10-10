import { CircleAlert, Loader2, TriangleAlert } from 'lucide-react';

import { CourtesyResultCard } from '@/features/courtesies/components/courtesy-result-card';
import { CourtesyResultError } from '@/features/courtesies/components/courtesy-result-error';
import { LifetimeConfirm } from '@/features/courtesies/components/lifetime-confirm';
import { PeriodFieldset } from '@/features/courtesies/components/period-fieldset';
import { ReviewSentence } from '@/features/courtesies/components/review-sentence';
import { ReviewTable } from '@/features/courtesies/components/review-table';
import { StepSummary } from '@/features/courtesies/components/step-summary';
import { TermPreview } from '@/features/courtesies/components/term-preview';
import { useCourtesyConfirmation } from '@/features/courtesies/hooks/use-courtesy-confirmation';
import { formatAccessPeriod } from '@/features/courtesies/utils/format-access-period';

type CourtesyConfirmationStepsProps = { studentId: string; studentName: string; studentEmail: string; courseId: string; courseTitle: string; step: number; setStep: (step: number) => void; onRestart: () => void; onViewGrants: () => void };
const existingAccessSuffix = 'A nova cortesia não estende nem encerra a outra; as duas passam a existir.';
export const CourtesyConfirmationSteps = ({ studentId, studentName, studentEmail, courseId, courseTitle, step, setStep, onRestart, onViewGrants }: CourtesyConfirmationStepsProps) => {
  const flow = useCourtesyConfirmation(studentId, courseId, step, setStep);
  const { form, period, preview, mutation } = flow;
  if (step === 5 && mutation.data && !flow.resultError) return <CourtesyResultCard grant={mutation.data} onRestart={onRestart} onViewGrants={onViewGrants} />;
  if (step === 5 && flow.resultError) return <CourtesyResultError kind={flow.resultError} retrying={mutation.isPending} setStep={setStep} onRetry={() => { void flow.confirm(); }} onBackToReview={flow.backToReview} />;
  const lifetime = period.type === 'lifetime';
  const reason = form.watch('reason');
  const overLimit = reason.length > 500;
  const reasonMessage = overLimit ? `Use até 500 caracteres: tire ${reason.length - 500}.` : form.formState.errors.reason?.message;
  const reasonInvalid = Boolean(form.formState.errors.reason) || overLimit;
  const previewPending = period.type === 'months' && (!preview.data || preview.isFetching || preview.isError);
  const periodText = formatAccessPeriod(period, preview.data?.endsOn);
  const courseSummary = <StepSummary label="Curso" value={courseTitle} onEdit={() => setStep(1)} />;
  return <div hidden={step < 2} className="courtesy-steps">
    {step === 2 ? courseSummary : null}
    {step === 3 ? <>{courseSummary}<StepSummary label="Vigência" value={periodText} onEdit={() => setStep(2)} /></> : null}
    <section hidden={step !== 2} className="catalog-record-card courtesy-lookup-card" aria-label="Passo Vigência">
      <div className="courtesy-step-heading"><h2>Vigência</h2><span>3 de 6</span></div>
      <PeriodFieldset form={form} lifetime={lifetime} invalid={flow.monthsInvalid} onMonths={flow.selectMonths} onLifetime={flow.selectLifetime} />
      <TermPreview lifetime={lifetime} invalid={flow.monthsInvalid} endsOn={preview.data?.endsOn} busy={preview.isFetching} error={preview.isError} onRetry={() => { void preview.refetch(); }} />
      <div className="courtesy-actions"><button type="button" className="secondary-button" onClick={() => setStep(1)}>Voltar</button>
        {preview.isError && !lifetime ? null : <button type="button" className="primary-button" disabled={!lifetime && (flow.monthsInvalid || previewPending)} onClick={async () => { if (await form.trigger('accessPeriod')) setStep(3); }}>Continuar</button>}</div>
    </section>
    <section hidden={step !== 3} className="catalog-record-card courtesy-lookup-card" aria-label="Passo Motivo">
      <div className="courtesy-step-heading"><h2>Motivo</h2><span>4 de 6</span></div>
      <p className="courtesy-step-description">Diga por que esta cortesia existe. O motivo vai para a trilha.</p>
      <label htmlFor="courtesy-reason">Motivo</label>
      <textarea id="courtesy-reason" rows={2} aria-label="Motivo da cortesia" aria-invalid={reasonInvalid} aria-describedby={reasonInvalid ? 'courtesy-reason-error' : undefined} {...form.register('reason')} />
      {reasonInvalid ? <p id="courtesy-reason-error" role="alert" className="field-error">{reasonMessage}</p> : null}
      <div className="courtesy-reason-meta"><span className="courtesy-hint">Não inclua dado pessoal de terceiros.</span><span className={`courtesy-hint ${overLimit ? 'is-over' : ''}`}>{reason.length}/500</span></div>
      {flow.reviewError ? <p role="alert" className="field-error">{flow.reviewError}</p> : null}
      <div className="courtesy-actions"><button type="button" className="secondary-button" onClick={() => setStep(2)}>Voltar</button>
        {reasonInvalid ? null : <button type="button" className="primary-button" disabled={flow.reviewing} onClick={() => { void flow.review(); }}>{flow.reviewing ? 'Calculando término…' : 'Revisar'}</button>}</div>
    </section>
    <section hidden={step !== 4} className="catalog-record-card courtesy-lookup-card" aria-label="Passo Revisão">
      <div className="courtesy-step-heading"><h2>Revisão</h2><span>5 de 6</span></div>
      <ReviewSentence student={studentEmail} course={courseTitle} reason={reason} lifetime={lifetime} endsOn={preview.data?.endsOn} />
      {flow.existingAccess ? <div className="courtesy-callout courtesy-callout-warning" role="status"><TriangleAlert size={16} aria-hidden="true" /><p><span>{flow.existingAccess}</span> <span>{existingAccessSuffix}</span></p></div> : null}
      <ReviewTable disabled={mutation.isPending} rows={[
        { label: 'Aluno', value: `${studentName} (${studentEmail})`, onEdit: () => setStep(0) },
        { label: 'Curso', value: courseTitle, onEdit: () => setStep(1) },
        { label: 'Vigência', value: periodText, onEdit: () => setStep(2) },
        { label: 'Motivo', value: reason, onEdit: () => setStep(3) },
      ]} />
      {flow.grants.isFetching ? <p role="status" className="courtesy-hint">Consultando concessões do aluno…</p> : null}
      {flow.grants.isError ? <p role="alert" className="courtesy-hint">Não foi possível consultar os acessos existentes. A cortesia pode ser concedida mesmo assim.</p> : null}
      {step === 4 && lifetime ? <LifetimeConfirm confirmed={flow.lifetimeConfirmed} disabled={mutation.isPending} onChange={flow.setLifetimeConfirmed} /> : null}
      {flow.error ? <div className="courtesy-callout courtesy-callout-danger" role="alert"><CircleAlert size={16} aria-hidden="true" /><p>{flow.error.text}</p>
        {flow.error.retryable ? <button type="button" className="text-button" disabled={mutation.isPending} onClick={() => { void flow.confirm(); }}>Tentar de novo</button> : null}</div> : null}
      <div className="courtesy-actions"><button type="button" className="secondary-button" disabled={mutation.isPending} onClick={() => setStep(3)}>Voltar</button>
        <button type="button" className="primary-button" disabled={mutation.isPending || lifetime && !flow.lifetimeConfirmed || previewPending} onClick={() => { void flow.confirm(); }}>
          {mutation.isPending ? <><Loader2 size={16} className="courtesy-spinner" aria-hidden="true" />Concedendo…</> : lifetime ? 'Confirmar cortesia vitalícia' : 'Confirmar cortesia'}</button></div>
    </section>
  </div>;
};
