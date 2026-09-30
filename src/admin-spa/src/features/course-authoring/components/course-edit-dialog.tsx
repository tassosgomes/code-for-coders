import { Dialog } from '@/components/ui/dialog';
import { TextDetailsForm } from '@/components/ui/form/text-details-form';
import { createLessonInputSchema } from '@/features/course-authoring/api/create-lesson';
import { createModuleInputSchema } from '@/features/course-authoring/api/create-module';
import { updateCourseInputSchema } from '@/features/course-authoring/api/update-course';
import type { Course } from '@/features/course-authoring/types/course';
import type { CourseEditTarget } from '@/features/course-authoring/types/course-edit-target';

type CourseEditDialogProps = {
  target: CourseEditTarget; course: Course; busy: boolean; error?: string;
  onClose: () => void; onSubmit: (input: { title: string; description: string }) => Promise<void>;
};
export const CourseEditDialog = ({ target, course, busy, error, onClose, onSubmit }: CourseEditDialogProps) => {
  const title = target.kind === 'course' ? 'Editar dados do curso' : target.kind === 'create-module' ? 'Novo módulo' : target.kind === 'module' ? 'Editar módulo' : target.kind === 'create-lesson' ? 'Nova aula' : 'Editar aula';
  const defaults = target.kind === 'course' ? course : target.kind === 'module' ? target.module : target.kind === 'lesson' ? target.lesson : { title: '', description: '' };
  const showDescription = target.kind !== 'module' && target.kind !== 'create-module';
  const schema = target.kind === 'course' ? updateCourseInputSchema : showDescription ? createLessonInputSchema : createModuleInputSchema;
  const formSchema = schema.extend({ description: createLessonInputSchema.shape.description.unwrap() });
  return <Dialog title={title} description={target.kind === 'create-lesson' ? `Módulo: ${target.module.title}` : 'As alterações são salvas no rascunho.'} busy={busy} onClose={onClose}>
    <TextDetailsForm schema={formSchema} defaultValues={{ title: defaults.title, description: 'description' in defaults ? defaults.description ?? '' : '' }} showDescription={showDescription} submitLabel={target.kind.startsWith('create-') ? (showDescription ? 'Criar aula' : 'Criar módulo') : 'Salvar alterações'} busy={busy} error={error} onSubmit={onSubmit} onCancel={onClose} />
  </Dialog>;
};
