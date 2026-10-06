import { Link } from 'react-router';

import { paths } from '@/config/paths';

export const FinanceAreaScreen = () => <main className="page-shell"><section className="empty-state finance-state">
  <h1>Esta área não é do seu papel</h1><p>Se você precisa dela, peça a um administrador. O menu só mostra as áreas das suas permissões.</p>
  <Link className="outline-button" to={paths.home.getHref()}>Voltar para o início</Link>
</section></main>;
