import { useState } from 'react';
import { useLoaderData, useNavigate } from 'react-router';

import axios from 'axios';

import { loadStaffSession } from '@/app/routes/staff-session-loader';
import { AppShell } from '@/components/app-shell';
import { paths } from '@/config/paths';
import { useEndCurrentStaffSession } from '@/features/staff-session/api/staff-session';
import { getStaffAreas } from '@/features/staff-session/utils/get-staff-areas';

type AdminLayoutRouteProps = {
  serviceName: string;
  title: string;
};

export const AdminLayoutRoute = ({ serviceName, title }: AdminLayoutRouteProps) => {
  const session = useLoaderData<typeof loadStaffSession>();
  const navigate = useNavigate();
  const [logoutError, setLogoutError] = useState<string | null>(null);
  const endSession = useEndCurrentStaffSession();

  const logout = async () => {
    setLogoutError(null);
    try {
      await endSession.mutateAsync();
      await navigate(paths.staffLogin.getHref(), { replace: true });
    } catch (error: unknown) {
      if (axios.isAxiosError(error) && error.response?.status === 401) {
        await navigate(paths.staffLogin.getHref(), { replace: true });
        return;
      }

      setLogoutError('Não foi possível sair agora. Tente novamente.');
    }
  };

  return (
    <AppShell
      areas={getStaffAreas(session.permissions)}
      outletContext={session}
      isLoggingOut={endSession.isPending}
      logoutError={logoutError}
      onLogout={() => void logout()}
      serviceName={serviceName}
      title={title}
    />
  );
};
