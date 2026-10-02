import { ArrowRight, ScrollText, Users, Wallet } from 'lucide-react';
import { Link } from 'react-router';

import type { StaffArea } from '@/types/staff-area';

type DashboardScreenProps = {
  areas?: readonly StaffArea[];
  name?: string;
  roles?: readonly string[];
};

export const DashboardScreen = ({ areas = [], name = '', roles = [] }: DashboardScreenProps) => {
  const firstName = name.split(' ')[0];
  const linkedAreas = areas.filter((area) => area.href).sort((first, second) => Number(second.permission === 'autoria.ler') - Number(first.permission === 'autoria.ler'));

  return <main className="page-shell dashboard-page authoring-page">
    <header className="dashboard-heading"><p className="eyebrow">Início</p>
    <h1>Olá, {firstName || 'equipe'}</h1><p className="page-subtitle">Organize o conteúdo da sua escola.</p></header>
    <p className="dashboard-roles"><span className="sr-only">Seus papéis: </span> {roles.length ? roles.map((role) => <span className="role-badge" key={role}>{role}</span>) : <span className="role-badge role-badge-empty">Sem papel</span>}</p>
    {linkedAreas.length ? <div className="area-grid">{linkedAreas.map((area) => <article className={area.permission === 'midia.enviar' ? 'area-card videos-area-card' : 'area-card'} key={area.permission ?? area.role ?? area.label}>
      <span className="area-icon">{area.permission === 'financeiro.ler'
        ? <Wallet size={22} />
        : area.permission === 'autoria.ler' ? <Users size={24} />
        : area.permission === 'midia.enviar'
          ? <Users size={24} />
          : area.role === 'administrador'
            ? <ScrollText size={22} />
          : <Users size={22} />}</span>
      <h2>{area.label}</h2>
      <p>{area.permission === 'acesso.gerir'
        ? 'Convide pessoas e ajuste os papéis da equipe.'
        : area.permission === 'autoria.ler' ? 'Monte módulos e aulas, depois publique.'
        : area.permission === 'midia.enviar'
          ? 'Envie gravações e acompanhe a preparação.'
          : area.role === 'administrador'
            ? 'Consulte os atos administrativos registrados para o seu tenant.'
            : 'Área financeira reservada à equipe autorizada.'}</p>
      <Link className="outline-button" to={area.href!}>Abrir {area.label} <ArrowRight size={16} /></Link>
    </article>)}</div> : <section className="empty-state"><h2>{roles.length ? 'As ferramentas do seu papel chegam em breve' : 'Você ainda não tem acesso a uma área'}</h2><p>{roles.length ? 'Sua conta está ativa. Volte em breve para usar as ferramentas da equipe.' : 'Peça a um administrador para conceder um papel à sua conta.'}</p></section>}
  </main>;
};
