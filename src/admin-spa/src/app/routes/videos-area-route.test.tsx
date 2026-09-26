import { screen } from '@testing-library/react';
import { createMemoryRouter, MemoryRouter, Outlet, RouterProvider } from 'react-router';
import { describe, expect, it } from 'vitest';

import { VideosAreaRoute } from '@/app/routes/videos-area-route';
import { paths } from '@/config/paths';
import { DashboardScreen } from '@/features/admin-dashboard/components/dashboard-screen';
import { getStaffAreas } from '@/features/staff-session/utils/get-staff-areas';
import { renderWithProviders } from '@/testing/test-utils';

const renderVideosRoute = (permissions: readonly string[]) => {
  const router = createMemoryRouter([
    {
      path: '/',
      element: <Outlet context={{
        permissions,
        name: 'Marina Alves',
        roles: ['professor'],
      }} />,
      children: [{ path: 'videos', element: <VideosAreaRoute /> }],
    },
  ], { initialEntries: ['/videos'] });

  renderWithProviders(<RouterProvider router={router} />);
};

describe('videos area', () => {
  it('shows the empty library returned by the BFF', async () => {
    renderVideosRoute(['midia.enviar']);

    expect(await screen.findByRole('heading', { name: 'Nenhum vídeo ainda' })).toBeInTheDocument();
    expect(screen.getByText('Envie a primeira gravação. Ela fica pronta para a aula sozinha.')).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Enviar vídeo' })).toHaveLength(2);
    expect(screen.getAllByRole('button', { name: 'Enviar vídeo' }).every((button) => button.hasAttribute('disabled'))).toBe(true);
  });

  it('adds the Videos menu area only for the send permission', () => {
    const allowedAreas = getStaffAreas(['midia.enviar']);

    expect(allowedAreas).toEqual([{ label: 'Vídeos', permission: 'midia.enviar', href: paths.videos.getHref() }]);
    expect(getStaffAreas(['suporte.atender']).some((area) => area.permission === 'midia.enviar')).toBe(false);
  });

  it('shows the Videos card on the teacher dashboard', () => {
    const areas = getStaffAreas(['midia.enviar']);
    renderWithProviders(<MemoryRouter><DashboardScreen areas={areas} name="Marina Alves" roles={['professor']} /></MemoryRouter>);

    expect(screen.getByRole('heading', { name: 'Vídeos' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Abrir vídeos/ })).toHaveAttribute('href', '/videos');
  });

  it('blocks direct access when the permission is missing', async () => {
    renderVideosRoute(['suporte.atender']);

    expect(await screen.findByRole('heading', { name: 'Sem permissão' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Voltar para o início' })).toHaveAttribute('href', '/');
  });
});
