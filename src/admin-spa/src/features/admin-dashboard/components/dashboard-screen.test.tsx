import { cleanup, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';

import { DashboardScreen } from '@/features/admin-dashboard/components/dashboard-screen';
import { renderWithProviders } from '@/testing/test-utils';

describe('admin dashboard route', () => {
  afterEach(cleanup);

  it('shows the areas granted to a member', () => {
    renderWithProviders(<MemoryRouter><DashboardScreen name="Marina Alves" roles={['administrador']} areas={[{ label: 'Acessos', permission: 'acesso.gerir', href: '/acessos' }]} /></MemoryRouter>);
    expect(screen.getByRole('heading', { name: 'Olá, Marina' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Abrir acessos/ })).toHaveAttribute('href', '/acessos');
  });

  it('shows the visible roles label without the extra subtitle', () => {
    renderWithProviders(<MemoryRouter><DashboardScreen name="Marina Alves" roles={['administrador', 'professor']} areas={[]} /></MemoryRouter>);
    expect(screen.getByText('Seus papéis:')).toBeVisible();
    expect(screen.getByText('professor', { selector: '.role-badge' })).toBeInTheDocument();
    expect(screen.queryByText('Organize o conteúdo da sua escola.')).not.toBeInTheDocument();
  });

  it.each([
    ['Acessos', { permission: 'acesso.gerir' }, 'Convide pessoas e ajuste os papéis da equipe.'],
    ['Autoria', { permission: 'autoria.ler' }, 'Monte módulos e aulas, depois publique.'],
    ['Vídeos', { permission: 'midia.enviar' }, 'Envie as gravações das aulas e acompanhe a preparação.'],
    ['Financeiro', { permission: 'financeiro.ler' }, 'Vendas, pedidos e repasses da escola.'],
    ['Catálogo', { permission: 'oferta.editar' }, 'Publique ofertas e ajuste preços dos cursos.'],
    ['Auditoria', { role: 'administrador' }, 'Consulte os atos administrativos registrados para o seu tenant.'],
  ])('describes the %s area with its own text', (label, grant, description) => {
    renderWithProviders(<MemoryRouter><DashboardScreen name="Marina" roles={['administrador']} areas={[{ label, ...grant, href: '/x' }]} /></MemoryRouter>);
    const card = screen.getByRole('heading', { name: label }).closest('article') as HTMLElement;
    expect(within(card).getByText(description)).toBeInTheDocument();
    expect(within(card).queryByText(/Área financeira/)).not.toBeInTheDocument();
  });
});
