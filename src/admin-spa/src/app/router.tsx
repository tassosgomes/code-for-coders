import { createBrowserRouter } from 'react-router';

import { routes } from '@/app/app-routes';

export const router = createBrowserRouter(routes, { basename: import.meta.env.BASE_URL });
