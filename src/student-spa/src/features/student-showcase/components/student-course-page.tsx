import { AlertCircle, ArrowLeft, ChevronRight } from 'lucide-react';
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

// P2.c: voltar, título e as duas colunas em barras.
const CourseSkeleton = () => (
  <div aria-label="Carregando curso" className="flex flex-col gap-6" role="status">
    <BackToShowcase />
    <div className="flex flex-col gap-3">
      <Skeleton aria-hidden="true" className="h-5 w-20" />
      <Skeleton aria-hidden="true" className="h-9 w-full max-w-120" />
    </div>
    <div className="grid gap-8 lg:grid-cols-[1fr_22.5rem] lg:gap-12">
      <div className="flex flex-col gap-4">
        <Skeleton aria-hidden="true" className="h-6 w-55" />
        <Skeleton aria-hidden="true" className="h-4 w-full max-w-160" />
        <Skeleton aria-hidden="true" className="h-4 w-full max-w-140" />
        <Skeleton aria-hidden="true" className="h-4 w-full max-w-155" />
      </div>
      <div className="flex flex-col gap-3 rounded-xl border p-4">
        <Skeleton aria-hidden="true" className="h-5 w-45" />
        <Skeleton aria-hidden="true" className="h-4 w-full" />
        <Skeleton aria-hidden="true" className="h-7 w-30" />
        <Skeleton aria-hidden="true" className="h-10 w-full" />
      </div>
    </div>
  </div>
);

// P4: centralizado na área de conteúdo.
const CourseNotAvailable = () => (
  <div className="flex flex-col items-center gap-6 py-12 text-center sm:py-24">
    <CodeWindow className="w-full max-w-135 text-left" code={'// curso não encontrado'} filename="curso.ts" />
    <div className="flex flex-col gap-6">
      <h1 className="typo-h2 sm:typo-h1">Este curso não está disponível.</h1>
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
        <h1 className="typo-h2 sm:typo-h1">{course.title}</h1>
      </div>

      <div className="grid gap-8 lg:grid-cols-[1fr_22.5rem] lg:items-start lg:gap-12">
        <div className="flex min-w-0 flex-col gap-8">
          {course.description.trim() ? (
            <section aria-labelledby="sobre-o-curso" className="space-y-3">
              <h2 className="typo-h4" id="sobre-o-curso">Sobre o curso</h2>
              <p className="whitespace-pre-line leading-6 text-foreground">{course.description}</p>
            </section>
          ) : null}

          {hasPrerequisite ? (
            <section aria-labelledby="recomendamos-saber-antes" className="space-y-3">
              <h2 className="typo-h4" id="recomendamos-saber-antes">
                Recomendamos saber antes
              </h2>
              {text?.trim() ? <p className="whitespace-pre-line leading-6 text-foreground">{text}</p> : null}
              {recommendedCourses.length > 0 ? (
                <div className="space-y-3">
                  <p className="text-sm font-medium text-muted-foreground">Cursos recomendados:</p>
                  <ul className="list-disc space-y-1.5 pl-5">
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
            <h2 className="typo-h4" id="o-que-voce-vai-ver">O que você vai ver</h2>
            {/* Cada módulo é um cartão (P2.a): seta à esquerda, contagem de aulas à direita. */}
            <Accordion className="flex flex-col gap-3" defaultValue={['module-0']} type="multiple">
              {course.modules.map((module, index) => (
                <div className="overflow-hidden rounded-xl border bg-card" key={`${index}-${module.title}`}>
                  <AccordionItem value={`module-${index}`}>
                    <AccordionTrigger className="group/module items-center! gap-3! px-4 hover:no-underline! [&>svg]:hidden">
                      <ChevronRight aria-hidden="true" className="size-4 shrink-0 transition-transform group-data-[state=open]/module:rotate-90" />
                      <span className="flex-1 text-base leading-6 text-foreground">{module.title}</span>
                      <span className="text-sm font-normal text-muted-foreground">{formatLessonCount(module.lessons.length)}</span>
                    </AccordionTrigger>
                    <AccordionContent className="pb-0!">
                      <ul className="flex flex-col gap-2 border-t px-11 py-3 text-foreground">
                        {module.lessons.map((lesson, lessonIndex) => (
                          <li key={`${lessonIndex}-${lesson.title}`}>{lesson.title}</li>
                        ))}
                      </ul>
                    </AccordionContent>
                  </AccordionItem>
                </div>
              ))}
            </Accordion>
          </section>
        </div>

        <section
          aria-labelledby={`${OPTIONS_ID}-titulo`}
          className="flex scroll-mt-24 flex-col gap-4 lg:sticky lg:top-24 lg:col-start-2 lg:row-start-1"
          id={OPTIONS_ID}
        >
          <h2 className="typo-h5" id={`${OPTIONS_ID}-titulo`}>Opções de acesso</h2>
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
        <Alert className="rounded-xl border-destructive p-4" variant="destructive">
          <AlertCircle aria-hidden="true" />
          <AlertTitle>Não foi possível carregar este curso agora.</AlertTitle>
          <AlertDescription>
            <p>Tente de novo em instantes.</p>
            <Button className="h-8 px-0 text-xs text-primary" onClick={onRetry} variant="link">
              Tentar de novo
            </Button>
          </AlertDescription>
        </Alert>
      </div>
    );
  }

  return <CourseContent course={result.course} />;
};
