import type { ReactNode } from 'react';
import { Menu, X } from 'lucide-react';
import { Link, Outlet } from 'react-router';

import { BrandLogo } from '@/components/blocks/brand-logo';
import { ThemeMenu } from '@/components/theme-menu';
import { Button } from '@/components/ui/button';
import {
  Sheet,
  SheetClose,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from '@/components/ui/sheet';
import { paths } from '@/config/paths';

type PublicLayoutProps = {
  children?: ReactNode;
};

const mobileLinkClassName =
  'flex h-10 items-center rounded-md px-3 text-sm font-medium text-foreground hover:bg-accent hover:text-accent-foreground';

// Public pages never read the student session: a signed-in student sees the same header as a visitor (RN-O13).
export const PublicLayout = ({ children }: PublicLayoutProps) => (
  <div className="flex min-h-svh flex-col bg-background">
    <a
      className="sr-only rounded-md bg-background px-4 py-2 text-sm font-medium focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-50"
      href="#conteudo"
    >
      Pular para o conteúdo
    </a>
    <header className="sticky top-0 z-20 border-b border-border bg-background/95 backdrop-blur">
      <div className="mx-auto flex min-h-17 w-full max-w-280 items-center gap-4 px-4 sm:px-6">
        <Link aria-label="Code4Coders — cursos" className="w-fit" to={paths.studentShowcase.getHref()}>
          <BrandLogo />
        </Link>
        <nav aria-label="Principal" className="ml-4 hidden md:block">
          <Link
            className="rounded-md px-3 py-2 text-sm font-medium text-foreground hover:bg-accent hover:text-accent-foreground"
            to={paths.studentShowcase.getHref()}
          >
            Cursos
          </Link>
        </nav>
        <div className="ml-auto flex items-center gap-2">
          <ThemeMenu />
          <div className="hidden items-center gap-2 md:flex">
            <Button asChild variant="ghost">
              <Link to={paths.studentLogin.getHref()}>Entrar</Link>
            </Button>
            <Button asChild>
              <Link to={paths.studentRegistration.getHref()}>Criar conta</Link>
            </Button>
          </div>
          <Sheet>
            <SheetTrigger asChild>
              <Button aria-label="Abrir menu" className="md:hidden" size="icon" variant="ghost">
                <Menu aria-hidden="true" />
              </Button>
            </SheetTrigger>
            <SheetContent className="w-72 p-0" showCloseButton={false} side="right">
              <SheetHeader className="relative border-b border-border p-6 text-left">
                <SheetTitle className="sr-only">Menu</SheetTitle>
                <SheetDescription className="sr-only">Cursos, entrar e criar conta.</SheetDescription>
                <BrandLogo />
                <SheetClose asChild>
                  <Button aria-label="Fechar menu" className="absolute right-4 top-4" size="icon" variant="ghost">
                    <X aria-hidden="true" />
                  </Button>
                </SheetClose>
              </SheetHeader>
              <nav aria-label="Menu" className="p-3">
                <ul className="grid gap-1">
                  <li>
                    <SheetClose asChild>
                      <Link className={mobileLinkClassName} to={paths.studentShowcase.getHref()}>
                        Cursos
                      </Link>
                    </SheetClose>
                  </li>
                  <li>
                    <SheetClose asChild>
                      <Link className={mobileLinkClassName} to={paths.studentLogin.getHref()}>
                        Entrar
                      </Link>
                    </SheetClose>
                  </li>
                  <li>
                    <SheetClose asChild>
                      <Link className={mobileLinkClassName} to={paths.studentRegistration.getHref()}>
                        Criar conta
                      </Link>
                    </SheetClose>
                  </li>
                </ul>
              </nav>
            </SheetContent>
          </Sheet>
        </div>
      </div>
    </header>
    <main className="mx-auto w-full max-w-280 flex-1 px-4 py-8 sm:px-6 sm:py-10" id="conteudo" tabIndex={-1}>
      {children ?? <Outlet />}
    </main>
    <footer className="border-t border-border px-4 py-6 text-center text-xs text-muted-foreground sm:px-6">
      © {new Date().getFullYear()} Code4Coders · cursos de programação
    </footer>
  </div>
);
