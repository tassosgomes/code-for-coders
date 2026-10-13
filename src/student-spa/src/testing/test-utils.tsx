import type { ReactElement } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, type RenderOptions } from '@testing-library/react';

const createTestQueryClient = () =>
  new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  });

type RenderWithProvidersOptions = Omit<RenderOptions, 'wrapper'>;

export const renderWithProviders = (
  ui: ReactElement,
  options?: RenderWithProvidersOptions,
) => {
  const queryClient = createTestQueryClient();

  const view = render(
    <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>,
    options,
  );
  return { ...view, rerender: (next: ReactElement) => view.rerender(
    <QueryClientProvider client={queryClient}>{next}</QueryClientProvider>,
  ) };
};
