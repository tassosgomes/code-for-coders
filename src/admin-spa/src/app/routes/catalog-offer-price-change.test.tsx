import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';

import { routes } from '@/app/app-routes';
import { env } from '@/config/env';
import { catalogOfferFixture, createCatalogOfferHandlers } from '@/testing/catalog-offer-handlers';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const renderRecord = (status = 'published') => {
  const model = createCatalogOfferHandlers([{ ...catalogOfferFixture, status, accessPeriod: { type: 'months', months: 12 } }]);
  server.use(...model.handlers, http.get(`${env.API_URL}/api/v1/staff-sessions/current`, () => HttpResponse.json({
    accountId: '0198dfac-674a-7000-8000-000000000002', name: 'Financeiro', roles: ['financeiro'], permissions: ['oferta.editar'], csrfToken: 'offer-csrf',
  })));
  renderWithProviders(<RouterProvider router={createMemoryRouter(routes, { initialEntries: [`/catalogo/${catalogOfferFixture.courseId}`] })} />);
  return model;
};
const open = async (user: ReturnType<typeof userEvent.setup>) => {
  await user.click(await screen.findByRole('button', { name: `Ações de ${catalogOfferFixture.name}` }));
  await user.click(screen.getByRole('button', { name: 'Editar' }));
  return within(screen.getByRole('dialog'));
};
const changePrice = async (user: ReturnType<typeof userEvent.setup>, price = '397,00') => {
  await user.clear(screen.getByLabelText('Preço (R$)')); await user.type(screen.getByLabelText('Preço (R$)'), price);
};

