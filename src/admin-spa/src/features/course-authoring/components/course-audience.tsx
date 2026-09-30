import { AutoSaveChoiceForm } from '@/components/ui/form/auto-save-choice-form';
import { updateCourseInputSchema } from '@/features/course-authoring/api/update-course';
import { useCourseLevel } from '@/features/course-authoring/hooks/use-course-level';
import type { Course } from '@/features/course-authoring/types/course';
import { courseLevelLabel } from '@/features/course-authoring/utils/course-level-label';

type CourseAudienceProps = { course: Course; canEdit: boolean };
export const CourseAudience = ({ course, canEdit }: CourseAudienceProps) => {
  const level = useCourseLevel(course.courseId);
  return <section className="course-audience" aria-labelledby="course-audience-heading">
    <h2 id="course-audience-heading">Para quem é este curso</h2>
    {canEdit ? <AutoSaveChoiceForm schema={updateCourseInputSchema} fieldName="level" value={course.level}
      legend="Nível do curso" help="Uma recomendação para quem está escolhendo o curso."
      options={[{ value: 'beginner', label: 'Iniciante' }, { value: 'intermediate', label: 'Intermediário' }, { value: 'advanced', label: 'Avançado' }, { value: null, label: 'Sem nível' }]}
      busy={level.busy} error={level.error} invalid={level.invalid} onSubmit={level.save} />
      : <><h3>Nível do curso</h3><p>{courseLevelLabel(course.level)}</p></>}
    <p role="status" aria-live="polite">{level.notice}</p>
  </section>;
};
