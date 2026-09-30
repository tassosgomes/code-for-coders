import axios from 'axios';
import { BookOpen } from 'lucide-react';
import { Link, useOutletContext, useParams } from 'react-router';

import { paths } from '@/config/paths';
import { useCourse } from '@/features/course-authoring/api/get-course';
import { CourseStatusBadge } from '@/features/course-authoring/components/course-status-badge';
import type { StaffSession } from '@/features/staff-session/api/staff-session';

export const AuthoringCourseRoute = () => {
  const session = useOutletContext<StaffSession>();
  const { courseId } = useParams();
  if (!session.permissions.includes('autoria.ler')) return <main className="page-shell"><h1>Esta área não é do seu papel</h1></main>;
  if (!courseId) return null;
  return <AuthoringCourseContent courseId={courseId} canEdit={session.permissions.includes('autoria.editar')} />;
};
const AuthoringCourseContent = ({ courseId, canEdit }: { courseId: string; canEdit: boolean }) => {
  const course = useCourse(courseId);
  if (course.isPending) return <main className="page-shell"><p role="status">Carregando curso…</p></main>;
  if (course.isError) return <main className="page-shell"><h1>{axios.isAxiosError(course.error) && course.error.response?.status === 404 ? 'Curso não encontrado' : 'Não foi possível carregar o curso'}</h1><Link to={paths.authoring.getHref()}>Voltar aos cursos</Link><button className="outline-button" onClick={() => void course.refetch()} type="button">Tentar novamente</button></main>;
  return <main className="page-shell authoring-page">
    <nav aria-label="Caminho" className="course-breadcrumb"><Link to={paths.authoring.getHref()}>Autoria · Cursos</Link><span>› {course.data.title}</span></nav>
    <p className="eyebrow">Autoria</p><h1>{course.data.title}</h1>
    {course.data.description ? <p className="page-subtitle">{course.data.description}</p> : null}
    <CourseStatusBadge course={course.data} />
    <p className="course-edit-metadata">Criado por {course.data.createdBy.name} · Editado por {course.data.lastEditedBy.name} em <time dateTime={course.data.lastEditedAt}>{new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(course.data.lastEditedAt))}</time></p>
    {!canEdit ? <p className="inline-alert">Somente leitura. Você pode consultar este curso.</p> : null}
    <div className="course-filters"><span className="course-draft-tab">Rascunho</span></div>
    <section className="empty-state course-curriculum"><BookOpen size={32} /><h2>Comece pelos módulos.</h2><p>Cada módulo terá ao menos uma aula com vídeo antes da publicação.</p></section>
  </main>;
};
