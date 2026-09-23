import { Link, Outlet } from 'react-router';

import { paths } from '@/config/paths';
import { useDocumentTitle } from '@/hooks/use-document-title';
import { useShellStore } from '@/stores/use-shell-store';

type AppShellProps = {
  serviceName: string;
  title: string;
};

export const AppShell = ({ serviceName, title }: AppShellProps) => {
  useDocumentTitle(title);
  const menuOpen = useShellStore((state) => state.menuOpen);
  const toggleMenu = useShellStore((state) => state.toggleMenu);

  return (
    <div className="app-shell">
      <header className="app-header">
        <Link className="brand" to={paths.home.getHref()}>
          {serviceName}
        </Link>
        <button
          aria-expanded={menuOpen}
          aria-label="Toggle navigation"
          className="menu-button"
          type="button"
          onClick={toggleMenu}
        >
          {menuOpen ? 'Close menu' : 'Open menu'}
        </button>
      </header>
      {menuOpen ? (
        <nav aria-label={`${serviceName} navigation`} className="app-nav">
          <Link to={paths.home.getHref()}>Overview</Link>
          <Link to={paths.studentRegistration.getHref()}>Criar conta</Link>
          <Link to={paths.studentLogin.getHref()}>Entrar</Link>
        </nav>
      ) : null}
      <Outlet />
    </div>
  );
};
