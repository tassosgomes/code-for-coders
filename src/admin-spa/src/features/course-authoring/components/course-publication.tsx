import { useState } from 'react';
import axios from 'axios';
import { z } from 'zod';

import { Dialog } from '@/components/ui/dialog';
import { RevisionNoteForm } from '@/components/ui/form/revision-note-form';
import { CourseLevelNotice } from '@/features/course-authoring/components/course-level-notice';
import { courseLevelLabel } from '@/features/course-authoring/utils/course-level-label';
import { publishCourseInputSchema, usePublishCourse, type PublishCourseInput } from '@/features/course-authoring/api/publish-course';
import type { Course } from '@/features/course-authoring/types/course';
import { publicationPendencies, publicationPendencySchema, pendencyLabel, type PublicationPendency } from '@/features/course-authoring/utils/publication-pendencies';

type CoursePublicationProps = { course: Course; onFocusPendency: (pendency: PublicationPendency) => void; onChooseLevel: () => void; onReload: () => Promise<unknown> };
export const CoursePublication = ({ course, onFocusPendency, onChooseLevel, onReload }: CoursePublicationProps) => {
  const publish = usePublishCourse();
  const [confirmation, setConfirmation] = useState<Course | null>(null);
  const [pendencies, setPendencies] = useState<PublicationPendency[]>([]);
  const [intent, setIntent] = useState<{ body: string; key: string } | null>(null);
  const [error, setError] = useState<string>();
  const [changed, setChanged] = useState(false);
  const open = () => { setConfirmation(course); setPendencies(publicationPendencies(course)); setError(undefined); setChanged(false); setIntent(null); };
  const submit = async (input: PublishCourseInput) => {
    const body = JSON.stringify(input); const key = intent?.body === body ? intent.key : crypto.randomUUID();
    setIntent({ body, key }); setError(undefined);
    try { await publish.mutateAsync({ courseId: course.courseId, input, idempotencyKey: key }); setConfirmation(null); }
    catch (failure) {
      const problem = axios.isAxiosError<{ code?: string; pendencies?: unknown }>(failure) ? failure.response?.data : undefined;
      if (problem?.code === 'COURSE_INCOMPLETE') {
        const parsed = z.array(publicationPendencySchema).safeParse(problem.pendencies);
        if (parsed.success) setPendencies(parsed.data);
        setError('O curso ainda tem pendências. Revise os itens abaixo.');
      } else if (problem?.code === 'DRAFT_CHANGED') {
        setChanged(true); setIntent(null); await onReload(); setError('O rascunho mudou. Recarregue e confira antes de publicar.');
      } else setError('Não foi possível publicar. Sua nota foi mantida. Tente novamente.');
    }
  };
  const reload = async () => { await onReload(); setConfirmation(null); };
  return <>
    {!course.currentVersion || course.hasUnpublishedChanges ? <button type="button" className="primary-button" onClick={open}>{course.currentVersion ? 'Publicar nova versão' : 'Publicar'}</button> : null}
    {publish.data ? <p className="inline-alert" role="status">Versão {publish.data.versionNumber} publicada por {publish.data.publishedBy.name} em <time dateTime={publish.data.publishedAt}>{new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(publish.data.publishedAt))}</time>. A propagação para outros serviços pode levar alguns instantes.</p> : null}
    {confirmation ? <Dialog title={pendencies.length ? 'Antes de publicar' : `Publicar versão ${(confirmation.currentVersion ?? 0) + 1}`} description="Confira o currículo. Esta versão será um retrato imutável do curso." busy={publish.isPending} onClose={() => setConfirmation(null)}>
      <p>{confirmation.title} · {confirmation.modules.length} módulos · {confirmation.modules.reduce((count, module) => count + module.lessons.length, 0)} aulas · Revisão {confirmation.draftRevision}</p>
      <p>Nível: {courseLevelLabel(confirmation.level)}</p>
      <CourseLevelNotice course={confirmation} canEdit publication onChooseLevel={() => { setConfirmation(null); onChooseLevel(); }} />
      {error ? <p role="alert" className="inline-alert">{error}</p> : null}
      {pendencies.length ? <><p role="alert">Resolva estas pendências antes de publicar.</p><ul>{pendencies.map((pendency, index) => <li key={`${pendency.code}-${index}`}><button type="button" className="text-button" onClick={() => { setConfirmation(null); onFocusPendency(pendency); }}>{pendencyLabel(pendency, confirmation)}</button></li>)}</ul><button type="button" className="outline-button" onClick={() => setConfirmation(null)}>Voltar ao rascunho</button></>
        : changed ? <button type="button" className="primary-button" onClick={() => void reload()}>Recarregar rascunho</button>
          : <RevisionNoteForm schema={publishCourseInputSchema} revision={confirmation.draftRevision} versionNumber={(confirmation.currentVersion ?? 0) + 1} busy={publish.isPending} onSubmit={submit} onCancel={() => setConfirmation(null)} />}
    </Dialog> : null}
  </>;
};
