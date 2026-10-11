import axios from 'axios';
import { ArrowLeft } from 'lucide-react';
import type { ReactNode } from 'react';
import { Link } from 'react-router';

import { paths } from '@/config/paths';
import { useCatalogCourse } from '@/features/catalog-courses/api/get-catalog-course';
import type { CatalogCourseRecord } from '@/features/catalog-courses/api/get-catalog-course';
import { useCachedCatalogCourse } from '@/features/catalog-courses/api/get-catalog-courses';
import { CatalogAlert } from '@/features/catalog-courses/components/catalog-alert';
import { LevelBadge, ShowcaseBadge } from '@/features/catalog-courses/components/catalog-badges';
import { CatalogOffers } from '@/features/catalog-courses/components/catalog-offers';
import { CatalogTaglineForm } from '@/features/catalog-courses/components/catalog-tagline-form';

type CatalogCourseRecordScreenProps = { courseId: string; canReadAuthoring: boolean };

const RecordTop = ({ title, subtitle, inShowcase }: { title: ReactNode; subtitle?: string; inShowcase?: boolean }) => <div className="catalog-record-top">
  <Link aria-label="Voltar ao Catálogo" className="catalog-back" to={paths.catalog.getHref()}><ArrowLeft aria-hidden="true" size={16} />Catálogo</Link>
  <div className="catalog-record-header">
    <header className="catalog-heading"><p className="eyebrow">Ficha do curso</p>{title}{subtitle ? <p className="page-subtitle">{subtitle}</p> : null}</header>
    {inShowcase === undefined ? null : <ShowcaseBadge inShowcase={inShowcase} />}
  </div>
</div>;

const levelAlert = (course: CatalogCourseRecord) => course.offers.some((offer) => offer.status === 'published')
  ? { title: 'O curso saiu da vitrine porque a versão atual não declara nível.', detail: 'As ofertas publicadas continuam publicadas e voltam à vitrine quando o professor declarar o nível e publicar o curso.' }
  : { title: 'Nenhuma oferta pode ser publicada até o professor declarar o nível do curso.', detail: 'O nível é declarado no curso e vale depois que o professor publicar.' };

const RecordSkeleton = () => <div aria-label="Carregando ficha do curso" className="catalog-record-skeleton" role="status">
  <div aria-hidden="true" className="catalog-skeleton-lines"><span className="catalog-skeleton-bar" /><span className="catalog-skeleton-bar" /><span className="catalog-skeleton-bar" /></div>
  {[0, 1].map((card) => <div aria-hidden="true" className="catalog-card" key={card}>
    <span className="catalog-skeleton-bar" /><span className="catalog-skeleton-bar" /><span className="catalog-skeleton-bar" />
  </div>)}
</div>;

export const CatalogCourseRecordScreen = ({ courseId, canReadAuthoring }: CatalogCourseRecordScreenProps) => {
  const query = useCatalogCourse({ courseId });
  const cached = useCachedCatalogCourse(courseId);
  const notFound = axios.isAxiosError(query.error) && query.error.response?.status === 404;
  const course = query.data;
  const cachedTitle = cached ? <h1>{cached.title}</h1> : null;

  if (query.isPending) return <main className="page-shell catalog-page catalog-record-page">
    <RecordTop title={cachedTitle ?? <span aria-hidden="true" className="catalog-skeleton-bar is-title" />} />
    <RecordSkeleton />
  </main>;

  if (notFound) return <main className="page-shell catalog-page catalog-record-page">
    <RecordTop subtitle="O curso pode nunca ter sido publicado, não existir ou pertencer a outra escola." title={<h1>Curso não encontrado no catálogo.</h1>} />
    <div className="catalog-record-body"><section className="catalog-empty is-compact"><h2>Curso não encontrado no catálogo.</h2>
      <Link className="outline-button" to={paths.catalog.getHref()}>Voltar ao Catálogo</Link></section></div>
  </main>;

  if (query.isError || !course) return <main className="page-shell catalog-page catalog-record-page">
    <RecordTop inShowcase={cached?.inShowcase} title={cachedTitle} />
    <div className="catalog-record-body">
      <CatalogAlert detail="O serviço do Catálogo não respondeu. Tente de novo em instantes." onRetry={() => void query.refetch()} retry title="Não foi possível carregar este curso agora." />
    </div>
  </main>;

  const recommended = course.prerequisite.recommendedCourses.map((reference) => reference.title).join(', ');
  return <main className="page-shell catalog-page catalog-record-page">
    <RecordTop inShowcase={course.inShowcase} title={<h1>{course.title}</h1>} />
    <div className="catalog-record-body">
      {course.level === null ? <CatalogAlert tone="warning" {...levelAlert(course)} /> : null}
      <section aria-labelledby="catalog-course-facts" className="catalog-card">
        <div className="catalog-card-header"><h2 id="catalog-course-facts">Do curso</h2><span className="catalog-badge is-outline">somente leitura</span></div>
        <dl className="catalog-facts">
          <div><dt>Nível</dt><dd><LevelBadge level={course.level} /></dd></div>
          <div><dt>Pré-requisito</dt><dd>{course.prerequisite.text ?? 'Não informado'}</dd></div>
          {recommended ? <div><dt>Recomendados</dt><dd>{recommended}</dd></div> : null}
        </dl>
        <p className="catalog-note">O professor altera nível e pré-requisito no curso.
          {canReadAuthoring ? <Link to={paths.authoringCourse.getHref(courseId)}>Abrir na Autoria<span aria-hidden="true"> →</span></Link> : null}</p>
      </section>
      <CatalogTaglineForm key={courseId} course={course} />
      <CatalogOffers course={course} />
    </div>
  </main>;
};
