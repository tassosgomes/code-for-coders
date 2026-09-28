import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, Link, Outlet, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';

import { VideosAreaRoute } from '@/app/routes/videos-area-route';
import { env } from '@/config/env';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

afterEach(cleanup);

const uploadId = 'e2ef6f47-cb6b-4a08-b126-c3b21e9475d2';
const videoId = 'c2733b6f-51ee-4c10-8f9c-255904b08a93';
const video = {
  videoId,
  title: 'Aula de exemplo',
  status: 'received',
  uploadedBy: {
    accountId: '3e4f5a6b-7c8d-4e9f-8a0b-1c2d3e4f5a6b',
    name: 'Marina Alves',
  },
  uploadedAt: '2026-09-26T14:05:11.0000999+00:00',
  durationSeconds: null,
  failureReason: null,
};

const upload = (overrides: Record<string, unknown> = {}) => ({
  uploadId,
  title: 'Aula de exemplo',
  fileName: 'aula.mp4',
  fileSize: 4,
  partSize: 2,
  partCount: 2,
  receivedParts: [],
  expiresAt: '2026-09-27T14:05:11Z',
  ...overrides,
});

const RouteHarness = () => <>
  <VideosAreaRoute />
  <Link to="/outra-area">Outra área</Link>
</>;

const renderRoute = () => {
  const router = createMemoryRouter([
    {
      path: '/',
      element: <Outlet context={{ permissions: ['midia.enviar'], name: 'Marina Alves', roles: ['professor'] }} />,
      children: [
        { path: 'videos', element: <RouteHarness /> },
        { path: 'outra-area', element: <h1>Outra área</h1> },
      ],
    },
  ], { initialEntries: ['/videos'] });

  renderWithProviders(<RouterProvider router={router} />);
};

const chooseFile = async (user: ReturnType<typeof userEvent.setup>, file: File) => {
  await user.click(screen.getAllByRole('button', { name: 'Enviar vídeo' })[0]!);
  await user.upload(screen.getByLabelText(/Escolher arquivo de vídeo/), file);
};

const submitUpload = async (user: ReturnType<typeof userEvent.setup>) => {
  const dialog = within(screen.getByRole('dialog', { name: 'Enviar vídeo' }));
  await user.click(dialog.getByRole('button', { name: 'Enviar' }));
};

const videoFile = () => new File([new Uint8Array([1, 2, 3, 4])], 'aula.mp4', {
  type: 'video/mp4',
  lastModified: 1_758_900_000_000,
});

const jsonVideoPage = (videos: typeof video[] = []) => HttpResponse.json({
  data: videos,
  pagination: { page: 1, size: 10, total: videos.length, totalPages: videos.length ? 1 : 0 },
});

