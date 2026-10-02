import { useState } from 'react';
import { CircleAlert, Mail } from 'lucide-react';
import { Link, useLocation, useNavigate, useOutletContext, useSearchParams } from 'react-router';

import { paths } from '@/config/paths';
import { useCourses } from '@/features/course-authoring/api/get-courses';
import { CreateCourseDialog } from '@/features/course-authoring/components/create-course-dialog';
import { CourseStatusBadge } from '@/features/course-authoring/components/course-status-badge';
import { CoursePagination } from '@/features/course-authoring/components/course-pagination';
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
    {notice ? <p role="status">{notice}</p> : null}
    <header className="course-page-heading"><div><p className="eyebrow">Autoria</p><h1>Cursos da escola</h1><p className="page-subtitle">Monte o currículo e publique quando estiver pronto.</p></div>
      {canEdit ? <button className="primary-button" onClick={() => setCreating(true)} type="button">+ Novo curso</button> : null}
    </header>
    {!canEdit ? <p className="inline-alert">Somente leitura. Você pode consultar os cursos da escola.</p> : null}
    <div className="course-filters" aria-label="Filtrar cursos">
      {([{ label: 'Todos', value: undefined }, { label: 'Rascunhos', value: 'draft' }, { label: 'Publicados', value: 'published' }] as const).map((filter) =>
        <button aria-pressed={status === filter.value} key={filter.label} onClick={() => setParams(filter.value ? { status: filter.value } : {})} type="button">{filter.label}</button>)}
    </div>
    {courses.isPending ? <p role="status">Carregando cursos…</p> : courses.isError ? <><section className="course-load-error" role="alert"><CircleAlert size={16} /><h2>Não foi possível carregar os cursos</h2></section><button className="outline-button" onClick={() => void courses.refetch()} type="button">Tentar novamente</button></>
      : courses.data.data.length === 0 ? <><section className="empty-state"><span className="course-status-tile"><Mail size={24} /></span><h2>{status === 'published' ? 'Nenhum curso publicado ainda.' : status === 'draft' ? 'Nenhum rascunho ainda.' : 'Nenhum curso ainda.'}</h2><p>{status === 'published' ? 'Os cursos publicados aparecerão aqui.' : status === 'draft' ? 'Os rascunhos aparecerão aqui.' : 'Crie o primeiro e organize suas aulas.'}</p>{!status && canEdit ? <button type="button" className="primary-button" onClick={() => setCreating(true)}>+ Novo curso</button> : null}</section>{status ? <button type="button" className="text-button" onClick={() => setParams({})}>Ver todos os cursos</button> : null}</>
        : <div className="course-table-wrap"><table className="course-table"><thead><tr><th>Curso</th><th>Estado</th><th>Última edição</th><th><span className="sr-only">Ações</span></th></tr></thead><tbody>
          {courses.data.data.map((course) => <tr key={course.courseId}><td><strong>{course.title}</strong>{course.status === 'published' && course.currentLevel === null ? <span className="course-level-indicator"><CircleAlert size={14} /><span className="role-badge">Sem nível</span></span> : null}</td><td><CourseStatusBadge course={course} /></td>
            <td>{course.lastEditedBy.name} · <time dateTime={course.lastEditedAt}>{new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(course.lastEditedAt))}</time></td>
            <td><div className="course-table-actions"><Link aria-label={`Abrir ${course.title}`} to={paths.authoringCourse.getHref(course.courseId)}>Abrir →</Link>
              {canEdit ? <CourseDelete course={course} presentation="menu" onReload={courses.refetch} onNotice={setNotice} onDeleted={() => setNotice('Curso excluído')} /> : null}</div></td></tr>)}
        </tbody></table></div>}
    {courses.data && courses.data.pagination.total > 0 ? <CoursePagination label="Páginas de cursos" page={page} totalPages={courses.data.pagination.totalPages} onChange={changePage} /> : null}
    {creating ? <CreateCourseDialog onClose={() => setCreating(false)} onCreated={(course) => void navigate(paths.authoringCourse.getHref(course.courseId))} /> : null}
  </main>;
};
