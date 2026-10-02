import { useState } from 'react';
import { Link } from 'react-router';

import { CoursePagination } from '@/features/course-authoring/components/course-pagination';
import { paths } from '@/config/paths';
import { useCourseVersions } from '@/features/course-authoring/api/get-course-versions';

type CourseHistoryProps = { courseId: string };
export const CourseHistory = ({ courseId }: CourseHistoryProps) => {
  const [page, setPage] = useState(1);
  const versions = useCourseVersions(courseId, page);
  if (versions.isPending) return <p role="status">Carregando versões…</p>;
  if (versions.isError) return <div role="alert"><p>Não foi possível consultar o histórico.</p><button type="button" className="outline-button" onClick={() => void versions.refetch()}>Tentar de novo</button></div>;
  return <section aria-label="Histórico de versões"><h2 className="sr-only">Versões publicadas</h2>
    {!versions.data.data.length ? <p>Este curso ainda não tem versões publicadas.</p> : null}
    <ol className="course-module-list course-history-list">{versions.data.data.map((version) => <li className="course-module course-history-item" key={version.versionNumber}>
      <div className="course-history-details"><h3>Versão {version.versionNumber} <span className="sr-only">{version.current ? '· Vigente' : '· Anterior'}</span></h3>{version.current ? <span className="course-current-badge">Vigente</span> : null}
      <p className="course-edit-metadata">Publicada por {version.publishedBy.name} · <time dateTime={version.publishedAt}>{new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(version.publishedAt))}</time></p>
      {version.versionNote ? <p>{version.versionNote}</p> : null}
      </div><Link className="outline-button" aria-label={`Ver versão ${version.versionNumber}`} to={paths.authoringVersion.getHref(courseId, version.versionNumber)}>Ver versão →</Link>
    </li>)}</ol>
    <CoursePagination label="Páginas de versões" page={page} totalPages={versions.data.pagination.totalPages} onChange={setPage} />
  </section>;
};
