import type { ReactNode } from 'react';
import { BookOpen } from 'lucide-react';

import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';

type DashboardScreenProps = {
  account?: ReactNode;
  isSessionLoading?: boolean;
  studentName?: string;
};

export const DashboardScreen = ({
  account,
  isSessionLoading = false,
  studentName,
}: DashboardScreenProps) => (
  <div aria-busy={isSessionLoading} className="mx-auto flex w-full max-w-7xl flex-col gap-8">
    <header className="space-y-2">
      <p className="typo-overline text-primary">Início</p>
      {isSessionLoading ? (
        <div aria-hidden="true" className="grid gap-2">
          <Skeleton className="h-9 w-64 max-w-full" />
          <Skeleton className="h-5 w-48 max-w-full" />
        </div>
      ) : (
        <>
          <h2 className="typo-h2">
            {studentName ? `Olá, ${studentName} 👋` : 'Olá!'}
          </h2>
          <p className="text-muted-foreground">Sua conta está pronta.</p>
        </>
      )}
    </header>

    {isSessionLoading ? (
      <div aria-label="Carregando início" className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_24rem]" role="status">
        <Card aria-label="Carregando seus cursos" className="gap-6">
          <CardContent className="flex flex-col gap-5">
            <Skeleton aria-hidden="true" className="h-28 w-full" />
            <Skeleton aria-hidden="true" className="h-7 w-3/5" />
            <Skeleton aria-hidden="true" className="h-4 w-full" />
            <Skeleton aria-hidden="true" className="h-4 w-4/5" />
          </CardContent>
        </Card>
        <Card aria-label="Carregando sua conta" className="gap-6">
          <CardContent className="flex flex-col gap-4">
            <Skeleton aria-hidden="true" className="h-7 w-2/5" />
            <Skeleton aria-hidden="true" className="h-4 w-3/5" />
            <Skeleton aria-hidden="true" className="h-4 w-4/5" />
            <Skeleton aria-hidden="true" className="h-9 w-full" />
          </CardContent>
        </Card>
      </div>
    ) : (
      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_24rem]">
        <Card className="min-w-0">
          <CardContent className="flex flex-col gap-5">
            <div aria-hidden="true" className="flex size-12 items-center justify-center rounded-full bg-muted">
              <BookOpen className="size-6 text-muted-foreground" />
            </div>
            <div className="space-y-2">
              <h2 className="typo-h4">Seus cursos aparecem aqui</h2>
              <p className="text-sm leading-relaxed text-muted-foreground">
                Quando você se matricular em um curso, é por aqui que retoma as aulas.
              </p>
            </div>
          </CardContent>
        </Card>
        {account}
      </div>
    )}
  </div>
);
