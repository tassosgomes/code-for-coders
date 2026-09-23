import { useEffect } from 'react';
import { useNavigate } from 'react-router';
import { useQueryClient } from '@tanstack/react-query';

import { paths } from '@/config/paths';
import { studentSessionQueryKey, studentSessionSchema } from '@/features/student-session/api/student-session';

export const useStudentSessionEvents = () => {
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  useEffect(() => {
    const handleSessionExpired = () => {
      void navigate(paths.studentLogin.getHref(), { replace: true });
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
  }, [navigate, queryClient]);
};
