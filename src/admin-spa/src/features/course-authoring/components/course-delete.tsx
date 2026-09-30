import { useState } from 'react';
import axios from 'axios';

import { Dialog } from '@/components/ui/dialog';
import { useDeleteCourse } from '@/features/course-authoring/api/delete-course';
import type { CourseSummary } from '@/features/course-authoring/types/course';

type CourseDeleteProps = { course: CourseSummary; onDeleted: () => void; onReload: () => Promise<unknown>; onNotice: (message: string) => void; presentation?: 'button' | 'menu' };
export const CourseDelete = ({ course, onDeleted, onReload, onNotice, presentation = 'button' }: CourseDeleteProps) => {
  const deletion = useDeleteCourse();
  const [key, setKey] = useState<string>();
  const [error, setError] = useState<string>();
  const [menuOpen, setMenuOpen] = useState(false);
  const confirm = () => { setMenuOpen(false); setKey(crypto.randomUUID()); setError(undefined); };
  const submit = async () => {
    if (!key) return;
    setError(undefined);
    try { await deletion.mutateAsync({ courseId: course.courseId, idempotencyKey: key }); setKey(undefined); onDeleted(); }
    catch (failure) {
      if (axios.isAxiosError<{ code?: string }>(failure) && failure.response?.data.code === 'COURSE_ALREADY_PUBLISHED') {
        setKey(undefined); onNotice('Este curso já foi publicado e não pode ser excluído.'); await onReload();
      } else setError('Não foi possível excluir o curso. Tente novamente.');
    }
  };
  if (course.status !== 'draft' || course.currentVersion !== null) return null;
  return <>
    {presentation === 'menu' ? <div className="course-item-actions" onBlur={(event) => { if (!event.currentTarget.contains(event.relatedTarget)) setMenuOpen(false); }} onKeyDown={(event) => { if (event.key === 'Escape') setMenuOpen(false); }}>
      <button type="button" className="course-action-trigger" aria-label={`Ações de ${course.title}`} aria-expanded={menuOpen} onClick={() => setMenuOpen(!menuOpen)}>⋯</button>
      {menuOpen ? <div className="course-action-menu"><button type="button" onClick={confirm}>Excluir curso</button></div> : null}
    </div> : <button type="button" className="outline-button" onClick={confirm}>Excluir curso</button>}
    {key ? <Dialog role="alertdialog" className="course-delete-dialog" title={`Excluir “${course.title}”?`} description="Este curso nunca foi publicado. O rascunho, seus módulos e suas aulas serão removidos. Esta ação não pode ser desfeita." busy={deletion.isPending} onClose={() => setKey(undefined)}>
      {error ? <p role="alert" className="inline-alert">{error}</p> : null}
      <div className="dialog-actions"><button autoFocus type="button" className="outline-button" disabled={deletion.isPending} onClick={() => setKey(undefined)}>Cancelar</button>
        <button type="button" className="primary-button course-delete-confirm" disabled={deletion.isPending} onClick={() => void submit()}>{deletion.isPending ? 'Excluindo…' : 'Excluir curso'}</button>
      </div>
    </Dialog> : null}
  </>;
};
