import { createBrowserRouter, type RouteObject } from 'react-router';

import { AppShell } from '@/components/app-shell';
import { paths } from '@/config/paths';

import { DashboardRoute } from '@/app/routes/dashboard-route';
import { RouteError } from '@/app/routes/route-error';
import { StudentRegistrationRoute } from '@/app/routes/student-registration-route';

const routes: RouteObject[] = [
  {
    path: paths.home.path,
    element: <AppShell serviceName="student-spa" title="Student Workspace" />,
    errorElement: <RouteError />,
    children: [
      { index: true, element: <DashboardRoute /> },
      { path: paths.studentRegistration.path.slice(1), element: <StudentRegistrationRoute /> },
    ],
  },
];

export const router = createBrowserRouter(routes, {
  basename: import.meta.env.BASE_URL,
});
