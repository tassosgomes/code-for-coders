import { SHOWCASE_PAGE_SIZE } from '@/features/student-showcase/api/get-showcase-courses';

type ShowcasePaginationProps = {
  page: number;
  totalPages: number;
  onPageChange: (page: number) => void;
};

const pageButtonClassName =
  'min-h-6 min-w-5 rounded-sm px-1 text-muted-foreground outline-none hover:text-foreground focus-visible:ring-[3px] focus-visible:ring-ring/50 disabled:cursor-not-allowed disabled:opacity-50 aria-[current=page]:font-semibold aria-[current=page]:text-foreground';

// P1.a: '‹ 1 2 › · 12 por página', compacta; à direita no desktop e à esquerda no mobile (P1.h).
export const ShowcasePagination = ({ page, totalPages, onPageChange }: ShowcasePaginationProps) => {
  if (totalPages <= 1) {
    return null;
  }

  const pages = Array.from({ length: totalPages }, (_, index) => index + 1);

  return (
    <nav aria-label="Paginação dos cursos" className="flex flex-wrap items-center gap-1 text-xs text-muted-foreground sm:justify-end">
      <button aria-label="Página anterior" className={pageButtonClassName} disabled={page <= 1} onClick={() => onPageChange(page - 1)} type="button">
        ‹
      </button>
      <ul className="flex items-center gap-1">
        {pages.map((item) => (
          <li key={item}>
            <button
              aria-current={item === page ? 'page' : undefined}
              aria-label={`Página ${item}`}
              className={pageButtonClassName}
              onClick={() => onPageChange(item)}
              type="button"
            >
              {item}
            </button>
          </li>
        ))}
      </ul>
      <button aria-label="Próxima página" className={pageButtonClassName} disabled={page >= totalPages} onClick={() => onPageChange(page + 1)} type="button">
        ›
      </button>
      <span className="ml-1">· {SHOWCASE_PAGE_SIZE} por página</span>
    </nav>
  );
};
