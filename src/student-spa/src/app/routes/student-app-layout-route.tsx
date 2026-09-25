import { Outlet } from 'react-router';

import { AppShell } from '@/components/app-shell';
import { StudentSessionPanel } from '@/features/student-session/components/student-session-panel';

export const StudentAppLayoutRoute = () => (
  <AppShell accountMenu={<StudentSessionPanel />}>
    <Outlet />
  </AppShell>
);
