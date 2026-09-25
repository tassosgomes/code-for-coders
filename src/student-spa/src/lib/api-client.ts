import { context, propagation } from '@opentelemetry/api';
import axios from 'axios';

import { env } from '@/config/env';
import { activeStudentSessionMarker, expiredStudentSessionMarker } from '@/config/session-markers';

export const apiClient = axios.create({
  baseURL: env.API_URL,
  withCredentials: true,
  headers: {
    Accept: 'application/json',
  },
});

apiClient.interceptors.request.use((config) => {
  const carrier: Record<string, string> = {};
  propagation.inject(context.active(), carrier);

  for (const [headerName, headerValue] of Object.entries(carrier)) {
    config.headers.set(headerName, headerValue);
  }

  return config;
});

apiClient.interceptors.response.use(
  (response) => response.data,
  async (error: unknown) => {
    const status = axios.isAxiosError(error) ? error.response?.status : undefined;
    const problemCode = axios.isAxiosError(error)
      && typeof error.response?.data === 'object'
      && error.response.data !== null
      && 'code' in error.response.data
      ? error.response.data.code
      : undefined;

    if (typeof window !== 'undefined') {
      window.dispatchEvent(new CustomEvent('app:api-error', { detail: { status } }));

      if (status === 403 && problemCode === 'CSRF_INVALID') {
        const session = await apiClient.get('/api/v1/student-sessions/current');
        window.dispatchEvent(new CustomEvent('app:csrf-refreshed', { detail: session }));
      }

      if (status === 401) {
        if (
          axios.isAxiosError(error)
          && error.config?.url?.includes('/student-sessions/current')
          && window.localStorage.getItem(activeStudentSessionMarker) === 'true'
        ) {
          window.sessionStorage.setItem(expiredStudentSessionMarker, 'true');
          window.localStorage.removeItem(activeStudentSessionMarker);
        }

        window.dispatchEvent(new Event('app:session-expired'));
      }
    }

    return Promise.reject(error);
  },
);
