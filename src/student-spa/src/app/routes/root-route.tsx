import { AppShell } from '@/components/app-shell';
import { useStudentSessionEvents } from '@/features/student-session/hooks/use-student-session-events';

export const RootRoute = () => {
  useStudentSessionEvents();

  return <AppShell serviceName="student-spa" title="Student Workspace" />;
};
