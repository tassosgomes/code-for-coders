import { cleanup, screen, within } from '@testing-library/react';
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
const video = {
  videoId: 'c2733b6f-51ee-4c10-8f9c-255904b08a93',
  title: 'Aula retomada',
  status: 'received',
  uploadedBy: {
    accountId: '3e4f5a6b-7c8d-4e9f-8a0b-1c2d3e4f5a6b',
    name: 'Marina Alves',
  },
  uploadedAt: '2026-09-26T14:05:11.0000999+00:00',
  durationSeconds: null,
  failureReason: null,
};

const partSize = 2 * 1024 * 1024;
const fileSize = 2 * partSize;

const upload = (overrides: Record<string, unknown> = {}) => ({
  uploadId,
  title: 'Aula retomada',
  fileName: 'aula.mp4',
  fileSize,
  partSize,
  partCount: 2,
  receivedParts: [],
  expiresAt: new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString(),
  ...overrides,
});

const pendingPage = (uploads: ReturnType<typeof upload>[]) => HttpResponse.json({
  data: uploads,
  pagination: { page: 1, size: 50, total: uploads.length, totalPages: uploads.length ? 1 : 0 },
});

const renderRoute = () => {
  const router = createMemoryRouter([
    {
      path: '/',
      element: <Outlet context={{ permissions: ['midia.enviar'], name: 'Marina Alves', roles: ['professor'] }} />,
      children: [{ path: 'videos', element: <VideosAreaRoute /> }],
    },
  ], { initialEntries: ['/videos'] });
  renderWithProviders(<RouterProvider router={router} />);
};

const videoFile = (name = 'aula.mp4') => new File([new Uint8Array(fileSize)], name, {
  type: 'video/mp4',
  lastModified: 1_758_900_000_000,
});

const selectFile = async (user: ReturnType<typeof userEvent.setup>, file: File) => {
  await user.click(screen.getByRole('button', { name: 'Selecionar o arquivo' }));
  await user.upload(screen.getByLabelText(/Escolher arquivo de vídeo/), file);
};

const submitUpload = async (user: ReturnType<typeof userEvent.setup>) => {
  const dialog = within(screen.getByRole('dialog', { name: 'Enviar vídeo' }));
  await user.click(dialog.getByRole('button', { name: 'Enviar' }));
};

describe('video upload resume', () => {
  it('groups multiple incomplete uploads with a file action for each one', async () => {
    server.use(http.get(`${env.API_URL}/api/v1/video-uploads`, () => pendingPage([
      upload(),
      upload({ uploadId: 'e2ef6f47-cb6b-4a08-b126-c3b21e9475d3', fileName: 'outra-aula.mkv' }),
    ])));
    renderRoute();

    expect(await screen.findByText('2 envios incompletos — selecione o mesmo arquivo para continuar cada um.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Selecionar aula.mp4' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Selecionar outra-aula.mkv' })).toBeInTheDocument();
  });

  it('shows pending parts and resumes by uploading only missing parts', async () => {
    const user = userEvent.setup();
    const sentParts: number[] = [];
    server.use(
      http.get(`${env.API_URL}/api/v1/video-uploads`, () => pendingPage([upload({ receivedParts: [1] })])),
      http.post(`${env.API_URL}/api/v1/video-uploads`, () => HttpResponse.json(upload({ receivedParts: [1] }), { status: 200 })),
      http.post(`${env.API_URL}/api/v1/video-uploads/:id/part-urls`, async ({ request }) => {
        const body = await request.json() as { partNumbers: number[] };
        return HttpResponse.json({
          parts: body.partNumbers.map((partNumber) => ({
            partNumber,
            url: `http://localhost:9000/part-${partNumber}?signature=resume`,
            expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
          })),
          uploadExpiresAt: new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString(),
        });
      }),
      http.put('http://localhost:9000/:part', async ({ params }) => {
        sentParts.push(Number(String(params.part).replace('part-', '')));
        await new Promise((resolve) => window.setTimeout(resolve, 100));
        return new HttpResponse(null, { status: 200 });
      }),
      http.post(`${env.API_URL}/api/v1/video-uploads/:id/complete`, () => HttpResponse.json(video, { status: 201 })),
    );
    renderRoute();

    expect(await screen.findByText(/Envio incompleto de aula\.mp4/)).toBeInTheDocument();
    await selectFile(user, videoFile());
    await submitUpload(user);

    expect(await screen.findByText(/Retomado de onde parou: 2,1 MB já estavam na escola/)).toBeInTheDocument();
    expect(await screen.findByText('Aula retomada recebido. A preparação começou.')).toBeInTheDocument();
    expect(sentParts).toEqual([2]);
  });

  it('warns when the selected file differs from a pending upload', async () => {
    const user = userEvent.setup({ applyAccept: false });
    server.use(http.get(`${env.API_URL}/api/v1/video-uploads`, () => pendingPage([upload({ fileName: 'aula-pendente.mp4' })])));
    renderRoute();

    await screen.findByText(/Envio incompleto de aula-pendente\.mp4/);
    await selectFile(user, videoFile('aula-nova.mp4'));

    const dialog = within(await screen.findByRole('dialog', { name: 'Enviar vídeo' }));
    expect(await dialog.findByRole('status')).toHaveTextContent(
      /Este não é o arquivo do envio incompleto \(aula-pendente\.mp4\)\. Ele será enviado como um vídeo novo;/,
    );
  });

  it('renews an expired part URL after a storage 403', async () => {
    const user = userEvent.setup();
    const putUrls: string[] = [];
    const urlBatches: number[][] = [];
    let expiredPartAttempts = 0;
    server.use(
      http.get(`${env.API_URL}/api/v1/video-uploads`, () => pendingPage([])),
      http.post(`${env.API_URL}/api/v1/video-uploads`, () => HttpResponse.json(upload(), { status: 201 })),
      http.post(`${env.API_URL}/api/v1/video-uploads/:id/part-urls`, async ({ request }) => {
        const body = await request.json() as { partNumbers: number[] };
        urlBatches.push(body.partNumbers);
        return HttpResponse.json({
          parts: body.partNumbers.map((partNumber) => ({
            partNumber,
            url: `http://localhost:9000/${partNumber === 1 && urlBatches.length > 1 ? 'fresh' : 'expired'}-part-${partNumber}`,
            expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
          })),
          uploadExpiresAt: new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString(),
        });
      }),
      http.put('http://localhost:9000/:key', ({ params }) => {
        const key = String(params.key);
        putUrls.push(key);
        if (key === 'expired-part-1' && expiredPartAttempts++ === 0) {
          return new HttpResponse(null, { status: 403 });
        }
        return new HttpResponse(null, { status: 200 });
      }),
      http.post(`${env.API_URL}/api/v1/video-uploads/:id/complete`, () => HttpResponse.json(video, { status: 201 })),
    );
    renderRoute();
    await screen.findByRole('heading', { name: 'Nenhum vídeo ainda' });
    await user.click(screen.getAllByRole('button', { name: 'Enviar vídeo' })[0]!);
    await user.upload(screen.getByLabelText(/Escolher arquivo de vídeo/), videoFile());
    await submitUpload(user);

    expect(await screen.findByText('Aula retomada recebido. A preparação começou.')).toBeInTheDocument();
    expect(urlBatches).toEqual([[1, 2], [1]]);
    expect(putUrls).toContain('fresh-part-1');
  });
});
