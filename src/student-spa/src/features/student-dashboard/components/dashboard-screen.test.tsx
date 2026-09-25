import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { DashboardScreen } from '@/features/student-dashboard/components/dashboard-screen';
import { renderWithProviders } from '@/testing/test-utils';

describe('student dashboard screen', () => {
  it('welcomes the student and shows an honest empty state without a catalog action', () => {
    renderWithProviders(<DashboardScreen studentName="Ana Souza" />);

    expect(screen.getByRole('heading', { name: 'Olá, Ana Souza 👋' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Seus cursos aparecem aqui' })).toBeInTheDocument();
    expect(screen.getByText('Sua conta está pronta.')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /curso|catálogo/i })).not.toBeInTheDocument();
    expect(screen.queryByText(/workspace service|runtime check/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/cursos\.length/)).not.toBeInTheDocument();
  });

  it('shows accessible loading skeletons for the account and course cards', () => {
    renderWithProviders(<DashboardScreen isSessionLoading />);

    expect(screen.getByRole('status', { name: 'Carregando início' })).toBeInTheDocument();
  });
});
