import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';

import { AdminLayoutRoute } from '@/app/routes/admin-layout-route';
import { AuthoringCourseRoute } from '@/app/routes/authoring-course-route';
import { loadStaffSession } from '@/app/routes/staff-session-loader';
import { env } from '@/config/env';
import { authoringCourseFixture } from '@/testing/authoring-course-handlers';
import { createStructureHandlers, structureCourseFixture } from '@/testing/authoring-structure-handlers';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const renderStructure = (permissions = ['autoria.ler', 'autoria.editar']) => {
  const boundary = createStructureHandlers();
  server.use(...boundary.handlers, http.get(`${env.API_URL}/api/v1/staff-sessions/current`, () => HttpResponse.json({
    accountId: '0198dfac-674a-7000-8000-000000000002', name: 'Professor', roles: ['professor'], permissions, csrfToken: 'structure-csrf',
  })));
  const router = createMemoryRouter([{ path: '/', loader: loadStaffSession, element: <AdminLayoutRoute title="Autoria" serviceName="admin-spa" />,
    children: [{ path: 'autoria/:courseId', element: <AuthoringCourseRoute /> }],
  }], { initialEntries: [`/autoria/${authoringCourseFixture.courseId}`] });
  renderWithProviders(<RouterProvider router={router} />);
  return boundary;
};
const openActions = async (user: ReturnType<typeof userEvent.setup>, title: string) => { await user.click(screen.getByLabelText(`Ações de ${title}`)); };
const fillTitle = async (user: ReturnType<typeof userEvent.setup>, title: string) => { await user.clear(screen.getByLabelText('Título *')); await user.type(screen.getByLabelText('Título *'), title); };

