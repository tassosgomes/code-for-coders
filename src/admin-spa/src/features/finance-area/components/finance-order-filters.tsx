import { useEffect, useRef, useState } from 'react';
import { CircleAlert, X } from 'lucide-react';
import { z } from 'zod';

import { ValidatedForm } from '@/components/ui/form/validated-form';
import { financeOrderFiltersSchema, type FinanceOrderFilters as FinanceFilters } from '@/features/finance-area/api/list-finance-orders';
import type { FinanceStudentLookup, FinanceCourseOptions } from '@/features/finance-area/types/finance-filter-support';
import { financeOrderStatuses } from '@/features/finance-area/utils/finance-order-format';

const formSchema = financeOrderFiltersSchema.and(z.object({ email: z.union([z.string().trim().toLowerCase().max(254).email('Informe um e-mail válido.'), z.literal('')]) }));
type FinanceOrderFiltersProps = {
  filters: FinanceFilters; count: number; apply: (filters: FinanceFilters) => void;
  lookup: FinanceStudentLookup; courses: FinanceCourseOptions;
};
const emptyFilters: FinanceFilters = { status: '', courseId: '', studentId: '', createdFrom: '', createdTo: '' };

/** FinanceFilterBar (F2.a): card no desktop e Sheet inferior no mobile, com os campos na mesma ordem. */
export const FinanceOrderFilters = ({ filters, count, apply, lookup, courses }: FinanceOrderFiltersProps) => {
  const [open, setOpen] = useState(false);
  const dialog = useRef<HTMLDialogElement>(null);
  useEffect(() => { if (open) dialog.current?.showModal(); }, [open]);
  const clear = () => { lookup.reset(); apply(emptyFilters); setOpen(false); };
  const fields = () => <ValidatedForm key={JSON.stringify(filters)} schema={formSchema} defaultValues={{ ...filters, email: '' }} onSubmit={async (input) => {
    if (input.email && !lookup.account) return false;
    apply({ status: input.status, courseId: input.courseId, studentId: lookup.account?.studentId ?? input.studentId, createdFrom: input.createdFrom, createdTo: input.createdTo });
    setOpen(false); return true;
  }}>{(form) => {
    const emailError = form.formState.errors.email?.message ?? (lookup.errorKind === 'not-found' ? lookup.error : null);
    const located = lookup.account ? `${lookup.account.name} · ${lookup.account.email}` : form.watch('studentId') ? 'Aluno localizado' : null;
    const removeStudent = () => { lookup.reset(); form.setValue('studentId', ''); form.setValue('email', ''); };
    const locate = async () => { if (await form.trigger('email') && form.getValues('email')) await lookup.locate(form.getValues('email').trim().toLowerCase()); };
    return <>
      <div className="finance-filter-fields">
        <label>Situação<select {...form.register('status')}><option value="">Todas</option>{Object.entries(financeOrderStatuses).map(([value, item]) => <option key={value} value={value}>{item.label}</option>)}</select></label>
        <label>Curso<select {...form.register('courseId')}><option value="">Todos os cursos</option>{filters.courseId && !courses.data.some((course) => course.courseId === filters.courseId) ? <option value={filters.courseId}>Curso selecionado</option> : null}{courses.data.map((course) => <option key={course.courseId} value={course.courseId}>{course.title}</option>)}</select>
          {courses.busy ? <span role="status" className="finance-field-help">Carregando cursos…</span> : null}
          {courses.unavailable ? <span role="alert" className="field-error">Não foi possível carregar os cursos.</span> : null}
        </label>
        <label>De<input type="date" {...form.register('createdFrom')} /></label>
        <label>Até<input type="date" aria-invalid={Boolean(form.formState.errors.createdTo)} {...form.register('createdTo')} />{form.formState.errors.createdTo ? <span role="alert" className="field-error">{form.formState.errors.createdTo.message}</span> : null}</label>
        {located ? <div className="finance-email-field"><span className="finance-field-label">E-mail do aluno</span>
          <p className="finance-student-chip">{located}<button type="button" aria-label="Remover aluno" onClick={removeStudent}><X aria-hidden="true" size={14} /></button></p>
        </div> : <div className="finance-email-row">
          <label className="finance-email-field">E-mail do aluno<input type="email" autoComplete="off" placeholder="aluno@example.com" aria-invalid={Boolean(emailError)}
            {...form.register('email', { onChange: () => { lookup.reset(); form.setValue('studentId', ''); } })} />
            {emailError ? <span role="alert" className="field-error">{emailError}</span> : null}</label>
          <button type="button" className="outline-button" disabled={lookup.busy} onClick={() => void locate()}>{lookup.busy ? 'Localizando…' : 'Localizar'}</button>
        </div>}
      </div>
      {lookup.errorKind === 'unavailable' ? <div role="alert" className="finance-alert"><CircleAlert aria-hidden="true" size={16} /><div><strong>{lookup.error}</strong>
        <button type="button" className="outline-button finance-alert-action" onClick={() => void locate()}>Tentar de novo</button></div></div> : null}
      <div className="finance-filter-actions">
        <button type="button" className="finance-link-button" onClick={clear}>Limpar filtros</button>
        <button type="submit" className="primary-button" disabled={lookup.busy || Boolean(form.watch('email') && !lookup.account)}>Filtrar</button>
      </div>
    </>;
  }}</ValidatedForm>;
  return <>
    <button type="button" className="outline-button finance-mobile-filter-button" onClick={() => setOpen(true)}>Filtros ({count})</button>
    <section className="finance-card finance-filter-card finance-desktop-filters" aria-label="Filtros de pedidos">{open ? null : fields()}</section>
    {open ? <dialog aria-labelledby="finance-filter-title" className="finance-filter-sheet" ref={dialog} onCancel={() => setOpen(false)} onClose={() => setOpen(false)}>
      <span aria-hidden="true" className="finance-sheet-handle" />
      <div className="finance-sheet-header"><h2 id="finance-filter-title">Filtros</h2>
        <button type="button" className="finance-icon-button" aria-label="Fechar filtros" onClick={() => setOpen(false)}><X aria-hidden="true" size={16} /></button></div>
      {fields()}
    </dialog> : null}
  </>;
};
