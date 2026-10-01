import { cleanup, screen, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';

import { routes } from '@/app/app-routes';
import { env } from '@/config/env';
import { catalogCourseRecordFixture } from '@/testing/catalog-course-record-handlers';
import { catalogOfferFixture } from '@/testing/catalog-offer-handlers';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const renderRecord = (counts: number[]) => {
  server.use(http.get(`${env.API_URL}/api/v1/staff-sessions/current`, () => HttpResponse.json({ accountId: '0198dfac-674a-7000-8000-000000000002', name: 'Financeiro', roles: ['financeiro'], permissions: ['oferta.editar'], csrfToken: 'csrf' })),
    http.get(`${env.API_URL}/api/v1/catalog/courses/:courseId`, () => HttpResponse.json({ ...catalogCourseRecordFixture, level: 'beginner', offers: counts.map((count, index) => ({ ...catalogOfferFixture, offerId: `0198dfac-674a-7000-8000-00000000004${index + 2}`, name: index === 0 ? 'Mensal' : 'Vitalício', purchaseIntentCount: count })) })));
  renderWithProviders(<RouterProvider router={createMemoryRouter(routes, { initialEntries: [`/catalogo/${catalogCourseRecordFixture.courseId}`] })} />);
};

describe('catalog purchase intents', () => {
  afterEach(cleanup);
  it('shows each offer total and the course total after clicks on the lifetime option', async () => {
    renderRecord([0, 14]); const lifetime = await screen.findByRole('heading', { name: 'Vitalício' });
    const monthly = screen.getByRole('heading', { name: 'Mensal' });
    expect(within(lifetime.closest('li')!).getByText(/14 cliques em Comprar/)).toBeInTheDocument();
    expect(within(monthly.closest('li')!).getByText(/Nenhum clique em Comprar ainda/)).toBeInTheDocument();
    expect(screen.getAllByText(/14 cliques em Comprar/)).toHaveLength(2);
  });
  it('shows totals across all offers including unpublished history and zero before any click', async () => {
    renderRecord([5, 14]); await screen.findByRole('heading', { name: 'Vitalício' });
    expect(screen.getByText('19 cliques em Comprar')).toBeInTheDocument();
    expect(screen.getByText(/5 cliques em Comprar/)).toBeInTheDocument();
  });
});
