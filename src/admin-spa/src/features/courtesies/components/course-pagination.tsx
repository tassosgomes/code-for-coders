type CoursePaginationProps = { page: number; totalPages: number; disabled: boolean; onPage: (page: number) => void };
const pagesAround = (page: number, totalPages: number) => {
  const first = Math.max(1, Math.min(page - 2, totalPages - 4));
  return Array.from({ length: Math.min(5, totalPages) }, (_, index) => first + index);
};
export const CoursePagination = ({ page, totalPages, disabled, onPage }: CoursePaginationProps) => totalPages > 1
  ? <nav className="courtesy-pagination" aria-label="Páginas de cursos">
    <button type="button" aria-label="Página anterior" disabled={disabled || page <= 1} onClick={() => onPage(page - 1)}>‹</button>
    {pagesAround(page, totalPages).map((item) => <button key={item} type="button" aria-label={`Página ${item}`} aria-current={item === page ? 'page' : undefined} className={item === page ? 'is-current' : undefined} disabled={disabled} onClick={() => onPage(item)}>{item}</button>)}
    <button type="button" aria-label="Próxima página" disabled={disabled || page >= totalPages} onClick={() => onPage(page + 1)}>›</button>
  </nav> : null;
