import axios from 'axios';
import { Link, useOutletContext, useParams } from 'react-router';

import { paths } from '@/config/paths';
import { courseLevelLabel } from '@/features/course-authoring/utils/course-level-label';
import { useCourseVersion } from '@/features/course-authoring/api/get-course-version';
import type { StaffSession } from '@/features/staff-session/api/staff-session';

export const AuthoringVersionRoute = () => {
  const session = useOutletContext<StaffSession>();
  const { courseId, versionNumber } = useParams();
  if (!session.permissions.includes('autoria.ler')) return <main className="page-shell"><h1>Esta área não é do seu papel</h1></main>;
  if (!courseId || !versionNumber || !Number.isSafeInteger(Number(versionNumber)) || Number(versionNumber) < 1) return <main className="page-shell"><h1>Versão não encontrada</h1></main>;
  return <AuthoringVersionContent courseId={courseId} versionNumber={Number(versionNumber)} />;
};
const AuthoringVersionContent = ({ courseId, versionNumber }: { courseId: string; versionNumber: number }) => {
  const query = useCourseVersion(courseId, versionNumber);
  if (query.isPending) return <main className="page-shell"><p role="status">Carregando versão…</p></main>;
  if (query.isError) return <main className="page-shell"><h1>{axios.isAxiosError(query.error) && query.error.response?.status === 404 ? 'Versão não encontrada' : 'Não foi possível carregar a versão'}</h1><Link to={paths.authoringCourse.getHref(courseId)}>Voltar ao curso</Link><button type="button" className="outline-button" onClick={() => void query.refetch()}>Tentar novamente</button></main>;
  const version = query.data;
  return <main className="page-shell authoring-page">
    <nav aria-label="Caminho" className="course-breadcrumb"><Link to={paths.authoring.getHref()}>Autoria · Cursos</Link><Link to={paths.authoringCourse.getHref(courseId)}>{version.title} · Histórico</Link><span>Versão {version.versionNumber}</span></nav>
    <p className="eyebrow">Versão {version.versionNumber} · {version.current ? 'Vigente' : 'Anterior'}</p><h1>{version.title}</h1>
    {version.description ? <p className="page-subtitle">{version.description}</p> : null}
    <p>Publicada por {version.publishedBy.name} em <time dateTime={version.publishedAt}>{new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(version.publishedAt))}</time></p>
    {version.versionNote ? <p>{version.versionNote}</p> : null}<p className="inline-alert">Somente leitura · Retrato imutável. Alterações são feitas no rascunho.</p>
    <section className="course-module" aria-label="Nível e pré-requisito desta versão">
      <h2>Nível e pré-requisito desta versão</h2>
      <p>Nível: {courseLevelLabel(version.level)}</p>
      {!version.prerequisite.text && !version.prerequisite.recommendedCourses.length ? <p>Pré-requisito: Sem pré-requisito</p> : <>
        <p>Pré-requisito: {version.prerequisite.text ?? 'Sem texto de pré-requisito'}</p>
        <p>Cursos recomendados (título na época da publicação):</p>
        {version.prerequisite.recommendedCourses.length ? <ol aria-label="Cursos recomendados na publicação">{version.prerequisite.recommendedCourses.map((course) => <li key={course.courseId}>{course.title}</li>)}</ol> : <p>Nenhum curso recomendado</p>}
      </>}
    </section>
    <div className="course-module-list">{version.modules.map((module) => <section className="course-module course-version-module" key={module.moduleId} aria-label={module.title}><h2>{module.position}. {module.title}</h2><p>ID do módulo: {module.moduleId}</p>
      {module.lessons.map((lesson) => <article className="course-lesson" key={lesson.lessonId} aria-label={lesson.title}><div className="course-lesson-details"><h3>{lesson.position}. {lesson.title}</h3>{lesson.description ? <p>{lesson.description}</p> : null}<p>ID da aula: {lesson.lessonId}</p><p>Vídeo vinculado · ID {lesson.videoId}</p></div></article>)}
    </section>)}</div>
  </main>;
};
