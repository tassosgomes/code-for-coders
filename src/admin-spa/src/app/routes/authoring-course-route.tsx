import axios from 'axios';
import { useState } from 'react';
import { Link, useNavigate, useOutletContext, useParams } from 'react-router';

import { paths } from '@/config/paths';
import { useCourse } from '@/features/course-authoring/api/get-course';
import { CourseCurriculum } from '@/features/course-authoring/components/course-curriculum';
import { CourseStatusBadge } from '@/features/course-authoring/components/course-status-badge';
import { CourseDelete } from '@/features/course-authoring/components/course-delete';
import type { StaffSession } from '@/features/staff-session/api/staff-session';

export const AuthoringCourseRoute = () => {
  const session = useOutletContext<StaffSession>();
  const { courseId } = useParams();
  if (!session.permissions.includes('autoria.ler')) return <main className="page-shell"><h1>Esta área não é do seu papel</h1></main>;
  if (!courseId) return null;
  return <AuthoringCourseContent courseId={courseId} canEdit={session.permissions.includes('autoria.editar')} canChooseVideo={session.permissions.includes('midia.enviar')} />;
};
const AuthoringCourseContent = ({ courseId, canEdit, canChooseVideo }: { courseId: string; canEdit: boolean; canChooseVideo: boolean }) => {
  const course = useCourse(courseId);
  const navigate = useNavigate();
  const [notice, setNotice] = useState('');
  if (course.isPending) return <main className="page-shell"><p role="status">Carregando curso…</p></main>;
  if (course.isError) return <main className="page-shell"><h1>{axios.isAxiosError(course.error) && course.error.response?.status === 404 ? 'Curso não encontrado' : 'Não foi possível carregar o curso'}</h1><Link to={paths.authoring.getHref()}>Voltar aos cursos</Link><button className="outline-button" onClick={() => void course.refetch()} type="button">Tentar novamente</button></main>;
  return <main className="page-shell authoring-page">
    <nav aria-label="Caminho" className="course-breadcrumb"><Link to={paths.authoring.getHref()}>Autoria · Cursos</Link><span>› {course.data.title}</span></nav>
    {notice ? <p role="status">{notice}</p> : null}
    {canEdit ? <CourseDelete course={course.data} onReload={course.refetch} onNotice={setNotice} onDeleted={() => void navigate(paths.authoring.getHref(), { state: { courseNotice: 'Curso excluído' } })} /> : null}
    <CourseCurriculum course={course.data} canEdit={canEdit} canChooseVideo={canChooseVideo}>
    <p className="eyebrow">Autoria</p><h1>{course.data.title}</h1>
    {course.data.description ? <p className="page-subtitle">{course.data.description}</p> : null}
    <CourseStatusBadge course={course.data} />
    <p className="course-edit-metadata">Criado por {course.data.createdBy.name} · Editado por {course.data.lastEditedBy.name} em <time dateTime={course.data.lastEditedAt}>{new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(course.data.lastEditedAt))}</time></p>
    {!canEdit ? <p className="inline-alert">Somente leitura. Você pode consultar este curso.</p> : null}
    </CourseCurriculum>
  </main>;
};
