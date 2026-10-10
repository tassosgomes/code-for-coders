import { CircleAlert } from 'lucide-react';
import { useState } from 'react';

import { useStudentAccessGrants } from '@/features/courtesies/api/list-student-access-grants';
import { AccessGrantRow } from '@/features/courtesies/components/access-grant-row';

const pageSize = 10;
type StudentAccessGrantsProps = { studentId: string; enabled: boolean };
export const StudentAccessGrants = ({ studentId, enabled }: StudentAccessGrantsProps) => {
  const query = useStudentAccessGrants(studentId, enabled);
  const [page, setPage] = useState(1);
  const data = query.data ?? [];
  const totalPages = Math.max(1, Math.ceil(data.length / pageSize));
  const current = Math.min(page, totalPages);
  const loading = query.isFetching && !query.isError && data.length === 0;
  return <section aria-label="Concessões do aluno" className="courtesy-student-grants">
    <div className="courtesy-grants-header"><span className="courtesy-section-label">Concessões desta conta</span><span className="courtesy-grants-size">{pageSize}/página</span></div>
    {loading ? <div className="courtesy-skeleton-stack" role="status" aria-label="Carregando concessões">{[0, 1, 2].map((item) => <span key={item} className="courtesy-skeleton courtesy-skeleton-row" />)}</div> : null}
    {query.isError ? <div className="courtesy-error-box" role="alert"><CircleAlert size={16} aria-hidden="true" /><p>Não foi possível carregar as concessões do aluno.</p><button type="button" className="text-button" onClick={() => { void query.refetch(); }}>Tentar de novo</button></div> : null}
    {!query.isFetching && !query.isError && query.data?.length === 0 ? <p className="courtesy-empty-box">Nenhuma concessão para este aluno ainda.</p> : null}
    {data.length ? <div className="courtesy-grants-table">
      <div className="courtesy-grants-head" aria-hidden="true"><span>Curso</span><span>Origem</span><span>Vigência</span><span>Situação</span></div>
      <ul className="courtesy-access-grant-list">{data.slice((current - 1) * pageSize, current * pageSize).map((grant) => <AccessGrantRow key={grant.grantId} grant={grant} />)}</ul>
      <div className="courtesy-grants-footer"><span>Vigência sempre em data absoluta.</span>
        {totalPages > 1 ? <nav className="courtesy-grants-pagination" aria-label="Paginação das concessões">
          <button type="button" aria-label="Página anterior" disabled={current === 1} onClick={() => setPage(current - 1)}>‹</button>
          {Array.from({ length: totalPages }, (_, index) => index + 1).map((number) => <button key={number} type="button" aria-label={`Página ${number}`} aria-current={number === current ? 'page' : undefined} onClick={() => setPage(number)}>{number}</button>)}
          <button type="button" aria-label="Próxima página" disabled={current === totalPages} onClick={() => setPage(current + 1)}>›</button>
        </nav> : null}
      </div>
    </div> : null}
  </section>;
};
