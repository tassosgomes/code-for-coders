import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';

import { routes } from '@/app/app-routes';
import { env } from '@/config/env';
import { catalogOfferFixture } from '@/testing/catalog-offer-handlers';
import { createOfferPublicationHandlers } from '@/testing/catalog-offer-publication-handlers';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const endpoint = `${env.API_URL}/api/v1/catalog/offers/:offerId/publish`;
const renderRecord = (status = 'draft', lifetime = false) => {
  const model = createOfferPublicationHandlers(status, lifetime); server.use(...model.handlers);
  renderWithProviders(<RouterProvider router={createMemoryRouter(routes, { initialEntries: [`/catalogo/${catalogOfferFixture.courseId}`] })} />);
  return model;
};
const open = async (user: ReturnType<typeof userEvent.setup>, label = 'Publicar') => {
  await user.click(await screen.findByRole('button', { name: `Ações de ${catalogOfferFixture.name}` }));
  await user.click(screen.getByRole('button', { name: label }));
  return within(screen.getByRole('dialog'));
};

describe('catalog offer publication', () => {
  afterEach(cleanup);

  it('confirms the exact monthly card, supports cancellation, and then shows published', async () => {
    const user = userEvent.setup(); const model = renderRecord(); let dialog = await open(user);
    expect(dialog.getByText(catalogOfferFixture.name)).toBeInTheDocument();
    expect(dialog.getByText('R$ 497,00')).toBeInTheDocument();
    expect(dialog.getByText('Acesso por 12 meses, contados a partir da liberação')).toBeInTheDocument();
    expect(dialog.queryByRole('button', { name: 'Comprar' })).not.toBeInTheDocument(); expect(model.keys).toHaveLength(0);
    await user.click(dialog.getByRole('button', { name: 'Cancelar' })); expect(model.keys).toHaveLength(0);
    dialog = await open(user); await user.click(dialog.getByRole('button', { name: 'Publicar' }));
    expect(await screen.findByText('Oferta publicada.')).toBeInTheDocument();
    expect(screen.getByText('✓ Publicada')).toBeInTheDocument(); expect(model.keys).toHaveLength(1); expect(model.keys[0]).toBeTruthy();
  });

  it('shows the lifetime promise when republishing an unpublished offer', async () => {
    const user = userEvent.setup(); const model = renderRecord('unpublished', true); const dialog = await open(user, 'Publicar de novo');
    expect(dialog.getByText('Acesso vitalício, sem data de término')).toBeInTheDocument();
    await user.click(dialog.getByRole('button', { name: 'Publicar' })); await screen.findByText('✓ Publicada'); expect(model.keys).toHaveLength(1);
  });

  it('keeps the draft and shows the missing course level beside the publication action', async () => {
    const user = userEvent.setup(); renderRecord();
    server.use(http.post(endpoint, () => HttpResponse.json({ code: 'COURSE_LEVEL_REQUIRED' }, { status: 422 })));
    const dialog = await open(user); await user.click(dialog.getByRole('button', { name: 'Publicar' }));
    expect(await dialog.findByRole('alert')).toHaveTextContent('O curso não declara nível. O professor precisa declarar o nível e publicar o curso.');
    expect(screen.getByText('Rascunho')).toBeInTheDocument(); expect(screen.queryByText('✓ Publicada')).not.toBeInTheDocument();
  });

  it('retries an uncertain publication with the same key and forwards CSRF', async () => {
    const user = userEvent.setup(); renderRecord(); const keys: (string | null)[] = [];
    server.use(http.post(endpoint, ({ request }) => {
      keys.push(request.headers.get('Idempotency-Key')); expect(request.headers.get('X-CSRF-Token')).toBe('publication-csrf');
      return HttpResponse.json({ code: 'COMMERCE_TIMEOUT' }, { status: 504 });
    }));
    const dialog = await open(user); await user.click(dialog.getByRole('button', { name: 'Publicar' }));
    await dialog.findByText('Não foi possível publicar agora.'); await user.click(dialog.getByRole('button', { name: 'Tentar de novo' }));
    await waitFor(() => expect(keys).toHaveLength(2)); expect(keys[0]).toBeTruthy(); expect(keys[1]).toBe(keys[0]);
  });

  it('blocks duplicate clicks while publication is pending', async () => {
    const user = userEvent.setup(); renderRecord(); let calls = 0; let release: (() => void) | undefined;
    const pending = new Promise<void>((resolve) => { release = resolve; });
    server.use(http.post(endpoint, async () => {
      calls++; await pending; return HttpResponse.json({ ...catalogOfferFixture, status: 'published' });
    }));
    const dialog = await open(user); await user.dblClick(dialog.getByRole('button', { name: 'Publicar' }));
    expect(dialog.getByRole('button', { name: 'Publicando…' })).toBeDisabled(); expect(calls).toBe(1);
    release?.(); await screen.findByText('Oferta publicada.'); expect(calls).toBe(1);
  });
});
