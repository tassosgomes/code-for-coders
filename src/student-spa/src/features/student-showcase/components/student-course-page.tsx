import { AlertCircle, ArrowLeft } from 'lucide-react';
import { Link } from 'react-router';

import { CodeWindow } from '@/components/blocks/code-window';
import { Accordion, AccordionContent, AccordionItem, AccordionTrigger } from '@/components/ui/accordion';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { paths } from '@/config/paths';
import type { ShowcaseCourseDetail } from '@/features/student-showcase/api/get-showcase-course';
import { OfferOption } from '@/features/student-showcase/components/offer-option';
import { getLevelLabel } from '@/features/student-showcase/utils/showcase-level';

type StudentCoursePageProps = {
  result: { status: 'loading' } | { status: 'error' } | { status: 'not-found' } | { status: 'success'; course: ShowcaseCourseDetail };
  onRetry: () => void;
};

const OPTIONS_ID = 'opcoes-de-acesso';

const formatLessonCount = (count: number) => (count === 1 ? '1 aula' : `${count} aulas`);

const BackToShowcase = () => (
  <Link
    className="inline-flex w-fit items-center gap-1 text-sm font-medium text-primary underline-offset-4 hover:underline"
    to={paths.studentShowcase.getHref()}
  >
    <ArrowLeft aria-hidden="true" className="size-4" />
    Todos os cursos
  </Link>
);

const CourseSkeleton = () => (
  <div aria-label="Carregando curso" className="grid gap-8 lg:grid-cols-[1fr_22rem]" role="status">
    <div className="flex flex-col gap-4">
      <Skeleton aria-hidden="true" className="h-5 w-24" />
      <Skeleton aria-hidden="true" className="h-10 w-3/4" />
      <Skeleton aria-hidden="true" className="h-24 w-full" />
      <Skeleton aria-hidden="true" className="h-24 w-full" />
      <Skeleton aria-hidden="true" className="h-24 w-full" />
    </div>
    <Skeleton aria-hidden="true" className="h-64 w-full" />
  </div>
);

const CourseNotAvailable = () => (
  <div className="flex max-w-xl flex-col items-start gap-5">
    <CodeWindow className="w-full" code={'// curso não encontrado'} filename="curso.ts" />
    <div className="space-y-2">
      <h1 className="typo-h2">Este curso não está disponível.</h1>
      <p className="text-muted-foreground">Ele pode ter saído da vitrine ou o endereço pode estar incorreto.</p>
    </div>
    <Button asChild>
      <Link to={paths.studentShowcase.getHref()}>Ver todos os cursos</Link>
    </Button>
  </div>
);

const CourseContent = ({ course }: { course: ShowcaseCourseDetail }) => {
  const levelLabel = getLevelLabel(course.level);
  const { text, recommendedCourses } = course.prerequisite;
  const hasPrerequisite = Boolean(text?.trim()) || recommendedCourses.length > 0;

  return (
    <div className="flex flex-col gap-6 pb-20 lg:pb-0">
      <div className="flex flex-col gap-3">
        <BackToShowcase />
        {levelLabel ? <Badge className="w-fit" variant="secondary">{levelLabel}</Badge> : null}
        <h1 className="typo-h2">{course.title}</h1>
      </div>

      <div className="grid gap-8 lg:grid-cols-[1fr_22rem] lg:items-start">
        <div className="flex min-w-0 flex-col gap-8">
          {course.description.trim() ? (
            <section aria-labelledby="sobre-o-curso" className="space-y-3">
              <h2 className="font-heading text-xl font-semibold" id="sobre-o-curso">Sobre o curso</h2>
              <p className="max-w-prose whitespace-pre-line leading-relaxed text-foreground">{course.description}</p>
            </section>
          ) : null}

          {hasPrerequisite ? (
            <section aria-labelledby="recomendamos-saber-antes" className="space-y-3">
              <h2 className="font-heading text-xl font-semibold" id="recomendamos-saber-antes">
                Recomendamos saber antes
              </h2>
              {text?.trim() ? <p className="max-w-prose whitespace-pre-line leading-relaxed text-foreground">{text}</p> : null}
              {recommendedCourses.length > 0 ? (
                <div className="space-y-2">
                  <p className="text-sm text-muted-foreground">Cursos recomendados:</p>
                  <ul className="list-disc space-y-1 pl-5">
                    {recommendedCourses.map((recommended) => (
                      <li key={recommended.courseId}>
                        {recommended.inShowcase ? (
                          <Link
                            className="font-medium text-primary underline-offset-4 hover:underline"
                            to={paths.studentShowcaseCourse.getHref(recommended.courseId)}
                          >
                            {recommended.title}
                          </Link>
                        ) : (
                          recommended.title
                        )}
                      </li>
                    ))}
                  </ul>
                </div>
              ) : null}
            </section>
          ) : null}

          <section aria-labelledby="o-que-voce-vai-ver" className="space-y-3">
            <h2 className="font-heading text-xl font-semibold" id="o-que-voce-vai-ver">O que você vai ver</h2>
            <Accordion defaultValue={['module-0']} type="multiple">
              {course.modules.map((module, index) => (
                <AccordionItem key={`${index}-${module.title}`} value={`module-${index}`}>
                  <AccordionTrigger>
                    <span>
                      {module.title} <span className="font-normal text-muted-foreground">({formatLessonCount(module.lessons.length)})</span>
                    </span>
                  </AccordionTrigger>
                  <AccordionContent>
                    <ul className="space-y-1 pl-4 text-foreground">
                      {module.lessons.map((lesson, lessonIndex) => (
                        <li key={`${lessonIndex}-${lesson.title}`}>{lesson.title}</li>
                      ))}
                    </ul>
                  </AccordionContent>
                </AccordionItem>
              ))}
            </Accordion>
          </section>
        </div>

        <section
          aria-labelledby={`${OPTIONS_ID}-titulo`}
          className="flex scroll-mt-24 flex-col gap-4 lg:sticky lg:top-24 lg:col-start-2 lg:row-start-1"
          id={OPTIONS_ID}
        >
          <h2 className="font-heading text-xl font-semibold" id={`${OPTIONS_ID}-titulo`}>Opções de acesso</h2>
          <ul className="flex flex-col gap-4">
            {course.offers.map((offer) => (
              <li key={offer.offerId}>
                <OfferOption courseId={course.courseId} offer={offer} />
              </li>
            ))}
          </ul>
        </section>
      </div>

      <div className="fixed inset-x-0 bottom-0 z-10 border-t border-border bg-background/95 p-3 backdrop-blur lg:hidden">
        <Button asChild className="w-full">
          <a href={`#${OPTIONS_ID}`}>Ver opções de acesso</a>
        </Button>
      </div>
    </div>
  );
};

export const StudentCoursePage = ({ result, onRetry }: StudentCoursePageProps) => {
  if (result.status === 'loading') {
    return <CourseSkeleton />;
  }

  if (result.status === 'not-found') {
    return <CourseNotAvailable />;
  }

  if (result.status === 'error') {
    return (
      <div className="flex flex-col gap-4">
        <BackToShowcase />
        <Alert variant="destructive">
          <AlertCircle aria-hidden="true" />
          <AlertTitle>Não foi possível carregar este curso agora.</AlertTitle>
          <AlertDescription>
            <Button onClick={onRetry} size="sm" variant="outline">
              Tentar de novo
            </Button>
          </AlertDescription>
        </Alert>
      </div>
    );
  }

  return <CourseContent course={result.course} />;
};
