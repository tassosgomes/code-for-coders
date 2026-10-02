import axios from 'axios';
import { Info } from 'lucide-react';
import { useState } from 'react';
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
  const [copyNotice, setCopyNotice] = useState('');
  const copyReference = async (reference: string) => {
    try { await navigator.clipboard.writeText(reference); setCopyNotice('Referência copiada.'); }
    catch { setCopyNotice(`Não foi possível copiar. Referência do vídeo: ${reference}`); }
  };
  const query = useCourseVersion(courseId, versionNumber);
  if (query.isPending) return <main className="page-shell"><p role="status">Carregando versão…</p></main>;
  if (query.isError) return <main className="page-shell"><h1>{axios.isAxiosError(query.error) && query.error.response?.status === 404 ? 'Versão não encontrada' : 'Não foi possível carregar a versão'}</h1><Link to={paths.authoringCourse.getHref(courseId)}>Voltar ao curso</Link><button type="button" className="outline-button" onClick={() => void query.refetch()}>Tentar novamente</button></main>;
  const version = query.data;
  return <main className="page-shell authoring-page course-version-page">
    <nav aria-label="Caminho" className="course-breadcrumb"><Link to={paths.authoring.getHref()}>Autoria · Cursos</Link><Link to={paths.authoringCourse.getHref(courseId)}>{version.title} · Histórico</Link><span>Versão {version.versionNumber}</span></nav>
    <header className="course-page-heading"><div><p className="eyebrow">Autoria</p><h1>Versão {version.versionNumber}</h1><p className="page-subtitle">{version.title}</p></div><Link className="outline-button" to={paths.authoringCourse.getHref(courseId)}>Voltar ao rascunho</Link></header>
    <span className={version.current ? 'course-current-badge' : 'course-previous-badge'}>{version.current ? 'Vigente' : 'Anterior'}</span>
    <p className="course-edit-metadata">Publicada por {version.publishedBy.name} em <time dateTime={version.publishedAt}>{new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(version.publishedAt))}</time></p>
    {version.versionNote ? <p>Nota: {version.versionNote}</p> : null}<p className="course-version-notice"><Info size={16} />Retrato da publicação. Alterações posteriores não mudam esta versão.</p>
    <section className="course-module course-version-audience" aria-label="Nível e pré-requisito desta versão">
      <h2>Nível e pré-requisito desta versão</h2>
      <dl>
        <div><dt>Nível<span className="sr-only">: </span></dt><dd>{courseLevelLabel(version.level)}</dd></div>
        <div><dt>Pré-requisito<span className="sr-only">: </span></dt><dd>{version.prerequisite.text ?? (version.prerequisite.recommendedCourses.length ? 'Sem texto de pré-requisito' : 'Sem pré-requisito')}</dd></div>
        {version.prerequisite.text || version.prerequisite.recommendedCourses.length ? <div><dt>Cursos recomendados (título na época da publicação)</dt><dd>
          {version.prerequisite.recommendedCourses.length ? <ol aria-label="Cursos recomendados na publicação">{version.prerequisite.recommendedCourses.map((course) => <li key={course.courseId}>{course.title}</li>)}</ol> : <p>Nenhum curso recomendado</p>}
        </dd></div> : null}
      </dl>
    </section>
    {version.description ? <p className="page-subtitle">{version.description}</p> : null}
    <p className="course-copy-notice" role="status" aria-live="polite">{copyNotice}</p>
    <div className="course-module-list">{version.modules.map((module) => <section className="course-module course-version-module" key={module.moduleId} aria-label={module.title}><h2>{module.position}. {module.title}</h2><span className="sr-only">ID do módulo: {module.moduleId}</span>
      {module.lessons.map((lesson) => <article className="course-lesson" key={lesson.lessonId} aria-label={lesson.title}><div className="course-lesson-details"><h3>{lesson.position}. {lesson.title}</h3>{lesson.description ? <p className="course-lesson-description">{lesson.description}</p> : null}<span className="sr-only">ID da aula: {lesson.lessonId}</span><p className="course-version-reference" title={lesson.videoId}>· {lesson.videoId.slice(0, 4)}…{lesson.videoId.slice(-4)}</p></div><button className="text-button" type="button" aria-label={`Copiar referência de ${lesson.title}`} onClick={() => void copyReference(lesson.videoId)}>Copiar referência</button></article>)}
    </section>)}</div>
  </main>;
};
