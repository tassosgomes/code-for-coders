import { getStudentWorkspaceStatus, useStudentWorkspaceStatus } from '@/features/student-dashboard/api/get-student-workspace-status';
import { getErrorMessage } from '@/utils/get-error-message';

export const DashboardScreen = () => {
  const { data, error, isError, isFetching, isPending, refetch } = useStudentWorkspaceStatus();

  const statusMessage = isPending
    ? 'Checking the student workspace service…'
    : isError
      ? getErrorMessage(error, 'The student workspace service is unavailable.')
      : data?.message ?? 'The student workspace service is ready.';

  return (
    <main className="page-shell">
      <p className="eyebrow">Student Workspace</p>
      <h1>Learning overview</h1>
      <p className="lead">Keep a clear view of learning activity in the student workspace.</p>

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

      <p className="implementation-note">Connected through {getStudentWorkspaceStatus.name}.</p>
    </main>
  );
};
