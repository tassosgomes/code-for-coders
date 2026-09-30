import { cleanup, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';

import { paths } from '@/config/paths';
import { AuditRecordDetailScreen } from '@/features/audit-trail/components/audit-record-detail-screen';
import { AuditTrailScreen } from '@/features/audit-trail/components/audit-trail-screen';
import { createAuditCourseBoundary, auditCourseRecordId, auditCourseId } from '@/testing/audit-course-target-handlers';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const renderDetail = (withTitle = true) => {
  const boundary = createAuditCourseBoundary(withTitle); server.use(...boundary.handlers);
  const router = createMemoryRouter([{ path: '/detail', element: <AuditRecordDetailScreen recordId={auditCourseRecordId} /> }, { path: paths.auditTrail.path, element: <AuditTrailScreen /> }], { initialEntries: ['/detail'] });
  renderWithProviders(<RouterProvider router={router} />); return boundary;
};
afterEach(cleanup);
describe('audit course target', () => {
  it('labels the content publication and filters by course target from the detail', async () => {
    const boundary = renderDetail(); const user = userEvent.setup();
    expect(await screen.findByRole('heading', { name: 'Versão publicada' })).toBeInTheDocument();
    expect(screen.getByText('Conteúdo e Currículo')).toBeInTheDocument(); expect(screen.getByText('Curso publicado')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Ver atos deste curso (alvo)' }));
    await waitFor(() => expect(boundary.searches).toContainEqual(expect.objectContaining({ targetId: auditCourseId })));
    expect(screen.getByRole('option', { name: 'Versão publicada' })).toHaveValue('versao-publicada');
  });

  it('keeps the opaque course reference when its title is unavailable', async () => {
    renderDetail(false); await screen.findByRole('heading', { name: 'Versão publicada' });
    expect(screen.getByText('Título não disponível')).toBeInTheDocument(); expect(screen.getByText(/Curso ·/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Ver atos deste curso (alvo)' })).toBeInTheDocument();
  });
});
