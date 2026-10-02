import { useRef, useState, type ReactNode } from 'react';
import { BookOpen, ChevronDown, ChevronUp, GripVertical } from 'lucide-react';

import { Dialog } from '@/components/ui/dialog';
import { CourseHistory } from '@/features/course-authoring/components/course-history';
import { CourseDiscard } from '@/features/course-authoring/components/course-discard';
import { Link } from 'react-router';
import { paths } from '@/config/paths';
import { CoursePublication } from '@/features/course-authoring/components/course-publication';
import { CourseAudience } from '@/features/course-authoring/components/course-audience';
import { CourseLevelNotice } from '@/features/course-authoring/components/course-level-notice';
import { useCourse } from '@/features/course-authoring/api/get-course';
import type { PublicationPendency } from '@/features/course-authoring/utils/publication-pendencies';
import { CourseVideoPicker } from '@/features/course-authoring/components/course-video-picker';
import { CourseEditDialog } from '@/features/course-authoring/components/course-edit-dialog';
import { CourseItemActions } from '@/features/course-authoring/components/course-item-actions';
import { useCourseStructure } from '@/features/course-authoring/hooks/use-course-structure';
import type { Course, CourseLesson, CourseModule } from '@/features/course-authoring/types/course';

type CourseCurriculumProps = { course: Course; canEdit: boolean; canChooseVideo: boolean; children: ReactNode; actions?: ReactNode };
type DraggedItem = { kind: 'module' | 'lesson'; id: string };
export const CourseCurriculum = ({ course, canEdit, canChooseVideo, children, actions }: CourseCurriculumProps) => {
  const [tab, setTab] = useState<'draft' | 'history'>('draft');
  const editor = useCourseStructure(course);
  const query = useCourse(course.courseId);
  const chooseLevel = () => {
    setTab('draft');
    requestAnimationFrame(() => document.querySelector<HTMLInputElement>('#level-options input:checked')?.focus());
  };
  const focusPendency = (pendency: PublicationPendency) => {
    if (pendency.moduleId) setCollapsed((current) => { const next = new Set(current); next.delete(pendency.moduleId!); return next; });
    const id = pendency.lessonId ? `aula-${pendency.lessonId}` : pendency.moduleId ? `modulo-${pendency.moduleId}` : 'course-modules';
    requestAnimationFrame(() => { document.getElementById(id)?.focus(); window.history.replaceState(null, '', `#${id}`); });
  };
  const [collapsed, setCollapsed] = useState(new Set<string>());
  const publicationButton = useRef<HTMLButtonElement>(null);
  const dragged = useRef<DraggedItem | null>(null);
  const moveLesson = (lessonId: string, input: { moduleId?: string; position?: number }) => {
    if (input.moduleId) setCollapsed((current) => { const next = new Set(current); next.delete(input.moduleId!); return next; });
    return editor.editLesson(lessonId, input);
  };
  const startDrag = (event: React.DragEvent, item: DraggedItem) => {
    dragged.current = item; event.dataTransfer.effectAllowed = 'move'; event.dataTransfer.setData('text/plain', item.id);
  };
  const drop = (event: React.DragEvent, module: CourseModule, lesson?: CourseLesson) => {
    event.preventDefault(); event.stopPropagation();
    const item = dragged.current; dragged.current = null;
    if (!item || !canEdit || editor.busy) return;
    if (item.kind === 'module') void editor.editModule(item.id, { position: module.position });
    else {
      const source = course.modules.find((parent) => parent.lessons.some((child) => child.lessonId === item.id));
      const position = lesson?.position ?? module.lessons.length + (source?.moduleId === module.moduleId ? 0 : 1);
      void moveLesson(item.id, { moduleId: module.moduleId, position });
    }
  };
  const itemRef = (id: string) => (element: HTMLElement | null) => {
    if (element) editor.elements.current.set(id, element); else editor.elements.current.delete(id);
  };
  const moveButton = (label: string, disabled: boolean, action: () => void) => <button type="button" disabled={disabled || editor.busy} title={disabled ? 'Item já está no limite da lista.' : undefined} onClick={action}>{label}</button>;
  return <>
    <div className="course-editor-overview" hidden={tab !== 'draft'}><div>{children}</div>{canEdit ? <button className="primary-button" type="button" disabled={editor.busy} onClick={() => editor.openEdit({ kind: 'course' })}>Editar dados</button> : null}</div>
    {tab === 'history' ? <header className="course-history-heading"><p className="eyebrow">Autoria</p><h1>{course.title}</h1><p className="page-subtitle">Versões publicadas deste curso.</p></header> : null}
    <div className="course-draft-audience" hidden={tab !== 'draft'}>
      <CourseLevelNotice course={course} canEdit={canEdit} onChooseLevel={chooseLevel} onPublish={() => publicationButton.current?.click()} />
      <CourseAudience course={course} canEdit={canEdit} />
    </div>
    <div className="course-draft-navigation">
    <div className="course-filters" role="tablist" aria-label="Conteúdo do curso"><button type="button" role="tab" aria-selected={tab === 'draft'} aria-controls="course-draft-panel" id="course-draft-tab" tabIndex={tab === 'draft' ? 0 : -1} onKeyDown={(event) => { if (event.key === 'ArrowRight' || event.key === 'ArrowLeft' || event.key === 'End') { event.preventDefault(); setTab('history'); document.getElementById('course-history-tab')?.focus(); } }} onClick={() => setTab('draft')}>Rascunho</button><button type="button" role="tab" aria-selected={tab === 'history'} aria-controls="course-history-panel" id="course-history-tab" tabIndex={tab === 'history' ? 0 : -1} onKeyDown={(event) => { if (event.key === 'ArrowLeft' || event.key === 'ArrowRight' || event.key === 'Home') { event.preventDefault(); setTab('draft'); document.getElementById('course-draft-tab')?.focus(); } }} onClick={() => setTab('history')}>Histórico</button></div>
    <div hidden={tab !== 'draft'} className="course-publication-actions">{canEdit ? <CoursePublication triggerRef={publicationButton} course={course} onFocusPendency={focusPendency} onChooseLevel={chooseLevel} onReload={query.refetch} /> : null}</div>
    </div>
    {tab === 'history' ? <div id="course-history-panel" role="tabpanel" aria-labelledby="course-history-tab"><CourseHistory courseId={course.courseId} /></div> : null}
    <div id="course-draft-panel" role="tabpanel" aria-labelledby="course-draft-tab" hidden={tab !== 'draft'}>
    <div id="course-modules" tabIndex={-1} className="course-curriculum-heading"><h2>Módulos</h2>{canEdit ? <button className="outline-button" type="button" disabled={editor.busy} onClick={() => editor.openEdit({ kind: 'create-module' })}>+ Adicionar módulo</button> : null}</div>
    <p role="status" aria-live="polite">{editor.notice}</p>
    {editor.error && !editor.editTarget && !editor.removeTarget && !editor.videoTarget ? <p className="inline-alert" role="alert">{editor.error}</p> : null}
    {!course.modules.length ? <section className="empty-state course-curriculum"><BookOpen size={32} /><h3>Comece pelos módulos.</h3><p>Cada módulo terá ao menos uma aula com vídeo antes da publicação.</p></section> : null}
    <div className="course-module-list">
      {course.modules.map((module) => <section key={module.moduleId} id={`modulo-${module.moduleId}`} aria-label={module.title} tabIndex={-1} ref={itemRef(`modulo-${module.moduleId}`)} className="course-module" onDragOver={(event) => { if (canEdit) event.preventDefault(); }} onDrop={(event) => drop(event, module)}>
        <div className="course-module-heading">
          {canEdit ? <button type="button" className="course-drag-handle" aria-label={`Arrastar módulo ${module.title}`} draggable={!editor.busy} disabled={editor.busy} onDragStart={(event) => startDrag(event, { kind: 'module', id: module.moduleId })} onDragEnd={() => { dragged.current = null; }}><GripVertical size={18} /></button> : null}
          <div className="course-module-summary"><h3>{module.position}. {module.title}</h3><span>{module.lessons.length} aulas</span></div>
          {canEdit ? <><button className="outline-button" type="button" disabled={editor.busy} onClick={() => editor.openEdit({ kind: 'create-lesson', module })} aria-label={`+ Aula em ${module.title}`}>+ Aula</button>
            <button type="button" className="course-collapse-button" aria-label={`${collapsed.has(module.moduleId) ? 'Expandir' : 'Recolher'} ${module.title}`} aria-expanded={!collapsed.has(module.moduleId)} aria-controls={`conteudo-${module.moduleId}`} onClick={() => setCollapsed((current) => { const next = new Set(current); if (!next.delete(module.moduleId)) next.add(module.moduleId); return next; })}>{collapsed.has(module.moduleId) ? <ChevronDown size={16} /> : <ChevronUp size={16} />}</button>
            <CourseItemActions title={module.title} disabled={editor.busy}>
              <button type="button" disabled={editor.busy} onClick={() => editor.openEdit({ kind: 'module', module })}>Editar título</button>
              {moveButton('Mover para cima', module.position === 1, () => void editor.editModule(module.moduleId, { position: module.position - 1 }))}
              {moveButton('Mover para baixo', module.position === course.modules.length, () => void editor.editModule(module.moduleId, { position: module.position + 1 }))}
              <button type="button" disabled={editor.busy} onClick={() => editor.openRemove({ kind: 'module', module })}>Remover módulo…</button>
            </CourseItemActions></> : null}
        </div>
        <div id={`conteudo-${module.moduleId}`} className="course-module-body" hidden={collapsed.has(module.moduleId)}>
          {!module.lessons.length ? <p className="course-empty-lessons">Este módulo ainda não tem aulas.</p> : null}
          {module.lessons.map((lesson) => <article key={lesson.lessonId} id={`aula-${lesson.lessonId}`} aria-label={lesson.title} tabIndex={-1} ref={itemRef(`aula-${lesson.lessonId}`)} className="course-lesson" onDragOver={(event) => { if (canEdit) event.preventDefault(); }} onDrop={(event) => drop(event, module, lesson)}>
            {canEdit ? <button type="button" className="course-drag-handle" aria-label={`Arrastar aula ${lesson.title}`} draggable={!editor.busy} disabled={editor.busy} onDragStart={(event) => { event.stopPropagation(); startDrag(event, { kind: 'lesson', id: lesson.lessonId }); }} onDragEnd={() => { dragged.current = null; }}><GripVertical size={18} /></button> : null}
            <div className="course-lesson-details"><h4>{lesson.position}. {lesson.title}</h4><span className={`course-video-state ${lesson.video ? 'is-linked' : 'is-missing'}`}>{lesson.video ? (lesson.video.title ? `Vídeo: ${lesson.video.title}${lesson.video.durationSeconds ? ` · ${Math.floor(lesson.video.durationSeconds / 60)}:${String(lesson.video.durationSeconds % 60).padStart(2, '0')}` : ''}` : `Vídeo vinculado · ID ${lesson.video.videoId}`) : 'Sem vídeo'}</span>{lesson.description ? <p>{lesson.description}</p> : null}{lesson.video && !lesson.video.title ? <p className="inline-alert">Os detalhes deste vídeo não estão disponíveis agora.</p> : null}</div>
            {canEdit ? <button className="outline-button" type="button" disabled={editor.busy || !canChooseVideo} title={canChooseVideo ? undefined : 'Seu papel precisa de acesso à área Vídeos.'} onClick={() => editor.openVideo(lesson)}>{lesson.video ? 'Trocar vídeo' : 'Escolher vídeo'}</button> : null}
            {canEdit ? <CourseItemActions title={lesson.title} disabled={editor.busy}>
              <button type="button" disabled={editor.busy} onClick={() => editor.openEdit({ kind: 'lesson', lesson })}>Editar aula</button>
              {moveButton('Mover para cima', lesson.position === 1, () => void moveLesson(lesson.lessonId, { position: lesson.position - 1 }))}
              {moveButton('Mover para baixo', lesson.position === module.lessons.length, () => void moveLesson(lesson.lessonId, { position: lesson.position + 1 }))}
              <span>Mover para outro módulo</span>
              {course.modules.filter((parent) => parent.moduleId !== module.moduleId).map((parent) => <button key={parent.moduleId} type="button" disabled={editor.busy} onClick={() => void moveLesson(lesson.lessonId, { moduleId: parent.moduleId })}>Mover para {parent.title}</button>)}
              {course.modules.length === 1 ? <button type="button" disabled title="Adicione outro módulo para mover a aula.">Mover para outro módulo</button> : null}
              <button type="button" disabled={editor.busy} onClick={() => editor.openRemove({ kind: 'lesson', lesson })}>Remover aula…</button>
            </CourseItemActions> : null}
          </article>)}
        </div>
      </section>)}
    </div>
    <footer className="course-draft-footer">
      {canEdit ? <CourseDiscard course={course} onReload={query.refetch} /> : null}
      {course.currentVersion ? <Link className="text-button" to={paths.authoringVersion.getHref(course.courseId, course.currentVersion)}>Ver versão vigente</Link> : null}
      {actions}
    </footer>
    </div>
    {editor.videoTarget ? <CourseVideoPicker lesson={editor.videoTarget} busy={editor.busy} error={editor.error} onClose={editor.closeVideo} onSave={(videoId) => editor.editLesson(editor.videoTarget!.lessonId, { videoId })} /> : null}
    {editor.editTarget ? <CourseEditDialog target={editor.editTarget} course={course} busy={editor.busy} error={editor.error} onClose={editor.closeEdit} onSubmit={editor.saveDetails} /> : null}
    {editor.removeTarget ? <Dialog title={`Remover “${editor.removeTarget.kind === 'module' ? editor.removeTarget.module.title : editor.removeTarget.lesson.title}”?`} description={editor.removeTarget.kind === 'module' ? `As ${editor.removeTarget.module.lessons.length} aulas deste módulo também saem do rascunho. Uma versão já publicada continua como estava.` : 'Ela sairá do rascunho. Uma aula nova terá outra identidade, mesmo com o mesmo título.'} busy={editor.busy} onClose={editor.closeRemove}>
      {editor.error ? <p role="alert" className="inline-alert">{editor.error}</p> : null}
      <div className="dialog-actions"><button autoFocus type="button" className="outline-button" disabled={editor.busy} onClick={editor.closeRemove}>Cancelar</button><button type="button" className="primary-button" disabled={editor.busy} onClick={() => void editor.remove()}>Remover {editor.removeTarget.kind === 'module' ? 'módulo' : 'aula'}</button></div>
    </Dialog> : null}
  </>;
};
