import { StudentPasswordChangeScreen } from '@/features/student-password-change/components/student-password-change-screen';
import { useStudentSession } from '@/features/student-session/api/student-session';

export const StudentPasswordChangeRoute = () => {
  const studentSession = useStudentSession();

  if (!studentSession.data) {
    return null;
  }

  return <StudentPasswordChangeScreen csrfToken={studentSession.data.csrfToken} />;
};
