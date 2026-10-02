import { CourtesyResultCard } from '@/features/courtesies/components/courtesy-result-card';
import { ReviewSentence } from '@/features/courtesies/components/review-sentence';
import { TermPreview } from '@/features/courtesies/components/term-preview';
import { useCourtesyConfirmation } from '@/features/courtesies/hooks/use-courtesy-confirmation';

type CourtesyConfirmationStepsProps = { studentId: string; studentName: string; studentEmail: string; courseId: string; courseTitle: string; step: number; setStep: (step: number) => void; onRestart: () => void };
export const CourtesyConfirmationSteps = ({ studentId, studentName, studentEmail, courseId, courseTitle, step, setStep, onRestart }: CourtesyConfirmationStepsProps) => {
  const flow = useCourtesyConfirmation(studentId, courseId, step, setStep);
  const { form, period, preview, mutation } = flow;
  if (step === 5 && mutation.data) return <CourtesyResultCard grant={mutation.data} onRestart={onRestart} />;
  return <div hidden={step < 2}>
    <section hidden={step !== 2} className="catalog-record-card courtesy-lookup-card" aria-label="Passo Prazo">
      <div className="courtesy-step-heading"><h2>Vigência</h2><span>3 de 6</span></div><p>Curso escolhido: {courseTitle}</p>
      <fieldset><legend>Prazo de acesso</legend><label><input type="radio" name="period" checked={period.type === 'months'} onChange={() => form.setValue('accessPeriod', { type: 'months', months: 6 })} />Por período</label>
        <label><input type="radio" name="period" checked={period.type === 'lifetime'} onChange={() => form.setValue('accessPeriod', { type: 'lifetime' })} />Vitalícia</label></fieldset>
      {period.type === 'months' ? <><label htmlFor="courtesy-months">Meses</label><input id="courtesy-months" type="number" min="1" max="60" {...form.register('accessPeriod.months', { valueAsNumber: true })} /></> : null}
      {form.formState.errors.accessPeriod ? <p role="alert" className="field-error">Informe de 1 a 60 meses inteiros.</p> : null}
      <TermPreview lifetime={period.type === 'lifetime'} endsOn={preview.data?.endsOn} busy={preview.isFetching} error={preview.isError} onRetry={() => { void preview.refetch(); }} />
      <div className="courtesy-actions"><button type="button" className="secondary-button" onClick={() => setStep(1)}>Voltar</button><button type="button" className="primary-button" disabled={period.type === 'months' && (!preview.data || preview.isFetching || preview.isError)} onClick={async () => { if (await form.trigger('accessPeriod')) setStep(3); }}>Continuar</button></div>
    </section>
    <section hidden={step !== 3} className="catalog-record-card courtesy-lookup-card" aria-label="Passo Motivo">
      <div className="courtesy-step-heading"><h2>Motivo</h2><span>4 de 6</span></div><label htmlFor="courtesy-reason">Motivo da cortesia</label>
      <textarea id="courtesy-reason" rows={5} aria-invalid={Boolean(form.formState.errors.reason)} {...form.register('reason')} /><p>Obrigatório. Até 500 caracteres. Evite dados pessoais de terceiros.</p>
      <p>{form.watch('reason').length}/500 caracteres</p>{form.formState.errors.reason ? <p role="alert" className="field-error">{form.formState.errors.reason.message}</p> : null}
      <div className="courtesy-actions"><button type="button" className="secondary-button" onClick={() => setStep(2)}>Voltar</button><button type="button" className="primary-button" disabled={flow.reviewing} onClick={() => { void flow.review(); }}>{flow.reviewing ? 'Calculando término…' : 'Revisar'}</button></div>
    </section>
    <section hidden={step !== 4} className="catalog-record-card courtesy-lookup-card" aria-label="Passo Revisão">
      <div className="courtesy-step-heading"><h2>Revisão</h2><span>5 de 6</span></div><ReviewSentence student={studentEmail} course={courseTitle} reason={form.watch('reason')} lifetime={period.type === 'lifetime'} endsOn={preview.data?.endsOn} /><p>Aluno: {studentName} ({studentEmail})</p>
      <p>A cortesia será registrada na trilha de auditoria.</p><div className="courtesy-actions"><button type="button" className="secondary-button" disabled={mutation.isPending} onClick={() => setStep(3)}>Voltar</button><button type="button" className="primary-button" disabled={mutation.isPending || period.type === 'months' && (!preview.data || preview.isFetching || preview.isError)} onClick={() => { void flow.confirm(); }}>{mutation.isPending ? 'Concedendo…' : 'Confirmar cortesia'}</button></div>
    </section>
    {flow.error ? <p role="alert" className="inline-alert">{flow.error}</p> : null}
  </div>;
};
