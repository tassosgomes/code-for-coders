import { useState } from 'react';
import { CodeXml, BookOpen, ChevronDown, Clapperboard, House, Gift, LogOut, Menu, ScrollText, Tags, Users, Wallet, X } from 'lucide-react';
import { Link, NavLink, Outlet } from 'react-router';

import { ThemeMenu } from '@/components/theme-menu';
import { paths } from '@/config/paths';
import { useDocumentTitle } from '@/hooks/use-document-title';
import { useShellStore } from '@/stores/use-shell-store';
import type { StaffArea } from '@/types/staff-area';

type AppShellProps = {
  serviceName: string;
  title: string;
  areas: readonly StaffArea[];
  name: string;
  roles: readonly string[];
  outletContext: unknown;
  isLoggingOut: boolean;
  logoutError: string | null;
  onLogout: () => void;
};

const formatRole = (role: string) => role.charAt(0).toUpperCase() + role.slice(1);

export const AppShell = ({ serviceName, title, areas, name, roles, outletContext, isLoggingOut, logoutError, onLogout }: AppShellProps) => {
  useDocumentTitle(title);
  const menuOpen = useShellStore((state) => state.menuOpen);
  const toggleMenu = useShellStore((state) => state.toggleMenu);
  const [accountOpen, setAccountOpen] = useState(false);
  const initials = name.split(/\s+/).slice(0, 2).map((part) => part[0]).join('').toUpperCase();

  return (
    <div className="app-shell">
      <aside className={`app-sidebar ${menuOpen ? 'is-open' : ''}`}>
        <Link aria-label={`${serviceName} — início`} className="backoffice-brand" to={paths.home.getHref()}>
          <span className="brand-mark"><CodeXml size={18} /></span><span>Code4Coders</span><span className="brand-badge">Backoffice</span>
        </Link>
        <nav aria-label={`${serviceName} navigation`} className="app-nav">
          <NavLink end to={paths.home.getHref()}><House size={18} />Início</NavLink>
          {(['content', 'commerce', 'finance', 'operations'] as const).map((section) => {
            const group = areas.filter((area) => {
              const areaSection = area.permission === 'autoria.ler' || area.permission === 'midia.enviar' ? 'content'
                : area.permission === 'oferta.editar' ? 'commerce' : area.permission === 'financeiro.ler' || area.permission === 'cortesia.conceder' ? 'finance' : 'operations';
              return areaSection === section && area.href;
            });
            if (group.length === 0) return null;
            return <div className="sidebar-group" key={section}><p className="sidebar-heading">{section === 'content' ? 'Conteúdo' : section === 'commerce' ? 'Comercial' : section === 'finance' ? 'Financeiro' : 'Operação'}</p>
              {group.map((area) => <NavLink key={area.label} to={area.href!}>
                {area.permission === 'autoria.ler' ? <BookOpen size={18} /> : area.permission === 'midia.enviar' ? <Clapperboard size={18} /> : area.permission === 'oferta.editar' ? <Tags size={18} /> : area.permission === 'cortesia.conceder' ? <Gift size={18} /> : area.permission === 'financeiro.ler' ? <Wallet size={18} /> : area.role === 'administrador' ? <ScrollText size={18} /> : <Users size={18} />}
                {area.label}
              </NavLink>)}
            </div>;
          })}
        </nav>
      </aside>
      <div className="app-main">
        <header className="app-header">
          <button aria-expanded={menuOpen} aria-label={menuOpen ? 'Fechar navegação' : 'Abrir navegação'} className="menu-button" onClick={toggleMenu} type="button">
            {menuOpen ? <X size={20} /> : <Menu size={20} />}
          </button>
          <Link aria-label={`${serviceName} — início`} className="mobile-backoffice-brand" to={paths.home.getHref()}>
            <span className="brand-mark"><CodeXml size={18} /></span><span>Code4Coders</span><span className="brand-badge">Backoffice</span>
          </Link>
          <ThemeMenu />
          <div className="account-menu-wrap">
            <button aria-expanded={accountOpen} aria-haspopup="menu" className="account-trigger" onClick={() => setAccountOpen(!accountOpen)} type="button">
              <span className="account-avatar">{initials}</span><span>{name}</span><ChevronDown size={16} />
            </button>
            {accountOpen ? <div aria-label="Conta" className="account-menu" role="menu">
              <div className="account-menu-label"><span className="account-menu-name">{name}</span>{roles.length ? <span className="account-menu-roles">{roles.map(formatRole).join(', ')}</span> : null}</div>
              <hr className="account-menu-separator" />
              <button className="account-menu-item" disabled={isLoggingOut} onClick={onLogout} role="menuitem" type="button"><LogOut aria-hidden="true" size={16} />{isLoggingOut ? 'Saindo…' : 'Sair'}</button>
            </div> : null}
          </div>
        </header>
        {logoutError ? <p className="inline-alert" role="alert">{logoutError}</p> : null}
        <Outlet context={outletContext} />
      </div>
    </div>
  );
};
