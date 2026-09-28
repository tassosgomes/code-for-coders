import { cleanup, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { RouterProvider, createMemoryRouter } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';

import { env } from '@/config/env';
import { AuditRecordDetailScreen } from '@/features/audit-trail/components/audit-record-detail-screen';
import type { AuditRecordDetail } from '@/features/audit-trail/api/get-audit-record';
import { AuditTrailScreen } from '@/features/audit-trail/components/audit-trail-screen';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const recordId = '5137eb89-3e71-4462-9c52-3994f7be0f9a';
const confirmationId = 'd69bd911-9631-4f59-8242-c43629806177';
const explanation = 'A autorização foi conferida com o chamado de suporte.';
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
  reason: 'Assumiu a turma após a saída do professor anterior.',
  attributes: { papel: 'professor' },
  nonComplianceReasons: [],
  complements: [],
};

const renderDetail = () => {
  const router = createMemoryRouter([{
    path: '/',
    element: <AuditRecordDetailScreen recordId={recordId} />,
  }], { initialEntries: ['/'] });
  renderWithProviders(<RouterProvider router={router} />);
};

const complement = (id: string, confirmation: string, createdAt: string, author: string, text: string) => ({
  id,
  confirmationId: confirmation,
  createdAt,
  author: { type: 'conta-interna', id: '337fcd34-6bf6-4fe5-a1e6-608fe9426be7', label: author },
  explanation: text,
});

afterEach(cleanup);

describe('audit complement recorded', () => {
  it('confirms the accepted complement after polling and keeps the original unchanged', async () => {
    const user = userEvent.setup();
    let recorded = false;
    server.use(
      http.get(detailPath, () => HttpResponse.json(recorded ? {
        ...recordDetail,
        hasComplements: true,
        complements: [complement('7137eb89-3e71-4462-9c52-3994f7be0f9a', confirmationId, '2026-09-27T11:00:00Z', 'Marina Costa', explanation)],
      } : recordDetail)),
      http.post(confirmationPath, () => {
        recorded = true;
        return HttpResponse.json({ confirmationId, status: 'accepted' }, { status: 202 });
      }),
    );
    renderDetail();

    await user.click(await screen.findByRole('button', { name: 'Acrescentar complemento' }));
    await user.type(screen.getByRole('textbox', { name: 'Explicação do que foi apurado' }), explanation);
    await user.click(screen.getByRole('button', { name: 'Confirmar' }));
    expect(await screen.findByText('Aguardando registro…')).toBeInTheDocument();

    expect(await screen.findByText('Complemento registrado', {}, { timeout: 6_000 })).toHaveAttribute('role', 'status');
    const original = screen.getByRole('region', { name: 'Recebido da origem' });
    const complements = screen.getByRole('region', { name: /Complementos/ });
    expect(within(complements).getByText(explanation)).toBeInTheDocument();
    expect(within(complements).getByText('Marina Costa')).toBeInTheDocument();
    expect(within(original).getByText(recordDetail.reason!)).toBeInTheDocument();
    expect(original).not.toHaveTextContent(explanation);
    expect(screen.queryByText('Aguardando registro…')).not.toBeInTheDocument();
  }, 10_000);

  it('renders recorded complements in order as entries separate from the original act', async () => {
    const earlierExplanation = 'A primeira informação complementar.';
    const laterExplanation = 'Uma segunda informação apurada depois.';
    server.use(http.get(detailPath, () => HttpResponse.json({
      ...recordDetail,
      hasComplements: true,
      complements: [
        complement('7137eb89-3e71-4462-9c52-3994f7be0f9a', 'e69bd911-9631-4f59-8242-c43629806177', '2026-09-27T11:00:00Z', 'Marina Costa', earlierExplanation),
        complement('8137eb89-3e71-4462-9c52-3994f7be0f9a', 'f69bd911-9631-4f59-8242-c43629806177', '2026-09-27T12:00:00Z', 'João Souza', laterExplanation),
      ],
    })));
    renderDetail();

    const timeline = await screen.findByRole('list');
    const entries = within(timeline).getAllByRole('listitem');
    expect(entries).toHaveLength(2);
    expect(within(entries[0]!).getByText(earlierExplanation)).toBeInTheDocument();
    expect(within(entries[0]!).getByText('Marina Costa')).toBeInTheDocument();
    expect(within(entries[1]!).getByText(laterExplanation)).toBeInTheDocument();
    expect(within(entries[1]!).getByText('João Souza')).toBeInTheDocument();
    expect(within(timeline).getAllByText('ACRESCENTADO DEPOIS')).toHaveLength(2);
    expect(screen.getByRole('region', { name: 'Recebido da origem' })).not.toHaveTextContent(earlierExplanation);
  });

  it('announces the chronological timeline politely and shows each confirmation moment', async () => {
    server.use(http.get(detailPath, () => HttpResponse.json({
      ...recordDetail,
      hasComplements: true,
      complements: [complement('7137eb89-3e71-4462-9c52-3994f7be0f9a', confirmationId, '2026-09-27T11:00:00Z', 'Marina Costa', explanation)],
    })));
    renderDetail();

    const timeline = await screen.findByRole('list');
    expect(timeline).toHaveAttribute('aria-live', 'polite');
    expect(timeline.querySelector('time')).toHaveAttribute('dateTime', '2026-09-27T11:00:00Z');
    expect(within(timeline).getByText('Marina Costa')).toBeInTheDocument();
    expect(within(timeline).getByText(explanation)).toBeInTheDocument();
  });

  it('marks records with complements in the audit list', async () => {
    const router = createMemoryRouter([{
      path: '/',
      element: <AuditTrailScreen />,
    }], { initialEntries: ['/'] });
    server.use(http.post(`${env.API_URL}/api/v1/audit-record-searches`, () => HttpResponse.json({
      data: [{
        id: recordId,
        type: 'papel-concedido',
        practicedAt: '2026-09-27T10:00:00Z',
        author: { type: 'conta-interna', id: '337fcd34-6bf6-4fe5-a1e6-608fe9426be7', label: 'Marina Costa' },
        target: { type: 'conta-interna', id: '550e8400-e29b-41d4-a716-446655440000', label: 'Rafael Silva' },
        compliant: true,
        hasComplements: true,
      }],
      pagination: { page: 1, size: 20, total: 1, totalPages: 1, snapshot: `snap_${'1'.repeat(43)}` },
    })));
    renderWithProviders(<RouterProvider router={router} />);

    const row = await screen.findByRole('link', { name: 'Abrir registro papel-concedido' });
    expect(within(row).getByText('Complementado')).toBeInTheDocument();
  });
});
