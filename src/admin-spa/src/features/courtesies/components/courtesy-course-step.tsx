import { ValidatedForm } from '@/components/ui/form/validated-form';
import { courtesyCourseSearchSchema, type CourtesyCourse } from '@/features/courtesies/api/list-courtesy-courses';
import { CoursePickRow } from '@/features/courtesies/components/course-pick-row';
import { useCourtesyCourseStep } from '@/features/courtesies/hooks/use-courtesy-course-step';

type CourtesyCourseStepProps = { selected: CourtesyCourse | null; onSelect: (course: CourtesyCourse) => void; onBack: () => void; onContinue: () => void };
export const CourtesyCourseStep = ({ selected, onSelect, onBack, onContinue }: CourtesyCourseStepProps) => {
  const { query, input, search, showAll, setPage } = useCourtesyCourseStep();
  const page = query.data?.pagination;
  return <section className="catalog-record-card courtesy-lookup-card" aria-label="Passo Curso">
    <div className="courtesy-step-heading"><h2>Curso</h2><span>2 de 6</span></div>
    <p>Escolha um curso publicado da escola. Cortesia não exige oferta.</p>
    <ValidatedForm key={input.title} schema={courtesyCourseSearchSchema} defaultValues={{ title: input.title }} onSubmit={search}>
      {(form) => <>
        <label htmlFor="courtesy-course-title">Buscar curso pelo título</label>
        <input id="courtesy-course-title" placeholder="Buscar pelo título" {...form.register('title')} aria-invalid={Boolean(form.formState.errors.title)} />
        {form.formState.errors.title ? <p className="field-error" role="alert">{form.formState.errors.title.message}</p> : null}
        <div className="courtesy-actions"><button className="secondary-button" type="submit" disabled={!form.formState.isValid || query.isFetching}>Buscar</button></div>
      </>}
    </ValidatedForm>
    {query.isPending ? <div role="status" aria-label="Carregando cursos" className="courtesy-course-loading">{Array.from({ length: 5 }, (_, index) => <div className="courtesy-loading" key={index}>Carregando…</div>)}</div>
      : query.isError ? <div className="inline-alert" role="alert"><p>Não foi possível carregar os cursos agora.</p><button type="button" className="secondary-button" onClick={() => { void query.refetch(); }}>Tentar de novo</button></div>
        : <>
          {query.data.data.length ? <>
            {input.title ? <p role="status">{page?.total} cursos com este título</p> : null}
            <ul className="courtesy-course-list" aria-label="Cursos publicados">{query.data.data.map((course) => <CoursePickRow key={course.courseId} course={course} selected={course.courseId === selected?.courseId} onSelect={onSelect} />)}</ul>
            <p>Só cursos com versão vigente.</p>
            <nav className="courtesy-pagination" aria-label="Páginas de cursos"><button type="button" className="secondary-button" disabled={input.page === 1 || query.isFetching} onClick={() => setPage(input.page - 1)}>Anterior</button><span>Página {page?.page} de {page?.totalPages}</span><button type="button" className="secondary-button" disabled={input.page >= (page?.totalPages ?? 0) || query.isFetching} onClick={() => setPage(input.page + 1)}>Próxima</button></nav>
          </> : <div role="status"><p>Nenhum curso publicado com este título.</p><button type="button" className="secondary-button" onClick={showAll}>Ver todos</button></div>}
        </>}
    <div className="courtesy-actions"><button type="button" className="secondary-button" onClick={onBack}>Voltar</button><button type="button" className="primary-button" disabled={!selected || query.isFetching || query.isError} onClick={onContinue}>Continuar</button></div>
  </section>;
};
