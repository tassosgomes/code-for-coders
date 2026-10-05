import { useQuery } from '@tanstack/react-query';
import { useParams, useSearchParams } from 'react-router';

import { getMyCoursesQueryOptions } from '@/features/student-dashboard/api/get-my-courses';
import { useOrder } from '@/features/student-purchase/api/get-order';
import { StudentOrderScreen } from '@/features/student-purchase/components/student-order-screen';
import { useOrderReturnDelay } from '@/features/student-purchase/hooks/use-order-return-delay';
import { useStudentSession } from '@/features/student-session/api/student-session';

const OrderContent = ({ orderId, result }: { orderId: string; result: string | null }) => {
  const session = useStudentSession();
  const delayed = useOrderReturnDelay(result === 'concluido');
  const interval = delayed ? 15_000 : 3_000;
  const order = useOrder(orderId, result === 'concluido' ? interval : false);
  const courses = useQuery({ ...getMyCoursesQueryOptions(), enabled: order.data?.status === 'paid' && !!order.data.accessGrantedAt,
    refetchInterval: (query) => query.state.data?.active.some((course) => course.courseId === order.data?.course.courseId) ? false : interval });
  const lessonId = courses.data?.active.find((course) => course.courseId === order.data?.course.courseId)?.continueLessonId;
  return <StudentOrderScreen orderId={orderId} order={order} result={result} delayed={delayed} lessonId={lessonId} csrfToken={session.data?.csrfToken ?? ''} />;
};
export const StudentOrderRoute = () => {
  const { orderId = '' } = useParams();
  const [search] = useSearchParams();
  return <OrderContent key={`${orderId}/${search.get('resultado')}`} orderId={orderId} result={search.get('resultado')} />;
};
