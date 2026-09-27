import { useOutletContext } from 'react-router';

import { AuditTrailForbidden } from '@/features/audit-trail/components/audit-trail-forbidden';
import { AuditTrailScreen } from '@/features/audit-trail/components/audit-trail-screen';
import type { StaffSession } from '@/features/staff-session/api/staff-session';

export const AuditTrailRoute = () => {
  const session = useOutletContext<StaffSession>();
  if (!session.roles.includes('administrador')) return <AuditTrailForbidden />;

  return <AuditTrailScreen />;
};
