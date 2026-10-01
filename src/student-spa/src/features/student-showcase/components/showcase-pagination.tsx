import { ChevronLeft, ChevronRight } from 'lucide-react';

import { Button } from '@/components/ui/button';

type ShowcasePaginationProps = {
  page: number;
  totalPages: number;
  onPageChange: (page: number) => void;
};

export const ShowcasePagination = ({ page, totalPages, onPageChange }: ShowcasePaginationProps) => {
  if (totalPages <= 1) {
    return null;
  }

  const pages = Array.from({ length: totalPages }, (_, index) => index + 1);

  return (
    <nav aria-label="Paginação dos cursos" className="flex flex-wrap items-center justify-center gap-2">
      <Button disabled={page <= 1} onClick={() => onPageChange(page - 1)} size="sm" variant="outline">
        <ChevronLeft aria-hidden="true" />
        Anterior
      </Button>
      <ul className="flex items-center gap-1">
        {pages.map((item) => (
          <li key={item}>
            <Button
              aria-current={item === page ? 'page' : undefined}
              aria-label={`Página ${item}`}
              onClick={() => onPageChange(item)}
              size="sm"
              variant={item === page ? 'default' : 'ghost'}
            >
              {item}
            </Button>
          </li>
        ))}
      </ul>
      <Button disabled={page >= totalPages} onClick={() => onPageChange(page + 1)} size="sm" variant="outline">
        Próxima
        <ChevronRight aria-hidden="true" />
      </Button>
    </nav>
  );
};
