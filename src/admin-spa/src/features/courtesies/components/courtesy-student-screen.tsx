import { ValidatedForm } from '@/components/ui/form/validated-form';
import { studentAccountLookupSchema } from '@/features/courtesies/api/lookup-student-account';
import { StudentAccountCard } from '@/features/courtesies/components/student-account-card';
import { useCourtesyStudentLookup } from '@/features/courtesies/hooks/use-courtesy-student-lookup';

const steps = ['Aluno', 'Curso', 'Prazo', 'Motivo', 'Revisão', 'Resultado'];
export const CourtesyStudentScreen = () => {
  const lookup = useCourtesyStudentLookup();
  return <main className="page-shell courtesy-page">
    <header><p className="eyebrow">Cortesias</p><h1>Conceder cortesia</h1><p>Conceda acesso a um curso sem compra, com motivo.</p></header>
    <div className="courtesy-flow">
      <ol className="courtesy-stepper" aria-label="Passos da cortesia">{steps.map((step, index) => <li key={step} aria-current={index === 0 ? 'step' : undefined}><span>{index + 1}</span><span>{step}</span></li>)}</ol>
      <section className="catalog-record-card courtesy-lookup-card" aria-label="Passo Aluno">
        <div className="courtesy-step-heading"><h2>Aluno</h2><span>1 de 6</span></div>
        <p>Informe o e-mail da conta de aluno.</p>
        <ValidatedForm schema={studentAccountLookupSchema} defaultValues={{ email: '' }} onSubmit={lookup.submit}>
          {(form) => <>
            <label htmlFor="courtesy-student-email">E-mail do aluno</label>
            <input id="courtesy-student-email" type="email" placeholder="joana.ribeiro@example.com" autoComplete="off" disabled={lookup.busy} aria-invalid={Boolean(form.formState.errors.email)} aria-describedby={form.formState.errors.email ? 'courtesy-email-error' : undefined} {...form.register('email', { onChange: lookup.reset })} />
            {form.formState.errors.email ? <p id="courtesy-email-error" className="field-error" role="alert">{form.formState.errors.email.message}</p> : null}
            <div className="courtesy-actions"><button className="primary-button" disabled={lookup.busy || !form.formState.isValid} type="submit">{lookup.busy ? 'Localizando…' : 'Localizar'}</button></div>
          </>}
        </ValidatedForm>
        {lookup.busy ? <div className="courtesy-loading" role="status">Localizando aluno…</div> : null}
        {lookup.error ? <p className="inline-alert" role="alert">{lookup.error}</p> : null}
        {lookup.account ? <StudentAccountCard account={lookup.account} onChange={lookup.reset} /> : null}
      </section>
    </div>
  </main>;
};
