import { useQuery } from '@tanstack/react-query';
import { useSearchParams } from 'react-router';

import { getMyCoursesQueryOptions } from '@/features/student-dashboard/api/get-my-courses';
import { useMyOrders } from '@/features/student-purchase/api/get-my-orders';
import { MyOrdersScreen } from '@/features/student-purchase/components/my-orders-screen';

export const StudentMyOrdersRoute = () => {
  const [search, setSearch] = useSearchParams();
  const requestedPage = Number(search.get('_page') ?? 1);
  const page = Number.isSafeInteger(requestedPage) && requestedPage >= 1 ? requestedPage : 1;
  const orders = useMyOrders({ page });
  const courses = useQuery({ ...getMyCoursesQueryOptions(), enabled: orders.data?.data.some((order) => order.status === 'paid') ?? false });
  const lessons = Object.fromEntries(courses.data?.active.map((course) => [course.courseId, course.continueLessonId]) ?? []);
  return <MyOrdersScreen orders={orders} lessons={lessons} onPageChange={(next) => setSearch((current) => { current.set('_page', String(next)); return current; })} />;
};
