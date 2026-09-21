import { context, propagation } from '@opentelemetry/api';
import axios from 'axios';

import { env } from '@/config/env';

export const apiClient = axios.create({
  baseURL: env.API_URL,
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
  (error: unknown) => {
    const status = axios.isAxiosError(error) ? error.response?.status : undefined;

    if (typeof window !== 'undefined') {
      window.dispatchEvent(new CustomEvent('app:api-error', { detail: { status } }));

      if (status === 401) {
        window.dispatchEvent(new Event('app:session-expired'));
      }
    }

    return Promise.reject(error);
  },
);
