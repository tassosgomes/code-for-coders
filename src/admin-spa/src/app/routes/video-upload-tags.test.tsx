import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, Outlet, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';

import { VideosAreaRoute } from '@/app/routes/videos-area-route';
import { env } from '@/config/env';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

afterEach(cleanup);
const uploadId = 'e2ef6f47-cb6b-4a08-b126-c3b21e9475d2';
const videoId = 'c2733b6f-51ee-4c10-8f9c-255904b08a93';
const upload = (tags: string[] = []) => ({
  uploadId, title: 'aula', fileName: 'aula.mp4', fileSize: 4, partSize: 67108864, partCount: 1,
  receivedParts: [], tags, expiresAt: '2026-12-27T14:05:11Z',
});
const renderRoute = () => {
  const router = createMemoryRouter([{
    path: '/', element: <Outlet context={{ permissions: ['midia.enviar'], roles: ['professor'], name: 'Marina Alves' }} />,
    children: [{ path: 'videos', element: <VideosAreaRoute /> }],
  }], { initialEntries: ['/videos'] });
  return renderWithProviders(<RouterProvider router={router} />);
};
const chooseFile = async (user: ReturnType<typeof userEvent.setup>) => {
  await user.click((await screen.findAllByRole('button', { name: 'Enviar vídeo' }))[0]!);
  await user.upload(screen.getByLabelText('Escolher arquivo de vídeo'), new File(['test'], 'aula.mp4', { type: 'video/mp4' }));
  return within(screen.getByRole('dialog', { name: 'Enviar vídeo' }));
};
const setupCompletion = (tags: string[]) => {
  let completed = false;
  const video = {
    videoId, title: 'aula', status: 'received', tags,
    uploadedBy: { accountId: '3e4f5a6b-7c8d-4e9f-8a0b-1c2d3e4f5a6b', name: 'Marina Alves' },
    uploadedAt: '2026-10-11T14:05:11Z', durationSeconds: null, failureReason: null,
  };
  server.use(
    http.get(`${env.API_URL}/api/v1/videos`, () => HttpResponse.json({
      data: completed ? [video] : [], pagination: { page: 1, size: 20, total: completed ? 1 : 0, totalPages: completed ? 1 : 0 },
    })),
    http.post(`${env.API_URL}/api/v1/video-uploads/:id/complete`, () => {
      completed = true; return HttpResponse.json(video, { status: 201 });
    }),
  );
};