describe('catalog offer price and period changes', () => {
  afterEach(cleanup);

  it('reviews before and after, returns to retained input, and confirms future purchases', async () => {
    const user = userEvent.setup(); const model = renderRecord(); const dialog = await open(user); await changePrice(user);
    await user.click(dialog.getByRole('button', { name: 'Revisar alteração' }));
    expect(dialog.getByRole('heading', { name: 'Confirmar alteração' })).toBeInTheDocument();
    expect(within(dialog.getByRole('region', { name: 'Antes' })).getByText('R$ 497,00')).toBeInTheDocument();
    expect(within(dialog.getByRole('region', { name: 'Depois' })).getByText('R$ 397,00')).toBeInTheDocument();
    expect(dialog.getByText('Vale para compras futuras e não altera quem já comprou.')).toBeInTheDocument(); expect(model.writes).toHaveLength(0);
    await user.click(dialog.getByRole('button', { name: 'Voltar e editar' }));
    expect(dialog.getByLabelText('Preço (R$)')).toHaveValue('397,00'); expect(model.writes).toHaveLength(0);
    await user.click(dialog.getByRole('button', { name: 'Revisar alteração' })); await user.click(dialog.getByRole('button', { name: 'Confirmar' }));
    await screen.findByText('Oferta salva.'); expect(screen.getByText('✓ Publicada')).toBeInTheDocument();
    expect(model.writes).toHaveLength(1); expect(model.writes[0]?.body).toMatchObject({ priceCents: 39700, accessPeriod: { type: 'months', months: 12 } });
  });

  it('requires confirmation for lifetime access when price stays the same', async () => {
    const user = userEvent.setup(); const model = renderRecord(); const dialog = await open(user);
    await user.click(dialog.getByRole('radio', { name: 'Vitalícia' })); await user.click(dialog.getByRole('button', { name: 'Revisar alteração' }));
    expect(within(dialog.getByRole('region', { name: 'Antes' })).getByText('12 meses')).toBeInTheDocument();
    expect(within(dialog.getByRole('region', { name: 'Depois' })).getByText('Vitalícia')).toBeInTheDocument();
    expect(dialog.getAllByText('R$ 497,00')).toHaveLength(2); expect(model.writes).toHaveLength(0);
    await user.click(dialog.getByRole('button', { name: 'Confirmar' })); await screen.findByText('Oferta salva.');
    expect(model.writes[0]?.body).toMatchObject({ priceCents: 49700, accessPeriod: { type: 'lifetime' } });
  });

  it('requires confirmation when only monthly duration changes', async () => {
    const user = userEvent.setup(); const model = renderRecord(); const dialog = await open(user);
    await user.clear(dialog.getByLabelText('Meses')); await user.type(dialog.getByLabelText('Meses'), '6');
    await user.click(dialog.getByRole('button', { name: 'Revisar alteração' }));
    expect(within(dialog.getByRole('region', { name: 'Depois' })).getByText('6 meses')).toBeInTheDocument();
    await user.click(dialog.getByRole('button', { name: 'Confirmar' })); await screen.findByText('Oferta salva.');
    expect(model.writes[0]?.body).toMatchObject({ accessPeriod: { type: 'months', months: 6 } });
  });

  it('saves a name change directly without confirmation', async () => {
    const user = userEvent.setup(); const model = renderRecord(); const dialog = await open(user);
    await user.clear(dialog.getByLabelText('Nome da opção')); await user.type(dialog.getByLabelText('Nome da opção'), 'Outro nome');
    await user.click(dialog.getByRole('button', { name: 'Salvar' })); await screen.findByText('Oferta salva.');
    expect(screen.queryByRole('heading', { name: 'Confirmar alteração' })).not.toBeInTheDocument();
    expect(model.writes).toHaveLength(1); expect(model.writes[0]?.body).toMatchObject({ name: 'Outro nome', priceCents: 49700 });
  });

  it('compares parsed prices so identical promises save without confirmation', async () => {
    const user = userEvent.setup(); const model = renderRecord(); const dialog = await open(user); await changePrice(user, '497');
    await user.click(dialog.getByRole('button', { name: 'Salvar' })); await screen.findByText('Oferta salva.');
    expect(screen.queryByRole('heading', { name: 'Confirmar alteração' })).not.toBeInTheDocument();
    expect(model.writes[0]?.body).toMatchObject({ priceCents: 49700, accessPeriod: { type: 'months', months: 12 } });
  });

  it.each(['draft', 'unpublished'])('edits %s terms without future purchase confirmation', async (status) => {
    const user = userEvent.setup(); const model = renderRecord(status); const dialog = await open(user); await changePrice(user);
    await user.click(dialog.getByRole('button', { name: status === 'draft' ? 'Salvar rascunho' : 'Salvar' }));
    await screen.findByText(status === 'draft' ? 'Rascunho salvo.' : 'Oferta salva.');
    expect(screen.queryByRole('heading', { name: 'Confirmar alteração' })).not.toBeInTheDocument();
    expect(screen.queryByText(/compras futuras/i)).not.toBeInTheDocument(); expect(model.writes).toHaveLength(1);
  });

  it('retries an uncertain confirmed change with the same key and CSRF', async () => {
    const user = userEvent.setup(); renderRecord(); const keys: (string | null)[] = [];
    server.use(http.patch(`${env.API_URL}/api/v1/catalog/offers/:offerId`, ({ request }) => {
      keys.push(request.headers.get('Idempotency-Key')); expect(request.headers.get('X-CSRF-Token')).toBeTruthy();
      return HttpResponse.json({ code: 'COMMERCE_TIMEOUT' }, { status: 504 });
    }));
    const dialog = await open(user); await changePrice(user); await user.click(dialog.getByRole('button', { name: 'Revisar alteração' }));
    await user.click(dialog.getByRole('button', { name: 'Confirmar' })); await dialog.findByRole('alert');
    await user.click(dialog.getByRole('button', { name: 'Tentar de novo' })); await waitFor(() => expect(keys).toHaveLength(2));
    expect(keys[0]).toBeTruthy(); expect(keys[1]).toBe(keys[0]);
  });

  it('sends combined changes once while confirmation is pending', async () => {
    const user = userEvent.setup(); renderRecord(); let calls = 0; let release: (() => void) | undefined;
    const pending = new Promise<void>((resolve) => { release = resolve; });
    server.use(http.patch(`${env.API_URL}/api/v1/catalog/offers/:offerId`, async ({ request }) => {
      calls++; const body = await request.json(); await pending;
      return HttpResponse.json({ ...catalogOfferFixture, ...(body as object), status: 'published' });
    }));
    const dialog = await open(user); await changePrice(user); await user.click(dialog.getByRole('radio', { name: 'Vitalícia' }));
    await user.click(dialog.getByRole('button', { name: 'Revisar alteração' })); await user.dblClick(dialog.getByRole('button', { name: 'Confirmar' }));
    expect(dialog.getByRole('button', { name: 'Salvando…' })).toBeDisabled(); expect(calls).toBe(1);
    release?.(); await screen.findByText('Oferta salva.'); expect(calls).toBe(1);
  });
});
