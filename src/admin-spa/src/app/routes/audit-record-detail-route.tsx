import { useParams } from 'react-router';

import { AuditRecordDetailScreen } from '@/features/audit-trail/components/audit-record-detail-screen';
import { RouteError } from '@/app/routes/route-error';

export const AuditRecordDetailRoute = () => {
  const { recordId } = useParams();
  if (!recordId) return <RouteError notFound />;

  return <AuditRecordDetailScreen recordId={recordId} />;
};
