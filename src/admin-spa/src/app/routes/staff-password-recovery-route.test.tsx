import { createMemoryRouter, RouterProvider } from 'react-router';
import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { paths } from '@/config/paths';
import { StaffLoginScreen } from '@/features/staff-session/components/staff-login-screen';
import { renderWithProviders } from '@/testing/test-utils';

describe('StaffPasswordRecovery navigation', () => {
  it('offers password recovery from the staff login screen', () => {
    const router = createMemoryRouter(
      [{ path: paths.staffLogin.path, element: <StaffLoginScreen /> }],
      { initialEntries: [paths.staffLogin.getHref()] },
    );
    renderWithProviders(<RouterProvider router={router} />);

    expect(screen.getByRole('link', { name: 'Esqueceu a senha?' })).toHaveAttribute(
      'href',
      paths.staffPasswordRecovery.getHref(),
    );
  });
});
