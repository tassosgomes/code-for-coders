import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { beforeEach, describe, expect, it } from 'vitest';

import { requireStudentSession } from '@/app/routes/dashboard-route';
import { StudentAppLayoutRoute } from '@/app/routes/student-app-layout-route';
import { StudentMyOrdersRoute } from '@/app/routes/student-my-orders-route';
import { StudentOrderRoute } from '@/app/routes/student-order-route';
import { env } from '@/config/env';
import { paths } from '@/config/paths';
import { myOrders, myOrdersCourses, myOrdersLessonId, myOrdersPage } from '@/testing/my-orders-data';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const renderRoute = (entry = '/pedidos') => {
  const router = createMemoryRouter([
    { loader: requireStudentSession, element: <StudentAppLayoutRoute />, children: [
      { path: paths.studentMyOrders.path, element: <StudentMyOrdersRoute /> },
      { path: paths.studentOrder.path, element: <StudentOrderRoute /> },
      { path: paths.studentLesson.path, element: <h2>Aula aberta</h2> },
      { path: '/', element: <h2>Início da conta</h2> },
    ] },
    { path: paths.studentShowcase.path, element: <h1>Vitrine</h1> },
    { path: paths.studentLogin.path, element: <h1>Entrar</h1> },
  ], { initialEntries: [entry] });
  renderWithProviders(<RouterProvider router={router} />);
  return router;
};

