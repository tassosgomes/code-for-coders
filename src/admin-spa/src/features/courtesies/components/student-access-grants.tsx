import { useState } from 'react';

import { useStudentAccessGrants } from '@/features/courtesies/api/list-student-access-grants';
import { AccessGrantRow } from '@/features/courtesies/components/access-grant-row';

type StudentAccessGrantsProps = { studentId: string; enabled: boolean };
export const StudentAccessGrants = ({ studentId, enabled }: StudentAccessGrantsProps) => {
  const query = useStudentAccessGrants(studentId, enabled);
  const [visible, setVisible] = useState(10);
  return <section aria-label="Concessões do aluno" className="courtesy-student-grants">
    <h3>Concessões do aluno</h3>
    {query.isFetching ? <p role="status">Carregando concessões…</p> : null}
    {query.isError ? <div role="alert"><p>Não foi possível carregar as concessões do aluno.</p><button type="button" className="secondary-button" onClick={() => { void query.refetch(); }}>Tentar de novo</button></div> : null}
    {!query.isFetching && !query.isError && query.data?.length === 0 ? <p>Este aluno ainda não tem concessões.</p> : null}
    {query.data?.length ? <ul className="courtesy-access-grant-list">{query.data.slice(0, visible).map((grant) => <AccessGrantRow key={grant.grantId} grant={grant} />)}</ul> : null}
    {query.data && visible < query.data.length ? <button type="button" className="secondary-button" onClick={() => setVisible(visible + 10)}>Ver mais concessões</button> : null}
  </section>;
};
