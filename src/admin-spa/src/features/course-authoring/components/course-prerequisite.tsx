import { useRef, useState } from 'react';

import { ValidatedForm } from '@/components/ui/form/validated-form';
import { coursePrerequisiteInputSchema } from '@/features/course-authoring/api/update-course';
import { CourseRecommendationPicker } from '@/features/course-authoring/components/course-recommendation-picker';
import { useCoursePrerequisite } from '@/features/course-authoring/hooks/use-course-prerequisite';
import type { Course, RecommendedCourse } from '@/features/course-authoring/types/course';

type CoursePrerequisiteProps = { course: Course; canEdit: boolean };
export const CoursePrerequisite = ({ course, canEdit }: CoursePrerequisiteProps) => {
  const editor = useCoursePrerequisite(course.courseId);
  const [picker, setPicker] = useState(false);
  const titles = useRef(new Map<string, string>());
  const defaultValues = { prerequisiteText: course.prerequisite.text, recommendedCourseIds: course.prerequisite.recommendedCourses.map((item) => item.courseId) };
  if (!canEdit) return <><h3>O que a pessoa deveria saber antes</h3><p>{course.prerequisite.text ?? 'Sem pré-requisito'}</p>
    <h3>Cursos recomendados</h3><ol>{course.prerequisite.recommendedCourses.map((item) => <li key={item.courseId}>{item.title}</li>)}</ol>
    {!course.prerequisite.recommendedCourses.length ? <p>Nenhum curso recomendado.</p> : null}</>;
  return <ValidatedForm key={`${course.courseId}:${JSON.stringify(defaultValues)}`} schema={coursePrerequisiteInputSchema} defaultValues={defaultValues} onSubmit={editor.save}>{(form) => {
    const { prerequisiteText, recommendedCourseIds } = form.watch();
    const setIds = (ids: string[]) => { form.setValue('recommendedCourseIds', ids, { shouldDirty: true, shouldValidate: true }); editor.clear(); };
    const add = (item: RecommendedCourse) => { titles.current.set(item.courseId, item.title); setIds([...recommendedCourseIds, item.courseId]); };
    const move = (index: number, offset: number) => {
      const ids = [...recommendedCourseIds]; const id = ids[index]; if (!id) return;
      ids.splice(index, 1); ids.splice(index + offset, 0, id); setIds(ids);
      requestAnimationFrame(() => document.getElementById(`recommended-${id}`)?.focus());
    };
    const textError = form.formState.errors.prerequisiteText?.message ?? (editor.error?.field === 'prerequisiteText' ? editor.error.message : undefined);
    return <>
      <label htmlFor="prerequisite-text">O que a pessoa deveria saber antes (opcional)</label>
      <textarea id="prerequisite-text" rows={4} maxLength={1000} disabled={editor.busy} aria-invalid={Boolean(textError)} aria-describedby="prerequisite-help prerequisite-count prerequisite-error" {...form.register('prerequisiteText', { setValueAs: (value: unknown) => value === '' ? null : value })} />
      <small id="prerequisite-help">É uma recomendação; não impede a compra nem o acesso.</small><small id="prerequisite-count">{prerequisiteText?.length ?? 0}/1000</small>
      <p id="prerequisite-error" role={textError ? 'alert' : undefined}>{textError}</p>
      <h3 id="recommended-heading">Cursos recomendados (até 5)</h3>
      {!recommendedCourseIds.length ? <p>Nenhum curso recomendado.</p> : null}
      <ol className="course-recommended-list" aria-labelledby="recommended-heading">{recommendedCourseIds.map((id, index) => {
        const title = course.prerequisite.recommendedCourses.find((item) => item.courseId === id)?.title ?? titles.current.get(id) ?? '';
        return <li key={id} id={`recommended-${id}`} tabIndex={-1}><span>{index + 1}. {title}</span>
          {editor.error?.field === 'recommendedCourseIds' && editor.error.index === index ? <span>Este curso não está mais disponível para recomendação.</span> : null}
          <div><button type="button" aria-label={`Mover ${title} para cima`} disabled={editor.busy || index === 0} onClick={() => move(index, -1)}>↑</button>
            <button type="button" aria-label={`Mover ${title} para baixo`} disabled={editor.busy || index === recommendedCourseIds.length - 1} onClick={() => move(index, 1)}>↓</button>
            <button type="button" aria-label={`Remover ${title}`} disabled={editor.busy} onClick={() => setIds(recommendedCourseIds.filter((item) => item !== id))}>Remover</button></div>
        </li>;
      })}</ol>
      {editor.error?.field === 'recommendedCourseIds' ? <p role="alert">{editor.error.message}</p> : null}
      <div><button className="outline-button" type="button" disabled={editor.busy || recommendedCourseIds.length >= 5} onClick={() => setPicker(true)}>+ Adicionar curso</button> <span>{recommendedCourseIds.length} de 5</span></div>
      {recommendedCourseIds.length >= 5 ? <p role="status">Você já escolheu 5 cursos. Remova um para adicionar outro.</p> : null}
      {editor.error?.field === 'service' ? <div role="alert"><p>{editor.error.message}</p><button type="button" disabled={editor.busy} onClick={() => void form.handleSubmit(editor.save)()}>Tentar de novo</button></div> : null}
      {form.formState.isDirty ? <><p>Alterações não salvas no pré-requisito.</p><div className="dialog-actions">
        <button className="outline-button" type="button" disabled={editor.busy} onClick={() => { form.reset(defaultValues); editor.clear(); }}>Desfazer</button>
        <button className="primary-button" type="submit" disabled={editor.busy || !form.formState.isValid}>{editor.busy ? 'Salvando…' : 'Salvar pré-requisito'}</button>
      </div></> : null}
      <p role="status" aria-live="polite">{editor.notice}</p>
      {picker ? <CourseRecommendationPicker courseId={course.courseId} selectedIds={recommendedCourseIds} busy={editor.busy} onAdd={add} onClose={() => setPicker(false)} /> : null}
    </>;
  }}</ValidatedForm>;
};
