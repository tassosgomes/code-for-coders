import { CodeXml } from 'lucide-react';
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

// ShowcaseCard do DS (206:155): miniatura de 160 px e corpo com intervalo de 8.
export const ShowcaseCourseCard = ({ course }: ShowcaseCourseCardProps) => {
  const levelLabel = getLevelLabel(course.level);
  const hasSeveralOffers = course.offerCount > 1;

  return (
    <Card className="group relative gap-0 overflow-hidden py-0 transition-shadow focus-within:ring-[3px] focus-within:ring-ring/50 hover:shadow-md">
      <div aria-hidden="true" className="hidden h-40 items-center justify-center bg-code text-code-accent sm:flex">
        <CodeXml className="size-12" strokeWidth={1.5} />
      </div>
      <div className="flex flex-col items-start gap-2 p-5">
        {levelLabel ? <Badge variant="secondary">{levelLabel}</Badge> : null}
        <h2 className="w-full font-heading text-base font-semibold leading-6 text-foreground">
          <Link
            className="outline-none after:absolute after:inset-0 after:content-['']"
            to={paths.studentShowcaseCourse.getHref(course.courseId)}
          >
            {course.title}
          </Link>
        </h2>
        <p className="w-full text-sm leading-5 text-muted-foreground">{course.summary}</p>
        <p className="text-sm font-semibold leading-5 text-foreground">
          {hasSeveralOffers ? `a partir de ${formatPriceCents(course.lowestPriceCents)}` : formatPriceCents(course.lowestPriceCents)}
        </p>
        {hasSeveralOffers ? <p className="text-xs leading-4 text-muted-foreground">{course.offerCount} opções de acesso</p> : null}
        <span aria-hidden="true" className="text-sm font-medium leading-5 text-primary">
          Ver curso <span className="inline-block transition-transform group-hover:translate-x-0.5">→</span>
        </span>
      </div>
    </Card>
  );
};
