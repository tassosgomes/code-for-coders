import { Check, CircleAlert } from 'lucide-react';

import { CourtesyConfirmationSteps } from '@/features/courtesies/components/courtesy-confirmation-steps';
import { CourtesyCourseStep } from '@/features/courtesies/components/courtesy-course-step';
import { useCourtesyForm } from '@/features/courtesies/hooks/use-courtesy-form';

import { ValidatedForm } from '@/components/ui/form/validated-form';
import { studentAccountLookupSchema } from '@/features/courtesies/api/lookup-student-account';
import { StepSummary } from '@/features/courtesies/components/step-summary';
import { StudentAccountCard } from '@/features/courtesies/components/student-account-card';
import { StudentAccessGrants } from '@/features/courtesies/components/student-access-grants';
import { useCourtesyStudentLookup } from '@/features/courtesies/hooks/use-courtesy-student-lookup';

const steps = ['Aluno', 'Curso', 'Vigência', 'Motivo', 'Revisão', 'Resultado'];
export const CourtesyStudentScreen = () => {
  const lookup = useCourtesyStudentLookup();
  const flow = useCourtesyForm();
  const resetStudent = () => { lookup.reset(); flow.clearCourse(); };

  return <main className="page-shell courtesy-page">
    <header><p className="eyebrow">Cortesias</p><h1>Conceder cortesia</h1><p>Conceda acesso a um curso sem compra, com motivo.</p></header>
    <div className="courtesy-flow">
      {flow.step < 5 ? <ol className="courtesy-stepper" aria-label="Passos da cortesia">{steps.map((step, index) => <li key={step} className={index < flow.step ? 'is-complete' : undefined} aria-current={index === flow.step ? 'step' : undefined}><span>{index < flow.step ? <Check size={14} aria-hidden="true" /> : index + 1}</span><span>{step}</span></li>)}</ol> : null}
      {flow.step >= 1 && flow.step <= 3 && lookup.account ? <StepSummary label="Aluno" value={`${lookup.account.name} · ${lookup.account.email}`} onEdit={() => flow.setStep(0)} /> : null}
      <section hidden={flow.step !== 0} className="catalog-record-card courtesy-lookup-card" aria-label="Passo Aluno">
        <div className="courtesy-step-heading"><h2>Aluno</h2><span>1 de 6</span></div>
        <p className="courtesy-step-description">Informe o e-mail da conta de aluno.</p>
        <ValidatedForm schema={studentAccountLookupSchema} defaultValues={{ email: '' }} onSubmit={lookup.submit}>
          {(form) => <>
            <label htmlFor="courtesy-student-email">E-mail do aluno</label>
            <input id="courtesy-student-email" type="email" placeholder="joana.ribeiro@example.com" autoComplete="off" disabled={lookup.busy} aria-invalid={Boolean(form.formState.errors.email) || lookup.notFound} aria-describedby={form.formState.errors.email ? 'courtesy-email-error' : lookup.notFound ? 'courtesy-email-not-found' : undefined} {...form.register('email', { onChange: resetStudent })} />
            {form.formState.errors.email ? <p id="courtesy-email-error" className="field-error" role="alert">{form.formState.errors.email.message}</p> : null}
            {lookup.notFound ? <p id="courtesy-email-not-found" className="courtesy-field-error">Não há conta de aluno com este e-mail.<br />Confira o e-mail e tente de novo.</p> : null}
            <div className="courtesy-actions"><button className="primary-button" disabled={lookup.busy || !form.formState.isValid} type="submit">{lookup.busy ? 'Localizando…' : 'Localizar'}</button></div>
          </>}
        </ValidatedForm>
        {lookup.busy ? <div className="courtesy-skeleton-stack" role="status" aria-label="Localizando aluno"><span className="courtesy-skeleton courtesy-skeleton-card" />{[0, 1, 2, 3].map((item) => <span key={item} className="courtesy-skeleton courtesy-skeleton-row" />)}</div> : null}
        {lookup.error ? <div className="courtesy-error-box" role="alert"><CircleAlert size={16} aria-hidden="true" /><p>{lookup.error}</p>{lookup.retryable ? <button type="button" className="text-button" onClick={lookup.retry}>Tentar de novo</button> : null}</div> : null}
        {lookup.account ? <StudentAccountCard account={lookup.account} /> : null}
        {lookup.account?.status === 'active' ? <StudentAccessGrants key={lookup.account.studentId} studentId={lookup.account.studentId} enabled={flow.step === 0} /> : null}
        {lookup.account?.status === 'active' ? <div className="courtesy-actions"><button type="button" className="secondary-button" onClick={resetStudent}>Voltar</button><button type="button" className="primary-button" disabled={lookup.busy} onClick={() => flow.setStep(1)}>Continuar</button></div> : null}
      </section>
      {flow.step === 1 ? <CourtesyCourseStep selected={flow.course} onSelect={flow.selectCourse} onBack={() => flow.setStep(0)} onContinue={() => flow.setStep(2)} /> : null}
      {lookup.account && flow.course ? <CourtesyConfirmationSteps studentId={lookup.account.studentId} studentName={lookup.account.name} studentEmail={lookup.account.email} courseId={flow.course.courseId} courseTitle={flow.course.title} step={flow.step} setStep={flow.setStep} onViewGrants={() => flow.setStep(0)} onRestart={() => { resetStudent(); flow.setStep(0); }} /> : null}
    </div>
  </main>;
};
