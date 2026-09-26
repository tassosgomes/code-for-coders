import type { ReactNode } from 'react';
import { CodeXml } from 'lucide-react';

type AuthLayoutProps = { children: ReactNode };

export const AuthLayout = ({ children }: AuthLayoutProps) => (
  <main className="auth-layout">
    <div className="auth-form-side">
      <div className="backoffice-brand">
        <span className="brand-mark"><CodeXml size={18} strokeWidth={2} /></span>
        <span>Code4Coders</span><span className="brand-badge">Backoffice</span>
      </div>
      <div className="auth-content">{children}</div>
      <small className="auth-copyright">© {new Date().getFullYear()} Code4Coders · Área restrita à equipe</small>
    </div>
    <aside aria-hidden="true" className="auth-panel">
      <div className="auth-panel-content">
        <div className="code-window">
          <div className="code-title"><span className="window-dots"><i /><i /><i /></span>acesso.ts <span>TS</span></div>
          <pre><span>1  // acesso.ts</span>{'\n'}2  if (!voce.pode(<em>"financeiro.ler"</em>)){'\n'}<span>3    return negar();  // 403</span>{'\n'}4{'\n'}5  abrir(area);  <strong>// ✓ permitido</strong></pre>
        </div>
        <h2>Operação da escola, com <span>acesso por papel.</span></h2>
        <p>Área restrita à equipe. Cada pessoa vê só o que é dela.</p>
      </div>
    </aside>
  </main>
);
