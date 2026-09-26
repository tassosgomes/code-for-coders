import axios from 'axios';
import { redirect } from 'react-router';

import { paths } from '@/config/paths';
import { getCurrentStaffSession } from '@/features/staff-session/api/staff-session';
import { clearCsrfToken } from '@/lib/api-client';

export const loadStaffSession = async () => {
  try {
    return await getCurrentStaffSession();
  } catch (error: unknown) {
    if (axios.isAxiosError(error) && error.response?.status === 401) {
      clearCsrfToken();
      throw redirect(paths.staffLogin.getHref());
    }

    throw error;
  }
};
