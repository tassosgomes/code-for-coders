import { Link, useSearchParams } from 'react-router';

import { paths } from '@/config/paths';

import { useCatalogCourses } from '@/features/catalog-courses/api/get-catalog-courses';
import type { CatalogCourse } from '@/features/catalog-courses/api/get-catalog-courses';
import { CatalogAlert } from '@/features/catalog-courses/components/catalog-alert';
import { LevelBadge, ShowcaseState } from '@/features/catalog-courses/components/catalog-badges';
import { CatalogPagination } from '@/features/catalog-courses/components/catalog-pagination';

const pageSize = 10;
const offerSummary = (counts: CatalogCourse['offerCounts']) => {
  const parts = [
    counts.published > 0 ? `${counts.published} ${counts.published === 1 ? 'publicada' : 'publicadas'}` : '',
    counts.draft > 0 ? `${counts.draft} ${counts.draft === 1 ? 'rascunho' : 'rascunhos'}` : '',
    counts.unpublished > 0 ? `${counts.unpublished} ${counts.unpublished === 1 ? 'despublicada' : 'despublicadas'}` : '',
  ].filter(Boolean);
  return parts.length > 0 ? parts.join(' · ') : 'Sem ofertas';
};

const CatalogTableHead = () => <thead><tr>
  <th scope="col">Curso</th><th className="catalog-col-level" scope="col">Nível</th><th className="catalog-col-showcase" scope="col">Na vitrine</th>
  <th className="catalog-col-offers" scope="col">Ofertas</th><th className="catalog-col-open" scope="col"><span className="visually-hidden">Abrir</span></th>
</tr></thead>;

const CatalogCoursesSkeleton = () => <div aria-label="Carregando catálogo" className="catalog-table-card" role="status">
  <table aria-hidden="true" className="catalog-courses-table"><CatalogTableHead /><tbody>{Array.from({ length: 5 }, (_, index) => <tr key={index}>
    <td><span className="catalog-skeleton-bar" /></td><td className="catalog-col-level"><span className="catalog-skeleton-bar" /></td>
    <td className="catalog-col-showcase"><span className="catalog-skeleton-bar" /></td><td className="catalog-col-offers"><span className="catalog-skeleton-bar" /></td>
    <td className="catalog-col-open"><span className="catalog-skeleton-bar" /></td>
  </tr>)}</tbody></table>
</div>;

const CatalogEmpty = () => <section className="catalog-empty">
  <div aria-hidden="true" className="code-window"><div className="code-title"><span className="window-dots"><i /><i /><i /></span>catalogo.ts<span>TS</span></div><pre>1  // nenhum curso publicado ainda</pre></div>
  <h2>Nenhum curso publicado ainda.</h2>
  <p>Só cursos publicados pelo professor aparecem aqui para receber ofertas.</p>
</section>;

export const CatalogCoursesScreen = () => {
  const [params, setParams] = useSearchParams();
  const requestedPage = Number(params.get('page') ?? 1);
  const page = Number.isSafeInteger(requestedPage) && requestedPage > 0 && requestedPage <= Math.floor(2147483647 / pageSize) ? requestedPage : 1;
  const courses = useCatalogCourses({ page, size: pageSize });
  return <main className="page-shell catalog-page">
    <header className="catalog-heading">
      <p className="eyebrow">Catálogo</p>
      <h1>Cursos publicados da escola</h1>
      <p className="page-subtitle">Escolha um curso para montar as ofertas de venda.</p>
    </header>
    {courses.isPending ? <CatalogCoursesSkeleton />
      : courses.isError ? <CatalogAlert detail="O serviço do Catálogo não respondeu. Tente de novo em instantes." onRetry={() => void courses.refetch()} retry title="Não foi possível carregar o catálogo agora." />
        : courses.data.data.length === 0 ? <CatalogEmpty />
          : <div className="catalog-table-card"><table className="catalog-courses-table"><CatalogTableHead /><tbody>
            {courses.data.data.map((course) => <tr key={course.courseId}>
              <td className="catalog-course-title">{course.title}</td>
              <td className="catalog-col-level"><LevelBadge level={course.level} /></td>
              <td className="catalog-col-showcase"><ShowcaseState inShowcase={course.inShowcase} /></td>
              <td className={`catalog-col-offers ${offerSummary(course.offerCounts) === 'Sem ofertas' ? 'is-muted' : ''}`}>{offerSummary(course.offerCounts)}</td>
              <td className="catalog-col-open"><Link aria-label={`Abrir ${course.title}`} className="catalog-open-link" to={paths.catalogCourse.getHref(course.courseId)}>Abrir →</Link></td>
            </tr>)}
          </tbody></table></div>}
    {courses.data && courses.data.pagination.total > 0 ? <CatalogPagination disabled={courses.isFetching} onPageChange={(next) => setParams(next > 1 ? { page: String(next) } : {})}
      page={page} size={pageSize} totalPages={courses.data.pagination.totalPages} /> : null}
  </main>;
};
