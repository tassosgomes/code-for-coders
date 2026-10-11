import { useOutletContext } from 'react-router';

import { ForbiddenScreen } from '@/components/forbidden-screen';
import { CatalogCoursesScreen } from '@/features/catalog-courses/components/catalog-courses-screen';
import type { StaffSession } from '@/features/staff-session/api/staff-session';

export const CatalogCoursesRoute = () => {
  const session = useOutletContext<StaffSession>();
  if (!session.permissions.includes('oferta.editar')) return <ForbiddenScreen />;
  return <CatalogCoursesScreen />;
};
