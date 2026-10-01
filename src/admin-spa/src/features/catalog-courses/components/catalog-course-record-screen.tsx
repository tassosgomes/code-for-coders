import axios from 'axios';
import { Link } from 'react-router';

import { paths } from '@/config/paths';
import { useCatalogCourse } from '@/features/catalog-courses/api/get-catalog-course';
import { CatalogOffers } from '@/features/catalog-courses/components/catalog-offers';
import { CatalogTaglineForm } from '@/features/catalog-courses/components/catalog-tagline-form';

type CatalogCourseRecordScreenProps = { courseId: string; canReadAuthoring: boolean };
const levelLabels = { beginner: 'Iniciante', intermediate: 'Intermediário', advanced: 'Avançado' };

export const CatalogCourseRecordScreen = ({ courseId, canReadAuthoring }: CatalogCourseRecordScreenProps) => {
  const query = useCatalogCourse({ courseId });
  const notFound = axios.isAxiosError(query.error) && query.error.response?.status === 404;
  const course = query.data;
  return <main className="page-shell catalog-page catalog-record-page">
    <Link to={paths.catalog.getHref()}>← Catálogo</Link><p className="eyebrow">Ficha do curso</p>
    {query.isPending ? <div className="catalog-skeleton" role="status" aria-label="Carregando ficha do curso"><div /><div /><div /></div>
      : query.isError ? <section className="empty-state" role="alert"><h1>{notFound ? 'Curso não encontrado no catálogo.' : 'Não foi possível carregar a ficha agora.'}</h1>
        {!notFound ? <button className="outline-button" type="button" onClick={() => void query.refetch()}>Tentar de novo</button> : null}</section>
        : course ? <>
          <h1>{course.title}</h1><p>{course.inShowcase ? '✓ Na vitrine' : '– Fora da vitrine'}</p>
          {course.level === null ? <p className="warning-alert" role="note">Nenhuma oferta pode ser publicada até o professor declarar o nível do curso. O nível é declarado no curso e vale depois que o professor publicar.</p> : null}
          <section className="catalog-record-card"><h2>Dados do curso</h2><dl>
            <dt>Nível</dt><dd>{course.level ? levelLabels[course.level] : 'Sem nível'}</dd>
            <dt>Pré-requisito</dt><dd>{course.prerequisite.text ?? 'Não informado'}</dd>
            {course.prerequisite.recommendedCourses.length > 0 ? <><dt>Recomendados</dt><dd><ul>{course.prerequisite.recommendedCourses.map((reference) => <li key={reference.courseId}>{reference.title}</li>)}</ul></dd></> : null}
          </dl><p>O professor altera nível e pré-requisito no curso.</p>
          {canReadAuthoring ? <Link to={paths.authoringCourse.getHref(courseId)}>Abrir na Autoria</Link> : null}</section>
          <CatalogTaglineForm key={courseId} course={course} />
          <CatalogOffers course={course} />
        </> : null}
  </main>;
};
