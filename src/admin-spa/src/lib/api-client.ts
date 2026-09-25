import { context, propagation } from '@opentelemetry/api';
import axios from 'axios';

import { env } from '@/config/env';

export const apiClient = axios.create({
  baseURL: env.API_URL,
  withCredentials: true,
  headers: {
    Accept: 'application/json',
  },
});

let csrfToken: string | null = null;

export const setCsrfToken = (value: string | null) => {
  csrfToken = value;
};

export const clearCsrfToken = () => {
  csrfToken = null;
};

apiClient.interceptors.request.use((config) => {
  const carrier: Record<string, string> = {};
  propagation.inject(context.active(), carrier);

  for (const [headerName, headerValue] of Object.entries(carrier)) {
    config.headers.set(headerName, headerValue);
  }

  if (csrfToken && ['delete', 'patch', 'post', 'put'].includes(config.method?.toLowerCase() ?? '')) {
    config.headers.set('X-CSRF-Token', csrfToken);
  }

  return config;
});

apiClient.interceptors.response.use(
  (response) => response.data,
  (error: unknown) => {
    const status = axios.isAxiosError(error) ? error.response?.status : undefined;
    if (status === 401) {
      clearCsrfToken();
    }

    if (typeof window !== 'undefined') {
      window.dispatchEvent(new CustomEvent('app:api-error', { detail: { status } }));

      if (status === 401) {
        window.dispatchEvent(new Event('app:session-expired'));
      }
    }

    return Promise.reject(error);
  },
);
