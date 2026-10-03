import { useOutletContext } from 'react-router';

import { CourtesyStudentScreen } from '@/features/courtesies/components/courtesy-student-screen';
import type { StaffSession } from '@/features/staff-session/api/staff-session';

export const CourtesiesRoute = () => {
  const session = useOutletContext<StaffSession>();
  if (!session.permissions.includes('cortesia.conceder')) return <main className="page-shell"><h1>Você não tem acesso a esta área.</h1><p>Peça acesso a um administrador.</p></main>;
  return <CourtesyStudentScreen />;
};
