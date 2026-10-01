import { useOutletContext, useParams } from 'react-router';

import { CatalogCourseRecordScreen } from '@/features/catalog-courses/components/catalog-course-record-screen';
import type { StaffSession } from '@/features/staff-session/api/staff-session';

export const CatalogCourseRecordRoute = () => {
  const session = useOutletContext<StaffSession>();
  const { courseId } = useParams();
  if (!session.permissions.includes('oferta.editar')) return <main className="page-shell"><h1>Você não tem acesso a esta área.</h1><p>Peça acesso a um administrador.</p></main>;
  return courseId ? <CatalogCourseRecordScreen courseId={courseId} canReadAuthoring={session.permissions.includes('autoria.ler')} /> : null;
};
