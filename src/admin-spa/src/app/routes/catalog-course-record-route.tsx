import { useOutletContext, useParams } from 'react-router';

import { ForbiddenScreen } from '@/components/forbidden-screen';
import { CatalogCourseRecordScreen } from '@/features/catalog-courses/components/catalog-course-record-screen';
import type { StaffSession } from '@/features/staff-session/api/staff-session';

export const CatalogCourseRecordRoute = () => {
  const session = useOutletContext<StaffSession>();
  const { courseId } = useParams();
  if (!session.permissions.includes('oferta.editar')) return <ForbiddenScreen />;
  return courseId ? <CatalogCourseRecordScreen courseId={courseId} canReadAuthoring={session.permissions.includes('autoria.ler')} /> : null;
};