describe('video upload', () => {
  it('rejects an unsupported extension before calling the API', async () => {
    const user = userEvent.setup({ applyAccept: false });
    let createRequests = 0;
    server.use(http.post(`${env.API_URL}/api/v1/video-uploads`, () => {
      createRequests += 1;
      return HttpResponse.json(upload(), { status: 201 });
    }));
    renderRoute();
    await screen.findByRole('heading', { name: 'Nenhum vídeo ainda' });

    await chooseFile(user, new File(['video'], 'aula.avi', { type: 'video/x-msvideo' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Formato não aceito. Escolha um arquivo MP4, MOV ou MKV.');
    expect(createRequests).toBe(0);
  });

  it('rejects files larger than 5 GiB before calling the API', async () => {
    const user = userEvent.setup({ applyAccept: false });
    let createRequests = 0;
    server.use(http.post(`${env.API_URL}/api/v1/video-uploads`, () => {
      createRequests += 1;
      return HttpResponse.json(upload(), { status: 201 });
    }));
    renderRoute();
    await screen.findByRole('heading', { name: 'Nenhum vídeo ainda' });
    const oversized = videoFile();
    Object.defineProperty(oversized, 'size', { value: 6 * 1024 * 1024 * 1024 });

    await chooseFile(user, oversized);

    expect(await screen.findByRole('alert')).toHaveTextContent('O arquivo deve ter até 5 GiB.');
    expect(createRequests).toBe(0);
  });

  it('uploads file slices without cookies and shows the received video', async () => {
    const user = userEvent.setup();
    let completed = false;
    const uploadedParts: number[] = [];
    const storageCookies: Array<string | null> = [];
    server.use(
      http.get(`${env.API_URL}/api/v1/videos`, () => jsonVideoPage(completed ? [video] : [])),
      http.post(`${env.API_URL}/api/v1/video-uploads`, async ({ request }) => {
        const body = await request.json() as { title: string; fileName: string; fileSize: number; contentType: string; fingerprint: string };
        expect(body).toMatchObject({ title: 'aula', fileName: 'aula.mp4', fileSize: 4, contentType: 'video/mp4' });
        expect(body.fingerprint).toHaveLength(64);
        return HttpResponse.json(upload({ title: body.title, fileName: body.fileName, fileSize: body.fileSize }), { status: 201 });
      }),
      http.post(`${env.API_URL}/api/v1/video-uploads/:id/part-urls`, async ({ request }) => {
        const body = await request.json() as { partNumbers: number[] };
        return HttpResponse.json({
          parts: body.partNumbers.map((partNumber) => ({
            partNumber,
            url: `http://localhost:9000/part-${partNumber}?X-Amz-Signature=private`,
            expiresAt: '2026-09-27T14:05:11Z',
          })),
          uploadExpiresAt: '2026-09-28T14:05:11Z',
        });
      }),
      http.put('http://localhost:9000/:part', ({ request, params }) => {
        uploadedParts.push(Number(String(params.part).replace('part-', '')));
        storageCookies.push(request.headers.get('cookie'));
        return new HttpResponse(null, { status: 200 });
      }),
      http.post(`${env.API_URL}/api/v1/video-uploads/:id/complete`, () => {
        completed = true;
        return HttpResponse.json(video, { status: 201 });
      }),
    );
    renderRoute();
    await screen.findByRole('heading', { name: 'Nenhum vídeo ainda' });
    await chooseFile(user, videoFile());
    await submitUpload(user);

    expect(await screen.findByText('Aula de exemplo')).toBeInTheDocument();
    expect(await screen.findByText('aula recebido. A preparação começou.')).toBeInTheDocument();
    expect(await screen.findByText('Marina Alves')).toBeInTheDocument();
    expect(uploadedParts.sort()).toEqual([1, 2]);
    expect(storageCookies).toEqual([null, null]);
  });

  it('retries an interrupted part and recovers from UPLOAD_INCOMPLETE by sending only missing parts', async () => {
    const user = userEvent.setup();
    const uploadedParts: number[] = [];
    let partOneAttempts = 0;
    let partTwoAttempts = 0;
    let completionAttempts = 0;
    let uploadLookups = 0;
    server.use(
      http.post(`${env.API_URL}/api/v1/video-uploads`, () => HttpResponse.json(upload({
        title: 'aula',
        fileName: 'aula.mp4',
      }), { status: 201 })),
      http.get(`${env.API_URL}/api/v1/video-uploads/${uploadId}`, () => {
        uploadLookups += 1;
        return HttpResponse.json(upload({ receivedParts: [1] }));
      }),
      http.post(`${env.API_URL}/api/v1/video-uploads/:id/part-urls`, async ({ request }) => {
        const body = await request.json() as { partNumbers: number[] };
        return HttpResponse.json({
          parts: body.partNumbers.map((partNumber) => ({
            partNumber,
            url: `http://localhost:9000/part-${partNumber}?X-Amz-Signature=private`,
            expiresAt: '2026-09-27T14:05:11Z',
          })),
          uploadExpiresAt: '2026-09-28T14:05:11Z',
        });
      }),
      http.put('http://localhost:9000/:part', ({ params }) => {
        const partNumber = Number(String(params.part).replace('part-', ''));
        uploadedParts.push(partNumber);
        if (partNumber === 1) {
          partOneAttempts += 1;
          return partOneAttempts === 1 ? HttpResponse.error() : new HttpResponse(null, { status: 200 });
        }
        partTwoAttempts += 1;
        return new HttpResponse(null, { status: 200 });
      }),
      http.post(`${env.API_URL}/api/v1/video-uploads/:id/complete`, () => {
        completionAttempts += 1;
        return completionAttempts === 1
          ? HttpResponse.json({ code: 'UPLOAD_INCOMPLETE' }, { status: 422 })
          : HttpResponse.json(video, { status: 201 });
      }),
    );
    renderRoute();
    await screen.findByRole('heading', { name: 'Nenhum vídeo ainda' });
    await chooseFile(user, videoFile());
    await submitUpload(user);

    expect(await screen.findByText('aula recebido. A preparação começou.')).toBeInTheDocument();
    expect(partOneAttempts).toBe(2);
    expect(partTwoAttempts).toBe(2);
    expect(uploadLookups).toBe(1);
    expect(uploadedParts.filter((partNumber) => partNumber === 1)).toHaveLength(2);
    expect(uploadedParts.filter((partNumber) => partNumber === 2)).toHaveLength(2);
  });

  it('asks before navigating away while an upload is active', async () => {
    const user = userEvent.setup();
    server.use(
      http.post(`${env.API_URL}/api/v1/video-uploads`, () => HttpResponse.json(upload({
        title: 'aula',
        fileName: 'aula.mp4',
      }), { status: 201 })),
      http.put('http://localhost:9000/:part', async () => {
        await new Promise((resolve) => window.setTimeout(resolve, 3000));
        return new HttpResponse(null, { status: 200 });
      }),
    );
    renderRoute();
    await screen.findByRole('heading', { name: 'Nenhum vídeo ainda' });
    await chooseFile(user, videoFile());
    await submitUpload(user);
    await screen.findByRole('region', { name: 'Transferência de vídeo' });
    await user.click(screen.getByRole('link', { name: 'Outra área' }));

    const alert = await screen.findByRole('alertdialog', { name: 'Sair interrompe o envio' });
    expect(alert).toHaveTextContent(/aula\.mp4 está em \d+%\. O que já foi enviado fica guardado até/);
    const continueButtons = within(alert).getAllByRole('button', { name: 'Continuar enviando' });
    await user.click(continueButtons[continueButtons.length - 1]!);
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Vídeos da escola' })).toBeInTheDocument();

    await user.click(screen.getByRole('link', { name: 'Outra área' }));
    await screen.findByRole('alertdialog', { name: 'Sair interrompe o envio' });
    await user.click(screen.getByRole('button', { name: 'Sair mesmo assim' }));
    expect(await screen.findByRole('heading', { name: 'Outra área' })).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
  });
});
