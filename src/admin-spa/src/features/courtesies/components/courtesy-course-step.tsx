import { CircleAlert } from 'lucide-react';

import { ValidatedForm } from '@/components/ui/form/validated-form';
import { courtesyCourseSearchSchema, type CourtesyCourse } from '@/features/courtesies/api/list-courtesy-courses';
import { CoursePagination } from '@/features/courtesies/components/course-pagination';
import { CoursePickRow } from '@/features/courtesies/components/course-pick-row';
import { useCourtesyCourseStep } from '@/features/courtesies/hooks/use-courtesy-course-step';

type CourtesyCourseStepProps = { selected: Pick<CourtesyCourse, 'courseId'> | null; onSelect: (course: CourtesyCourse) => void; onBack: () => void; onContinue: () => void };
export const CourtesyCourseStep = ({ selected, onSelect, onBack, onContinue }: CourtesyCourseStepProps) => {
  const { query, input, search, showAll, setPage } = useCourtesyCourseStep();
  const page = query.data?.pagination;
  const total = page?.total ?? 0;
  return <section className="catalog-record-card courtesy-lookup-card" aria-label="Passo Curso">
    <div className="courtesy-step-heading"><h2>Curso</h2><span>2 de 6</span></div>
    <p className="courtesy-step-description">Escolha um curso publicado da escola. Cortesia não exige oferta.</p>
    <ValidatedForm key={input.title} schema={courtesyCourseSearchSchema} defaultValues={{ title: input.title }} onSubmit={search}>
      {(form) => <>
        <label htmlFor="courtesy-course-title">Buscar curso pelo título</label>
        <div className="courtesy-search-row">
          <input id="courtesy-course-title" placeholder="Digite parte do título" {...form.register('title')} aria-invalid={Boolean(form.formState.errors.title)} />
          <button className="secondary-button" type="submit" disabled={!form.formState.isValid || query.isFetching}>Buscar</button>
        </div>
        {form.formState.errors.title ? <p className="field-error" role="alert">{form.formState.errors.title.message}</p> : null}
      </>}
    </ValidatedForm>
    {query.isPending ? <div role="status" aria-label="Carregando cursos" className="courtesy-course-loading">{Array.from({ length: 5 }, (_, index) => <div className="courtesy-skeleton-row" key={index} />)}</div>
      : query.isError ? <div className="courtesy-callout courtesy-callout-danger" role="alert"><CircleAlert size={16} aria-hidden="true" /><p>Não foi possível carregar os cursos agora.</p><button type="button" className="text-button" onClick={() => { void query.refetch(); }}>Tentar de novo</button></div>
        : query.data.data.length ? <>
          {input.title ? <p role="status" className="courtesy-hint">{total === 1 ? '1 curso com este título' : `${total} cursos com este título`}</p> : null}
          <div className="courtesy-course-table">
            <div className="courtesy-course-head" aria-hidden="true"><span>Curso</span><span>Oferta</span><span /></div>
            <ul className="courtesy-course-list" aria-label="Cursos publicados">{query.data.data.map((course) => <CoursePickRow key={course.courseId} course={course} selected={course.courseId === selected?.courseId} onSelect={onSelect} />)}</ul>
            <div className="courtesy-course-footer"><span className="courtesy-hint">Só cursos com versão vigente.</span>
              <CoursePagination page={input.page} totalPages={page?.totalPages ?? 0} disabled={query.isFetching} onPage={setPage} /></div>
          </div>
        </> : <div role="status" className="courtesy-empty-box"><p>Nenhum curso publicado com este título.</p><button type="button" className="secondary-button" onClick={showAll}>Ver todos</button></div>}
    <div className="courtesy-actions"><button type="button" className="secondary-button" onClick={onBack}>Voltar</button><button type="button" className="primary-button" disabled={!selected || query.isFetching || query.isError} onClick={onContinue}>Continuar</button></div>
  </section>;
};
