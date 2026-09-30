import { useState } from 'react';

import { Dialog } from '@/components/ui/dialog';
import { useCourses } from '@/features/course-authoring/api/get-courses';
import type { RecommendedCourse } from '@/features/course-authoring/types/course';

type CourseRecommendationPickerProps = { courseId: string; selectedIds: string[]; busy: boolean; onAdd: (course: RecommendedCourse) => void; onClose: () => void };
export const CourseRecommendationPicker = ({ courseId, selectedIds, busy, onAdd, onClose }: CourseRecommendationPickerProps) => {
  const [title, setTitle] = useState(''); const [page, setPage] = useState(1); const [notice, setNotice] = useState('');
  const validTitle = title.length >= 2 && title.length <= 100;
  const courses = useCourses({ status: 'published', title, page }, validTitle);
  const candidates = courses.data?.data.filter((course) => course.status === 'published' && course.courseId !== courseId && !selectedIds.includes(course.courseId)) ?? [];
  return <Dialog className="course-video-sheet" title="Cursos recomendados" description="Só cursos publicados da sua escola. Você pode recomendar até 5." busy={busy} onClose={onClose}>
    <p>{selectedIds.length} de 5</p>
    {selectedIds.length === 5 ? <p role="status">Você já escolheu 5 cursos. Remova um para adicionar outro.</p> : null}
    <label className="course-video-search">Buscar por título<input autoFocus type="search" maxLength={100} value={title} onChange={(event) => { setTitle(event.target.value); setPage(1); }} /></label>
    {!validTitle ? <p>Digite ao menos 2 letras para buscar.</p> : courses.isPending ? <p role="status">Carregando cursos…</p> : null}
    {validTitle && courses.isError ? <div role="alert"><p>Não foi possível carregar os cursos.</p><button type="button" disabled={courses.isFetching} onClick={() => void courses.refetch()}>Tentar de novo</button></div> : null}
    {validTitle && courses.isSuccess && !candidates.length ? <><p>Nenhum curso publicado com esse título.</p><button type="button" onClick={() => { setTitle(''); setPage(1); }}>Limpar busca</button></> : null}
    <ul className="course-recommendation-results">{validTitle && !courses.isError ? candidates.map((course) => <li key={course.courseId}>
      <div><strong>{course.title}</strong><small>Publicado · v{course.currentVersion}</small></div>
      <button type="button" className="outline-button" disabled={busy || selectedIds.length >= 5} aria-label={`Adicionar ${course.title}`} onClick={() => {
        onAdd({ courseId: course.courseId, title: course.title }); setNotice(`${course.title} adicionado à lista`);
        requestAnimationFrame(() => document.querySelector<HTMLElement>('.course-recommendation-results button:not(:disabled)')?.focus());
      }}>Adicionar</button>
    </li>) : null}</ul>
    <p role="status" aria-live="polite">{notice}</p>
    {validTitle && courses.isSuccess ? <nav aria-label="Páginas de cursos recomendados" className="course-video-pagination">
      <button type="button" disabled={page === 1 || courses.isFetching} onClick={() => setPage(page - 1)}>Anterior</button><span>Página {page} · 20 por página</span>
      <button type="button" disabled={page >= courses.data.pagination.totalPages || courses.isFetching} onClick={() => setPage(page + 1)}>Próxima</button>
    </nav> : null}
    <div className="dialog-actions"><button type="button" className="primary-button" onClick={onClose}>Concluir</button></div>
  </Dialog>;
};
