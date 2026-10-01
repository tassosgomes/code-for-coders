import { useOutletContext } from 'react-router';

import { CatalogCoursesScreen } from '@/features/catalog-courses/components/catalog-courses-screen';
import type { StaffSession } from '@/features/staff-session/api/staff-session';

export const CatalogCoursesRoute = () => {
  const session = useOutletContext<StaffSession>();
  if (!session.permissions.includes('oferta.editar')) return <main className="page-shell"><h1>Você não tem acesso a esta área.</h1><p>Peça acesso a um administrador.</p></main>;
  return <CatalogCoursesScreen />;
};
