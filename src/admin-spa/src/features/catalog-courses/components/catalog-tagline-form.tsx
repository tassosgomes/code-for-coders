import { ValidatedForm } from '@/components/ui/form/validated-form';
import { updateCatalogCourseInputSchema } from '@/features/catalog-courses/api/update-catalog-course';
import type { CatalogCourseRecord } from '@/features/catalog-courses/api/get-catalog-course';
import { useCatalogTagline } from '@/features/catalog-courses/hooks/use-catalog-tagline';

type CatalogTaglineFormProps = { course: CatalogCourseRecord };
export const CatalogTaglineForm = ({ course }: CatalogTaglineFormProps) => {
  const editing = useCatalogTagline(course.courseId);
  return <section className="catalog-record-card"><h2>Chamada comercial</h2>
    <ValidatedForm schema={updateCatalogCourseInputSchema} defaultValues={{ tagline: course.tagline }} onSubmit={editing.save}>
      {(form) => {
        const length = (form.watch('tagline') ?? '').length;
        const error = length > 160 ? `Use até 160 caracteres: tire ${length - 160}.` : form.formState.errors.tagline?.message ?? editing.fieldError;
        return <>
          <label htmlFor="catalog-tagline">Chamada comercial (opcional)</label>
          <textarea id="catalog-tagline" rows={3} disabled={editing.busy} aria-invalid={Boolean(error)} aria-describedby="tagline-guidance tagline-count tagline-error"
            {...form.register('tagline', { setValueAs: (value: string) => value === '' ? null : value })} />
          <p id="tagline-guidance">Descreva o que a pessoa vai aprender. Não prometa exclusividade de conteúdo nem proteção contra cópia.</p>
          <small id="tagline-count" className={length > 160 ? 'field-error' : ''}>{length}/160</small>
          {error ? <p id="tagline-error" className="field-error" role="alert">{error}</p> : null}
          {editing.error ? <p className="inline-alert" role="alert">{editing.error}</p> : null}
          {editing.notice ? <p role="status">{editing.notice}</p> : null}
          <button className="primary-button" disabled={editing.busy || length > 160} type="submit">{editing.busy ? 'Salvando…' : 'Salvar chamada'}</button>
        </>;
      }}
    </ValidatedForm>
  </section>;
};
