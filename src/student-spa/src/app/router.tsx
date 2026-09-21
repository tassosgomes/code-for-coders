import { createBrowserRouter, type RouteObject } from 'react-router';

import { AppShell } from '@/components/app-shell';
import { paths } from '@/config/paths';

import { DashboardRoute } from '@/app/routes/dashboard-route';
import { RouteError } from '@/app/routes/route-error';

const routes: RouteObject[] = [
  {
    path: paths.home.path,
    element: <AppShell serviceName="student-spa" title="Student Workspace" />,
    errorElement: <RouteError />,
    children: [{ index: true, element: <DashboardRoute /> }],
  },
];

export const router = createBrowserRouter(routes, {
  basename: import.meta.env.BASE_URL,
});
