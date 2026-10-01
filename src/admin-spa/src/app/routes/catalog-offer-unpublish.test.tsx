import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';

import { routes } from '@/app/app-routes';
import { env } from '@/config/env';
import { catalogOfferFixture } from '@/testing/catalog-offer-handlers';
import { createOfferUnpublishHandlers } from '@/testing/catalog-offer-unpublish-handlers';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const endpoint = `${env.API_URL}/api/v1/catalog/offers/:offerId/unpublish`;
const renderRecord = (offerCount: 1 | 2 = 1) => {
  const model = createOfferUnpublishHandlers(offerCount); server.use(...model.handlers);
  renderWithProviders(<RouterProvider router={createMemoryRouter(routes, { initialEntries: [`/catalogo/${catalogOfferFixture.courseId}`] })} />);
  return model;
};
const openMenu = async (user: ReturnType<typeof userEvent.setup>, name = 'Acesso por 12 meses') => {
  await user.click(await screen.findByRole('button', { name: `Ações de ${name}` }));
};
const open = async (user: ReturnType<typeof userEvent.setup>) => {
  await openMenu(user); await user.click(screen.getByRole('button', { name: 'Despublicar' }));
  return within(screen.getByRole('alertdialog'));
};

describe('catalog offer unpublication', () => {
  afterEach(cleanup);

  it('asks for confirmation, supports cancellation, and then shows the offer as unpublished without Excluir', async () => {
    const user = userEvent.setup(); const model = renderRecord(2); const dialog = await open(user);
    expect(dialog.getByText('Despublicar “Acesso por 12 meses”?')).toBeInTheDocument();
    expect(dialog.getByText(/Quem já comprou não é afetado/)).toBeInTheDocument();
    expect(dialog.queryByText('O curso também sai da vitrine.')).not.toBeInTheDocument();
    await user.click(dialog.getByRole('button', { name: 'Cancelar' })); expect(model.keys).toHaveLength(0);
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    await openMenu(user); await user.click(screen.getByRole('button', { name: 'Despublicar' }));
    await user.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Despublicar' }));
    expect(await screen.findByText('Oferta despublicada.')).toBeInTheDocument();
    expect(screen.getByText('– Despublicada')).toBeInTheDocument(); expect(model.keys).toHaveLength(1); expect(model.keys[0]).toBeTruthy();
    expect(screen.getByText('✓ Na vitrine')).toBeInTheDocument();
    await openMenu(user);
    expect(screen.getByRole('button', { name: 'Publicar de novo' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Excluir' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Despublicar' })).not.toBeInTheDocument();
  });

  it('warns that unpublishing the last published offer removes the course from the showcase and refreshes it', async () => {
    const user = userEvent.setup(); renderRecord(1); expect(await screen.findByText('✓ Na vitrine')).toBeInTheDocument();
    const dialog = await open(user); expect(dialog.getByText('O curso também sai da vitrine.')).toBeInTheDocument();
    await user.click(dialog.getByRole('button', { name: 'Despublicar' }));
    expect(await screen.findByText('– Fora da vitrine')).toBeInTheDocument(); expect(screen.getByText('– Despublicada')).toBeInTheDocument();
  });

  it('keeps the offer published, shows the conflict, and retries an uncertain unpublication with the same key and CSRF', async () => {
    const user = userEvent.setup(); renderRecord(); const keys: (string | null)[] = []; let attempt = 0;
    server.use(http.post(endpoint, ({ request }) => {
      keys.push(request.headers.get('Idempotency-Key')); expect(request.headers.get('X-CSRF-Token')).toBe('unpublish-csrf');
      return ++attempt === 1 ? HttpResponse.json({ code: 'COMMERCE_TIMEOUT' }, { status: 504 })
        : HttpResponse.json({ code: 'OFFER_STATE_CONFLICT' }, { status: 422 });
    }));
    const dialog = await open(user); await user.click(dialog.getByRole('button', { name: 'Despublicar' }));
    await dialog.findByText('Não foi possível despublicar agora.'); await user.click(dialog.getByRole('button', { name: 'Tentar de novo' }));
    expect(await dialog.findByText('Esta oferta não está mais publicada. Atualize a ficha do curso.')).toBeInTheDocument();
    await waitFor(() => expect(keys).toHaveLength(2)); expect(keys[0]).toBeTruthy(); expect(keys[1]).toBe(keys[0]);
    expect(screen.getByText('✓ Publicada')).toBeInTheDocument();
  });
});
