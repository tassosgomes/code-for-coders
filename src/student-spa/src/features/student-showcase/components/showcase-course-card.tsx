import { ArrowRight, CodeXml } from 'lucide-react';
import { Link } from 'react-router';

import { Badge } from '@/components/ui/badge';
import { Card } from '@/components/ui/card';
import { paths } from '@/config/paths';
import type { ShowcaseCourseCard as ShowcaseCourseCardData } from '@/features/student-showcase/api/get-showcase-courses';
import { formatPriceCents } from '@/features/student-showcase/utils/format-price';
import { getLevelLabel } from '@/features/student-showcase/utils/showcase-level';

type ShowcaseCourseCardProps = {
  course: ShowcaseCourseCardData;
};

export const ShowcaseCourseCard = ({ course }: ShowcaseCourseCardProps) => {
  const levelLabel = getLevelLabel(course.level);
  const hasSeveralOffers = course.offerCount > 1;

  return (
    <Card className="group relative h-full gap-0 overflow-hidden py-0 transition-shadow focus-within:ring-[3px] focus-within:ring-ring/50 hover:shadow-md">
      <div
        aria-hidden="true"
        className="hidden h-32 items-center justify-center bg-inverse text-inverse-foreground sm:flex"
      >
        <CodeXml className="size-10" />
      </div>
      <div className="flex flex-1 flex-col gap-3 p-5">
        {levelLabel ? <Badge variant="secondary">{levelLabel}</Badge> : null}
        <h2 className="font-heading text-lg font-semibold leading-snug text-foreground">
          <Link
            className="outline-none after:absolute after:inset-0 after:content-['']"
            to={paths.studentShowcaseCourse.getHref(course.courseId)}
          >
            {course.title}
          </Link>
        </h2>
        <p className="text-sm leading-relaxed text-muted-foreground">{course.summary}</p>
        <div className="mt-auto flex items-end justify-between gap-3 pt-2">
          <div>
            <p className="text-base font-semibold text-foreground">
              {hasSeveralOffers ? `a partir de ${formatPriceCents(course.lowestPriceCents)}` : formatPriceCents(course.lowestPriceCents)}
            </p>
            {hasSeveralOffers ? (
              <p className="text-xs text-muted-foreground">{course.offerCount} opções de acesso</p>
            ) : null}
          </div>
          <span aria-hidden="true" className="inline-flex items-center gap-1 text-sm font-medium text-primary">
            Ver
            <ArrowRight className="size-4 transition-transform group-hover:translate-x-0.5" />
          </span>
        </div>
      </div>
    </Card>
  );
};
