import { cleanup, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';

import { ForbiddenScreen } from '@/components/forbidden-screen';
import { renderWithProviders } from '@/testing/test-utils';

describe('forbidden screen', () => {
  afterEach(cleanup);

  it('explains the missing permission and links back home', () => {
    window.history.pushState({}, '', '/admin/cortesias');
    renderWithProviders(<MemoryRouter><ForbiddenScreen /></MemoryRouter>);
    expect(screen.getByRole('heading', { level: 1, name: 'Esta área não é do seu papel' })).toBeInTheDocument();
    expect(screen.getByText(/Se você precisa dela, peça a um administrador/)).toBeInTheDocument();
    expect(screen.getByText(/\$ GET \/admin\/cortesias/)).toBeInTheDocument();
    expect(screen.getByText('403 Forbidden')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Voltar para o início' })).toHaveAttribute('href', '/');
  });

  it('shows an explicit path when given', () => {
    renderWithProviders(<MemoryRouter><ForbiddenScreen path="/admin/financeiro" /></MemoryRouter>);
    expect(screen.getByText(/\$ GET \/admin\/financeiro/)).toBeInTheDocument();
  });
});
