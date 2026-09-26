import { isRouteErrorResponse, Link, useRouteError } from 'react-router';

import { paths } from '@/config/paths';

export const RouteError = ({ notFound: explicitNotFound = false }: { notFound?: boolean }) => {
  const error = useRouteError();
  const notFound = explicitNotFound || (isRouteErrorResponse(error) && error.status === 404);
  const title = notFound ? 'Página não encontrada' : 'Algo saiu do esperado';
  const message = notFound
    ? 'Não encontramos esta página do backoffice.'
    : 'Não foi possível abrir esta página agora. Tente novamente em instantes.';

  return (
    <main className="page-shell error-page" role="alert">
      <section className="empty-state">
        <p className="eyebrow">Backoffice · {notFound ? '404' : 'Erro'}</p>
        <h1>{title}</h1>
        <p>{message}</p>
        <Link className="outline-button" to={paths.home.getHref()}>Voltar para o início</Link>
      </section>
    </main>
  );
};
