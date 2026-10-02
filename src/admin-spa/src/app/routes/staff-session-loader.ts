import axios from 'axios';
import { redirect } from 'react-router';

import { paths } from '@/config/paths';
import { activeStaffSessionMarker, expiredStaffSessionMarker } from '@/config/session-markers';
import { getCurrentStaffSession } from '@/features/staff-session/api/staff-session';
import { clearCsrfToken } from '@/lib/api-client';

export const loadStaffSession = async () => {
  try {
    const session = await getCurrentStaffSession();
    window.sessionStorage.setItem(activeStaffSessionMarker, 'true');
    window.sessionStorage.removeItem(expiredStaffSessionMarker);
    return session;
  } catch (error: unknown) {
    if (axios.isAxiosError(error) && error.response?.status === 401) {
      const hadActiveSession = window.sessionStorage.getItem(activeStaffSessionMarker) === 'true';
      window.sessionStorage.removeItem(activeStaffSessionMarker);
      if (hadActiveSession) {
        window.sessionStorage.setItem(expiredStaffSessionMarker, 'true');
      }
      clearCsrfToken();
      throw redirect(paths.staffLogin.getHref());
    }

    throw error;
  }
};
