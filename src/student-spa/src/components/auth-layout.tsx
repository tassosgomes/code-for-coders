import type { ReactNode } from 'react';
import { Link, Outlet } from 'react-router';

import { BrandLogo } from '@/components/blocks/brand-logo';
import { CodeWindow } from '@/components/blocks/code-window';
import { paths } from '@/config/paths';

export const AuthBrandPanel = () => (
  <aside className="relative hidden min-h-svh flex-col justify-between overflow-hidden bg-muted p-10 lg:flex xl:p-14">
    <Link aria-label="Code4Coders — início" className="w-fit" to={paths.home.getHref()}>
      <BrandLogo />
    </Link>
    <div className="relative z-10 mx-auto grid w-full max-w-xl gap-8">
      <div className="grid gap-4">
        <p className="typo-overline text-primary">Sua próxima linha começa aqui</p>
        <h2 className="font-heading text-4xl font-bold tracking-tight text-foreground xl:text-5xl">
          Aprenda. Pratique. Construa.
        </h2>
        <p className="max-w-prose text-lg leading-8 text-muted-foreground">
          Um espaço para desenvolver suas habilidades de programação, passo a passo.
        </p>
      </div>
      <CodeWindow
        code={'const proximoPasso = "aprender";\n\nconsole.log(`Olá, ${proximoPasso}!`);'}
        filename="sua-jornada.ts"
      />
    </div>
    <p className="relative z-10 text-sm text-muted-foreground">Aprendizado em tecnologia, no seu ritmo.</p>
    <div aria-hidden="true" className="pointer-events-none absolute -right-32 -top-32 size-96 rounded-full bg-primary/5 blur-3xl" />
    <div aria-hidden="true" className="pointer-events-none absolute -bottom-40 -left-20 size-96 rounded-full bg-secondary blur-3xl" />
  </aside>
);

type AuthLayoutProps = {
  children?: ReactNode;
};

export const AuthLayout = ({ children }: AuthLayoutProps) => (
  <div className="min-h-svh bg-background lg:grid lg:grid-cols-2">
    <AuthBrandPanel />
    <section className="flex min-h-svh flex-col">
      <header className="flex items-center justify-between px-6 py-5 lg:hidden">
        <Link aria-label="Code4Coders — início" to={paths.home.getHref()}>
          <BrandLogo />
        </Link>
      </header>
      <div className="flex flex-1 items-center justify-center px-6 py-8 sm:px-10">
        <div className="w-full max-w-md">
          {children ?? <Outlet />}
        </div>
      </div>
      <footer className="px-6 py-5 text-center text-xs text-muted-foreground">
        © {new Date().getFullYear()} Code4Coders
      </footer>
    </section>
  </div>
);
