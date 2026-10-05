import { createBrowserRouter, type RouteObject } from 'react-router';

import { StudentPurchaseRoute } from '@/app/routes/student-purchase-route';
import { StudentOrderRoute } from '@/app/routes/student-order-route';

import { StudentLessonRoute } from '@/app/routes/student-lesson-route';

import { paths } from '@/config/paths';

import { DashboardRoute, requireStudentSession } from '@/app/routes/dashboard-route';
import { StudentAppLayoutRoute } from '@/app/routes/student-app-layout-route';
import { RootRoute } from '@/app/routes/root-route';
import { RouteError } from '@/app/routes/route-error';
import { AuthLayout } from '@/components/auth-layout';
import { PublicLayout } from '@/components/public-layout';
import { StudentRegistrationRoute } from '@/app/routes/student-registration-route';
import { StudentConfirmationRoute } from '@/app/routes/student-confirmation-route';
import { StudentLoginRoute } from '@/app/routes/student-login-route';
import { StudentPasswordRecoveryRoute, StudentPasswordResetRoute } from '@/app/routes/student-password-recovery-route';
import { StudentPasswordChangeRoute } from '@/app/routes/student-password-change-route';
import { StudentShowcaseCourseRoute } from '@/app/routes/student-showcase-course-route';
import { StudentShowcaseRoute } from '@/app/routes/student-showcase-route';

const routes: RouteObject[] = [
  {
    path: paths.home.path,
    element: <RootRoute />,
    errorElement: <RouteError />,
    children: [
      {
        element: <AuthLayout />,
        errorElement: <RouteError layout="auth" />,
        children: [
          { path: paths.studentRegistration.path.slice(1), element: <StudentRegistrationRoute /> },
          { path: paths.studentAccountConfirmation.path.slice(1), element: <StudentConfirmationRoute /> },
          { path: paths.studentLogin.path.slice(1), element: <StudentLoginRoute /> },
          { path: paths.studentPasswordRecovery.path.slice(1), element: <StudentPasswordRecoveryRoute /> },
          { path: paths.studentPasswordReset.path.slice(1), element: <StudentPasswordResetRoute /> },
        ],
      },
      {
        // Public area: outside requireStudentSession, never reads the session, same page for a signed-in student (RN-O13).
        element: <PublicLayout />,
        errorElement: <RouteError layout="public" />,
        children: [
          { path: paths.studentShowcase.path.slice(1), element: <StudentShowcaseRoute /> },
          { path: paths.studentShowcaseCourse.path.slice(1), element: <StudentShowcaseCourseRoute /> },
        ],
      },
      {
        loader: requireStudentSession,
        element: <StudentAppLayoutRoute />,
        errorElement: <RouteError layout="app" />,
        children: [
          { index: true, element: <DashboardRoute /> },
          { path: paths.studentPurchase.path.slice(1), element: <StudentPurchaseRoute /> },
          { path: paths.studentOrder.path.slice(1), element: <StudentOrderRoute /> },
          { path: paths.studentLesson.path.slice(1), element: <StudentLessonRoute /> },
          { path: paths.studentPasswordChange.path.slice(1), element: <StudentPasswordChangeRoute /> },
        ],
      },
    ],
  },
];

export const router = createBrowserRouter(routes, {
  basename: import.meta.env.BASE_URL,
});
