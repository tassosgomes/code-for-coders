import type { ElementType, ReactNode } from 'react';

type AuthResultCardProps = {
  tone: 'neutral' | 'success' | 'warning';
  icon: ElementType<{ size?: number; 'aria-hidden'?: boolean }>;
  title: string;
  titleId: string;
  description: string;
  alert?: boolean;
  children?: ReactNode;
};

export const AuthResultCard = ({ tone, icon: Icon, title, titleId, description, alert = false, children }: AuthResultCardProps) => (
  <section aria-labelledby={titleId} className="password-reset-card auth-card auth-result-card">
    <span aria-hidden="true" className={`auth-status-tile auth-status-tile-${tone}`}><Icon aria-hidden size={24} /></span>
    <div className="auth-card-header">
      <h1 id={titleId}>{title}</h1>
      <p role={alert ? 'alert' : 'status'}>{description}</p>
    </div>
    {children ? <div className="auth-result-actions">{children}</div> : null}
  </section>
);
