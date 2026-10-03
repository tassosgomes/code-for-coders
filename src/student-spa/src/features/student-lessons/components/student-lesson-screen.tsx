import { useEffect } from 'react';
import axios from 'axios';
import { Link, useLocation } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import * as z from 'zod';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { env } from '@/config/env';
import { paths } from '@/config/paths';
import { ProtectedVideoPlayer } from '@/features/student-lessons/components/protected-video-player';
import { StudentLessonNav } from '@/features/student-lessons/components/student-lesson-nav';
import {
  getStudentLessonQueryOptions,
  useStudentLesson,
  type StudentLessonScreenData,
} from '@/features/student-lessons/api/get-student-lesson';
import {
  findKnownSiblingLessonId,
  recordKnownCourse,
} from '@/features/student-lessons/utils/known-course-store';
import { useDocumentTitle } from '@/hooks/use-document-title';

const problemSchema = z.object({
  code: z.string(),
  reason: z.string().optional(),
  accessEndedAt: z.iso.datetime({ offset: true }).optional(),
});

type StudentLessonScreenProps = { lessonId: string; csrfToken: string };

type NavigationState = {
  course?: StudentLessonScreenData['course'];
  knownLessonId?: string;
} | null;

export const StudentLessonScreen = ({ lessonId, csrfToken }: StudentLessonScreenProps) => {
  const location = useLocation();
  const navState = location.state as NavigationState;
  const query = useStudentLesson(lessonId);

  useEffect(() => {
    if (query.isSuccess) {
      recordKnownCourse(query.data.course, query.data.lesson.lessonId);
    }
  }, [query.isSuccess, query.data]);

  useDocumentTitle(query.isSuccess ? query.data.lesson.title : 'Aula');

  const candidateLessonId =
    navState?.knownLessonId && navState.knownLessonId !== lessonId
      ? navState.knownLessonId
      : navState?.course?.modules
          .flatMap((m) => m.lessons)
          .find((l) => l.lessonId !== lessonId)?.lessonId ??
        findKnownSiblingLessonId(lessonId);

  const isLessonNotAvailable =
    query.isError &&
    axios.isAxiosError(query.error) &&
    problemSchema.safeParse(query.error.response?.data).data?.code === 'LESSON_NOT_AVAILABLE';

  const recoveryQuery = useQuery({
    ...getStudentLessonQueryOptions(candidateLessonId!),
    enabled: Boolean(isLessonNotAvailable && candidateLessonId),
  });

  useEffect(() => {
    if (recoveryQuery.isSuccess) {
      recordKnownCourse(recoveryQuery.data.course);
    }
  }, [recoveryQuery.isSuccess, recoveryQuery.data]);

  if (query.isPending) {
    return (
      <div className="grid gap-6 lg:grid-cols-[1fr_360px]" aria-label="Carregando aula" role="status">
        <div>
          <Skeleton className="mb-4 h-8 w-2/3" />
          <Skeleton className="aspect-video w-full" />
          <p>Carregando aula…</p>
        </div>
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (query.isError) {
    const body = axios.isAxiosError(query.error) ? query.error.response?.data : undefined;
    const parsed = problemSchema.safeParse(body);
    const problem = parsed.success ? parsed.data : undefined;
    const denied = problem?.code === 'ACCESS_DENIED';
    const missing = problem?.code === 'LESSON_NOT_AVAILABLE';
    let message = missing
      ? 'Esta aula não está disponível.'
      : denied
        ? 'Você não tem acesso a este curso.'
        : 'Não foi possível confirmar seu acesso agora.';

    if (denied && problem?.reason === 'grant-ended' && problem.accessEndedAt) {
      const lastDay = new Date(new Date(problem.accessEndedAt).getTime() - 1);
      const date = new Intl.DateTimeFormat('pt-BR', { timeZone: env.SCHOOL_TIME_ZONE }).format(lastDay);
      message = `Seu acesso a este curso terminou em ${date}.`;
    }

    if (missing && candidateLessonId && recoveryQuery.isPending) {
      return (
        <div className="grid gap-6 lg:grid-cols-[1fr_360px]" aria-label="Carregando aula" role="status">
          <section>
            <Alert role="alert">
              <AlertTitle>{message}</AlertTitle>
              <AlertDescription>
                <Button asChild variant="link">
                  <Link to={paths.studentShowcase.getHref()}>Ver cursos</Link>
                </Button>
              </AlertDescription>
            </Alert>
          </section>
          <Skeleton className="h-64 w-full" />
        </div>
      );
    }

    const currentCourse = missing ? recoveryQuery.data?.course : undefined;
    if (currentCourse) {
      return (
        <div className="grid gap-6 lg:grid-cols-[1fr_360px]">
          <section>
            <Button asChild variant="link" className="px-0">
              <Link to={paths.studentShowcaseCourse.getHref(currentCourse.courseId)}>Voltar para o curso</Link>
            </Button>
            <Alert role="alert">
              <AlertTitle>{message}</AlertTitle>
              <AlertDescription>
                <Button asChild variant="link">
                  <Link to={paths.studentShowcase.getHref()}>Ver cursos</Link>
                </Button>
              </AlertDescription>
            </Alert>
          </section>
          <StudentLessonNav course={currentCourse} />
        </div>
      );
    }

    return (
      <Alert role="alert">
        <AlertTitle>{message}</AlertTitle>
        <AlertDescription>
          {!denied && !missing ? <Button onClick={() => void query.refetch()}>Tentar de novo</Button> : null}
          <Button asChild variant="link">
            <Link to={paths.studentShowcase.getHref()}>Ver cursos</Link>
          </Button>
        </AlertDescription>
      </Alert>
    );
  }

  const { lesson, course } = query.data;
  return (
    <div className="grid gap-6 lg:grid-cols-[1fr_360px]">
      <section>
        <Button asChild variant="link" className="px-0">
          <Link to={paths.studentShowcaseCourse.getHref(course.courseId)}>Voltar para o curso</Link>
        </Button>
        <h1 className="typo-h3 mb-4">{lesson.title}</h1>
        {csrfToken ? (
          <ProtectedVideoPlayer lessonId={lessonId} csrfToken={csrfToken} />
        ) : (
          <div role="status">Carregando vídeo…</div>
        )}
      </section>
      <StudentLessonNav course={course} currentLessonId={lesson.lessonId} />
    </div>
  );
};
