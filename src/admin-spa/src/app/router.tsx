import { createBrowserRouter, type RouteObject } from 'react-router';

import { AppShell } from '@/components/app-shell';
import { paths } from '@/config/paths';

import { DashboardRoute } from '@/app/routes/dashboard-route';
import { RouteError } from '@/app/routes/route-error';
import { StaffPasswordResetRoute } from '@/app/routes/staff-password-reset-route';

const routes: RouteObject[] = [
  {
    path: paths.home.path,
    element: <AppShell serviceName="admin-spa" title="Admin Workspace" />,
    errorElement: <RouteError />,
    children: [{ index: true, element: <DashboardRoute /> }],
  },
  {
    path: paths.staffPasswordReset.path.slice(1),
    element: <StaffPasswordResetRoute />,
    errorElement: <RouteError />,
  },
];

export const router = createBrowserRouter(routes, {
  basename: import.meta.env.BASE_URL,
});
