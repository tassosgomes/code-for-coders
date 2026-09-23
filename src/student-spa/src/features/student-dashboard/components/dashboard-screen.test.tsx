import { screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';

import { DashboardScreen } from '@/features/student-dashboard/components/dashboard-screen';
import { renderWithProviders } from '@/testing/test-utils';

describe('student dashboard route', () => {
  it('shows the workspace service status returned by the API', async () => {
    renderWithProviders(
      <MemoryRouter>
        <DashboardScreen />
      </MemoryRouter>,
    );

    await waitFor(() =>
      expect(screen.getByRole('status')).toHaveTextContent(
        'The student workspace service is ready.',
      ),
    );
    expect(screen.getByRole('heading', { name: 'Learning overview' })).toBeInTheDocument();
  });
});
