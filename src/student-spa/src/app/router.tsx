import { createBrowserRouter, type RouteObject } from 'react-router';

import { paths } from '@/config/paths';

import { DashboardRoute, requireStudentSession } from '@/app/routes/dashboard-route';
import { RootRoute } from '@/app/routes/root-route';
import { RouteError } from '@/app/routes/route-error';
import { StudentRegistrationRoute } from '@/app/routes/student-registration-route';
import { StudentConfirmationRoute } from '@/app/routes/student-confirmation-route';
import { StudentLoginRoute } from '@/app/routes/student-login-route';
import { StudentPasswordRecoveryRoute, StudentPasswordResetRoute } from '@/app/routes/student-password-recovery-route';

const routes: RouteObject[] = [
  {
    path: paths.home.path,
    element: <RootRoute />,
    errorElement: <RouteError />,
    children: [
      { index: true, loader: requireStudentSession, element: <DashboardRoute /> },
      { path: paths.studentRegistration.path.slice(1), element: <StudentRegistrationRoute /> },
      { path: paths.studentAccountConfirmation.path.slice(1), element: <StudentConfirmationRoute /> },
      { path: paths.studentLogin.path.slice(1), element: <StudentLoginRoute /> },
      { path: paths.studentPasswordRecovery.path.slice(1), element: <StudentPasswordRecoveryRoute /> },
      { path: paths.studentPasswordReset.path.slice(1), element: <StudentPasswordResetRoute /> },
    ],
  },
];

export const router = createBrowserRouter(routes, {
  basename: import.meta.env.BASE_URL,
});
