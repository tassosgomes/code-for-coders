import { CodeXml } from 'lucide-react';

type BrandLogoProps = {
  compact?: boolean;
};

export const BrandLogo = ({ compact = false }: BrandLogoProps) => (
  <span className="inline-flex items-center gap-3 text-foreground">
    <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-primary text-primary-foreground shadow-xs">
      <CodeXml aria-hidden="true" className="size-5" strokeWidth={2} />
    </span>
    {compact ? null : (
      <span className="font-heading text-lg font-bold tracking-tight">
        Code<span className="text-primary">4</span>Coders
      </span>
    )}
  </span>
);
