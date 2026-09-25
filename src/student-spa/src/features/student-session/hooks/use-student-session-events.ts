import { useEffect } from 'react';
import { useLocation, useNavigate } from 'react-router';
import { useQueryClient } from '@tanstack/react-query';

import { paths } from '@/config/paths';
import { activeStudentSessionMarker, expiredStudentSessionMarker } from '@/config/session-markers';
import { studentSessionQueryKey, studentSessionSchema } from '@/features/student-session/api/student-session';

export const useStudentSessionEvents = () => {
  const { pathname } = useLocation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  useEffect(() => {
    const handleSessionExpired = () => {
      if (pathname !== paths.home.path && pathname !== paths.studentPasswordChange.path) {
        return;
      }

      if (!queryClient.getQueryData(studentSessionQueryKey)) {
        return;
      }

      queryClient.removeQueries({ queryKey: studentSessionQueryKey });
      window.sessionStorage.setItem(expiredStudentSessionMarker, 'true');
      window.localStorage.removeItem(activeStudentSessionMarker);
      void navigate(paths.studentLogin.getHref(), {
        replace: true,
        state: { sessionExpired: true },
      });
    };
    const handleCsrfRefreshed = (event: Event) => {
      if (!(event instanceof CustomEvent)) {
        return;
      }

      const session = studentSessionSchema.safeParse(event.detail);
      if (session.success) {
        queryClient.setQueryData(studentSessionQueryKey, session.data);
      }
    };

    window.addEventListener('app:session-expired', handleSessionExpired);
    window.addEventListener('app:csrf-refreshed', handleCsrfRefreshed);
    return () => {
      window.removeEventListener('app:session-expired', handleSessionExpired);
      window.removeEventListener('app:csrf-refreshed', handleCsrfRefreshed);
    };
  }, [navigate, pathname, queryClient]);
};
