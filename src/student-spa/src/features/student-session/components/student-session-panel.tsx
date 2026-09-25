import { useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { ChevronDown, KeyRound, LogOut } from 'lucide-react';
import { Link, useNavigate } from 'react-router';
import { toast } from 'sonner';

import { Alert, AlertDescription } from '@/components/ui/alert';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { paths } from '@/config/paths';
import { activeStudentSessionMarker, expiredStudentSessionMarker } from '@/config/session-markers';
import {
  studentSessionQueryKey,
  useEndStudentSession,
  useStudentSession,
} from '@/features/student-session/api/student-session';

const getStudentInitials = (name: string) => name
  .trim()
  .split(/\s+/u)
  .filter(Boolean)
  .slice(0, 2)
  .map((part) => Array.from(part)[0]?.toLocaleUpperCase('pt-BR') ?? '')
  .join('');

type StudentAccountCardProps = {
  name: string;
};

export const StudentAccountCard = ({ name }: StudentAccountCardProps) => (
  <Card aria-label="Sua conta" className="h-full" role="region">
    <CardHeader>
      <div className="flex items-center gap-4">
        {name ? (
          <Avatar aria-hidden="true" className="size-10">
            <AvatarFallback>{getStudentInitials(name)}</AvatarFallback>
          </Avatar>
        ) : null}
        <div className="min-w-0">
          <h2 className="typo-h5">Sua conta</h2>
          <p className="truncate text-sm text-muted-foreground">{name || 'Conta do aluno'}</p>
        </div>
      </div>
    </CardHeader>
    <CardContent>
      <Button asChild className="w-full" variant="outline">
        <Link to={paths.studentPasswordChange.getHref()}>Trocar senha</Link>
      </Button>
    </CardContent>
  </Card>
);

export const StudentSessionPanel = () => {
  const studentSession = useStudentSession();
  const endSession = useEndStudentSession();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const logoutKeyRef = useRef<string | null>(null);
  const [logoutError, setLogoutError] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);

  const handleLogout = async () => {
    if (!studentSession.data) {
      return;
    }

    logoutKeyRef.current ??= crypto.randomUUID();
    setLogoutError(false);
    endSession.reset();
    try {
      await endSession.mutateAsync({
        csrfToken: studentSession.data.csrfToken,
        idempotencyKey: logoutKeyRef.current,
      });
      logoutKeyRef.current = null;
      queryClient.removeQueries({ queryKey: studentSessionQueryKey });
      window.localStorage.removeItem(activeStudentSessionMarker);
      window.sessionStorage.removeItem(expiredStudentSessionMarker);
      toast.success('Você saiu da sua conta.');
      await navigate(paths.studentLogin.getHref(), { replace: true });
    } catch {
      setLogoutError(true);
      toast.error('Não foi possível encerrar sua sessão. Tente novamente.');
    }
  };

  if (!studentSession.data) {
    return null;
  }

  return (
    <DropdownMenu onOpenChange={setMenuOpen} open={menuOpen}>
      <DropdownMenuTrigger asChild>
        <Button
          aria-label={`Abrir o menu da conta de ${studentSession.data.name}`}
          className="h-auto gap-2 px-2"
          variant="ghost"
        >
          <Avatar aria-hidden="true" className="size-9">
            <AvatarFallback>{getStudentInitials(studentSession.data.name)}</AvatarFallback>
          </Avatar>
          <span className="hidden max-w-40 truncate text-sm font-medium sm:inline">
            {studentSession.data.name}
          </span>
          <ChevronDown aria-hidden="true" className="size-4 text-muted-foreground" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-56">
        <DropdownMenuLabel className="font-normal">
          <p className="text-sm font-medium leading-none">{studentSession.data.name}</p>
          <p className="mt-1 text-xs text-muted-foreground">Conta do aluno</p>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem asChild>
          <Link to={paths.studentPasswordChange.getHref()}>
            <KeyRound aria-hidden="true" />
            Trocar senha
          </Link>
        </DropdownMenuItem>
        {logoutError ? (
          <Alert className="mx-1 my-2 border-destructive/30 px-2 py-1.5 text-xs" role="alert" variant="destructive">
            <AlertDescription>Não foi possível encerrar sua sessão. Tente novamente.</AlertDescription>
          </Alert>
        ) : null}
        <DropdownMenuItem
          disabled={endSession.isPending}
          variant="destructive"
          onSelect={(event) => {
            event.preventDefault();
            void handleLogout();
          }}
        >
          <LogOut aria-hidden="true" />
          {endSession.isPending ? 'Saindo…' : 'Sair'}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
};
