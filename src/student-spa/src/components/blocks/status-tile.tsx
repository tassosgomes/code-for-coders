import type { ReactNode } from 'react';
import { CheckCircle2, Info, TriangleAlert } from 'lucide-react';

import { Card, CardContent, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';

type StatusTileProps = {
  announceAs?: 'alert' | 'status';
  children?: ReactNode;
  description: ReactNode;
  title: string;
  tone?: 'info' | 'success' | 'warning';
};

const toneStyles = {
  info: {
    icon: Info,
    iconClassName: 'bg-secondary text-secondary-foreground',
  },
  success: {
    icon: CheckCircle2,
    iconClassName: 'bg-success-soft text-success-soft-foreground',
  },
  warning: {
    icon: TriangleAlert,
    iconClassName: 'bg-warning-soft text-warning-soft-foreground',
  },
} as const;

export const StatusTile = ({
  announceAs = 'status',
  children,
  description,
  title,
  tone = 'info',
}: StatusTileProps) => {
  const { icon: Icon, iconClassName } = toneStyles[tone];

  return (
    <Card aria-live={announceAs === 'alert' ? 'assertive' : 'polite'} role={announceAs}>
      <CardHeader>
        <div className="flex items-start gap-4">
          <span className={`grid size-11 shrink-0 place-items-center rounded-xl ${iconClassName}`}>
            <Icon aria-hidden="true" className="size-5" />
          </span>
          <CardTitle className="typo-h4 pt-1">
            <h1>{title}</h1>
          </CardTitle>
        </div>
      </CardHeader>
      <CardContent className="pt-0 text-sm leading-relaxed text-muted-foreground">
        {description}
      </CardContent>
      {children ? <CardFooter className="flex-wrap gap-3">{children}</CardFooter> : null}
    </Card>
  );
};
