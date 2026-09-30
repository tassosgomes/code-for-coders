import { useState } from 'react';
import { Link } from 'react-router';

import { paths } from '@/config/paths';
import { useCourseVersions } from '@/features/course-authoring/api/get-course-versions';

type CourseHistoryProps = { courseId: string };
export const CourseHistory = ({ courseId }: CourseHistoryProps) => {
  const [page, setPage] = useState(1);
  const versions = useCourseVersions(courseId, page);
  if (versions.isPending) return <p role="status">Carregando versões…</p>;
  if (versions.isError) return <div role="alert"><p>Não foi possível consultar o histórico.</p><button type="button" className="outline-button" onClick={() => void versions.refetch()}>Tentar de novo</button></div>;
  return <section aria-label="Histórico de versões"><h2>Versões publicadas</h2>
    {!versions.data.data.length ? <p>Este curso ainda não tem versões publicadas.</p> : null}
    <ol className="course-module-list course-history-list">{versions.data.data.map((version) => <li className="course-module course-history-item" key={version.versionNumber}>
      <h3>Versão {version.versionNumber} {version.current ? '· Vigente' : '· Anterior'}</h3>
      <p>{version.publishedBy.name} · <time dateTime={version.publishedAt}>{new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(version.publishedAt))}</time></p>
      {version.versionNote ? <p>{version.versionNote}</p> : null}
      <Link to={paths.authoringVersion.getHref(courseId, version.versionNumber)}>Ver versão {version.versionNumber}</Link>
    </li>)}</ol>
    {versions.data.pagination.totalPages > 1 ? <nav aria-label="Páginas de versões"><button type="button" disabled={page === 1} onClick={() => setPage(page - 1)}>Anterior</button><span>Página {page} de {versions.data.pagination.totalPages}</span><button type="button" disabled={page >= versions.data.pagination.totalPages} onClick={() => setPage(page + 1)}>Próxima</button></nav> : null}
  </section>;
};
