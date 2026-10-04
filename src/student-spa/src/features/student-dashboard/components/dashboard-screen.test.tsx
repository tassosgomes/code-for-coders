import { MemoryRouter } from 'react-router';

import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { DashboardScreen } from '@/features/student-dashboard/components/dashboard-screen';
import { renderWithProviders } from '@/testing/test-utils';

describe('student dashboard screen', () => {
  it('welcomes the student and links an empty course list to the catalog', () => {
    renderWithProviders(<MemoryRouter><DashboardScreen studentName="Ana Souza" courses={{ progressAvailable: true, active: [], ended: [] }} /></MemoryRouter>);

    expect(screen.getByRole('heading', { name: 'Olá, Ana Souza 👋' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Você ainda não tem cursos' })).toBeInTheDocument();
    expect(screen.getByText('Bora continuar de onde parou.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Explorar cursos' })).toHaveAttribute('href', '/cursos');
    expect(screen.queryByText(/workspace service|runtime check/i)).not.toBeInTheDocument();
    expect(screen.getByText('cursos.length === 0')).toBeInTheDocument();
  });

  it('shows accessible loading skeletons for the account and course cards', () => {
    renderWithProviders(<DashboardScreen isSessionLoading />);

    expect(screen.getByRole('status', { name: 'Carregando início' })).toBeInTheDocument();
  });
});
