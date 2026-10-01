import { cleanup, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';

import { routes } from '@/app/app-routes';
import { env } from '@/config/env';
import { catalogCourseRecordFixture, catalogCourseRecordHandlers } from '@/testing/catalog-course-record-handlers';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const renderRecord = (permissions = ['financeiro.ler', 'oferta.editar']) => {
  server.use(...catalogCourseRecordHandlers, http.get(`${env.API_URL}/api/v1/staff-sessions/current`, () => HttpResponse.json({
    accountId: '0198dfac-674a-7000-8000-000000000002', name: 'Financeiro', roles: ['financeiro'], permissions, csrfToken: 'catalog-csrf',
  })));
  const router = createMemoryRouter(routes, { initialEntries: [`/catalogo/${catalogCourseRecordFixture.courseId}`] });
  renderWithProviders(<RouterProvider router={router} />);
};
const endpoint = `${env.API_URL}/api/v1/catalog/courses/:courseId`;

describe('catalog course record', () => {
  afterEach(cleanup);

  it('shows read-only professor data, missing-level warning, guidance, counter and empty offers', async () => {
    renderRecord(); expect(await screen.findByRole('heading', { name: 'Fundamentos de C#' })).toBeInTheDocument();
    expect(screen.getByText('Conhecimentos básicos de programação.')).toBeInTheDocument(); expect(screen.getByText('Lógica atualizada')).toBeInTheDocument();
    expect(screen.getByRole('note')).toHaveTextContent('Nenhuma oferta pode ser publicada até o professor declarar o nível');
    expect(screen.getByText('Descreva o que a pessoa vai aprender. Não prometa exclusividade de conteúdo nem proteção contra cópia.')).toBeInTheDocument();
    expect(screen.getByText('0/160')).toBeInTheDocument(); expect(screen.getByText('0 cliques em Comprar')).toBeInTheDocument();
    expect(screen.getByText('Nenhuma oferta ainda. Crie a primeira para colocar o curso à venda.')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Abrir na Autoria' })).not.toBeInTheDocument(); expect(screen.queryByRole('combobox')).not.toBeInTheDocument();
  });

  it('saves with csrf and idempotency then rereads the record, and clears using null', async () => {
    const user = userEvent.setup(); renderRecord(); const requests: { tagline: string | null }[] = []; const keys: string[] = []; let tagline: string | null = null; let reads = 0;
    server.use(http.get(endpoint, () => { reads++; return HttpResponse.json({ ...catalogCourseRecordFixture, tagline }); }),
      http.patch(endpoint, async ({ request }) => {
        const body = await request.json(); if (!body || typeof body !== 'object' || !('tagline' in body) || (typeof body.tagline !== 'string' && body.tagline !== null)) throw new Error('Invalid tagline body');
        tagline = body.tagline; requests.push({ tagline }); keys.push(request.headers.get('Idempotency-Key') ?? ''); expect(request.headers.get('X-CSRF-Token')).toBe('catalog-csrf');
        return HttpResponse.json({ ...catalogCourseRecordFixture, tagline });
      }));
    const field = await screen.findByRole('textbox', { name: 'Chamada comercial (opcional)' });
    await user.type(field, 'Uma API no ar.'); expect(screen.getByText('14/160')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Salvar chamada' })); expect(await screen.findByRole('status')).toHaveTextContent('Chamada salva.');
    await waitFor(() => expect(reads).toBe(2)); await user.clear(field); await user.click(screen.getByRole('button', { name: 'Salvar chamada' }));
    await waitFor(() => expect(requests).toEqual([{ tagline: 'Uma API no ar.' }, { tagline: null }])); await waitFor(() => expect(reads).toBe(3));
    expect(keys[0]).not.toBe(''); expect(keys[0]).not.toBe(keys[1]);
  });

  it('rejects 161 characters with a field error and disabled save', async () => {
    const user = userEvent.setup(); renderRecord(); let calls = 0;
    server.use(http.patch(endpoint, () => { calls++; return HttpResponse.json(catalogCourseRecordFixture); }));
    await user.type(await screen.findByRole('textbox', { name: 'Chamada comercial (opcional)' }), 'a'.repeat(161));
    expect(screen.getByText('161/160')).toBeInTheDocument(); expect(screen.getByRole('alert')).toHaveTextContent('Use até 160 caracteres: tire 1.');
    expect(screen.getByRole('button', { name: 'Salvar chamada' })).toBeDisabled(); expect(calls).toBe(0);
  });

  it('retries the same intention after 504 and creates a new key when the body changes', async () => {
    const user = userEvent.setup(); renderRecord(); const keys: string[] = []; let failed = true;
    server.use(http.patch(endpoint, ({ request }) => {
      keys.push(request.headers.get('Idempotency-Key') ?? '');
      return failed ? HttpResponse.json({ code: 'COMMERCE_TIMEOUT' }, { status: 504 }) : HttpResponse.json(catalogCourseRecordFixture);
    }));
    const field = await screen.findByRole('textbox', { name: 'Chamada comercial (opcional)' }); await user.type(field, 'Intent');
    await user.click(screen.getByRole('button', { name: 'Salvar chamada' })); expect(await screen.findByRole('alert')).toHaveTextContent('Tente de novo');
    await user.click(screen.getByRole('button', { name: 'Salvar chamada' })); await waitFor(() => expect(keys).toHaveLength(2)); expect(keys[0]).toBe(keys[1]);
    await screen.findByRole('alert'); await user.type(field, ' changed'); failed = false;
    await user.click(screen.getByRole('button', { name: 'Salvar chamada' })); expect(await screen.findByRole('status')).toHaveTextContent('Chamada salva.');
    expect(keys[2]).not.toBe(keys[0]);
  });

  it('shows FIELD_INVALID at the tagline and allows retry', async () => {
    const user = userEvent.setup(); renderRecord();
    server.use(http.patch(endpoint, () => HttpResponse.json({ code: 'FIELD_INVALID', detail: 'tagline must contain between 1 and 160 characters.' }, { status: 422 })));
    await user.type(await screen.findByRole('textbox', { name: 'Chamada comercial (opcional)' }), 'Commercial');
    await user.click(screen.getByRole('button', { name: 'Salvar chamada' })); expect(await screen.findByRole('alert')).toHaveTextContent('Use até 160 caracteres na chamada comercial.');
  });

  it('shows authoring link only with permission and keeps declared level read only', async () => {
    renderRecord(['oferta.editar', 'autoria.ler']); server.use(http.get(endpoint, () => HttpResponse.json({ ...catalogCourseRecordFixture, level: 'advanced' })));
    expect(await screen.findByText('Avançado')).toBeInTheDocument(); expect(screen.queryByRole('note')).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Abrir na Autoria' })).toHaveAttribute('href', `/autoria/${catalogCourseRecordFixture.courseId}`);
  });

  it('denies the direct route without permission without requesting a record', async () => {
    renderRecord(['autoria.ler']); let calls = 0;
    server.use(http.get(endpoint, () => { calls++; return HttpResponse.json(catalogCourseRecordFixture); }));
    expect(await screen.findByRole('heading', { name: 'Você não tem acesso a esta área.' })).toBeInTheDocument(); expect(calls).toBe(0);
  });

  it('uses the same not-found screen for inaccessible courses and retries unavailable records', async () => {
    const user = userEvent.setup(); renderRecord(); let status = 502;
    server.use(http.get(endpoint, () => HttpResponse.json({ code: status === 404 ? 'CATALOG_COURSE_NOT_FOUND' : 'COMMERCE_UNAVAILABLE' }, { status })));
    expect(await screen.findByRole('heading', { name: 'Não foi possível carregar a ficha agora.' })).toBeInTheDocument();
    status = 404; await user.click(screen.getByRole('button', { name: 'Tentar de novo' }));
    expect(await screen.findByRole('heading', { name: 'Curso não encontrado no catálogo.' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: '← Catálogo' })).toHaveAttribute('href', '/catalogo');
  });
});