describe('video-upload-tags', () => {
  it('sends confirmed tags and the pending draft, then shows canonical received tags', async () => {
    const user = userEvent.setup(); let received: unknown;
    setupCompletion(['Docker', 'Módulo 2']);
    server.use(http.post(`${env.API_URL}/api/v1/video-uploads`, async ({ request }) => {
      received = await request.json(); return HttpResponse.json(upload(['Docker', 'Módulo 2']), { status: 201 });
    }));
    renderRoute(); const dialog = await chooseFile(user);
    const field = dialog.getByRole('combobox', { name: 'Tags (opcional)' });
    await user.type(field, 'docker{Enter}Módulo 2');
    await user.click(dialog.getByRole('button', { name: 'Enviar' }));
    await screen.findByText('aula recebido. A preparação começou.');
    expect(received).toMatchObject({ tags: ['docker', 'Módulo 2'] });
    const row = screen.getByRole('region', { name: 'Biblioteca de vídeos' });
    expect(within(row).getByText('Docker')).toBeInTheDocument();
    expect(within(row).getByText('Módulo 2')).toBeInTheDocument();
    expect(within(row).getByText('Recebido')).toBeInTheDocument();
  });

  it('keeps tags optional and sends an empty list', async () => {
    const user = userEvent.setup(); let received: unknown;
    setupCompletion([]);
    server.use(http.post(`${env.API_URL}/api/v1/video-uploads`, async ({ request }) => {
      received = await request.json(); return HttpResponse.json(upload(), { status: 201 });
    }));
    renderRoute(); const dialog = await chooseFile(user);
    await user.click(dialog.getByRole('button', { name: 'Enviar' }));
    await screen.findByText('aula recebido. A preparação começou.');
    expect(received).toMatchObject({ tags: [] });
  });

  it.each([
    ['TAG_INVALID', 'Cada tag precisa ter de 1 a 30 caracteres.'],
    ['TAGS_LIMIT_EXCEEDED', 'Um vídeo pode ter no máximo 10 tags.'],
  ])('shows %s in the tags field and keeps chips and draft for retry', async (code, message) => {
    const user = userEvent.setup(); const bodies: unknown[] = [];
    server.use(http.post(`${env.API_URL}/api/v1/video-uploads`, async ({ request }) => {
      bodies.push(await request.json()); return HttpResponse.json({ code }, { status: 422 });
    }));
    renderRoute(); const dialog = await chooseFile(user);
    const field = dialog.getByRole('combobox', { name: 'Tags (opcional)' });
    await user.type(field, 'Docker{Enter}Módulo 2');
    await user.click(dialog.getByRole('button', { name: 'Enviar' }));
    expect(await dialog.findByText(message)).toBeInTheDocument();
    expect(field).toHaveAttribute('aria-invalid', 'true');
    expect(field).toHaveValue('Módulo 2');
    expect(dialog.getByRole('button', { name: 'Remover tag Docker' })).toBeInTheDocument();
    await user.click(dialog.getByRole('button', { name: 'Enviar' }));
    await waitFor(() => expect(bodies).toHaveLength(2));
    expect(bodies[0]).toMatchObject({ tags: ['Docker', 'Módulo 2'] });
    expect(bodies[1]).toMatchObject({ tags: ['Docker', 'Módulo 2'] });
  });

  it('rejects an invalid unconfirmed draft before calling the API', async () => {
    const user = userEvent.setup(); let requests = 0;
    server.use(http.post(`${env.API_URL}/api/v1/video-uploads`, () => { requests++; return HttpResponse.json(upload(), { status: 201 }); }));
    renderRoute(); const dialog = await chooseFile(user);
    await user.type(dialog.getByRole('combobox', { name: 'Tags (opcional)' }), 'abcdefghijklmnopqrstuvwxyz12345');
    await user.click(dialog.getByRole('button', { name: 'Enviar' }));
    expect(await dialog.findByText('Cada tag precisa ter de 1 a 30 caracteres.')).toBeInTheDocument();
    expect(requests).toBe(0);
  });

  it('shows the original pending tags when the server resumes an upload', async () => {
    const user = userEvent.setup();
    server.use(
      http.post(`${env.API_URL}/api/v1/video-uploads`, () => HttpResponse.json(upload(['Original']), { status: 200 })),
      http.get(`${env.API_URL}/api/v1/video-uploads`, () => HttpResponse.json({
        data: [upload(['Original'])], pagination: { page: 1, size: 50, total: 1, totalPages: 1 },
      })),
      http.post(`${env.API_URL}/api/v1/video-uploads/:id/part-urls`, () => HttpResponse.json({ code: 'MEDIA_UNAVAILABLE' }, { status: 503 })),
    );
    renderRoute(); const dialog = await chooseFile(user);
    await user.type(dialog.getByRole('combobox', { name: 'Tags (opcional)' }), 'Other{Enter}');
    await user.click(dialog.getByRole('button', { name: 'Enviar' }));
    const panel = await screen.findByRole('region', { name: 'Transferência de vídeo' });
    expect(within(panel).getByText('Original')).toBeInTheDocument();
    expect(within(panel).queryByText('Other')).not.toBeInTheDocument();
    await screen.findByText(/Envio pausado/);
  });

  it('refreshes cached tag suggestions after completing an upload with a new tag', async () => {
    const user = userEvent.setup(); let queries = 0; let uploaded = false;
    setupCompletion(['Nova']);
    server.use(
      http.get(`${env.API_URL}/api/v1/video-tags`, () => { queries++; return HttpResponse.json({ data: uploaded ? ['Nova'] : [] }); }),
      http.post(`${env.API_URL}/api/v1/video-uploads`, () => HttpResponse.json(upload(['Nova']), { status: 201 })),
      http.put('http://localhost:9000/:part', () => { uploaded = true; return new HttpResponse(null, { status: 200 }); }),
    );
    renderRoute(); const dialog = await chooseFile(user);
    await waitFor(() => expect(queries).toBeGreaterThan(0));
    await user.type(dialog.getByRole('combobox', { name: 'Tags (opcional)' }), 'Nova{Enter}');
    await user.click(dialog.getByRole('button', { name: 'Enviar' }));
    await screen.findByText('aula recebido. A preparação começou.');
    const afterCompletion = queries;
    const next = await chooseFile(user);
    await user.click(next.getByRole('combobox', { name: 'Tags (opcional)' }));
    await next.findByRole('option', { name: 'Nova' });
    expect(queries).toBeGreaterThan(afterCompletion);
  });
});
