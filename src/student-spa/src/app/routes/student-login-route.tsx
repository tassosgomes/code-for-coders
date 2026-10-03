import { useSearchParams } from 'react-router';

import { StudentLoginScreen } from '@/features/student-session/components/student-login-screen';
import { internalReturnPath } from '@/utils/internal-return-path';

export const StudentLoginRoute = () => {
  const [params] = useSearchParams();
  return <StudentLoginScreen returnTo={internalReturnPath(params.get('returnTo'))} />;
};
