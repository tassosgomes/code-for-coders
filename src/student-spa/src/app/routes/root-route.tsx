import { Outlet } from 'react-router';

import { useStudentSessionEvents } from '@/features/student-session/hooks/use-student-session-events';

export const RootRoute = () => {
  useStudentSessionEvents();

  return <Outlet />;
};
