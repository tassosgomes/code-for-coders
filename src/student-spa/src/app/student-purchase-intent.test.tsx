import { cleanup, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse, delay } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { StudentShowcaseCourseRoute } from '@/app/routes/student-showcase-course-route';
import { env } from '@/config/env';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const courseId = '0198dfac-674a-7000-8000-000000000041';
const monthlyId = '0198dfac-674a-7000-8000-000000000042';
const lifetimeId = '0198dfac-674a-7000-8000-000000000043';
const course = { courseId, title: 'C#', level: 'beginner', description: 'Aprenda C#', prerequisite: { text: null, recommendedCourses: [] }, modules: [],
  offers: [{ offerId: monthlyId, name: 'Mensal', priceCents: 10000, accessPeriod: { type: 'months', months: 12 } },
    { offerId: lifetimeId, name: 'Vitalício', priceCents: 20000, accessPeriod: { type: 'lifetime' } }] };
const endpoint = `${env.API_URL}/api/v1/showcase/offers/:offerId/purchase-intents`;
const renderCourse = () => renderWithProviders(<RouterProvider router={createMemoryRouter([{ path: '/cursos/:courseId', element: <StudentShowcaseCourseRoute /> }], { initialEntries: [`/cursos/${courseId}`] })} />);

describe('student purchase intent', () => {
  beforeEach(() => server.use(http.get(`${env.API_URL}/api/v1/showcase/courses/:courseId`, () => HttpResponse.json(course))));
  afterEach(cleanup);

  it('opens and announces the notice immediately, sends only the selected offer and restores focus on acknowledgement', async () => {
    const user = userEvent.setup(); let request: Request | undefined;
    server.use(http.post(endpoint, async ({ request: received, params }) => {
      request = received; expect(params.offerId).toBe(lifetimeId); await delay(300);
      return HttpResponse.json({ purchaseAvailability: 'coming-soon' }, { status: 202 });
    }));
    renderCourse(); const buy = await screen.findByRole('button', { name: 'Comprar Vitalício' }); await user.click(buy);
    expect(screen.getByRole('dialog', { name: 'A compra estará disponível em breve' })).toHaveAccessibleDescription(/Não pedimos nenhum dado/);
    expect(screen.getByRole('button', { name: 'Entendi' })).toHaveFocus();
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
    await waitFor(() => expect(request).toBeDefined());
    expect(request?.credentials).toBe('omit'); expect(request?.headers.has('X-CSRF-Token')).toBe(false);
    expect(request?.headers.get('Idempotency-Key')).toMatch(/^[0-9a-f-]{36}$/);
    expect(await request?.text()).toBe('');
    await user.click(screen.getByRole('button', { name: 'Entendi' })); expect(buy).toHaveFocus();
  });

  it.each([404, 429, 500])('keeps the notice visible without an error or retry when counting responds %s', async (status) => {
    const user = userEvent.setup(); let calls = 0; const errors: Event[] = []; const onError = (event: Event) => errors.push(event);
    window.addEventListener('app:api-error', onError);
    server.use(http.post(endpoint, () => { calls++; return HttpResponse.json({ code: 'COUNT_FAILED' }, { status, headers: { 'Retry-After': '60' } }); }));
    try {
      renderCourse(); await user.click(await screen.findByRole('button', { name: 'Comprar Mensal' }));
      await waitFor(() => expect(calls).toBe(1));
      expect(screen.getByRole('dialog')).toHaveTextContent('A compra estará disponível em breve');
      expect(screen.queryByRole('alert')).not.toBeInTheDocument(); expect(errors).toEqual([]);
      await user.keyboard('{Escape}'); expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Comprar Mensal' })).toHaveFocus();
    } finally { window.removeEventListener('app:api-error', onError); }
  });

  it('generates a different key for every click without persisting it or reading the student session', async () => {
    const user = userEvent.setup(); const keys: string[] = []; let sessionReads = 0;
    const local = window.localStorage.length; const session = window.sessionStorage.length;
    server.use(http.post(endpoint, ({ request }) => { keys.push(request.headers.get('Idempotency-Key') ?? ''); return HttpResponse.json({ purchaseAvailability: 'coming-soon' }, { status: 202 }); }),
      http.get(`${env.API_URL}/api/v1/student-sessions/current`, () => { sessionReads++; return HttpResponse.json({}); }));
    renderCourse(); await user.click(await screen.findByRole('button', { name: 'Comprar Mensal' }));
    await user.click(screen.getByRole('button', { name: 'Fechar' }));
    await user.click(screen.getByRole('button', { name: 'Comprar Mensal' }));
    await waitFor(() => expect(keys).toHaveLength(2)); expect(keys[0]).not.toBe(keys[1]);
    expect(window.localStorage.length).toBe(local); expect(window.sessionStorage.length).toBe(session); expect(sessionReads).toBe(0);
  });
});
