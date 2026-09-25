import { createBrowserRouter, type RouteObject } from 'react-router';

import { paths } from '@/config/paths';

import { DashboardRoute, requireStudentSession } from '@/app/routes/dashboard-route';
import { StudentAppLayoutRoute } from '@/app/routes/student-app-layout-route';
import { RootRoute } from '@/app/routes/root-route';
import { RouteError } from '@/app/routes/route-error';
import { AuthLayout } from '@/components/auth-layout';
import { StudentRegistrationRoute } from '@/app/routes/student-registration-route';
import { StudentConfirmationRoute } from '@/app/routes/student-confirmation-route';
import { StudentLoginRoute } from '@/app/routes/student-login-route';
import { StudentPasswordRecoveryRoute, StudentPasswordResetRoute } from '@/app/routes/student-password-recovery-route';
import { StudentPasswordChangeRoute } from '@/app/routes/student-password-change-route';

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
        loader: requireStudentSession,
        element: <StudentAppLayoutRoute />,
        errorElement: <RouteError layout="app" />,
        children: [
          { index: true, element: <DashboardRoute /> },
          { path: paths.studentPasswordChange.path.slice(1), element: <StudentPasswordChangeRoute /> },
        ],
      },
    ],
  },
];

export const router = createBrowserRouter(routes, {
  basename: import.meta.env.BASE_URL,
});
