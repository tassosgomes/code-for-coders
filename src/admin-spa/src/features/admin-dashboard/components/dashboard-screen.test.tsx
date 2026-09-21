import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { DashboardScreen } from '@/features/admin-dashboard/components/dashboard-screen';
import { renderWithProviders } from '@/testing/test-utils';

describe('admin dashboard route', () => {
  it('shows the workspace service status returned by the API', async () => {
    renderWithProviders(<DashboardScreen />);

    await waitFor(() =>
      expect(screen.getByRole('status')).toHaveTextContent(
        'The admin workspace service is ready.',
      ),
    );
    expect(screen.getByRole('heading', { name: 'Operations overview' })).toBeInTheDocument();
  });
});
