import { useEffect, useRef, useState } from 'react';
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
export const FinanceOrderFilters = ({ filters, count, apply, lookup, courses }: FinanceOrderFiltersProps) => {
  const [open, setOpen] = useState(false);
  const dialog = useRef<HTMLDialogElement>(null);
  useEffect(() => { if (open) dialog.current?.showModal(); }, [open]);
  const fields = (mobile: boolean) => <ValidatedForm key={JSON.stringify(filters)} schema={formSchema} defaultValues={{ ...filters, email: '' }} onSubmit={async (input) => {
    if (input.email && !lookup.account) return false;
    apply({ status: input.status, courseId: input.courseId, studentId: lookup.account?.studentId ?? input.studentId, createdFrom: input.createdFrom, createdTo: input.createdTo });
    setOpen(false); return true;
  }}>{(form) => <>
    <div className="finance-filter-fields">
      <label>Situação<select {...form.register('status')}><option value="">Todas</option>{Object.entries(financeOrderStatuses).map(([value, item]) => <option key={value} value={value}>{item.label}</option>)}</select></label>
      <div><label>Buscar curso<input type="search" value={courses.search} onChange={(event) => courses.setSearch(event.target.value)} /></label>
        <label>Curso<select {...form.register('courseId')}><option value="">Todos os cursos</option>{filters.courseId && !courses.data.some((course) => course.courseId === filters.courseId) ? <option value={filters.courseId}>Curso selecionado</option> : null}{courses.data.map((course) => <option key={course.courseId} value={course.courseId}>{course.title}</option>)}</select></label>
        {courses.busy ? <p role="status">Carregando cursos…</p> : null}
        {courses.unavailable ? <p role="alert">Não foi possível carregar os cursos.</p> : null}
        {courses.totalPages > 1 ? <div className="finance-course-pages"><button type="button" disabled={courses.page === 1} onClick={() => courses.changePage(courses.page - 1)}>Cursos anteriores</button><span>{courses.page} / {courses.totalPages}</span><button type="button" disabled={courses.page >= courses.totalPages} onClick={() => courses.changePage(courses.page + 1)}>Mais cursos</button></div> : null}
      </div>
      <label>De<input type="date" {...form.register('createdFrom')} /></label>
      <label>Até<input type="date" aria-invalid={Boolean(form.formState.errors.createdTo)} {...form.register('createdTo')} />{form.formState.errors.createdTo ? <span role="alert" className="field-error">{form.formState.errors.createdTo.message}</span> : null}</label>
      <label className="finance-email">E-mail do aluno<input type="email" autoComplete="off" {...form.register('email', { onChange: () => { lookup.reset(); form.setValue('studentId', ''); } })} />{form.formState.errors.email ? <span role="alert" className="field-error">{form.formState.errors.email.message}</span> : null}</label>
      <button type="button" className="outline-button" disabled={lookup.busy} onClick={async () => {
        if (await form.trigger('email') && form.getValues('email')) await lookup.locate(form.getValues('email').trim().toLowerCase());
      }}>{lookup.busy ? 'Localizando…' : 'Localizar'}</button>
    </div>
    {lookup.error ? <p role="alert" className="inline-alert">{lookup.error}</p> : null}
    {lookup.account || form.watch('studentId') ? <p className="finance-student-chip">{lookup.account ? `${lookup.account.name} · ${lookup.account.email}` : 'Aluno localizado'} <button type="button" aria-label="Remover aluno" onClick={() => { lookup.reset(); form.setValue('studentId', ''); form.setValue('email', ''); }}>×</button></p> : null}
    <div className="finance-filter-actions"><button type="button" className="secondary-button" onClick={() => { lookup.reset(); apply({ status: '', courseId: '', studentId: '', createdFrom: '', createdTo: '' }); setOpen(false); }}>Limpar filtros</button><button type="submit" className="primary-button" disabled={lookup.busy || Boolean(form.watch('email') && !lookup.account)}>Filtrar</button>{mobile ? <button type="button" className="outline-button" onClick={() => setOpen(false)}>Fechar filtros</button> : null}</div>
  </>}</ValidatedForm>;
  return <>
    <button type="button" className="outline-button finance-mobile-filter-button" onClick={() => setOpen(true)}>Filtros ({count})</button>
    <section className="catalog-record-card finance-desktop-filters" aria-label="Filtros de pedidos">{fields(false)}</section>
    {open ? <dialog aria-labelledby="finance-filter-title" className="finance-filter-sheet" ref={dialog} onCancel={() => setOpen(false)} onClose={() => setOpen(false)}><h2 id="finance-filter-title">Filtros de pedidos</h2>{fields(true)}</dialog> : null}
  </>;
};