describe('authoring structure', () => {
  afterEach(cleanup);

  it('edits course metadata and creates and renames modules and lessons from confirmed drafts', async () => {
    const user = userEvent.setup(); const boundary = renderStructure();
    await user.click(await screen.findByRole('button', { name: 'Editar dados' }));
    expect(screen.getByLabelText('Título *')).toHaveValue(authoringCourseFixture.title);
    await fillTitle(user, 'Curso atualizado'); await user.click(screen.getByRole('button', { name: 'Salvar alterações' }));
    expect(await screen.findByRole('heading', { name: 'Curso atualizado' })).toBeInTheDocument();
    expect(await screen.findByText(/Editado por Editor confirmado/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: '+ Adicionar módulo' }));
    expect(screen.queryByLabelText('Descrição pedagógica (opcional)')).not.toBeInTheDocument();
    await user.type(screen.getByLabelText('Título *'), 'Novo módulo'); await user.click(screen.getByRole('button', { name: 'Criar módulo' }));
    await screen.findByRole('region', { name: 'Novo módulo' });
    await openActions(user, 'Novo módulo'); await user.click(screen.getByRole('button', { name: 'Editar título' }));
    await fillTitle(user, 'Persistência'); await user.click(screen.getByRole('button', { name: 'Salvar alterações' }));
    await user.click(await screen.findByRole('button', { name: '+ Aula em Persistência' }));
    await user.type(screen.getByLabelText('Título *'), 'Banco'); await user.type(screen.getByLabelText('Descrição pedagógica (opcional)'), 'Aprenda dados');
    await user.click(screen.getByRole('button', { name: 'Criar aula' }));
    await screen.findByRole('article', { name: 'Banco' });
    await openActions(user, 'Banco'); await user.click(screen.getByRole('button', { name: 'Editar aula' }));
    await fillTitle(user, 'PostgreSQL'); await user.click(screen.getByRole('button', { name: 'Salvar alterações' }));
    expect(await screen.findByRole('article', { name: 'PostgreSQL' })).toHaveTextContent('Aprenda dados');
    expect(boundary.requests).toHaveLength(5);
    expect(boundary.requests.every((request) => request.key && request.csrf === 'structure-csrf')).toBe(true);
  });

  it('keyboard commands reorder modules and lessons and move between modules with IDs and focus preserved', async () => {
    const user = userEvent.setup(); const boundary = renderStructure();
    await screen.findByRole('region', { name: 'Coleções' });
    await openActions(user, 'Fundamentos');
    screen.getByLabelText('Ações de Coleções').focus(); await user.keyboard('{Enter}');
    const up = screen.getByRole('button', { name: 'Mover para cima' }); up.focus(); await user.keyboard('{Enter}');
    await waitFor(() => expect(screen.getByRole('region', { name: 'Coleções' })).toHaveFocus());
    expect(screen.getByRole('heading', { name: '1 Coleções' })).toBeInTheDocument();
    await openActions(user, 'Fluxo');
    within(screen.getByRole('article', { name: 'Fluxo' })).getByRole('button', { name: 'Mover para cima' }).focus(); await user.keyboard('{Enter}');
    await waitFor(() => expect(screen.getByRole('article', { name: 'Fluxo' })).toHaveFocus());
    expect(screen.getByRole('heading', { name: '1 Fluxo' })).toBeInTheDocument();
    await openActions(user, 'Fluxo');
    await user.click(within(screen.getByRole('article', { name: 'Fluxo' })).getByRole('button', { name: /Mover para outro módulo/ })); within(screen.getByRole('article', { name: 'Fluxo' })).getByRole('button', { name: 'Mover para Coleções' }).focus(); await user.keyboard('{Enter}');
    await waitFor(() => expect(screen.getByRole('article', { name: 'Fluxo' })).toHaveFocus());
    expect(within(screen.getByRole('region', { name: 'Coleções' })).getByRole('article', { name: 'Fluxo' })).toHaveAttribute('id', `aula-${structureCourseFixture.modules[0]?.lessons[1]?.lessonId}`);
    expect(boundary.snapshot().modules[1]?.lessons[0]?.position).toBe(1);
  });

  it('confirms module lesson count, cancellation does not write, and removed lesson recreation has a new ID', async () => {
    const user = userEvent.setup(); const boundary = renderStructure();
    await screen.findByRole('region', { name: 'Fundamentos' });
    await openActions(user, 'Fundamentos'); await user.click(screen.getByRole('button', { name: 'Remover módulo…' }));
    expect(screen.getByRole('dialog')).toHaveTextContent('As 2 aulas deste módulo');
    await user.click(screen.getByRole('button', { name: 'Cancelar' })); expect(boundary.requests).toHaveLength(0);
    await openActions(user, 'Tipos'); await user.click(screen.getByRole('button', { name: 'Remover aula…' }));
    await user.click(screen.getByRole('button', { name: 'Remover aula' }));
    await waitFor(() => expect(screen.queryByRole('article', { name: 'Tipos' })).not.toBeInTheDocument());
    await user.click(screen.getByRole('button', { name: '+ Aula em Fundamentos' })); await user.type(screen.getByLabelText('Título *'), 'Tipos');
    await user.click(screen.getByRole('button', { name: 'Criar aula' }));
    expect(await screen.findByRole('article', { name: 'Tipos' })).not.toHaveAttribute('id', `aula-${structureCourseFixture.modules[0]?.lessons[0]?.lessonId}`);
    await openActions(user, 'Fundamentos');
    await user.click(within(screen.getByRole('region', { name: 'Fundamentos' })).getByRole('button', { name: 'Remover módulo…' }));
    await user.click(screen.getByRole('button', { name: 'Remover módulo' }));
    await waitFor(() => expect(screen.queryByRole('region', { name: 'Fundamentos' })).not.toBeInTheDocument());
    expect(screen.getByRole('heading', { name: '1 Coleções' })).toBeInTheDocument();
  });

  it('failed writes retain the form and reuse the same intention on retry', async () => {
    const user = userEvent.setup(); const boundary = renderStructure(); boundary.setFail(true);
    await user.click(await screen.findByRole('button', { name: '+ Adicionar módulo' }));
    await user.type(screen.getByLabelText('Título *'), 'Rede'); await user.click(screen.getByRole('button', { name: 'Criar módulo' }));
    expect(await screen.findByText(/Seus dados foram mantidos/)).toBeInTheDocument();
    expect(screen.getByLabelText('Título *')).toHaveValue('Rede'); expect(screen.queryByRole('region', { name: 'Rede' })).not.toBeInTheDocument();
    boundary.setFail(false); await user.click(screen.getByRole('button', { name: 'Criar módulo' }));
    expect(await screen.findByRole('region', { name: 'Rede' })).toBeInTheDocument();
    expect(boundary.requests).toHaveLength(2); expect(boundary.requests[0]?.key).toBe(boundary.requests[1]?.key);
  });

  it('blank title validation keeps the dialog and does not call the server', async () => {
    const user = userEvent.setup(); const boundary = renderStructure();
    await user.click(await screen.findByRole('button', { name: '+ Adicionar módulo' }));
    await user.click(screen.getByRole('button', { name: 'Criar módulo' }));
    expect(await screen.findByText('Informe o título.')).toBeInTheDocument(); expect(boundary.requests).toHaveLength(0);
  });

  it('readers see the ordered curriculum without mutation controls', async () => {
    renderStructure(['autoria.ler']);
    expect(await screen.findByRole('heading', { name: '1 Fundamentos' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: '2 Fluxo' })).toBeInTheDocument();
    expect(screen.getByText(/Somente leitura/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Editar|Adicionar|Arrastar|Aula/ })).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Ações de Tipos')).not.toBeInTheDocument();
  });
});
