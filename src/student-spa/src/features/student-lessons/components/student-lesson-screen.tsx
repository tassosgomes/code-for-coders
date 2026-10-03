import axios from 'axios';
import { Link } from 'react-router';
import * as z from 'zod';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { env } from '@/config/env';
import { paths } from '@/config/paths';
import { useStudentLesson } from '@/features/student-lessons/api/get-student-lesson';
import { useDocumentTitle } from '@/hooks/use-document-title';

const problemSchema = z.object({ code: z.string(), reason: z.string().optional(), accessEndedAt: z.iso.datetime({ offset: true }).optional() });
type StudentLessonScreenProps = { lessonId: string };

export const StudentLessonScreen = ({ lessonId }: StudentLessonScreenProps) => {
  const query = useStudentLesson(lessonId);
  useDocumentTitle(query.isSuccess ? query.data.lesson.title : 'Aula');
  if (query.isPending) return (
    <div className="grid gap-6 lg:grid-cols-[1fr_360px]" aria-label="Carregando aula" role="status">
      <div><Skeleton className="mb-4 h-8 w-2/3" /><Skeleton className="aspect-video w-full" /><p>Carregando aula…</p></div>
      <Skeleton className="h-64 w-full" />
    </div>
  );
  if (query.isError) {
    const body = axios.isAxiosError(query.error) ? query.error.response?.data : undefined;
    const parsed = problemSchema.safeParse(body);
    const problem = parsed.success ? parsed.data : undefined;
    const denied = problem?.code === 'ACCESS_DENIED';
    const missing = problem?.code === 'LESSON_NOT_AVAILABLE';
    let message = missing ? 'Esta aula não está disponível.' : denied ? 'Você não tem acesso a este curso.' : 'Não foi possível confirmar seu acesso agora.';
    if (denied && problem?.reason === 'grant-ended' && problem.accessEndedAt) {
      const lastDay = new Date(new Date(problem.accessEndedAt).getTime() - 1);
      const date = new Intl.DateTimeFormat('pt-BR', { timeZone: env.SCHOOL_TIME_ZONE }).format(lastDay);
      message = `Seu acesso a este curso terminou em ${date}.`;
    }
    return <Alert role="alert"><AlertTitle>{message}</AlertTitle><AlertDescription>
      {!denied && !missing ? <Button onClick={() => void query.refetch()}>Tentar de novo</Button> : null}
      <Button asChild variant="link"><Link to={paths.studentShowcase.getHref()}>Ver cursos</Link></Button>
    </AlertDescription></Alert>;
  }
  const { lesson, course } = query.data;
  return <div className="grid gap-6 lg:grid-cols-[1fr_360px]">
    <section>
      <Button asChild variant="link" className="px-0"><Link to={paths.studentShowcaseCourse.getHref(course.courseId)}>Voltar para o curso</Link></Button>
      <h1 className="typo-h3 mb-4">{lesson.title}</h1>
      <div className="flex aspect-video items-center justify-center rounded-lg bg-muted" role="status">Carregando vídeo…</div>
    </section>
    <nav aria-label="Aulas do curso" className="rounded-lg border p-4">
      <h2 className="typo-h4">Aulas do curso</h2><p className="text-muted-foreground">{course.title}</p>
      <ul className="mt-4 space-y-4">{course.modules.map((module) => <li key={module.moduleId}>
        <h3 className="font-semibold">{module.position}. {module.title}</h3>
        <ul className="mt-2 space-y-1">{module.lessons.map((item) => <li key={item.lessonId}>
          <Link to={paths.studentLesson.getHref(item.lessonId)} aria-current={item.lessonId === lesson.lessonId ? 'page' : undefined}
            className={`block rounded-md p-2 focus-visible:outline focus-visible:outline-ring ${item.lessonId === lesson.lessonId ? 'bg-secondary' : 'hover:bg-muted'}`}>
            {item.position}. {item.title}{item.lessonId === lesson.lessonId ? <span className="ml-2 text-sm">▶ Aula atual</span> : null}
          </Link>
        </li>)}</ul>
      </li>)}</ul>
    </nav>
  </div>;
};