describe('my-orders', () => {
  beforeEach(() => {
    server.use(
      http.get(`${env.API_URL}/api/v1/student-sessions/current`, () => HttpResponse.json({ accountId: '00000000-0000-7000-8000-000000000001', name: 'Ana', csrfToken: 'csrf' })),
      http.get(`${env.API_URL}/api/v1/orders`, () => HttpResponse.json(myOrdersPage)),
      http.get(`${env.API_URL}/api/v1/my-courses`, () => HttpResponse.json(myOrdersCourses)),
      http.get(`${env.API_URL}/api/v1/orders/:orderId`, ({ params }) => HttpResponse.json(myOrders.find((order) => order.orderId === params.orderId))),
    );
  });

  it('shows paid expired and highlighted pending snapshots without duplicating orders', async () => {
    renderRoute();
    const pending = await screen.findByRole('article', { name: 'Pedido #000124' });
    expect(pending).toHaveClass('border-primary');
    expect(within(pending).getByText('Aguardando pagamento')).toBeInTheDocument();
    expect(within(pending).getByText('Pague até 08/10/2026 às 23:59')).toBeInTheDocument();
    expect(within(pending).getByText('PIX')).toBeInTheDocument();
    expect(screen.getAllByRole('article')).toHaveLength(3);
    const paid = screen.getByRole('article', { name: 'Pedido #000123' });
    expect(within(paid).getByText('Pago')).toBeInTheDocument();
    expect(within(paid).getByText('R$ 497,00')).toBeInTheDocument();
    expect(within(paid).getByText('Cartão de crédito')).toBeInTheDocument();
    expect(within(paid).getByText('Acesso por 12 meses')).toBeInTheDocument();
    expect(screen.getByText('Expirado')).toBeInTheDocument();
    expect(screen.getByText('Ainda não escolhido')).toBeInTheDocument();
    expect(within(paid).getByRole('link', { name: '#000123' })).toHaveAttribute('href', paths.studentOrder.getHref(myOrders[1].orderId));
    expect(screen.getByRole('link', { name: 'Meus pedidos' })).toHaveAttribute('aria-current', 'page');
  });

  it('retomar opens the order page and its PIX instructions', async () => {
    const router = renderRoute();
    await userEvent.click(await screen.findByRole('link', { name: 'Retomar pagamento' }));
    expect(await screen.findByText('Aguardando pagamento do PIX')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe(paths.studentOrder.getHref(myOrders[0].orderId));
    expect(screen.getByRole('link', { name: '← Meus pedidos' })).toHaveAttribute('href', '/pedidos');
  });

  it('paid order opens the active course and its number still opens the order', async () => {
    const router = renderRoute();
    const courseLink = await screen.findByRole('link', { name: 'Ir para o curso' });
    expect(courseLink).toHaveAttribute('href', paths.studentLesson.getHref(myOrdersLessonId));
    await userEvent.click(courseLink);
    expect(await screen.findByRole('heading', { name: 'Aula aberta' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe(paths.studentLesson.getHref(myOrdersLessonId));
  });

  it('paid order without active access keeps a path to the order', async () => {
    server.use(http.get(`${env.API_URL}/api/v1/my-courses`, () => HttpResponse.json({ progressAvailable: true, active: [], ended: [] })));
    renderRoute();
    expect(await screen.findByRole('link', { name: 'Liberando seu acesso' })).toHaveAttribute('href', paths.studentOrder.getHref(myOrders[1].orderId));
  });

  it('empty list offers a link to the showcase', async () => {
    server.use(http.get(`${env.API_URL}/api/v1/orders`, () => HttpResponse.json({ data: [], pagination: { page: 1, size: 10, total: 0, totalPages: 0 } })));
    const router = renderRoute();
    expect(await screen.findByText('Você ainda não tem compras')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('link', { name: 'Ver cursos' }));
    expect(router.state.location.pathname).toBe('/cursos');
  });

  it('failed list offers Tentar de novo and recovers on a successful response', async () => {
    let failed = true;
    server.use(http.get(`${env.API_URL}/api/v1/orders`, () => failed ? HttpResponse.json({}, { status: 502 }) : HttpResponse.json(myOrdersPage)));
    renderRoute();
    expect(await screen.findByText('Não foi possível carregar seus pedidos agora.')).toBeInTheDocument();
    failed = false;
    await userEvent.click(screen.getByRole('button', { name: 'Tentar de novo' }));
    expect(await screen.findByRole('article', { name: 'Pedido #000124' })).toBeInTheDocument();
  });

  it('pagination updates the URL and pending orders are ordinary rows on page two', async () => {
    const requests: string[] = [];
    server.use(http.get(`${env.API_URL}/api/v1/orders`, ({ request }) => {
      const url = new URL(request.url);
      requests.push(url.search);
      const page = Number(url.searchParams.get('_page'));
      return HttpResponse.json({ data: page === 1 ? myOrders : [myOrders[0]], pagination: { page, size: 10, total: 11, totalPages: 2 } });
    }));
    const router = renderRoute();
    await userEvent.click(await screen.findByRole('button', { name: 'Próxima' }));
    await waitFor(() => expect(screen.getByText('Página 2 de 2')).toBeInTheDocument());
    expect(router.state.location.search).toBe('?_page=2');
    expect(requests).toContain('?_page=2&_size=10');
    expect(screen.getByRole('article', { name: 'Pedido #000124' })).not.toHaveClass('border-primary');
    expect(screen.getAllByRole('article')).toHaveLength(1);
    expect(screen.getByRole('button', { name: 'Próxima' })).toBeDisabled();
  });

  it('account menu also links to Meus pedidos', async () => {
    renderRoute();
    await userEvent.click(await screen.findByRole('button', { name: 'Abrir o menu da conta de Ana' }));
    expect(screen.getByRole('menuitem', { name: 'Meus pedidos' })).toHaveAttribute('href', '/pedidos');
  });

  it('anonymous student is redirected to login with returnTo', async () => {
    server.use(http.get(`${env.API_URL}/api/v1/student-sessions/current`, () => HttpResponse.json({}, { status: 401 })));
    const router = renderRoute();
    expect(await screen.findByRole('heading', { name: 'Entrar' })).toBeInTheDocument();
    expect(router.state.location.search).toBe('?returnTo=%2Fpedidos');
  });
});
