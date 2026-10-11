import type { ReactNode } from 'react';
import { CircleAlert, Info } from 'lucide-react';

type StaffDialogNoticeProps = {
  children: ReactNode;
  title?: string;
  tone: 'info' | 'warning';
};

export const StaffDialogNotice = ({ children, title, tone }: StaffDialogNoticeProps) => (
  <div className={`staff-notice staff-notice-${tone}`}>
    {tone === 'warning' ? <CircleAlert aria-hidden="true" size={16} /> : <Info aria-hidden="true" size={16} />}
    <div>{title ? <strong>{title}</strong> : null}<p>{children}</p></div>
  </div>
);
