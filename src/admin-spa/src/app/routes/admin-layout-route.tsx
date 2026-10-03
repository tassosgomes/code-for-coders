import { useEffect, useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { useLoaderData, useLocation, useNavigate } from 'react-router';

import axios from 'axios';

import { loadStaffSession } from '@/app/routes/staff-session-loader';
import { AppShell } from '@/components/app-shell';
import { paths } from '@/config/paths';
import { activeStaffSessionMarker, expiredStaffSessionMarker } from '@/config/session-markers';
import { useEndCurrentStaffSession } from '@/features/staff-session/api/staff-session';
import { getStaffAreas } from '@/features/staff-session/utils/get-staff-areas';

type AdminLayoutRouteProps = {
  serviceName: string;
  title: string;
};

export const AdminLayoutRoute = ({ serviceName, title }: AdminLayoutRouteProps) => {
  const session = useLoaderData<typeof loadStaffSession>();
  const { key } = useLocation();

  return (
    <AdminLayoutContent
      key={key}
      serviceName={serviceName}
      session={session}
      title={title}
    />
  );
};

type AdminLayoutContentProps = AdminLayoutRouteProps & {
  session: Awaited<ReturnType<typeof loadStaffSession>>;
};

const AdminLayoutContent = ({ serviceName, session, title }: AdminLayoutContentProps) => {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const [logoutError, setLogoutError] = useState<string | null>(null);
  const endSession = useEndCurrentStaffSession();
  const sessionExpiredHandled = useRef(false);

  useEffect(() => {
    const handleSessionExpired = () => {
      if (sessionExpiredHandled.current) {
        return;
      }

      sessionExpiredHandled.current = true;
      queryClient.clear();
      window.sessionStorage.removeItem(activeStaffSessionMarker);
      window.sessionStorage.setItem(expiredStaffSessionMarker, 'true');
      void navigate(paths.staffLogin.getHref(), { replace: true });
    };

    window.addEventListener('app:session-expired', handleSessionExpired);
    return () => window.removeEventListener('app:session-expired', handleSessionExpired);
  }, [navigate, queryClient]);

  const logout = async () => {
    setLogoutError(null);
    try {
      await endSession.mutateAsync();
      queryClient.clear();
      window.sessionStorage.removeItem(activeStaffSessionMarker);
      window.sessionStorage.removeItem(expiredStaffSessionMarker);
      await navigate(paths.staffLogin.getHref(), { replace: true });
    } catch (error: unknown) {
      if (axios.isAxiosError(error) && error.response?.status === 401) {
        queryClient.clear();
        return;
      }

      setLogoutError('Não foi possível sair agora. Tente novamente.');
    }
  };

  return (
    <AppShell
      areas={getStaffAreas(session.permissions, session.roles)}
      name={session.name}
      roles={session.roles}
      outletContext={session}
      isLoggingOut={endSession.isPending}
      logoutError={logoutError}
      onLogout={() => void logout()}
      serviceName={serviceName}
      title={title}
    />
  );
};
