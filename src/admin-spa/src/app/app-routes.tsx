import type { RouteObject } from 'react-router';

import { CatalogCoursesRoute } from '@/app/routes/catalog-courses-route';
import { CatalogCourseRecordRoute } from '@/app/routes/catalog-course-record-route';
import { AdminLayoutRoute } from '@/app/routes/admin-layout-route';
import { paths } from '@/config/paths';

import { DashboardRoute } from '@/app/routes/dashboard-route';
import { RouteError } from '@/app/routes/route-error';
import { StaffPasswordResetRoute } from '@/app/routes/staff-password-reset-route';
import { StaffPasswordRecoveryRoute } from '@/app/routes/staff-password-recovery-route';
import { StaffInvitationAcceptanceRoute } from '@/app/routes/staff-invitation-acceptance-route';
import { loadStaffSession } from '@/app/routes/staff-session-loader';
import { StaffLoginRoute } from '@/app/routes/staff-login-route';
import { StaffAccessRoute } from '@/app/routes/staff-access-route';
import { CourtesiesRoute } from '@/app/routes/courtesies-route';
import { FinanceOrderDetailRoute } from '@/app/routes/finance-order-detail-route';
import { FinanceAreaRoute } from '@/app/routes/finance-area-route';
import { VideosAreaRoute } from '@/app/routes/videos-area-route';
import { AuditTrailRoute } from '@/app/routes/audit-trail-route';
import { AuditRecordDetailRoute } from '@/app/routes/audit-record-detail-route';
import { AuditTrailScreen } from '@/features/audit-trail/components/audit-trail-screen';

import { AuthoringCoursesRoute } from '@/app/routes/authoring-courses-route';
import { AuthoringCourseRoute } from '@/app/routes/authoring-course-route';
import { AuthoringVersionRoute } from '@/app/routes/authoring-version-route';

export const routes: RouteObject[] = [
  {
    path: paths.home.path,
    id: 'admin-root',
    loader: loadStaffSession,
    element: <AdminLayoutRoute serviceName="admin-spa" title="Admin Workspace" />,
    errorElement: <RouteError />,
    children: [
      { index: true, element: <DashboardRoute /> },
      { path: paths.staffAccess.path.slice(1), element: <StaffAccessRoute /> },
      { path: paths.catalog.path.slice(1), element: <CatalogCoursesRoute /> },
      { path: paths.catalogCourse.path.slice(1), element: <CatalogCourseRecordRoute /> },
      { path: paths.courtesies.path.slice(1), element: <CourtesiesRoute />, errorElement: <RouteError /> },
      { path: paths.staffFinance.path.slice(1), element: <FinanceAreaRoute />, errorElement: <RouteError /> },
      { path: paths.staffFinanceOrder.path.slice(1), element: <FinanceOrderDetailRoute />, errorElement: <RouteError /> },
      { path: paths.authoring.path.slice(1), element: <AuthoringCoursesRoute /> },
      { path: paths.authoringCourse.path.slice(1), element: <AuthoringCourseRoute /> },
      { path: paths.authoringVersion.path.slice(1), element: <AuthoringVersionRoute /> },
      { path: paths.videos.path.slice(1), element: <VideosAreaRoute /> },
      {
        path: paths.auditTrail.path.slice(1),
        element: <AuditTrailRoute />,
        children: [
          { index: true, element: <AuditTrailScreen /> },
          { path: ':recordId', element: <AuditRecordDetailRoute /> },
        ],
      },
      { path: '*', element: <RouteError notFound /> },
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
    path: paths.staffPasswordRecovery.path.slice(1),
    element: <StaffPasswordRecoveryRoute />,
    errorElement: <RouteError />,
  },
  {
    path: paths.staffInvitation.path.slice(1),
    element: <StaffInvitationAcceptanceRoute />,
    errorElement: <RouteError />,
  },
];
