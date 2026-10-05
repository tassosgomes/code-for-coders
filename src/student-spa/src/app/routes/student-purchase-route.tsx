import { useEffect } from 'react';
import { useParams, useSearchParams } from 'react-router';

import { savePendingPurchase } from '@/features/student-purchase/utils/pending-purchase';
import { PurchaseSummaryScreen } from '@/features/student-purchase/components/purchase-summary-screen';
import { useStudentSession } from '@/features/student-session/api/student-session';

export const StudentPurchaseRoute = () => {
  const { offerId = '' } = useParams();
  const session = useStudentSession();
  const [search] = useSearchParams();
  const courseId = search.get('courseId');
  useEffect(() => {
    const preservePurchase = () => { if (courseId) savePendingPurchase(offerId, courseId); };
    window.addEventListener('app:session-expired', preservePurchase);
    return () => window.removeEventListener('app:session-expired', preservePurchase);
  }, [offerId, courseId]);
  return session.data ? <PurchaseSummaryScreen key={offerId} offerId={offerId} csrfToken={session.data.csrfToken} /> : null;
};
