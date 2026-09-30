import { readFileSync } from 'node:fs';

import { cleanup, screen, within } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';
import { parse } from 'yaml';
import { z } from 'zod';

import { routes } from '@/app/app-routes';
import { paths } from '@/config/paths';
import { courseVersionSchema } from '@/features/course-authoring/types/course-version';
import { createVersionBoundary } from '@/testing/authoring-version-handlers';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const renderVersion = (boundary: ReturnType<typeof createVersionBoundary>) => {
  server.use(...boundary.handlers);
  const router = createMemoryRouter(routes, { basename: '/admin', initialEntries: [`/admin${paths.authoringVersion.getHref(boundary.snapshot().courseId, 1)}`] });
  return renderWithProviders(<RouterProvider router={router} />);
};
afterEach(cleanup);
describe('authoring version level', () => {
  it('shows the immutable published level, prerequisite and historical titles in order as text', async () => {
    renderVersion(createVersionBoundary(true, true, { level: 'advanced', prerequisite: { text: 'Git e C# básico.', recommendedCourses: [
      { courseId: '0198dfac-674a-7000-8000-000000000011', title: 'Título antigo de Git' },
      { courseId: '0198dfac-674a-7000-8000-000000000012', title: 'Título antigo de C#' },
    ] } }));
    const audience = await screen.findByRole('region', { name: 'Nível e pré-requisito desta versão' });
    expect(audience).toHaveTextContent('Nível: Avançado'); expect(audience).toHaveTextContent('Pré-requisito: Git e C# básico.');
    expect(within(audience).getAllByRole('listitem').map((item) => item.textContent)).toEqual(['Título antigo de Git', 'Título antigo de C#']);
    expect(within(audience).queryByRole('link')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Salvar|Editar|Publicar|Descartar/ })).not.toBeInTheDocument();
  });

  it('shows Sem nível and Sem pré-requisito for versions published before this delivery', async () => {
    renderVersion(createVersionBoundary());
    const audience = await screen.findByRole('region', { name: 'Nível e pré-requisito desta versão' });
    expect(audience).toHaveTextContent('Nível: Sem nível'); expect(audience).toHaveTextContent('Pré-requisito: Sem pré-requisito');
    expect(within(audience).queryByRole('list')).not.toBeInTheDocument();
  });

  it('shows text without recommendations and recommendations without text to a reader', async () => {
    const first = renderVersion(createVersionBoundary(false, true, { level: 'beginner', prerequisite: { text: 'Noções de lógica.', recommendedCourses: [] } }));
    const audience = await screen.findByRole('region', { name: 'Nível e pré-requisito desta versão' });
    expect(audience).toHaveTextContent('Nível: Iniciante'); expect(audience).toHaveTextContent('Noções de lógica.'); expect(audience).toHaveTextContent('Nenhum curso recomendado');
    first.unmount();
    renderVersion(createVersionBoundary(false, true, { level: 'intermediate', prerequisite: { text: null, recommendedCourses: [{ courseId: '0198dfac-674a-7000-8000-000000000011', title: 'Git da época' }] } }));
    const next = await screen.findByRole('region', { name: 'Nível e pré-requisito desta versão' });
    expect(next).toHaveTextContent('Nível: Intermediário'); expect(next).toHaveTextContent('Sem texto de pré-requisito'); expect(next).toHaveTextContent('Git da época');
  });

  it('strict version schema parses the actual getCourseVersion OpenAPI 1.1.0 example', () => {
    const document: unknown = parse(readFileSync('../../tasks/prd-nivel-prerequisito-curso/api-contract.yaml', 'utf8'));
    const contract = z.object({ info: z.object({ version: z.literal('1.1.0') }), paths: z.record(z.string(), z.unknown()) }).parse(document);
    const operation = z.object({ get: z.object({ responses: z.object({ '200': z.object({ content: z.object({ 'application/json': z.object({ examples: z.object({ versao4: z.object({ value: z.unknown() }) }) }) }) }) }) }) }).parse(contract.paths['/courses/{courseId}/versions/{versionNumber}']);
    const version = courseVersionSchema.parse(operation.get.responses['200'].content['application/json'].examples.versao4.value);
    expect(version.level).toBe('advanced'); expect(version.prerequisite.text).toBe('Git e C# básico.'); expect(version.prerequisite.recommendedCourses[0]?.title).toBe('Fundamentos de C#');
    expect(courseVersionSchema.safeParse({ ...version, unexpected: true }).success).toBe(false);
  });
});
