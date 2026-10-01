import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';

import { routes } from '@/app/app-routes';
import { env } from '@/config/env';
import { createCatalogOfferHandlers, catalogOfferFixture } from '@/testing/catalog-offer-handlers';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const postEndpoint = `${env.API_URL}/api/v1/catalog/courses/:courseId/offers`;
const renderRecord = (initial: Parameters<typeof createCatalogOfferHandlers>[0] = []) => {
  const model = createCatalogOfferHandlers(initial); server.use(...model.handlers,
    http.get(`${env.API_URL}/api/v1/staff-sessions/current`, () => HttpResponse.json({
      accountId: '0198dfac-674a-7000-8000-000000000002', name: 'Financeiro', roles: ['financeiro'], permissions: ['oferta.editar'], csrfToken: 'offer-csrf',
    })));
  renderWithProviders(<RouterProvider router={createMemoryRouter(routes, { initialEntries: [`/catalogo/${catalogOfferFixture.courseId}`] })} />);
  return model;
};
const fill = async (user: ReturnType<typeof userEvent.setup>, name = 'Acesso por 12 meses', price = '497,00') => {
  await user.click(await screen.findByRole('button', { name: '+ Nova oferta' }));
  await user.type(screen.getByRole('textbox', { name: 'Nome da opção' }), name);
  await user.type(screen.getByRole('textbox', { name: 'Preço (R$)' }), price);
};

