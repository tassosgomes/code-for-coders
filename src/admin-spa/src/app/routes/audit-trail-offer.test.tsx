import { cleanup, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';

import { AuditRecordDetailRoute } from '@/app/routes/audit-record-detail-route';
import { paths } from '@/config/paths';
import { AuditTrailScreen } from '@/features/audit-trail/components/audit-trail-screen';
import { auditOfferCourseId, auditOfferId, createOfferAuditBoundary, offerAuditRecordId } from '@/testing/audit-trail-offer-handlers';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const renderAudit = (type = 'oferta-alterada', withLabel = true, attributes?: Record<string, string>, list = false) => {
  const boundary = createOfferAuditBoundary(type, withLabel, attributes);
  server.use(...boundary.handlers);
  const router = createMemoryRouter([
    { path: '/detail/:recordId', element: <AuditRecordDetailRoute /> },
    { path: paths.auditTrail.path, element: <AuditTrailScreen /> },
  ], { initialEntries: [list ? paths.auditTrail.getHref() : `/detail/${offerAuditRecordId}`] });
  renderWithProviders(<RouterProvider router={router} />);
  return boundary;
};

afterEach(cleanup);
describe('audit trail offer', () => {
  it.each([
    ['oferta-publicada', 'Oferta publicada'],
    ['oferta-alterada', 'Oferta alterada'],
    ['oferta-despublicada', 'Oferta despublicada'],
  ])('recognizes %s and shows that its reason does not apply', async (type, label) => {
    renderAudit(type);
    expect(await screen.findByRole('heading', { name: label })).toBeInTheDocument();
    expect(screen.getByText('Curso de C# — Acesso por 12 meses')).toBeInTheDocument();
    expect(screen.getByText('Oferta')).toBeInTheDocument();
    expect(screen.getByText('Não se aplica a este tipo')).toBeInTheDocument();
    expect(screen.queryByText('Tipo desconhecido')).not.toBeInTheDocument();
    expect(screen.queryByText(/Motivo obrigatório não informado/)).not.toBeInTheDocument();
  });

  it('formats price and period changes while keeping the course identifier', async () => {
    renderAudit();
    await screen.findByRole('heading', { name: 'Oferta alterada' });
    expect(screen.getByText('Preço anterior')).toBeInTheDocument();
    expect(screen.getByText('Preço novo')).toBeInTheDocument();
    expect(screen.getByText(/R\$\s*497,00/)).toBeInTheDocument();
    expect(screen.getByText(/R\$\s*397,00/)).toBeInTheDocument();
    expect(screen.getByText('Vigência anterior')).toBeInTheDocument();
    expect(screen.getByText('12 meses')).toBeInTheDocument();
    expect(screen.getByText('Vitalícia')).toBeInTheDocument();
    expect(screen.getByText(auditOfferCourseId)).toBeInTheDocument();
    const fields = screen.getAllByRole('term').map(field => field.textContent);
    expect(fields.indexOf('Preço anterior')).toBeLessThan(fields.indexOf('Preço novo'));
    expect(fields.indexOf('Preço novo')).toBeLessThan(fields.indexOf('Vigência anterior'));
    expect(fields.indexOf('Vigência anterior')).toBeLessThan(fields.indexOf('Vigência nova'));
  });

  it('keeps a deleted offer reference readable and filters by its opaque target', async () => {
    const boundary = renderAudit('oferta-despublicada', false, { curso: auditOfferCourseId });
    const user = userEvent.setup();
    await screen.findByRole('heading', { name: 'Oferta despublicada' });
    expect(screen.getByText('Rótulo não disponível')).toBeInTheDocument();
    expect(screen.getByText(/Oferta ·/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Ver atos desta oferta (alvo)' }));
    await waitFor(() => expect(boundary.searches).toContainEqual(expect.objectContaining({ targetId: auditOfferId })));
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('offers all three type filters and sends the selected type to the search', async () => {
    const boundary = renderAudit('oferta-alterada', true, undefined, true);
    const user = userEvent.setup();
    await screen.findByRole('heading', { name: 'Trilha de atos administrativos' });
    const select = screen.getByRole('combobox', { name: 'Tipo' });
    for (const { type, label } of [
      { type: 'oferta-publicada', label: 'Oferta publicada' },
      { type: 'oferta-alterada', label: 'Oferta alterada' },
      { type: 'oferta-despublicada', label: 'Oferta despublicada' },
    ]) {
      expect(screen.getByRole('option', { name: label })).toHaveValue(type);
      await user.selectOptions(select, type);
      await user.click(screen.getByRole('button', { name: 'Buscar' }));
      await waitFor(() => expect(boundary.searches).toContainEqual(expect.objectContaining({ type })));
    }
    expect(screen.getAllByText('Curso de C# — Acesso por 12 meses').length).toBeGreaterThan(0);
  });

  it('keeps malformed historical values visible instead of showing misleading formatted values', async () => {
    renderAudit('oferta-alterada', true, { precoAnterior: 'inválido', vigenciaNova: 'desconhecida' });
    await screen.findByRole('heading', { name: 'Oferta alterada' });
    expect(screen.getByText('inválido')).toBeInTheDocument();
    expect(screen.getByText('desconhecida')).toBeInTheDocument();
  });
});
