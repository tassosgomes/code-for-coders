import { useSearchParams } from 'react-router';

import { paths } from '@/config/paths';
import { getPendingPurchase } from '@/features/student-purchase/utils/pending-purchase';

import { StudentLoginScreen } from '@/features/student-session/components/student-login-screen';
import { internalReturnPath } from '@/utils/internal-return-path';

export const StudentLoginRoute = () => {
  const [params] = useSearchParams();
  const pending = getPendingPurchase();
  return <StudentLoginScreen returnTo={internalReturnPath(params.get('returnTo'))} pendingPurchaseHref={pending ? paths.studentPurchase.getHref(pending.offerId, pending.courseId) : undefined} />;
};
