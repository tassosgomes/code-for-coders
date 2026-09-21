import { isRouteErrorResponse, Link, useRouteError } from 'react-router';

import { paths } from '@/config/paths';

export const RouteError = () => {
  const error = useRouteError();
  const message = isRouteErrorResponse(error)
    ? `The requested route returned ${error.status}.`
    : 'The student workspace could not render this route.';

  return (
    <main className="page-shell" role="alert">
      <p className="eyebrow">Student Workspace</p>
      <h1>Something needs attention</h1>
      <p>{message}</p>
      <Link className="primary-link" to={paths.home.getHref()}>
        Return to overview
      </Link>
    </main>
  );
};
