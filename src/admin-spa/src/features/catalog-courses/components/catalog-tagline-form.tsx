import { ValidatedForm } from '@/components/ui/form/validated-form';
import { Toast } from '@/components/toast';
import { updateCatalogCourseInputSchema } from '@/features/catalog-courses/api/update-catalog-course';
import type { CatalogCourseRecord } from '@/features/catalog-courses/api/get-catalog-course';
import { CatalogAlert } from '@/features/catalog-courses/components/catalog-alert';
import { useCatalogTagline } from '@/features/catalog-courses/hooks/use-catalog-tagline';

type CatalogTaglineFormProps = { course: CatalogCourseRecord };
export const CatalogTaglineForm = ({ course }: CatalogTaglineFormProps) => {
  const editing = useCatalogTagline(course.courseId);
  return <section aria-labelledby="catalog-tagline-title" className="catalog-card">
    <div className="catalog-card-header"><h2 id="catalog-tagline-title">Chamada comercial</h2></div>
    <ValidatedForm schema={updateCatalogCourseInputSchema} defaultValues={{ tagline: course.tagline }} onSubmit={editing.save}>
      {(form) => {
        const length = (form.watch('tagline') ?? '').length;
        const error = length > 160 ? `Use até 160 caracteres: tire ${length - 160}.` : form.formState.errors.tagline?.message ?? editing.fieldError;
        return <>
          <label className="visually-hidden" htmlFor="catalog-tagline">Chamada comercial (opcional)</label>
          <textarea id="catalog-tagline" disabled={editing.busy} aria-invalid={Boolean(error)} aria-describedby="tagline-guidance tagline-count tagline-error"
            {...form.register('tagline', { setValueAs: (value: string) => value === '' ? null : value })} />
          <div className="catalog-tagline-meta">
            <div>
              <p id="tagline-guidance">Descreva o que a pessoa vai aprender. Não prometa exclusividade de conteúdo nem proteção contra cópia.</p>
              {error ? <p id="tagline-error" className="catalog-tagline-error" role="alert">{error}</p> : null}
            </div>
            <small id="tagline-count" className={`catalog-tagline-count ${length > 160 ? 'is-over' : ''}`}>{length}/160</small>
          </div>
          {editing.error ? <CatalogAlert title={editing.error} /> : null}
          <div className="catalog-card-actions"><button className="primary-button" disabled={editing.busy || length > 160} type="submit">{editing.busy ? 'Salvando…' : 'Salvar chamada'}</button></div>
        </>;
      }}
    </ValidatedForm>
    {editing.notice ? <Toast message={editing.notice} /> : null}
  </section>;
};
