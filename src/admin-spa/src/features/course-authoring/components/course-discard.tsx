import { useState } from 'react';
import axios from 'axios';

import { Dialog } from '@/components/ui/dialog';
import { useDiscardCourseDraft } from '@/features/course-authoring/api/discard-course-draft';
import type { Course } from '@/features/course-authoring/types/course';

type CourseDiscardProps = { course: Course; onReload: () => Promise<unknown> };
export const CourseDiscard = ({ course, onReload }: CourseDiscardProps) => {
  const discard = useDiscardCourseDraft();
  const [confirmation, setConfirmation] = useState<{ revision: number; key: string } | null>(null);
  const [changed, setChanged] = useState(false);
  const [error, setError] = useState<string>();
  const submit = async () => {
    if (!confirmation) return;
    setError(undefined);
    try { await discard.mutateAsync({ courseId: course.courseId, input: { draftRevision: confirmation.revision }, idempotencyKey: confirmation.key }); setConfirmation(null); }
    catch (failure) {
      if (axios.isAxiosError<{ code?: string }>(failure) && failure.response?.data.code === 'DRAFT_CHANGED') {
        setChanged(true); await onReload(); setError('O rascunho mudou. Revise o curso e confirme novamente.');
      } else setError('Não foi possível descartar. Tente novamente.');
    }
  };
  if (!course.currentVersion || (!course.hasUnpublishedChanges && !confirmation)) return null;
  return <>
    <button type="button" className="outline-button" onClick={() => { setConfirmation({ revision: course.draftRevision, key: crypto.randomUUID() }); setChanged(false); setError(undefined); }}>Descartar alterações</button>
    {confirmation ? <Dialog title="Descartar alterações não publicadas?" description={`O rascunho voltará a ser igual à versão ${course.currentVersion}. Alterações não publicadas serão perdidas.`} busy={discard.isPending} onClose={() => setConfirmation(null)}>
      <p>Revisão {confirmation.revision}</p>{error ? <p role="alert" className="inline-alert">{error}</p> : null}
      <div className="dialog-actions"><button autoFocus type="button" className="outline-button" disabled={discard.isPending} onClick={() => setConfirmation(null)}>{changed ? 'Voltar ao rascunho' : 'Cancelar'}</button>
        {!changed ? <button type="button" className="primary-button" disabled={discard.isPending} onClick={() => void submit()}>{discard.isPending ? 'Descartando…' : 'Descartar alterações'}</button> : null}
      </div>
    </Dialog> : null}
  </>;
};
