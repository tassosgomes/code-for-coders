import type { CSSProperties, ReactNode } from 'react';
import { House, KeyRound, Menu, X } from 'lucide-react';
import { Link, Outlet, useLocation } from 'react-router';

import { BrandLogo } from '@/components/blocks/brand-logo';
import { Button } from '@/components/ui/button';
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarInset,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarProvider,
} from '@/components/ui/sidebar';
import {
  Sheet,
  SheetClose,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from '@/components/ui/sheet';
import { ThemeMenu } from '@/components/theme-menu';
import { paths } from '@/config/paths';
import { useDocumentTitle } from '@/hooks/use-document-title';

type AppShellProps = {
  accountMenu?: ReactNode;
  children?: ReactNode;
};

const getPageTitle = (pathname: string) =>
  pathname === paths.studentPasswordChange.path ? 'Trocar senha' : 'Início';

const sidebarStyle: CSSProperties & { '--sidebar-width': string } = {
  '--sidebar-width': '16.5rem',
};

export const AppShell = ({ accountMenu, children }: AppShellProps) => {
  const { pathname } = useLocation();
  const pageTitle = getPageTitle(pathname);
  const onHome = pathname === paths.home.path;
  const onPasswordChange = pathname === paths.studentPasswordChange.path;

  useDocumentTitle(`${pageTitle} | Code4Coders`);

  return (
    <SidebarProvider style={sidebarStyle}>
      <Sidebar className="hidden shrink-0 md:flex" collapsible="none" variant="sidebar">
        <SidebarHeader className="p-6">
          <Link aria-label="Code4Coders — início" className="w-fit" to={paths.home.getHref()}>
            <BrandLogo />
          </Link>
        </SidebarHeader>
        <SidebarContent className="px-3">
          <SidebarGroup>
            <SidebarGroupLabel>Navegação</SidebarGroupLabel>
            <SidebarGroupContent>
              <SidebarMenu>
                <SidebarMenuItem>
                  <SidebarMenuButton asChild isActive={onHome}>
                    <Link aria-current={onHome ? 'page' : undefined} to={paths.home.getHref()}>
                      <House aria-hidden="true" />
                      <span>Início</span>
                    </Link>
                  </SidebarMenuButton>
                </SidebarMenuItem>
                <SidebarMenuItem>
                  <SidebarMenuButton asChild isActive={onPasswordChange}>
                    <Link
                      aria-current={onPasswordChange ? 'page' : undefined}
                      to={paths.studentPasswordChange.getHref()}
                    >
                      <KeyRound aria-hidden="true" />
                      <span>Trocar senha</span>
                    </Link>
                  </SidebarMenuButton>
                </SidebarMenuItem>
              </SidebarMenu>
            </SidebarGroupContent>
          </SidebarGroup>
        </SidebarContent>
        <SidebarFooter className="p-4">
          <p className="text-xs text-muted-foreground">Seu espaço de aprendizagem</p>
        </SidebarFooter>
      </Sidebar>

      <SidebarInset>
        <header className="sticky top-0 z-20 flex min-h-17 items-center gap-3 border-b border-border bg-background/95 px-4 backdrop-blur sm:px-6">
          <Sheet>
            <SheetTrigger asChild>
              <Button aria-label="Abrir navegação" className="md:hidden" size="icon" variant="ghost">
                <Menu aria-hidden="true" />
              </Button>
            </SheetTrigger>
            <SheetContent className="w-72 p-0" showCloseButton={false} side="left">
              <SheetHeader className="relative border-b border-border p-6 text-left">
                <SheetTitle className="sr-only">Navegação principal</SheetTitle>
                <SheetDescription className="sr-only">Acesse as áreas da sua conta.</SheetDescription>
                <Link aria-label="Code4Coders — início" className="w-fit" to={paths.home.getHref()}>
                  <BrandLogo />
                </Link>
                <SheetClose asChild>
                  <Button aria-label="Fechar navegação" className="absolute right-4 top-4" size="icon" variant="ghost">
                    <X aria-hidden="true" />
                  </Button>
                </SheetClose>
              </SheetHeader>
              <nav aria-label="Navegação principal" className="p-3">
                <ul className="grid gap-1">
                  <li>
                    <SheetClose asChild>
                      <Link
                        aria-current={onHome ? 'page' : undefined}
                        className={`flex h-10 items-center gap-3 rounded-md px-3 text-sm font-medium ${onHome ? 'bg-sidebar-accent text-sidebar-accent-foreground' : 'text-sidebar-foreground hover:bg-sidebar-accent hover:text-sidebar-accent-foreground'}`}
                        to={paths.home.getHref()}
                      >
                        <House aria-hidden="true" className="size-4" />
                        Início
                      </Link>
                    </SheetClose>
                  </li>
                  <li>
                    <SheetClose asChild>
                      <Link
                        aria-current={onPasswordChange ? 'page' : undefined}
                        className={`flex h-10 items-center gap-3 rounded-md px-3 text-sm font-medium ${onPasswordChange ? 'bg-sidebar-accent text-sidebar-accent-foreground' : 'text-sidebar-foreground hover:bg-sidebar-accent hover:text-sidebar-accent-foreground'}`}
                        to={paths.studentPasswordChange.getHref()}
                      >
                        <KeyRound aria-hidden="true" className="size-4" />
                        Trocar senha
                      </Link>
                    </SheetClose>
                  </li>
                </ul>
              </nav>
            </SheetContent>
          </Sheet>
          <div className="min-w-0">
            <p className="typo-overline text-primary">Conta do aluno</p>
            <h1 className="truncate font-heading text-lg font-semibold text-foreground">{pageTitle}</h1>
          </div>
          <div className="ml-auto flex shrink-0 items-center gap-2">
            <ThemeMenu />
            {accountMenu}
          </div>
        </header>
        <div className="w-full flex-1 p-4 sm:p-6 lg:p-8">{children ?? <Outlet />}</div>
      </SidebarInset>
    </SidebarProvider>
  );
};
