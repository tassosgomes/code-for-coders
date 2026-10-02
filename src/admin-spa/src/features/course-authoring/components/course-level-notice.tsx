import { CircleAlert } from 'lucide-react';

import type { Course } from '@/features/course-authoring/types/course';
import { courseLevelLabel } from '@/features/course-authoring/utils/course-level-label';

type CourseLevelNoticeProps = { course: Course; canEdit: boolean; onChooseLevel?: () => void; publication?: boolean; onPublish?: () => void };
export const CourseLevelNotice = ({ course, canEdit, onChooseLevel, onPublish, publication = false }: CourseLevelNoticeProps) => {
  if (publication ? course.level !== null : course.currentLevel !== null) return null;
  return <div className="course-level-notice" role="status" aria-live="polite">
    <CircleAlert size={16} /><div><p>{!publication && course.level ? `O nível ${courseLevelLabel(course.level)} só vale depois de publicar. ${course.currentVersion ? 'A versão vigente está sem nível e, por isso, o curso ainda não pode entrar na vitrine.' : 'Este curso ainda não foi publicado.'}`
      : course.currentVersion ? 'Sem nível, este curso não pode entrar na vitrine. Você ainda pode publicar e corrigir aulas normalmente.'
        : 'Sem nível, este curso não poderá entrar na vitrine depois de publicado. Você ainda pode publicar e corrigir aulas normalmente.'}</p>
    {canEdit ? <button type="button" className="text-button" onClick={!publication && course.level && course.currentVersion && onPublish ? onPublish : onChooseLevel}>{!publication && course.level && course.currentVersion && onPublish ? 'Publicar nova versão' : 'Escolher nível'}</button> : <p>Peça a quem edita o curso para declarar o nível.</p>}
  </div></div>;
};
