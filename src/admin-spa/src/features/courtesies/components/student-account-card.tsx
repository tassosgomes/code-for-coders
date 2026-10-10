import { Info, TriangleAlert } from 'lucide-react';

import type { StudentAccount } from '@/features/courtesies/api/lookup-student-account';

const initials = (name: string) => {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  const first = parts[0]?.[0] ?? '';
  const last = parts.length > 1 ? (parts[parts.length - 1]?.[0] ?? '') : '';
  return `${first}${last}`.toUpperCase();
};

type StudentAccountCardProps = { account: StudentAccount };
export const StudentAccountCard = ({ account }: StudentAccountCardProps) => {
  const active = account.status === 'active';
  const pill = !active ? <span className="courtesy-pill courtesy-pill-danger">Desativada</span>
    : !account.emailConfirmed ? <span className="courtesy-pill courtesy-pill-warning">E-mail não confirmado</span>
      : <span className="courtesy-pill courtesy-pill-success">Ativa</span>;
  return <section className="courtesy-student-card" aria-label="Aluno localizado">
    <div className="courtesy-student-heading">
      <span className="courtesy-avatar" aria-hidden="true">{initials(account.name)}</span>
      <div className="courtesy-student-identity"><h2>{account.name}</h2><p>{account.email}</p></div>
      {pill}
    </div>
    {active && !account.emailConfirmed ? <p className="courtesy-info-box" role="status"><Info size={16} aria-hidden="true" />E-mail ainda não confirmado. Você pode conceder; o aluno usa o acesso depois de confirmar e entrar.</p> : null}
    {!active ? <p className="courtesy-disabled-box" role="alert"><TriangleAlert size={16} aria-hidden="true" />Esta conta está desativada e não pode receber cortesia.</p> : null}
  </section>;
};
