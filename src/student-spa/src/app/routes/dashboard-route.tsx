import axios from 'axios';
import { redirect } from 'react-router';

import { paths } from '@/config/paths';
import { DashboardScreen } from '@/features/student-dashboard/components/dashboard-screen';
import { getCurrentStudentSession, studentSessionQueryKey } from '@/features/student-session/api/student-session';
import { StudentSessionPanel } from '@/features/student-session/components/student-session-panel';
import { queryClient } from '@/lib/query-client';

export const requireStudentSession = async () => {
  try {
    const session = await getCurrentStudentSession();
    queryClient.setQueryData(studentSessionQueryKey, session);
    return null;
  } catch (error) {
    if (axios.isAxiosError(error) && error.response?.status === 401) {
      return redirect(paths.studentLogin.getHref());
    }

    throw error;
  }
};

export const DashboardRoute = () => <DashboardScreen session={<StudentSessionPanel />} />;
