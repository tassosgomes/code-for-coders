import { cleanup, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { RouterProvider, createMemoryRouter } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { env } from '@/config/env';
import { AuditRecordDetailScreen } from '@/features/audit-trail/components/audit-record-detail-screen';
import type { AuditRecordDetail } from '@/features/audit-trail/api/get-audit-record';
import { clearCsrfToken, setCsrfToken } from '@/lib/api-client';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const recordId = '5137eb89-3e71-4462-9c52-3994f7be0f9a';
const confirmationId = 'd69bd911-9631-4f59-8242-c43629806177';
const explanation = 'The access change was verified against the internal support case.';
const detailPath = `${env.API_URL}/api/v1/audit-records/:recordId`;
const confirmationPath = `${env.API_URL}/api/v1/audit-records/:recordId/complement-confirmations`;

const recordDetail: AuditRecordDetail = {
  id: recordId,
  type: 'papel-concedido',
  practicedAt: '2026-09-27T10:00:00Z',
  author: { type: 'conta-interna', id: '337fcd34-6bf6-4fe5-a1e6-608fe9426be7', label: 'Marina Costa' },
  target: { type: 'conta-interna', id: '550e8400-e29b-41d4-a716-446655440000', label: 'Rafael Silva' },
  compliant: true,
  hasComplements: false,
  origin: 'identidade',
  receivedAt: '2026-09-27T10:00:04Z',
  reason: 'Assumiu a turma de .NET após a saída do professor anterior.',
  attributes: { papel: 'professor' },
  nonComplianceReasons: [],
  complements: [],
};

const renderScreen = () => {
  const router = createMemoryRouter([{
    path: '/',
    element: <AuditRecordDetailScreen recordId={recordId} />,
  }], { initialEntries: ['/'] });
  renderWithProviders(<RouterProvider router={router} />);
  return router;
};

const respondWithDetail = (getDetail: () => AuditRecordDetail = () => recordDetail) => {
  server.use(http.get(detailPath, () => HttpResponse.json(getDetail())));
};

afterEach(() => {
  cleanup();
  clearCsrfToken();
  vi.useRealTimers();
});

describe('audit complement confirmation', () => {
  it('opens an inline explanation form with the counter and irreversible notice', async () => {
    const user = userEvent.setup();
    respondWithDetail();
    renderScreen();

    await user.click(await screen.findByRole('button', { name: 'Acrescentar complemento' }));

    expect(screen.getByRole('textbox', { name: 'Explicação do que foi apurado' })).toHaveAttribute('maxLength', '1000');
    expect(screen.getByText('0/1000')).toBeInTheDocument();
    expect(screen.getByText('Complementos não podem ser editados nem excluídos. O registro original não muda.')).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: 'Explicação do que foi apurado' })).toHaveAccessibleDescription(expect.stringContaining('Não cite dados pessoais de outras pessoas.'));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('shows the required explanation error without sending an empty request', async () => {
    const user = userEvent.setup();
    let requestCount = 0;
    respondWithDetail();
    server.use(http.post(confirmationPath, () => {
      requestCount += 1;
      return HttpResponse.json({ confirmationId, status: 'accepted' }, { status: 202 });
    }));
    renderScreen();

    await user.click(await screen.findByRole('button', { name: 'Acrescentar complemento' }));
    await user.type(screen.getByRole('textbox', { name: 'Explicação do que foi apurado' }), '   ');
    await user.click(screen.getByRole('button', { name: 'Confirmar' }));

    expect(screen.getByRole('alert')).toHaveTextContent('Escreva a explicação do que foi apurado.');
    expect(requestCount).toBe(0);
  });

  it('sends the explanation with a fresh idempotency key and session CSRF token', async () => {
    const user = userEvent.setup();
    const requests: { key: string | null; csrf: string | null; body: unknown }[] = [];
    setCsrfToken('spa-csrf-proof');
    respondWithDetail();
    server.use(http.post(confirmationPath, async ({ request }) => {
      requests.push({
        key: request.headers.get('Idempotency-Key'),
        csrf: request.headers.get('X-CSRF-Token'),
        body: await request.json(),
      });
      return HttpResponse.json({ confirmationId, status: 'accepted' }, { status: 202 });
    }));
    renderScreen();

    await user.click(await screen.findByRole('button', { name: 'Acrescentar complemento' }));
    await user.type(screen.getByRole('textbox', { name: 'Explicação do que foi apurado' }), explanation);
    await user.click(screen.getByRole('button', { name: 'Confirmar' }));

    expect(await screen.findByText('Aguardando registro…')).toBeInTheDocument();
    expect(screen.getByRole('list')).toHaveAttribute('aria-live', 'polite');
    await waitFor(() => expect(requests).toHaveLength(1));
    expect(requests[0]).toMatchObject({ csrf: 'spa-csrf-proof', body: { explanation } });
    expect(requests[0]?.key).toMatch(/^[0-9a-f-]{36}$/i);
  });

  it('reuses the same key and text when retrying after a network failure', async () => {
    const user = userEvent.setup();
    const requests: { key: string | null; body: unknown }[] = [];
    setCsrfToken('spa-csrf-proof');
    respondWithDetail();
    server.use(http.post(confirmationPath, async ({ request }) => {
      requests.push({ key: request.headers.get('Idempotency-Key'), body: await request.json() });
      return requests.length === 1
        ? HttpResponse.error()
        : HttpResponse.json({ confirmationId, status: 'accepted' }, { status: 202 });
    }));
    renderScreen();

    await user.click(await screen.findByRole('button', { name: 'Acrescentar complemento' }));
    await user.type(screen.getByRole('textbox', { name: 'Explicação do que foi apurado' }), explanation);
    await user.click(screen.getByRole('button', { name: 'Confirmar' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Não conseguimos confirmar agora. Seu texto foi mantido.');
    expect(screen.getByRole('textbox', { name: 'Explicação do que foi apurado' })).toHaveValue(explanation);
    await user.click(screen.getByRole('button', { name: 'Tentar de novo' }));

    expect(await screen.findByText('Aguardando registro…')).toBeInTheDocument();
    expect(requests).toHaveLength(2);
    expect(requests[0]?.key).toBe(requests[1]?.key);
    expect(requests[0]?.body).toEqual(requests[1]?.body);
    expect(requests[1]?.body).toEqual({ explanation });
  });

  it('keeps the accepted confirmation pending without sending a second key automatically', async () => {
    const user = userEvent.setup();
    let requestCount = 0;
    respondWithDetail();
    server.use(http.post(confirmationPath, () => {
      requestCount += 1;
      return HttpResponse.json({ confirmationId, status: 'accepted' }, { status: 202 });
    }));
    renderScreen();

    await user.click(await screen.findByRole('button', { name: 'Acrescentar complemento' }));
    await user.type(screen.getByRole('textbox', { name: 'Explicação do que foi apurado' }), explanation);
    await user.click(screen.getByRole('button', { name: 'Confirmar' }));
    expect(await screen.findByText('Aguardando registro…')).toBeInTheDocument();

    expect(requestCount).toBe(1);
    expect(screen.getByRole('button', { name: 'Acrescentar complemento' })).toBeDisabled();
    expect(screen.queryByText(/falhou/i)).not.toBeInTheDocument();
  });

  it('stops waiting when the detail returns the matching confirmation id', async () => {
    const user = userEvent.setup();
    let confirmed = false;
    respondWithDetail(() => confirmed ? {
      ...recordDetail,
      hasComplements: true,
      complements: [{
        id: '7137eb89-3e71-4462-9c52-3994f7be0f9a',
        confirmationId,
        createdAt: '2026-09-27T11:00:00Z',
        author: { type: 'conta-interna', id: '337fcd34-6bf6-4fe5-a1e6-608fe9426be7' },
        explanation,
      }],
    } : recordDetail);
    server.use(http.post(confirmationPath, () => {
      confirmed = true;
      return HttpResponse.json({ confirmationId, status: 'accepted' }, { status: 202 });
    }));
    renderScreen();

    await user.click(await screen.findByRole('button', { name: 'Acrescentar complemento' }));
    await user.type(screen.getByRole('textbox', { name: 'Explicação do que foi apurado' }), explanation);
    await user.click(screen.getByRole('button', { name: 'Confirmar' }));
    expect(await screen.findByText('Aguardando registro…')).toBeInTheDocument();

    await waitFor(() => expect(screen.queryByText('Aguardando registro…')).not.toBeInTheDocument(), { timeout: 4_000 });
    expect(screen.getByText(explanation)).toBeInTheDocument();
  });

  it('returns focus to the add button when the form is cancelled', async () => {
    const user = userEvent.setup();
    respondWithDetail();
    renderScreen();
    await screen.findByRole('button', { name: 'Acrescentar complemento' });

    await user.click(screen.getByRole('button', { name: 'Acrescentar complemento' }));
    await user.click(screen.getByRole('button', { name: 'Cancelar' }));

    await waitFor(() => expect(screen.getByRole('button', { name: 'Acrescentar complemento' })).toHaveFocus());
    expect(screen.queryByRole('textbox', { name: 'Explicação do que foi apurado' })).not.toBeInTheDocument();
  });
});
