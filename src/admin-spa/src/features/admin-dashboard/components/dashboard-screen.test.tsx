import { screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';

import { DashboardScreen } from '@/features/admin-dashboard/components/dashboard-screen';
import { renderWithProviders } from '@/testing/test-utils';

describe('admin dashboard route', () => {
  it('shows the areas granted to a member', () => {
    renderWithProviders(<MemoryRouter><DashboardScreen name="Marina Alves" roles={['administrador']} areas={[{ label: 'Acessos', permission: 'acesso.gerir', href: '/acessos' }]} /></MemoryRouter>);
    expect(screen.getByRole('heading', { name: 'Olá, Marina' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Abrir acessos/ })).toHaveAttribute('href', '/acessos');
  });
});
