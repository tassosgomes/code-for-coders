import axios from 'axios';
import { redirect, type LoaderFunctionArgs } from 'react-router';

import { paths } from '@/config/paths';
import { activeStudentSessionMarker } from '@/config/session-markers';
import { useMyCourses } from '@/features/student-dashboard/api/get-my-courses';
import { DashboardScreen } from '@/features/student-dashboard/components/dashboard-screen';
import {
  getCurrentStudentSession,
  studentSessionQueryKey,
  useStudentSession,
} from '@/features/student-session/api/student-session';
import { StudentAccountCard } from '@/features/student-session/components/student-session-panel';
import { queryClient } from '@/lib/query-client';

export const requireStudentSession = async (args?: LoaderFunctionArgs) => {
  try {
    const session = await getCurrentStudentSession();
    queryClient.setQueryData(studentSessionQueryKey, session);
    window.localStorage.setItem(activeStudentSessionMarker, 'true');
    return null;
  } catch (error) {
    if (axios.isAxiosError(error) && error.response?.status === 401) {
      const url = args ? new URL(args.request.url) : undefined;
      const base = import.meta.env.BASE_URL.replace(/\/$/u, '');
      const pathname = url?.pathname;
      const internal = pathname && base && pathname.startsWith(`${base}/`) ? pathname.slice(base.length) : pathname;
      return redirect(paths.studentLogin.getHref(internal ? `${internal}${url?.search ?? ''}${url?.hash ?? ''}` : undefined));
    }

    throw error;
  }
};

export const DashboardRoute = () => {
  const studentSession = useStudentSession();
  const courses = useMyCourses(studentSession.isSuccess);

  return (
    <DashboardScreen
      account={<StudentAccountCard name={studentSession.data?.name ?? ''} />}
      courses={courses.isError ? undefined : courses.data}
      isCoursesLoading={courses.isPending}
      isSessionLoading={studentSession.isPending}
      studentName={studentSession.data?.name}
    />
  );
};
