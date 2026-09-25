import { createBrowserRouter, type RouteObject } from 'react-router';

import { AdminLayoutRoute } from '@/app/routes/admin-layout-route';
import { paths } from '@/config/paths';

import { DashboardRoute } from '@/app/routes/dashboard-route';
import { RouteError } from '@/app/routes/route-error';
import { StaffPasswordResetRoute } from '@/app/routes/staff-password-reset-route';
import { StaffInvitationAcceptanceRoute } from '@/app/routes/staff-invitation-acceptance-route';
import { loadStaffSession } from '@/app/routes/staff-session-loader';
import { StaffLoginRoute } from '@/app/routes/staff-login-route';
import { StaffAccessRoute } from '@/app/routes/staff-access-route';
import { FinanceAreaRoute } from '@/app/routes/finance-area-route';

const routes: RouteObject[] = [
  {
    path: paths.home.path,
    id: 'admin-root',
    loader: loadStaffSession,
    element: <AdminLayoutRoute serviceName="admin-spa" title="Admin Workspace" />,
    errorElement: <RouteError />,
    children: [
      { index: true, element: <DashboardRoute /> },
      { path: paths.staffAccess.path.slice(1), element: <StaffAccessRoute /> },
      { path: paths.staffFinance.path.slice(1), element: <FinanceAreaRoute /> },
    ],
  },
  {
    path: paths.staffLogin.path.slice(1),
    element: <StaffLoginRoute />,
    errorElement: <RouteError />,
  },
  {
    path: paths.staffPasswordReset.path.slice(1),
    element: <StaffPasswordResetRoute />,
    errorElement: <RouteError />,
  },
  {
    path: paths.staffInvitation.path.slice(1),
    element: <StaffInvitationAcceptanceRoute />,
    errorElement: <RouteError />,
  },
];

export const router = createBrowserRouter(routes, {
  basename: import.meta.env.BASE_URL,
});
