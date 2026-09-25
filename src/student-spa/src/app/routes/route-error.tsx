import { AlertTriangle, ArrowLeft, House } from 'lucide-react';
import { isRouteErrorResponse, Link, useRouteError } from 'react-router';

import { AppShell } from '@/components/app-shell';
import { AuthLayout } from '@/components/auth-layout';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { paths } from '@/config/paths';
import { StudentSessionPanel } from '@/features/student-session/components/student-session-panel';
import { useStudentSession } from '@/features/student-session/api/student-session';

type RouteErrorProps = {
  layout?: 'app' | 'auth' | 'auto';
};

const RouteErrorContent = ({ authenticated }: { authenticated: boolean }) => {
  const error = useRouteError();
  const isNotFound = isRouteErrorResponse(error) && error.status === 404;

  return (
    <Card className="mx-auto w-full max-w-xl">
      <CardHeader className="gap-4">
        <div className="grid size-12 place-items-center rounded-xl bg-warning-soft text-warning-soft-foreground">
          <AlertTriangle aria-hidden="true" className="size-6" />
        </div>
        <CardTitle className="font-heading text-2xl">
          {isNotFound ? 'Página não encontrada' : 'Não foi possível carregar esta página'}
        </CardTitle>
      </CardHeader>
      <CardContent>
        <p className="text-muted-foreground">
          {isNotFound
            ? 'O endereço pode estar incorreto ou a página não existe mais.'
            : 'Ocorreu um erro inesperado. Tente novamente ou volte para uma área conhecida.'}
        </p>
      </CardContent>
      <CardFooter className="flex flex-wrap gap-3">
        {authenticated ? (
          <Button asChild>
            <Link to={paths.home.getHref()}>
              <House aria-hidden="true" />
              Voltar ao início
            </Link>
          </Button>
        ) : (
          <Button asChild>
            <Link to={paths.studentLogin.getHref()}>
              <ArrowLeft aria-hidden="true" />
              Ir para entrar
            </Link>
          </Button>
        )}
      </CardFooter>
    </Card>
  );
};

const AutoRouteError = () => {
  const session = useStudentSession();

  if (session.isPending) {
    return (
      <main aria-label="Carregando estado da conta" className="mx-auto grid w-full max-w-xl gap-4 px-6 py-10">
        <Skeleton className="h-10 w-2/3" />
        <Skeleton className="h-28 w-full" />
        <Skeleton className="h-10 w-36" />
      </main>
    );
  }

  if (session.data) {
    return (
      <AppShell accountMenu={<StudentSessionPanel />}>
        <RouteErrorContent authenticated />
      </AppShell>
    );
  }

  return (
    <AuthLayout>
      <RouteErrorContent authenticated={false} />
    </AuthLayout>
  );
};

export const RouteError = ({ layout = 'auto' }: RouteErrorProps) => {
  if (layout === 'auto') {
    return <AutoRouteError />;
  }

  if (layout === 'app') {
    return (
      <AppShell accountMenu={<StudentSessionPanel />}>
        <RouteErrorContent authenticated />
      </AppShell>
    );
  }

  return (
    <AuthLayout>
      <RouteErrorContent authenticated={false} />
    </AuthLayout>
  );
};
