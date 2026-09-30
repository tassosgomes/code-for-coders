import { AlertTriangle } from 'lucide-react';
import { useSearchParams } from 'react-router';

import { useCatalogCourses } from '@/features/catalog-courses/api/get-catalog-courses';
import type { CatalogCourse } from '@/features/catalog-courses/api/get-catalog-courses';

const levelLabels = { beginner: 'Iniciante', intermediate: 'Intermediário', advanced: 'Avançado' };
const offerSummary = (counts: CatalogCourse['offerCounts']) => {
  const parts = [
    counts.published > 0 ? `${counts.published} ${counts.published === 1 ? 'publicada' : 'publicadas'}` : '',
    counts.draft > 0 ? `${counts.draft} ${counts.draft === 1 ? 'rascunho' : 'rascunhos'}` : '',
    counts.unpublished > 0 ? `${counts.unpublished} ${counts.unpublished === 1 ? 'despublicada' : 'despublicadas'}` : '',
  ].filter(Boolean);
  return parts.length > 0 ? parts.join(' · ') : 'Sem ofertas';
};

export const CatalogCoursesScreen = () => {
  const [params, setParams] = useSearchParams();
  const requestedPage = Number(params.get('page') ?? 1);
  const page = Number.isSafeInteger(requestedPage) && requestedPage > 0 && requestedPage <= Math.floor(2147483647 / 10) ? requestedPage : 1;
  const courses = useCatalogCourses({ page, size: 10 });
  return <main className="page-shell catalog-page">
    <p className="eyebrow">Catálogo</p>
    <h1>Cursos publicados da escola</h1>
    <p className="page-subtitle">Escolha um curso para montar as ofertas de venda.</p>
    {courses.isPending ? <div aria-label="Carregando catálogo" role="status" className="catalog-skeleton">{Array.from({ length: 5 }, (_, index) => <div key={index} />)}</div>
      : courses.isError ? <section className="empty-state" role="alert"><h2>Não foi possível carregar o catálogo agora.</h2><button className="outline-button" onClick={() => void courses.refetch()} type="button">Tentar de novo</button></section>
        : courses.data.data.length === 0 ? <section className="empty-state"><h2>Nenhum curso publicado ainda.</h2><p>Só cursos publicados pelo professor aparecem aqui para receber ofertas.</p></section>
          : <div className="course-table-wrap"><table className="course-table catalog-table"><thead><tr><th>Curso</th><th>Nível</th><th>Na vitrine</th><th>Ofertas</th></tr></thead><tbody>
            {courses.data.data.map((course) => <tr key={course.courseId}>
              <td><strong>{course.title}</strong></td><td>{course.level ? levelLabels[course.level] : <span className="catalog-no-level"><AlertTriangle aria-hidden="true" size={16} />Sem nível</span>}</td>
              <td>{course.inShowcase ? '✓ Sim' : '– Não'}</td><td>{offerSummary(course.offerCounts)}</td>
            </tr>)}
          </tbody></table></div>}
    {courses.data && courses.data.pagination.total > 0 ? <nav aria-label="Páginas do catálogo" className="course-pagination">
      <button aria-label="Página anterior" className="outline-button" disabled={page === 1 || courses.isFetching} onClick={() => setParams(page > 2 ? { page: String(page - 1) } : {})} type="button">‹</button>
      <span>Página {page} de {courses.data.pagination.totalPages} · 10 por página</span>
      <button aria-label="Próxima página" className="outline-button" disabled={page >= courses.data.pagination.totalPages || courses.isFetching} onClick={() => setParams({ page: String(page + 1) })} type="button">›</button>
    </nav> : null}
  </main>;
};
