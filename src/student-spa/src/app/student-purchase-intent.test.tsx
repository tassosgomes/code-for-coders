import { screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { expect, it } from 'vitest';

import { StudentShowcaseCourseRoute } from '@/app/routes/student-showcase-course-route';
import { env } from '@/config/env';
import { paths } from '@/config/paths';
import { server } from '@/testing/server';
import { purchaseCourseId, purchaseOfferId, purchaseShowcaseCourse } from '@/testing/student-purchase-data';
import { renderWithProviders } from '@/testing/test-utils';

it('Comprar is a summary link supporting a new tab without anonymous counting', async () => {
  let clicks = 0;
  server.use(http.get(`${env.API_URL}/api/v1/showcase/courses/:courseId`, () => HttpResponse.json(purchaseShowcaseCourse)), http.post(`${env.API_URL}/api/v1/showcase/offers/:offerId/purchase-intents`, () => { clicks++; return HttpResponse.json({ purchaseAvailability: 'available' }); }));
  renderWithProviders(<RouterProvider router={createMemoryRouter([{ path: paths.studentShowcaseCourse.path, element: <StudentShowcaseCourseRoute /> }], { initialEntries: [paths.studentShowcaseCourse.getHref(purchaseCourseId)] })} />);
  expect(await screen.findByRole('link', { name: 'Comprar Acesso por 12 meses' })).toHaveAttribute('href', paths.studentPurchase.getHref(purchaseOfferId, purchaseCourseId));
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument(); expect(clicks).toBe(0);
});
