import type { RouteObject } from 'react-router';

import { AdminLayoutRoute } from '@/app/routes/admin-layout-route';
import { paths } from '@/config/paths';
import { RouteError } from '@/app/routes/route-error';
import { loadStaffSession } from '@/app/routes/staff-session-loader';

// Telas carregadas sob demanda (chunk por rota); layout, guarda de sessão e erro ficam no pacote inicial.
const RouteFallback = () => (
  <div role="status" aria-live="polite" style={{ padding: '2rem' }}>
    Carregando...
  </div>
);

export const routes: RouteObject[] = [
  {
    path: paths.home.path,
    id: 'admin-root',
    loader: loadStaffSession,
    element: <AdminLayoutRoute serviceName="admin-spa" title="Admin Workspace" />,
    errorElement: <RouteError />,
    HydrateFallback: RouteFallback,
    children: [
      { index: true, lazy: () => import('@/app/routes/dashboard-route').then((m) => ({ Component: m.DashboardRoute })) },
      { path: paths.staffAccess.path.slice(1), lazy: () => import('@/app/routes/staff-access-route').then((m) => ({ Component: m.StaffAccessRoute })) },
      { path: paths.catalog.path.slice(1), lazy: () => import('@/app/routes/catalog-courses-route').then((m) => ({ Component: m.CatalogCoursesRoute })) },
      { path: paths.catalogCourse.path.slice(1), lazy: () => import('@/app/routes/catalog-course-record-route').then((m) => ({ Component: m.CatalogCourseRecordRoute })) },
      { path: paths.courtesies.path.slice(1), lazy: () => import('@/app/routes/courtesies-route').then((m) => ({ Component: m.CourtesiesRoute })), errorElement: <RouteError /> },
      { path: paths.staffFinance.path.slice(1), lazy: () => import('@/app/routes/finance-area-route').then((m) => ({ Component: m.FinanceAreaRoute })), errorElement: <RouteError /> },
      { path: paths.staffFinanceOrder.path.slice(1), lazy: () => import('@/app/routes/finance-order-detail-route').then((m) => ({ Component: m.FinanceOrderDetailRoute })), errorElement: <RouteError /> },
      { path: paths.authoring.path.slice(1), lazy: () => import('@/app/routes/authoring-courses-route').then((m) => ({ Component: m.AuthoringCoursesRoute })) },
      { path: paths.authoringCourse.path.slice(1), lazy: () => import('@/app/routes/authoring-course-route').then((m) => ({ Component: m.AuthoringCourseRoute })) },
      { path: paths.authoringVersion.path.slice(1), lazy: () => import('@/app/routes/authoring-version-route').then((m) => ({ Component: m.AuthoringVersionRoute })) },
      { path: paths.videos.path.slice(1), lazy: () => import('@/app/routes/videos-area-route').then((m) => ({ Component: m.VideosAreaRoute })) },
      {
        path: paths.auditTrail.path.slice(1),
        lazy: () => import('@/app/routes/audit-trail-route').then((m) => ({ Component: m.AuditTrailRoute })),
        children: [
          { index: true, lazy: () => import('@/features/audit-trail/components/audit-trail-screen').then((m) => ({ Component: m.AuditTrailScreen })) },
          { path: ':recordId', lazy: () => import('@/app/routes/audit-record-detail-route').then((m) => ({ Component: m.AuditRecordDetailRoute })) },
        ],
      },
      { path: '*', element: <RouteError notFound /> },
    ],
  },
  {
    path: paths.staffLogin.path.slice(1),
    lazy: () => import('@/app/routes/staff-login-route').then((m) => ({ Component: m.StaffLoginRoute })),
    errorElement: <RouteError />,
  },
  {
    path: paths.staffPasswordReset.path.slice(1),
    lazy: () => import('@/app/routes/staff-password-reset-route').then((m) => ({ Component: m.StaffPasswordResetRoute })),
    errorElement: <RouteError />,
  },
  {
    path: paths.staffPasswordRecovery.path.slice(1),
    lazy: () => import('@/app/routes/staff-password-recovery-route').then((m) => ({ Component: m.StaffPasswordRecoveryRoute })),
    errorElement: <RouteError />,
  },
  {
    path: paths.staffInvitation.path.slice(1),
    lazy: () => import('@/app/routes/staff-invitation-acceptance-route').then((m) => ({ Component: m.StaffInvitationAcceptanceRoute })),
    errorElement: <RouteError />,
  },
];
