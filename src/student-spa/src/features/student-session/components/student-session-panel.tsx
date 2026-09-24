import { useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate } from 'react-router';

import { paths } from '@/config/paths';
import {
  studentSessionQueryKey,
  useEndStudentSession,
  useStudentSession,
} from '@/features/student-session/api/student-session';

export const StudentSessionPanel = () => {
  const studentSession = useStudentSession();
  const endSession = useEndStudentSession();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const logoutKeyRef = useRef<string | null>(null);
  const [logoutError, setLogoutError] = useState(false);

  const handleLogout = async () => {
    if (!studentSession.data) {
      return;
    }

    logoutKeyRef.current ??= crypto.randomUUID();
    setLogoutError(false);
    endSession.reset();
    try {
      await endSession.mutateAsync({
        csrfToken: studentSession.data.csrfToken,
        idempotencyKey: logoutKeyRef.current,
      });
      logoutKeyRef.current = null;
      queryClient.removeQueries({ queryKey: studentSessionQueryKey });
      await navigate(paths.studentLogin.getHref(), { replace: true });
    } catch {
      setLogoutError(true);
    }
  };

  if (!studentSession.data) {
    return null;
  }

  return (
    <section aria-label="Student session" className="status-card">
      <p>Signed in as <strong>{studentSession.data.name}</strong></p>
      <p><Link className="primary-link" to={paths.studentPasswordChange.getHref()}>Trocar senha</Link></p>
      {logoutError ? <p className="form-error" role="alert">Não foi possível encerrar sua sessão. Tente novamente.</p> : null}
      <button
        className="secondary-button"
        disabled={endSession.isPending}
        type="button"
        onClick={() => void handleLogout()}
      >
        {endSession.isPending ? 'Saindo…' : 'Sair'}
      </button>
    </section>
  );
};
