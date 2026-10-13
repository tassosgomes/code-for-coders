import { createBrowserRouter, type RouteObject } from 'react-router';

import { paths } from '@/config/paths';

import { DashboardRoute, requireStudentSession } from '@/app/routes/dashboard-route';
import { StudentAppLayoutRoute } from '@/app/routes/student-app-layout-route';
import { RootRoute } from '@/app/routes/root-route';
import { RouteError } from '@/app/routes/route-error';
import { AuthLayout } from '@/components/auth-layout';
import { PublicLayout } from '@/components/public-layout';

// Fallback enquanto a rota lazy (ou o loader) resolve na primeira carga.
const PendingFallback = () => <div role="status" aria-live="polite" className="p-6 text-sm text-muted-foreground">Carregando…</div>;

const routes: RouteObject[] = [
  {
    path: paths.home.path,
    element: <RootRoute />,
    errorElement: <RouteError />,
    HydrateFallback: PendingFallback,
    children: [
      {
        element: <AuthLayout />,
        errorElement: <RouteError layout="auth" />,
        children: [
          { path: paths.studentRegistration.path.slice(1), lazy: () => import('@/app/routes/student-registration-route').then((m) => ({ Component: m.StudentRegistrationRoute })) },
          { path: paths.studentAccountConfirmation.path.slice(1), lazy: () => import('@/app/routes/student-confirmation-route').then((m) => ({ Component: m.StudentConfirmationRoute })) },
          { path: paths.studentLogin.path.slice(1), lazy: () => import('@/app/routes/student-login-route').then((m) => ({ Component: m.StudentLoginRoute })) },
          { path: paths.studentPasswordRecovery.path.slice(1), lazy: () => import('@/app/routes/student-password-recovery-route').then((m) => ({ Component: m.StudentPasswordRecoveryRoute })) },
          { path: paths.studentPasswordReset.path.slice(1), lazy: () => import('@/app/routes/student-password-recovery-route').then((m) => ({ Component: m.StudentPasswordResetRoute })) },
        ],
      },
      {
        // Public area: outside requireStudentSession, never reads the session, same page for a signed-in student (RN-O13).
        element: <PublicLayout />,
        errorElement: <RouteError layout="public" />,
        children: [
          { path: paths.studentShowcase.path.slice(1), lazy: () => import('@/app/routes/student-showcase-route').then((m) => ({ Component: m.StudentShowcaseRoute })) },
          { path: paths.studentShowcaseCourse.path.slice(1), lazy: () => import('@/app/routes/student-showcase-course-route').then((m) => ({ Component: m.StudentShowcaseCourseRoute })) },
        ],
      },
      {
        loader: requireStudentSession,
        element: <StudentAppLayoutRoute />,
        errorElement: <RouteError layout="app" />,
        children: [
          { index: true, element: <DashboardRoute /> },
          { path: paths.studentPurchase.path.slice(1), lazy: () => import('@/app/routes/student-purchase-route').then((m) => ({ Component: m.StudentPurchaseRoute })) },
          { path: paths.studentOrder.path.slice(1), lazy: () => import('@/app/routes/student-order-route').then((m) => ({ Component: m.StudentOrderRoute })) },
          { path: paths.studentMyOrders.path.slice(1), lazy: () => import('@/app/routes/student-my-orders-route').then((m) => ({ Component: m.StudentMyOrdersRoute })) },
          { path: paths.studentLesson.path.slice(1), lazy: () => import('@/app/routes/student-lesson-route').then((m) => ({ Component: m.StudentLessonRoute })) },
          { path: paths.studentPasswordChange.path.slice(1), lazy: () => import('@/app/routes/student-password-change-route').then((m) => ({ Component: m.StudentPasswordChangeRoute })) },
        ],
      },
    ],
  },
];

export const router = createBrowserRouter(routes, {
  basename: import.meta.env.BASE_URL,
});
