import { AlertTriangle, CircleCheck, CircleMinus, Clock } from 'lucide-react';

import type { CatalogCourse } from '@/features/catalog-courses/api/get-catalog-courses';
import type { CatalogOffer } from '@/features/catalog-courses/types/catalog-offer';

export const levelLabels = { beginner: 'Iniciante', intermediate: 'Intermediário', advanced: 'Avançado' };

export const LevelBadge = ({ level }: { level: CatalogCourse['level'] }) => level
  ? <span className="catalog-badge">{levelLabels[level]}</span>
  : <span className="catalog-badge-group"><AlertTriangle aria-hidden="true" size={16} /><span className="catalog-badge is-warning">Sem nível</span></span>;

export const ShowcaseBadge = ({ inShowcase }: { inShowcase: boolean }) => inShowcase
  ? <span className="catalog-badge-group"><CircleCheck aria-hidden="true" className="is-success" size={16} /><span className="catalog-badge is-success">Na vitrine</span></span>
  : <span className="catalog-badge-group"><CircleMinus aria-hidden="true" className="is-muted" size={16} /><span className="catalog-badge is-outline">Fora da vitrine</span></span>;

export const ShowcaseState = ({ inShowcase }: { inShowcase: boolean }) => <span className={`catalog-showcase-state ${inShowcase ? 'is-on' : 'is-off'}`}>
  {inShowcase ? <CircleCheck aria-hidden="true" size={16} /> : <CircleMinus aria-hidden="true" size={16} />}
  <span className="catalog-desktop-only">{inShowcase ? 'Sim' : 'Não'}</span>
  <span className="catalog-mobile-only">{inShowcase ? 'Na vitrine' : 'Fora da vitrine'}</span>
</span>;

const offerStatus = {
  draft: { icon: Clock, label: 'Rascunho', tone: '' },
  published: { icon: CircleCheck, label: 'Publicada', tone: 'is-success' },
  unpublished: { icon: CircleMinus, label: 'Despublicada', tone: 'is-outline' },
};
export const OfferStatusBadge = ({ status }: { status: CatalogOffer['status'] }) => {
  const { icon: Icon, label, tone } = offerStatus[status];
  return <span className="catalog-badge-group"><Icon aria-hidden="true" size={16} /><span className={`catalog-badge ${tone}`}>{label}</span></span>;
};
