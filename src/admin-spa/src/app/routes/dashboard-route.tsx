import { useOutletContext } from 'react-router';

import { getStaffAreas } from '@/features/staff-session/utils/get-staff-areas';
import type { StaffSession } from '@/features/staff-session/api/staff-session';
import { DashboardScreen } from '@/features/admin-dashboard/components/dashboard-screen';

export const DashboardRoute = () => {
  const session = useOutletContext<StaffSession>();
  return <DashboardScreen areas={getStaffAreas(session.permissions, session.roles).filter((area) => area.permission !== 'cortesia.conceder')} name={session.name} roles={session.roles} />;
};
