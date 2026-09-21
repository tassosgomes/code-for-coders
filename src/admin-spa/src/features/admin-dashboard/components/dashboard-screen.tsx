import { getAdminWorkspaceStatus, useAdminWorkspaceStatus } from '@/features/admin-dashboard/api/get-admin-workspace-status';
import { getErrorMessage } from '@/utils/get-error-message';

export const DashboardScreen = () => {
  const { data, error, isError, isFetching, isPending, refetch } = useAdminWorkspaceStatus();

  const statusMessage = isPending
    ? 'Checking the admin workspace service…'
    : isError
      ? getErrorMessage(error, 'The admin workspace service is unavailable.')
      : data?.message ?? 'The admin workspace service is ready.';

  return (
    <main className="page-shell">
      <p className="eyebrow">Admin Workspace</p>
      <h1>Operations overview</h1>
      <p className="lead">Keep a clear view of platform operations in the admin workspace.</p>

      <section aria-labelledby="service-status" className="status-card">
        <div className="status-card-header">
          <div>
            <p className="eyebrow">Runtime check</p>
            <h2 id="service-status">Workspace service</h2>
          </div>
          <span className={`status-pill status-pill-${data?.status ?? 'unknown'}`}>
            {data?.status ?? 'unknown'}
          </span>
        </div>
        <p aria-live="polite" role="status">
          {statusMessage}
        </p>
        <button
          className="secondary-button"
          disabled={isFetching}
          type="button"
          onClick={() => {
            void refetch();
          }}
        >
          {isFetching ? 'Refreshing…' : 'Refresh status'}
        </button>
      </section>

      <p className="implementation-note">Connected through {getAdminWorkspaceStatus.name}.</p>
    </main>
  );
};
