import { Link } from 'react-router';

import { paths } from '@/config/paths';

type FinanceAreaScreenProps = { state: 'forbidden' | 'loading' | 'unavailable' | 'ready' };

export const FinanceAreaScreen = ({ state }: FinanceAreaScreenProps) => {
  if (state === 'forbidden') return <main className="page-shell"><section className="empty-state finance-state">
    <div aria-hidden="true" className="code-window"><div className="code-title"><span className="window-dots"><i /><i /><i /></span>terminal</div><pre>1  $ GET /admin/financeiro{'\n'}2  <span className="code-error">403 Forbidden</span></pre></div>
    <h1>Esta área não é do seu papel</h1><p>Se você precisa dela, peça a um administrador. O menu só mostra as áreas das suas permissões.</p>
    <Link className="outline-button" to={paths.home.getHref()}>Voltar para o início</Link>
  </section></main>;

  return <main className="page-shell finance-page"><p className="eyebrow">Financeiro</p><h1>Financeiro</h1>
    <section className="empty-state finance-state"><div aria-hidden="true" className="code-window"><div className="code-title"><span className="window-dots"><i /><i /><i /></span>finance-area.http</div><pre>1  GET /api/v1/finance-area{'\n'}2  <strong>→ 200</strong> {'{ "status": "reserved" }'}</pre></div>
      {state === 'ready' ? <><h2>Área reservada</h2><p role="status">Seu acesso está liberado. Vendas, pedidos e repasses aparecem aqui quando o módulo financeiro chegar.</p></> : <p role={state === 'unavailable' ? 'alert' : 'status'}>{state === 'loading' ? 'Carregando a área financeira…' : 'Não foi possível carregar a área financeira.'}</p>}
    </section>
  </main>;
};
