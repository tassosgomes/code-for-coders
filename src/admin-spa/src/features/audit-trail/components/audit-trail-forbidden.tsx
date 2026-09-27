import { Link } from 'react-router';

import { paths } from '@/config/paths';

export const AuditTrailForbidden = () => <main className="page-shell"><section className="empty-state finance-state" role="alert">
  <div aria-hidden="true" className="code-window"><div className="code-title"><span className="window-dots"><i /><i /><i /></span>terminal</div><pre>1  $ GET /admin/auditoria{'\n'}2  <span className="code-error">403 Forbidden</span></pre></div>
  <h1>Esta área não é do seu papel</h1><p>Se você precisa dela, peça a um administrador.</p>
  <Link className="outline-button" to={paths.home.getHref()}>Voltar para o início</Link>
</section></main>;
