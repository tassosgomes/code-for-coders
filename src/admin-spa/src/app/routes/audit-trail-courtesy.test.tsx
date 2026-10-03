import { cleanup, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';

import { AuditRecordDetailRoute } from '@/app/routes/audit-record-detail-route';
import { AuditTrailRoute } from '@/app/routes/audit-trail-route';
import { AdminLayoutRoute } from '@/app/routes/admin-layout-route';
import { loadStaffSession } from '@/app/routes/staff-session-loader';
import { paths } from '@/config/paths';
import { AuditTrailScreen } from '@/features/audit-trail/components/audit-trail-screen';
import { courtesyAuditCourseId, courtesyAuditEmail, courtesyAuditRecordId, courtesyAuditStudentId, createCourtesyAuditBoundary } from '@/testing/audit-trail-courtesy-handlers';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const renderAudit = (options?: Parameters<typeof createCourtesyAuditBoundary>[0], list = false) => {
  const boundary = createCourtesyAuditBoundary(options);
  server.use(...boundary.handlers);
  const router = createMemoryRouter([{
    path: '/', loader: loadStaffSession,
    element: <AdminLayoutRoute serviceName="admin-spa" title="Admin Workspace" />,
    children: [{
      element: <AuditTrailRoute />,
      children: [
        { path: paths.auditRecordDetail.path, element: <AuditRecordDetailRoute /> },
        { path: paths.auditTrail.path, element: <AuditTrailScreen /> },
      ],
    }],
  }], { initialEntries: [list ? paths.auditTrail.getHref() : paths.auditRecordDetail.getHref(courtesyAuditRecordId)] });
  renderWithProviders(<RouterProvider router={router} />);
  return boundary;
};

afterEach(cleanup);

describe('audit trail courtesy', () => {
  it('shows the administrator, student name, course title, period and reason without student email', async () => {
    renderAudit();
    expect(await screen.findByRole('heading', { name: 'Cortesia concedida' })).toBeInTheDocument();
    expect(screen.getByText('Matrícula e Direito de Acesso')).toBeInTheDocument();
    expect(screen.getByText('Marina Costa')).toBeInTheDocument();
    expect(screen.getByText('Aluno')).toBeInTheDocument();
    expect(screen.getByText('Joana Ribeiro')).toBeInTheDocument();
    expect(screen.getByText('Testes na prática')).toBeInTheDocument();
    expect(screen.getByText('Vigência')).toBeInTheDocument();
    expect(screen.getByText('6 meses')).toBeInTheDocument();
    expect(screen.getByText('Bolsa integral do parceiro municipal — turma 2027/1.')).toBeInTheDocument();
    expect(screen.queryByText(/Tipo desconhecido|Motivo obrigatório não informado/)).not.toBeInTheDocument();
    expect(screen.queryByText(courtesyAuditCourseId)).not.toBeInTheDocument();
    expect(screen.queryByText('cursoTitulo')).not.toBeInTheDocument();
    expect(document.body).not.toHaveTextContent(courtesyAuditEmail);
    expect(screen.queryByText(/e-mail/i)).not.toBeInTheDocument();
  });

  it.each([['vitalicia', 'Vitalícia'], ['1m', '1 mês']])('formats %s in the courtesy detail as %s', async (period, label) => {
    renderAudit({ period });
    await screen.findByRole('heading', { name: 'Cortesia concedida' });
    expect(screen.getByText(label)).toBeInTheDocument();
    expect(screen.queryByText(period)).not.toBeInTheDocument();
  });

  it('offers the courtesy filter and sends its type to the search without copying the reason to the list', async () => {
    const boundary = renderAudit(undefined, true);
    const user = userEvent.setup();
    await screen.findByRole('heading', { name: 'Trilha de atos administrativos' });
    const select = screen.getByRole('combobox', { name: 'Tipo' });
    expect(screen.getByRole('option', { name: 'Cortesia concedida' })).toHaveValue('cortesia-concedida');
    await user.selectOptions(select, 'cortesia-concedida');
    await user.click(screen.getByRole('button', { name: 'Buscar' }));
    await waitFor(() => expect(boundary.searches).toContainEqual(expect.objectContaining({ type: 'cortesia-concedida' })));
    expect(screen.getAllByText('Joana Ribeiro').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Marina Costa').length).toBeGreaterThan(0);
    expect(screen.queryByText(/Bolsa integral/)).not.toBeInTheDocument();
    expect(document.body).not.toHaveTextContent(courtesyAuditEmail);
  });

  it('keeps unresolved student and course references visible without inventing their labels', async () => {
    const boundary = renderAudit({ resolved: false });
    const user = userEvent.setup();
    await screen.findByRole('heading', { name: 'Cortesia concedida' });
    expect(screen.getByText('Nome não disponível')).toBeInTheDocument();
    expect(screen.getByText(/Conta de aluno · 0198…0062/)).toBeInTheDocument();
    expect(screen.getByText(courtesyAuditCourseId)).toBeInTheDocument();
    expect(screen.queryByText('Joana Ribeiro')).not.toBeInTheDocument();
    expect(screen.queryByText('Testes na prática')).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Ver atos desta pessoa (alvo)' }));
    await waitFor(() => expect(boundary.searches).toContainEqual(expect.objectContaining({ targetId: courtesyAuditStudentId })));
    expect(document.body).not.toHaveTextContent(courtesyAuditEmail);
  });

  it.each([false, true])('denies the courtesy audit %s route when the server returns 403', async (list) => {
    renderAudit({ status: 403 }, list);
    expect(await screen.findByRole('heading', { name: 'Esta área não é do seu papel' })).toBeInTheDocument();
    expect(screen.queryByText('Joana Ribeiro')).not.toBeInTheDocument();
    expect(screen.queryByText('Testes na prática')).not.toBeInTheDocument();
  });
});
