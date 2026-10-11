import type { ReactNode } from 'react';
import { CircleAlert } from 'lucide-react';

type AuthNoticeProps = {
  tone: 'danger' | 'neutral';
  role: 'alert' | 'status';
  title?: string;
  children: ReactNode;
};

export const AuthNotice = ({ tone, role, title, children }: AuthNoticeProps) => (
  <div className={`auth-notice auth-notice-${tone}`} role={role}>
    <CircleAlert aria-hidden="true" size={16} />
    <div className="auth-notice-content">
      {title ? <p className="auth-notice-title">{title}</p> : null}
      <p>{children}</p>
    </div>
  </div>
);
