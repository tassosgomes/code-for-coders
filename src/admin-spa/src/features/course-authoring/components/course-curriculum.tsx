import { useRef, useState, type ReactNode } from 'react';
import { BookOpen, GripVertical } from 'lucide-react';

import { Dialog } from '@/components/ui/dialog';
import { CoursePublication } from '@/features/course-authoring/components/course-publication';
import { useCourse } from '@/features/course-authoring/api/get-course';
import type { PublicationPendency } from '@/features/course-authoring/utils/publication-pendencies';
import { CourseVideoPicker } from '@/features/course-authoring/components/course-video-picker';
import { CourseEditDialog } from '@/features/course-authoring/components/course-edit-dialog';
import { CourseItemActions } from '@/features/course-authoring/components/course-item-actions';
import { useCourseStructure } from '@/features/course-authoring/hooks/use-course-structure';
import type { Course, CourseLesson, CourseModule } from '@/features/course-authoring/types/course';

type CourseCurriculumProps = { course: Course; canEdit: boolean; canChooseVideo: boolean; children: ReactNode };
type DraggedItem = { kind: 'module' | 'lesson'; id: string };
export const CourseCurriculum = ({ course, canEdit, canChooseVideo, children }: CourseCurriculumProps) => {
  const editor = useCourseStructure(course);
  const query = useCourse(course.courseId);
  const focusPendency = (pendency: PublicationPendency) => {
    if (pendency.moduleId) setCollapsed((current) => { const next = new Set(current); next.delete(pendency.moduleId!); return next; });
    const id = pendency.lessonId ? `aula-${pendency.lessonId}` : pendency.moduleId ? `modulo-${pendency.moduleId}` : 'course-modules';
    requestAnimationFrame(() => { document.getElementById(id)?.focus(); window.history.replaceState(null, '', `#${id}`); });
  };
  const [collapsed, setCollapsed] = useState(new Set<string>());
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
    <div className="course-editor-overview"><div>{children}</div>{canEdit ? <button className="primary-button" type="button" disabled={editor.busy} onClick={() => editor.openEdit({ kind: 'course' })}>Editar dados</button> : null}</div>
    {canEdit ? <CoursePublication course={course} onFocusPendency={focusPendency} onReload={query.refetch} /> : null}
    <div className="course-filters"><span className="course-draft-tab">Rascunho</span></div>
    <div id="course-modules" tabIndex={-1} className="course-curriculum-heading"><h2>Módulos</h2>{canEdit ? <button className="outline-button" type="button" disabled={editor.busy} onClick={() => editor.openEdit({ kind: 'create-module' })}>+ Adicionar módulo</button> : null}</div>
    <p role="status" aria-live="polite">{editor.notice}</p>
    {editor.error && !editor.editTarget && !editor.removeTarget && !editor.videoTarget ? <p className="inline-alert" role="alert">{editor.error}</p> : null}
    {!course.modules.length ? <section className="empty-state course-curriculum"><BookOpen size={32} /><h3>Comece pelos módulos.</h3><p>Cada módulo terá ao menos uma aula com vídeo antes da publicação.</p></section> : null}
    <div className="course-module-list">
      {course.modules.map((module) => <section key={module.moduleId} id={`modulo-${module.moduleId}`} aria-label={module.title} tabIndex={-1} ref={itemRef(`modulo-${module.moduleId}`)} className="course-module" onDragOver={(event) => { if (canEdit) event.preventDefault(); }} onDrop={(event) => drop(event, module)}>
        <div className="course-module-heading">
          {canEdit ? <button type="button" className="course-drag-handle" aria-label={`Arrastar módulo ${module.title}`} draggable={!editor.busy} disabled={editor.busy} onDragStart={(event) => startDrag(event, { kind: 'module', id: module.moduleId })} onDragEnd={() => { dragged.current = null; }}><GripVertical size={18} /></button> : null}
          <h3>{module.position}. {module.title}</h3><span>{module.lessons.length} aulas</span>
          {canEdit ? <><button className="outline-button" type="button" disabled={editor.busy} onClick={() => editor.openEdit({ kind: 'create-lesson', module })} aria-label={`+ Aula em ${module.title}`}>+ Aula</button>
            <button type="button" className="course-collapse-button" aria-label={`${collapsed.has(module.moduleId) ? 'Expandir' : 'Recolher'} ${module.title}`} aria-expanded={!collapsed.has(module.moduleId)} aria-controls={`conteudo-${module.moduleId}`} onClick={() => setCollapsed((current) => { const next = new Set(current); if (!next.delete(module.moduleId)) next.add(module.moduleId); return next; })}>{collapsed.has(module.moduleId) ? '⌄' : '⌃'}</button>
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
            <div className="course-lesson-details"><h4>{lesson.position}. {lesson.title}</h4>{lesson.description ? <p>{lesson.description}</p> : null}<span className="course-video-state">{lesson.video ? (lesson.video.title ? `Vídeo: ${lesson.video.title}${lesson.video.durationSeconds ? ` · ${Math.floor(lesson.video.durationSeconds / 60)}:${String(lesson.video.durationSeconds % 60).padStart(2, '0')}` : ''}` : `Vídeo vinculado · ID ${lesson.video.videoId}`) : 'Sem vídeo'}</span>{lesson.video && !lesson.video.title ? <p className="inline-alert">Os detalhes deste vídeo não estão disponíveis agora.</p> : null}</div>
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
    {editor.videoTarget ? <CourseVideoPicker lesson={editor.videoTarget} busy={editor.busy} error={editor.error} onClose={editor.closeVideo} onSave={(videoId) => editor.editLesson(editor.videoTarget!.lessonId, { videoId })} /> : null}
    {editor.editTarget ? <CourseEditDialog target={editor.editTarget} course={course} busy={editor.busy} error={editor.error} onClose={editor.closeEdit} onSubmit={editor.saveDetails} /> : null}
    {editor.removeTarget ? <Dialog title={`Remover “${editor.removeTarget.kind === 'module' ? editor.removeTarget.module.title : editor.removeTarget.lesson.title}”?`} description={editor.removeTarget.kind === 'module' ? `As ${editor.removeTarget.module.lessons.length} aulas deste módulo também saem do rascunho. Uma versão já publicada continua como estava.` : 'Ela sairá do rascunho. Uma aula nova terá outra identidade, mesmo com o mesmo título.'} busy={editor.busy} onClose={editor.closeRemove}>
      {editor.error ? <p role="alert" className="inline-alert">{editor.error}</p> : null}
      <div className="dialog-actions"><button autoFocus type="button" className="outline-button" disabled={editor.busy} onClick={editor.closeRemove}>Cancelar</button><button type="button" className="primary-button" disabled={editor.busy} onClick={() => void editor.remove()}>Remover {editor.removeTarget.kind === 'module' ? 'módulo' : 'aula'}</button></div>
    </Dialog> : null}
  </>;
};
