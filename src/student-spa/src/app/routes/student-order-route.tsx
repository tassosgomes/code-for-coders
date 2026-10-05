import { useParams } from 'react-router';

import { StudentOrderScreen } from '@/features/student-purchase/components/student-order-screen';

export const StudentOrderRoute = () => {
  const { orderId = '' } = useParams();
  return <StudentOrderScreen key={orderId} orderId={orderId} />;
};
