import { Link } from 'react-router';

import { paths } from '@/config/paths';

type ForbiddenScreenProps = {
  path?: string;
};

export const ForbiddenScreen = ({ path }: ForbiddenScreenProps) => {
  const shownPath = path ?? window.location.pathname;

  return <main className="page-shell forbidden-page">
    <section className="forbidden-card">
      <div aria-hidden="true" className="code-window">
        <div className="code-title"><span className="window-dots"><i /><i /><i /></span>terminal</div>
        <pre><span className="code-ln">1  </span>$ GET {shownPath}{'\n'}<span className="code-ln">2  </span><span className="code-error">403 Forbidden</span></pre>
      </div>
      <h1>Esta área não é do seu papel</h1>
      <p>Se você precisa dela, peça a um administrador. O menu só mostra as áreas das suas permissões.</p>
      <Link className="outline-button" to={paths.home.getHref()}>Voltar para o início</Link>
    </section>
  </main>;
};
