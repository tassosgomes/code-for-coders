import { useOutletContext } from 'react-router';

import { ForbiddenScreen } from '@/components/forbidden-screen';
import { CourtesyStudentScreen } from '@/features/courtesies/components/courtesy-student-screen';
import type { StaffSession } from '@/features/staff-session/api/staff-session';

export const CourtesiesRoute = () => {
  const session = useOutletContext<StaffSession>();
  if (!session.permissions.includes('cortesia.conceder')) return <ForbiddenScreen />;
  return <CourtesyStudentScreen />;
};