describe('catalog offer form', () => {
  afterEach(cleanup);

  it('creates both monthly and lifetime drafts with exact integer cents and shows both promises', async () => {
    const user = userEvent.setup(); const model = renderRecord(); await fill(user);
    await user.click(screen.getByRole('button', { name: 'Salvar rascunho' })); await screen.findByText('Rascunho salvo.');
    expect(model.writes[0]?.body).toEqual({ name: 'Acesso por 12 meses', priceCents: 49700, accessPeriod: { type: 'months', months: 12 } });
    await fill(user, 'Acesso vitalício', '897,00'); await user.click(screen.getByRole('radio', { name: 'Vitalícia' }));
    expect(screen.queryByRole('textbox', { name: 'Meses' })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Salvar rascunho' })); await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(model.writes[1]?.body).toEqual({ name: 'Acesso vitalício', priceCents: 89700, accessPeriod: { type: 'lifetime' } });
    expect(await screen.findByText('Acesso vitalício')).toBeInTheDocument(); expect(screen.getByText('R$ 497,00 ·')).toBeInTheDocument(); expect(screen.getByText('R$ 897,00 ·')).toBeInTheDocument();
    expect(model.writes[0]?.key).toBeTruthy(); expect(model.writes[0]?.key).not.toBe(model.writes[1]?.key);
  });

  it.each([['0,01', '1', 1], ['99999,99', '60', 9999999]])('accepts inclusive price %s and period %s limits', async (price, months, cents) => {
    const user = userEvent.setup(); const model = renderRecord(); await fill(user, 'Limit', price);
    await user.clear(screen.getByRole('textbox', { name: 'Meses' })); await user.type(screen.getByRole('textbox', { name: 'Meses' }), months);
    await user.click(screen.getByRole('button', { name: 'Salvar rascunho' })); await screen.findByText('Rascunho salvo.');
    expect(model.writes[0]?.body).toEqual({ name: 'Limit', priceCents: cents, accessPeriod: { type: 'months', months: Number(months) } });
  });

  it('rejects more than two decimal places without sending a write', async () => {
    const user = userEvent.setup(); const model = renderRecord(); await fill(user, 'Option', '497,005');
    await user.click(screen.getByRole('button', { name: 'Salvar rascunho' }));
    expect(await screen.findByText('Use até duas casas decimais.')).toBeInTheDocument(); expect(model.writes).toHaveLength(0);
    expect(screen.getByRole('textbox', { name: 'Preço (R$)' })).toHaveAttribute('aria-invalid', 'true');
  });

  it.each(['0', '-1', '100000,00'])('rejects price %s and keeps the error at the price', async (price) => {
    const user = userEvent.setup(); const model = renderRecord(); await fill(user, 'Option', price); await user.click(screen.getByRole('button', { name: 'Salvar rascunho' }));
    expect(await screen.findByText('Informe um valor entre R$ 0,01 e R$ 99.999,99.')).toBeInTheDocument(); expect(model.writes).toHaveLength(0);
  });

  it.each(['0', '1,5', '61'])('rejects months %s at the months field', async (months) => {
    const user = userEvent.setup(); const model = renderRecord(); await fill(user);
    await user.clear(screen.getByRole('textbox', { name: 'Meses' })); await user.type(screen.getByRole('textbox', { name: 'Meses' }), months);
    await user.click(screen.getByRole('button', { name: 'Salvar rascunho' })); expect(await screen.findByText('Informe de 1 a 60 meses inteiros.')).toBeInTheDocument(); expect(model.writes).toHaveLength(0);
  });

  it('edits a draft and reloads its price and lifetime promise', async () => {
    const user = userEvent.setup(); const model = renderRecord([{ ...catalogOfferFixture, accessPeriod: { type: 'months', months: 12 } }]);
    await user.click(await screen.findByRole('button', { name: `Ações de ${catalogOfferFixture.name}` }));
    await user.click(screen.getByRole('button', { name: 'Editar' })); expect(screen.getByRole('textbox', { name: 'Preço (R$)' })).toHaveValue('497,00');
    await user.clear(screen.getByRole('textbox', { name: 'Preço (R$)' })); await user.type(screen.getByRole('textbox', { name: 'Preço (R$)' }), '897,00');
    await user.click(screen.getByRole('radio', { name: 'Vitalícia' })); await user.click(screen.getByRole('button', { name: 'Salvar rascunho' }));
    expect(await screen.findByText('R$ 897,00 ·')).toBeInTheDocument(); expect(model.writes[0]?.method).toBe('PATCH');
    expect(model.writes[0]?.body).toEqual({ name: catalogOfferFixture.name, priceCents: 89700, accessPeriod: { type: 'lifetime' } });
  });

  it('requires deletion confirmation, supports cancel and removes only the confirmed draft', async () => {
    const user = userEvent.setup(); const model = renderRecord([{ ...catalogOfferFixture, accessPeriod: { type: 'months', months: 12 } }]);
    await user.click(await screen.findByRole('button', { name: `Ações de ${catalogOfferFixture.name}` }));
    await user.click(screen.getByRole('button', { name: 'Excluir' })); expect(screen.getByRole('alertdialog')).toHaveTextContent('Esta ação não pode ser desfeita.'); expect(model.writes).toHaveLength(0);
    await user.click(screen.getByRole('button', { name: 'Cancelar' })); expect(model.writes).toHaveLength(0);
    await user.click(screen.getByRole('button', { name: `Ações de ${catalogOfferFixture.name}` }));
    await user.click(screen.getByRole('button', { name: 'Excluir' })); await user.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Excluir' }));
    expect(await screen.findByText('Nenhuma oferta ainda. Crie a primeira para colocar o curso à venda.')).toBeInTheDocument();
    expect(model.writes[0]?.method).toBe('DELETE'); expect(model.writes[0]?.key).toBeTruthy();
  });

  it.each([
    ['name', 'Nome da opção', 'Informe um nome de 1 a 60 caracteres.'],
    ['priceCents', 'Preço (R$)', 'Informe um valor entre R$ 0,01 e R$ 99.999,99.'],
    ['accessPeriod', 'Meses', 'Informe de 1 a 60 meses inteiros.'],
  ])('shows server FIELD_INVALID for %s at its named field', async (field, label, message) => {
    const user = userEvent.setup(); renderRecord(); server.use(http.post(postEndpoint, () => HttpResponse.json({ code: 'FIELD_INVALID', detail: `${field} is invalid.` }, { status: 422 })));
    await fill(user); await user.click(screen.getByRole('button', { name: 'Salvar rascunho' }));
    expect(await screen.findByText(message)).toBeInTheDocument(); expect(screen.getByRole('textbox', { name: label })).toHaveAttribute('aria-invalid', 'true');
  });

  it('shows the course limit and preserves the form', async () => {
    const user = userEvent.setup(); renderRecord(); server.use(http.post(postEndpoint, () => HttpResponse.json({ code: 'FIELD_INVALID', detail: 'offers cannot exceed 50 per course.' }, { status: 422 })));
    await fill(user); await user.click(screen.getByRole('button', { name: 'Salvar rascunho' }));
    expect(await screen.findByText('O curso já tem 50 ofertas. Exclua um rascunho primeiro.')).toBeInTheDocument(); expect(screen.getByRole('textbox', { name: 'Preço (R$)' })).toHaveValue('497,00');
  });

  it('retries an uncertain write with the same key and changes the key for another body', async () => {
    const user = userEvent.setup(); renderRecord(); const keys: (string | null)[] = [];
    server.use(http.post(postEndpoint, ({ request }) => { keys.push(request.headers.get('Idempotency-Key')); expect(request.headers.get('X-CSRF-Token')).toBe('offer-csrf'); return HttpResponse.json({ code: 'COMMERCE_TIMEOUT' }, { status: 504 }); }));
    await fill(user); await user.click(screen.getByRole('button', { name: 'Salvar rascunho' })); await screen.findByRole('alert');
    await user.click(screen.getByRole('button', { name: 'Salvar rascunho' })); await waitFor(() => expect(keys).toHaveLength(2)); expect(keys[0]).toBe(keys[1]);
    await screen.findByRole('alert'); await user.type(screen.getByRole('textbox', { name: 'Nome da opção' }), ' novo');
    await user.click(screen.getByRole('button', { name: 'Salvar rascunho' })); await waitFor(() => expect(keys).toHaveLength(3)); expect(keys[2]).not.toBe(keys[0]);
  });

  it('lists drafts first and offers deletion only for drafts', async () => {
    renderRecord([{ ...catalogOfferFixture, name: 'Published', status: 'published', accessPeriod: { type: 'lifetime' } },
      { ...catalogOfferFixture, offerId: '0198dfac-674a-7000-8000-000000000052', accessPeriod: { type: 'months', months: 12 } },
      { ...catalogOfferFixture, offerId: '0198dfac-674a-7000-8000-000000000053', name: 'Unpublished', status: 'unpublished', accessPeriod: { type: 'lifetime' } }]);
    await screen.findByText('Published'); const rows = screen.getAllByRole('listitem').filter((row) => row.querySelector('h3'));
    expect(rows[0]).toHaveTextContent(catalogOfferFixture.name); expect(screen.getAllByRole('button', { name: `Ações de ${catalogOfferFixture.name}` })).toHaveLength(1);
    expect(within(rows[1]!).queryByRole('button', { name: 'Excluir' })).not.toBeInTheDocument();
  });
});
