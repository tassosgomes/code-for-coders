import type { StudentAccount } from '@/features/courtesies/api/lookup-student-account';

type StudentAccountCardProps = { account: StudentAccount; onChange: () => void };
export const StudentAccountCard = ({ account, onChange }: StudentAccountCardProps) => <section className="courtesy-student-card" aria-label="Aluno localizado">
  <div className="courtesy-student-heading"><div><h2>{account.name}</h2><p>{account.email}</p></div>
    <span className={`role-badge courtesy-account-status ${account.status === 'active' ? '' : 'field-error'}`}>{account.status === 'active' ? 'Conta ativa' : 'Conta desativada'}</span>
  </div>
  {!account.emailConfirmed ? <p className="courtesy-warning" role="status">E-mail ainda não confirmado. Isso não impede a cortesia.</p> : null}
  {account.status !== 'active' ? <p className="inline-alert" role="alert">Esta conta está desativada. Não é possível conceder cortesia.</p> : null}
  <button className="secondary-button" onClick={onChange} type="button">Trocar aluno</button>
</section>;
