import { useState } from 'react';
import { Link, useLocation, useNavigate, useOutletContext, useSearchParams } from 'react-router';

import { paths } from '@/config/paths';
import { useCourses } from '@/features/course-authoring/api/get-courses';
import { CreateCourseDialog } from '@/features/course-authoring/components/create-course-dialog';
import { CourseStatusBadge } from '@/features/course-authoring/components/course-status-badge';
import { CourseDelete } from '@/features/course-authoring/components/course-delete';
import type { StaffSession } from '@/features/staff-session/api/staff-session';

export const AuthoringCoursesRoute = () => {
  const session = useOutletContext<StaffSession>();
  if (!session.permissions.includes('autoria.ler')) return <main className="page-shell"><h1>Esta área não é do seu papel</h1><p>Peça acesso a um administrador.</p></main>;
  return <AuthoringCoursesContent canEdit={session.permissions.includes('autoria.editar')} />;
};
const AuthoringCoursesContent = ({ canEdit }: { canEdit: boolean }) => {
  const [params, setParams] = useSearchParams();
  const page = Math.max(1, Number(params.get('page')) || 1);
  const rawStatus = params.get('status');
  const status = rawStatus === 'draft' || rawStatus === 'published' ? rawStatus : undefined;
  const courses = useCourses({ page, status });
  const [creating, setCreating] = useState(false);
  const navigate = useNavigate();
  const location = useLocation();
  const [notice, setNotice] = useState(location.state?.courseNotice === 'Curso excluído' ? 'Curso excluído' : '');
  const changePage = (nextPage: number, nextStatus = status) => setParams({ ...(nextPage > 1 ? { page: String(nextPage) } : {}), ...(nextStatus ? { status: nextStatus } : {}) });
  return <main className="page-shell authoring-page">
    <p className="eyebrow">Autoria</p>
    {notice ? <p role="status">{notice}</p> : null}
    <div className="course-page-heading"><div><h1>Cursos da escola</h1><p className="page-subtitle">Monte o currículo e publique quando estiver pronto.</p></div>
      {canEdit ? <button className="primary-button" onClick={() => setCreating(true)} type="button">+ Novo curso</button> : null}
    </div>
    {!canEdit ? <p className="inline-alert">Somente leitura. Você pode consultar os cursos da escola.</p> : null}
    <div className="course-filters" aria-label="Filtrar cursos">
      {([{ label: 'Todos', value: undefined }, { label: 'Rascunhos', value: 'draft' }, { label: 'Publicados', value: 'published' }] as const).map((filter) =>
        <button aria-pressed={status === filter.value} key={filter.label} onClick={() => setParams(filter.value ? { status: filter.value } : {})} type="button">{filter.label}</button>)}
    </div>
    {courses.isPending ? <p role="status">Carregando cursos…</p> : courses.isError ? <section className="empty-state"><h2>Não foi possível carregar os cursos</h2><button className="outline-button" onClick={() => void courses.refetch()} type="button">Tentar novamente</button></section>
      : courses.data.data.length === 0 ? <section className="empty-state"><h2>{status ? 'Nenhum curso neste filtro' : 'Sua escola ainda não tem cursos'}</h2><p>{status ? 'Escolha outro estado para encontrar cursos.' : 'Crie o primeiro rascunho para começar.'}</p></section>
        : <div className="course-table-wrap"><table className="course-table"><thead><tr><th>Curso</th><th>Estado</th><th>Última edição</th><th><span className="sr-only">Ações</span></th></tr></thead><tbody>
          {courses.data.data.map((course) => <tr key={course.courseId}><td><strong>{course.title}</strong>{course.status === 'published' && course.currentLevel === null ? <span className="course-level-indicator">Sem nível</span> : null}</td><td><CourseStatusBadge course={course} /></td>
            <td>{course.lastEditedBy.name} · <time dateTime={course.lastEditedAt}>{new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(course.lastEditedAt))}</time></td>
            <td><Link aria-label={`Abrir ${course.title}`} to={paths.authoringCourse.getHref(course.courseId)}>Abrir →</Link>
              {canEdit ? <CourseDelete course={course} presentation="menu" onReload={courses.refetch} onNotice={setNotice} onDeleted={() => setNotice('Curso excluído')} /> : null}</td></tr>)}
        </tbody></table></div>}
    {courses.data && courses.data.pagination.total > 0 ? <nav aria-label="Páginas de cursos" className="course-pagination">
      <button aria-label="Página anterior" className="outline-button" disabled={page === 1} onClick={() => changePage(page - 1)} type="button">‹</button>
      <span>Página {page} de {courses.data.pagination.totalPages} · 20 por página</span>
      <button aria-label="Próxima página" className="outline-button" disabled={page >= courses.data.pagination.totalPages} onClick={() => changePage(page + 1)} type="button">›</button>
    </nav> : null}
    {creating ? <CreateCourseDialog onClose={() => setCreating(false)} onCreated={(course) => void navigate(paths.authoringCourse.getHref(course.courseId))} /> : null}
  </main>;
};
