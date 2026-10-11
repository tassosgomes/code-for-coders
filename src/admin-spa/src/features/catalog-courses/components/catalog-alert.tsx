import { CircleAlert, Info } from 'lucide-react';

import type { CatalogProblem } from '@/features/catalog-courses/types/catalog-offer';

type CatalogAlertProps = CatalogProblem & { tone?: 'danger' | 'warning' | 'info'; onRetry?: () => void; retryType?: 'button' | 'submit' };
const roles = { danger: 'alert', warning: 'note', info: 'note' } as const;

export const CatalogAlert = ({ title, detail, retry = false, tone = 'danger', onRetry, retryType = 'button' }: CatalogAlertProps) => <div className={`catalog-alert is-${tone}`} role={roles[tone]}>
  <p className="catalog-alert-title">{tone === 'info' ? <Info aria-hidden="true" size={16} /> : <CircleAlert aria-hidden="true" size={16} />}{title}</p>
  {detail ? <p className="catalog-alert-detail">{detail}</p> : null}
  {retry && (onRetry || retryType === 'submit') ? <button className="catalog-alert-action" onClick={onRetry} type={retryType}>Tentar de novo</button> : null}
</div>;
